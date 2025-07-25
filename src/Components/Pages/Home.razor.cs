using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace nanoleaf_controller.Components.Pages
{
    public partial class Home : ComponentBase
    {
        [Inject]
        private IConfiguration Configuration { get; set; }

        private string? statusMessage;
        private int brightness = 50;
        private int colorTemperature = 4000; // Default color temperature
        private string selectedEffect = "Cozy";
        private string[] effects = new string[0];
        private bool isNanoleafOn;

        protected override async Task OnInitializedAsync()
        {
            effects = Configuration.GetSection("Nanoleaf:Effects").Get<string[]>() ?? new string[0];
            selectedEffect = effects.FirstOrDefault() ?? "";
            await GetNanoleafState();
        }

        private async Task SendPowerCommand(bool on)
        {
            await SendCommand(new { on = new { value = on } }, "state");
            await GetNanoleafState(); // Refresh state after power command
        }

        private async Task SetBrightness()
        {
            await SendCommand(new { brightness = new { value = brightness } }, "state");
        }

        private async Task SetEffect()
        {
            await SendCommand(new { select = selectedEffect }, "effects");
        }

        private async Task SetColorTemperature()
        {
            await SendCommand(new { ct = new { value = colorTemperature } }, "state");
        }

        private async Task SendCommand(object body, string endpoint)
        {
            var nanoleafIp = Configuration["Nanoleaf:IpAddress"];
            var authToken = Configuration["Nanoleaf:AuthToken"];

            if (string.IsNullOrEmpty(nanoleafIp) || nanoleafIp == "YOUR_NANOLEAF_IP" || string.IsNullOrEmpty(authToken) || authToken == "REDACTED")
            {
                statusMessage = "IP address or Auth Token is not configured. Please check your appsettings.json or environment variables.";
                return;
            }

            var client = new HttpClient();
            var url = $"http://{nanoleafIp}:16021/api/v1/{authToken}/{endpoint}";
            var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PutAsync(url, content);
                response.EnsureSuccessStatusCode();
                statusMessage = null;
            }
            catch (Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
            }
        }

        private async Task GetNanoleafState()
        {
            var nanoleafIp = Configuration["Nanoleaf:IpAddress"];
            var authToken = Configuration["Nanoleaf:AuthToken"];

            if (string.IsNullOrEmpty(nanoleafIp) || nanoleafIp == "YOUR_NANOLEAF_IP" || string.IsNullOrEmpty(authToken) || authToken == "REDACTED")
            {
                statusMessage = "IP address or Auth Token is not configured. Please check your appsettings.json or environment variables.";
                return;
            }

            var client = new HttpClient();
            var url = $"http://{nanoleafIp}:16021/api/v1/{authToken}/state";

            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                {
                    if (doc.RootElement.TryGetProperty("on", out JsonElement onElement) && onElement.TryGetProperty("value", out JsonElement valueElement))
                    {
                        isNanoleafOn = valueElement.GetBoolean();
                    }
                }
                statusMessage = null;
            }
            catch (Exception ex)
            {
                statusMessage = $"Error fetching state: {ex.Message}";
            }
        }
    }
}
