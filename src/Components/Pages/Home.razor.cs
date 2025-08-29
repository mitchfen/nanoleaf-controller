using Microsoft.AspNetCore.Components;
using System.Text.Json;
using System.Timers;

namespace nanoleaf_controller.Components.Pages
{
    public partial class Home : ComponentBase, IDisposable
    {
        [Inject]
        private IConfiguration Configuration { get; set; }

        private System.Timers.Timer debounceTimer;
        private System.Timers.Timer pollingTimer;
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

            pollingTimer = new System.Timers.Timer(5000);
            pollingTimer.Elapsed += async (sender, e) =>
            {
                await GetNanoleafState();
                await InvokeAsync(StateHasChanged);
            };
            pollingTimer.AutoReset = true;
            pollingTimer.Start();
        }

        public void Dispose()
        {
            debounceTimer?.Dispose();
            pollingTimer?.Dispose();
        }

        private async Task SendPowerCommand(bool on)
        {
            await SendCommand(new { on = new { value = on } }, "state");
            await GetNanoleafState(); // Refresh state after power command
        }

        private void SetBrightness(ChangeEventArgs e)
        {
            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Dispose();
            }

            brightness = Convert.ToInt32(e.Value);
            debounceTimer = new System.Timers.Timer(200);
            debounceTimer.Elapsed += async (sender, e) => await DebouncedSetBrightness();
            debounceTimer.AutoReset = false;
            debounceTimer.Start();
        }

        private async Task DebouncedSetBrightness()
        {
            await SendCommand(new { on = new { value = true } }, "state");
            await SendCommand(new { brightness = new { value = brightness } }, "state");
            await GetNanoleafState();
            await InvokeAsync(StateHasChanged);
        }

        private async Task SetEffect(ChangeEventArgs e)
        {
            selectedEffect = e.Value.ToString();
            await SendCommand(new { on = new { value = true } }, "state");
            await SendCommand(new { select = selectedEffect }, "effects");
            await GetNanoleafState();
        }

        private void SetColorTemperature(ChangeEventArgs e)
        {
            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Dispose();
            }

            colorTemperature = Convert.ToInt32(e.Value);
            debounceTimer = new System.Timers.Timer(200);
            debounceTimer.Elapsed += async (sender, e) => await DebouncedSetColorTemperature();
            debounceTimer.AutoReset = false;
            debounceTimer.Start();
        }

        private async Task DebouncedSetColorTemperature()
        {
            await SendCommand(new { on = new { value = true } }, "state");
            await SendCommand(new { ct = new { value = colorTemperature } }, "state");
            await GetNanoleafState();
            await InvokeAsync(StateHasChanged);
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
