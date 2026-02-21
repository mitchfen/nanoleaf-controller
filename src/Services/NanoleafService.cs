using System.Text;
using System.Text.Json;

namespace nanoleaf_controller.Services;

public class NanoleafService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string? _baseUrl;

    public NanoleafService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        var ip = _configuration["Nanoleaf:IpAddress"];
        var token = _configuration["Nanoleaf:AuthToken"];
        
        // Basic validation - in a real app we might want more robust error handling for missing config
        if (!string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(token))
        {
            _baseUrl = $"http://{ip}:16021/api/v1/{token}/";
        }
    }

    private bool IsConfigured => !string.IsNullOrEmpty(_baseUrl);

    public async Task<NanoleafState?> GetStateAsync()
    {
        if (!IsConfigured) return null;

        try
        {
            // Fetch all info to get state and effects in one go if possible, 
            // but for now let's stick to the specific endpoints or the root which gives everything.
            // Calling the root "/" returns everything including state, effects, panelLayout, etc.
            var response = await _httpClient.GetAsync(_baseUrl);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var state = new NanoleafState();

            if (root.TryGetProperty("state", out var stateEl))
            {
                if (stateEl.TryGetProperty("on", out var onEl) && onEl.TryGetProperty("value", out var onVal))
                    state.IsOn = onVal.GetBoolean();
                
                if (stateEl.TryGetProperty("brightness", out var briEl) && briEl.TryGetProperty("value", out var briVal))
                    state.Brightness = briVal.GetInt32();
                
                if (stateEl.TryGetProperty("ct", out var ctEl) && ctEl.TryGetProperty("value", out var ctVal))
                    state.ColorTemperature = ctVal.GetInt32();
                
                if (stateEl.TryGetProperty("hue", out var hueEl) && hueEl.TryGetProperty("value", out var hueVal))
                    state.Hue = hueVal.GetInt32();
                
                if (stateEl.TryGetProperty("sat", out var satEl) && satEl.TryGetProperty("value", out var satVal))
                    state.Saturation = satVal.GetInt32();
                
                if (stateEl.TryGetProperty("colorMode", out var cmEl))
                    state.ColorMode = cmEl.GetString();
            }

            if (root.TryGetProperty("effects", out var effectsEl))
            {
                 if (effectsEl.TryGetProperty("select", out var selEl))
                    state.SelectedEffect = selEl.GetString();
                 
                 if (effectsEl.TryGetProperty("effectsList", out var listEl))
                 {
                     state.Effects = listEl.EnumerateArray()
                         .Select(e => e.GetString())
                         .Where(s => s != null)
                         .Cast<string>()
                         .ToList();
                 }
            }

            return state;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching state: {ex.Message}");
            return null;
        }
    }

    public async Task SetPowerAsync(bool on)
    {
        await SendCommandAsync("state", new { on = new { value = on } });
    }

    public async Task SetBrightnessAsync(int brightness)
    {
        await SendCommandAsync("state", new { brightness = new { value = brightness } });
    }

    public async Task SetColorTemperatureAsync(int ct)
    {
        await SendCommandAsync("state", new { ct = new { value = ct } });
    }

    public async Task SetHueAsync(int hue)
    {
        await SendCommandAsync("state", new { hue = new { value = hue } });
    }

    public async Task SetSaturationAsync(int sat)
    {
        await SendCommandAsync("state", new { sat = new { value = sat } });
    }

    public async Task SetEffectAsync(string effect)
    {
        await SendCommandAsync("effects", new { select = effect });
    }

    private async Task SendCommandAsync(string endpoint, object data)
    {
        if (!IsConfigured) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(_baseUrl + endpoint, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending command to {endpoint}: {ex.Message}");
            throw; // Re-throw to let UI handle or show error
        }
    }

    public async Task<PanelLayout?> GetPanelLayoutAsync()
    {
        if (!IsConfigured) return null;

        try
        {
            var response = await _httpClient.GetAsync(_baseUrl + "panelLayout/layout");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PanelLayout>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching panel layout: {ex.Message}");
            return null;
        }
    }

    public async Task<NanoleafEffect?> GetEffectDetailsAsync(string effectName)
    {
        if (!IsConfigured) return null;

        try
        {
            var command = new
            {
                write = new
                {
                    command = "request",
                    animName = effectName
                }
            };
            
            var json = JsonSerializer.Serialize(command);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(_baseUrl + "effects", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<NanoleafEffect>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching effect details: {ex.Message}");
            return null;
        }
    }
}

public class NanoleafState
{
    public bool IsOn { get; set; }
    public int Brightness { get; set; }
    public int ColorTemperature { get; set; }
    public int Hue { get; set; }
    public int Saturation { get; set; }
    public string? ColorMode { get; set; } // "ct", "effect", "hs"
    public string? SelectedEffect { get; set; }
    public List<string>? Effects { get; set; }
}

public class PanelLayout
{
    public int NumPanels { get; set; }
    public int SideLength { get; set; }
    public List<PanelPosition>? PositionData { get; set; }
}

public class PanelPosition
{
    public int PanelId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int O { get; set; }
    public int ShapeType { get; set; }
}

public class NanoleafEffect
{
    public string? AnimName { get; set; }
    public string? PluginType { get; set; }
    public List<NanoleafPaletteColor>? Palette { get; set; }
}

public class NanoleafPaletteColor
{
    public int Hue { get; set; }
    public int Saturation { get; set; }
    public int Brightness { get; set; }
}
