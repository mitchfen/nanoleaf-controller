using Microsoft.AspNetCore.Components;
using nanoleaf_controller.Services;
using System.Timers;

namespace nanoleaf_controller.Components.Pages
{
    public partial class Home : ComponentBase, IDisposable
    {
        [Inject]
        private NanoleafService NanoleafService { get; set; } = default!;

        private System.Timers.Timer? debounceTimer;
        private System.Timers.Timer? pollingTimer;
        private string? statusMessage;
        private int brightness = 50;
        private int colorTemperature = 4000;
        private string selectedEffect = "Cozy";
        private List<string> effects = new();
        private bool isNanoleafOn;
        private string hexColor = "#ffffff";

        protected override async Task OnInitializedAsync()
        {
            await RefreshState();

            pollingTimer = new System.Timers.Timer(5000);
            pollingTimer.Elapsed += async (sender, e) =>
            {
                await RefreshState();
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

        private async Task RefreshState()
        {
            var state = await NanoleafService.GetStateAsync();
            if (state != null)
            {
                isNanoleafOn = state.IsOn;
                brightness = state.Brightness;
                colorTemperature = state.ColorTemperature;
                // If current effect is not in list (e.g. solid color), keep selectedEffect as is or update if available
                if (!string.IsNullOrEmpty(state.SelectedEffect))
                {
                    selectedEffect = state.SelectedEffect;
                }
                
                if (state.Effects != null)
                {
                    effects = state.Effects;
                }
                
                // Convert HSV to Hex for the color picker
                hexColor = HsvToHex(state.Hue, state.Saturation, 100);

                statusMessage = null;
            }
            else
            {
                statusMessage = "Unable to connect to Nanoleaf controller.";
            }
        }

        private async Task SendPowerCommand(bool on)
        {
            await NanoleafService.SetPowerAsync(on);
            // Optimistic update
            isNanoleafOn = on;
            // Refresh to confirm
            await RefreshState();
        }

        private void SetBrightness(ChangeEventArgs e)
        {
            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Dispose();
            }

            if (int.TryParse(e.Value?.ToString(), out int val))
            {
                brightness = val;
                debounceTimer = new System.Timers.Timer(200);
                debounceTimer.Elapsed += async (sender, e) => await DebouncedSetBrightness();
                debounceTimer.AutoReset = false;
                debounceTimer.Start();
            }
        }

        private async Task DebouncedSetBrightness()
        {
            if (!isNanoleafOn)
            {
                await NanoleafService.SetPowerAsync(true);
                isNanoleafOn = true;
            }
            await NanoleafService.SetBrightnessAsync(brightness);
            // await RefreshState(); // Optional: polling will catch it, or we can refresh
        }

        private async Task SetEffect(ChangeEventArgs e)
        {
            var val = e.Value?.ToString();
            if (!string.IsNullOrEmpty(val))
            {
                selectedEffect = val;
                if (!isNanoleafOn)
                {
                    await NanoleafService.SetPowerAsync(true);
                    isNanoleafOn = true;
                }
                await NanoleafService.SetEffectAsync(selectedEffect);
                await RefreshState();
            }
        }

        private void SetColorTemperature(ChangeEventArgs e)
        {
            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Dispose();
            }

            if (int.TryParse(e.Value?.ToString(), out int val))
            {
                colorTemperature = val;
                debounceTimer = new System.Timers.Timer(200);
                debounceTimer.Elapsed += async (sender, e) => await DebouncedSetColorTemperature();
                debounceTimer.AutoReset = false;
                debounceTimer.Start();
            }
        }

        private async Task DebouncedSetColorTemperature()
        {
            if (!isNanoleafOn)
            {
                await NanoleafService.SetPowerAsync(true);
                isNanoleafOn = true;
            }
            await NanoleafService.SetColorTemperatureAsync(colorTemperature);
            // await RefreshState();
        }

        private async Task SetColor(ChangeEventArgs e)
        {
            hexColor = e.Value?.ToString() ?? "#ffffff";
            
            if (!isNanoleafOn)
            {
                await NanoleafService.SetPowerAsync(true);
                isNanoleafOn = true;
            }

            // Convert Hex to HSV
            var (h, s, v) = HexToHsv(hexColor);
            
            // Nanoleaf uses separate calls for Hue and Saturation usually, or we can try to optimize.
            // Sending Hue and Saturation. Value (Brightness) is handled by the brightness slider.
            await NanoleafService.SetHueAsync(h);
            await NanoleafService.SetSaturationAsync(s);
            
            // Note: We might want to update brightness if the color picker's "value" component is significant, 
            // but usually we keep them separate.
        }

        // Helper methods for Color Conversion
        private string HsvToHex(double h, double s, double v)
        {
            // h [0, 360], s [0, 100], v [0, 100]
            s /= 100;
            v /= 100;

            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;

            if (0 <= h && h < 60) { r = c; g = x; b = 0; }
            else if (60 <= h && h < 120) { r = x; g = c; b = 0; }
            else if (120 <= h && h < 180) { r = 0; g = c; b = x; }
            else if (180 <= h && h < 240) { r = 0; g = x; b = c; }
            else if (240 <= h && h < 300) { r = x; g = 0; b = c; }
            else if (300 <= h && h < 360) { r = c; g = 0; b = x; }

            int rInt = (int)((r + m) * 255);
            int gInt = (int)((g + m) * 255);
            int bInt = (int)((b + m) * 255);

            return $"#{rInt:X2}{gInt:X2}{bInt:X2}";
        }

        private (int h, int s, int v) HexToHsv(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);

            if (hex.Length != 6) return (0, 0, 100);

            int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            // Correct HSV calculation from RGB
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;
            
            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;
            
            double h = 0;
            if (delta == 0) h = 0;
            else if (max == rd) h = 60 * (((gd - bd) / delta) % 6);
            else if (max == gd) h = 60 * (((bd - rd) / delta) + 2);
            else if (max == bd) h = 60 * (((rd - gd) / delta) + 4);
            
            if (h < 0) h += 360;
            
            double s = max == 0 ? 0 : delta / max;
            double v = max;

            return ((int)h, (int)(s * 100), (int)(v * 100));
        }
    }
}

