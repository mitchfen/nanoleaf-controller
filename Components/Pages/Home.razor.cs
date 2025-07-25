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
        private string selectedEffect = "Cozy";
        private string[] effects = new string[0];

        protected override void OnInitialized()
        {
            effects = Configuration.GetSection("Nanoleaf:Effects").Get<string[]>() ?? new string[0];
            selectedEffect = effects.FirstOrDefault() ?? "";
        }

        private async Task SendPowerCommand(bool on)
        {
            await SendCommand(new { on = new { value = on } }, "state");
        }

        private async Task SetBrightness()
        {
            await SendCommand(new { brightness = new { value = brightness } }, "state");
        }

        private async Task SetEffect()
        {
            await SendCommand(new { select = selectedEffect }, "effects");
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
                statusMessage = "Command sent successfully.";
            }
            catch (Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
            }
        }
    }
}
