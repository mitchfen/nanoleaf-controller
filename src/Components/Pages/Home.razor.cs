using Microsoft.AspNetCore.Components;
using nanoleaf_controller.Services;
using System.Text;
using System.Timers;

namespace nanoleaf_controller.Components.Pages
{
    public partial class Home : ComponentBase, IDisposable
    {
        [Inject]
        private NanoleafService NanoleafService { get; set; } = default!;

        private System.Timers.Timer? debounceTimer;
        private System.Timers.Timer? pollingTimer;
        private System.Timers.Timer? animationTimer;
        private string? statusMessage;
        
        // Global Controls
        private int brightness = 50;
        private string selectedEffect = "Cozy";
        private List<string> effects = new();
        private bool isNanoleafOn;
        private string hexColor = "#ffffff";

        // Panel Layout
        private PanelLayout? layout;
        private Dictionary<int, string> panelColors = new();
        private List<string> currentPalette = new();
        private Random random = new Random();
        
        // SVG Visualization
        private int viewBoxX = 0;
        private int viewBoxY = 0;
        private int viewBoxWidth = 1000;
        private int viewBoxHeight = 1000;

        protected override async Task OnInitializedAsync()
        {
            await RefreshState();
            await RefreshLayout();

            pollingTimer = new System.Timers.Timer(5000);
            pollingTimer.Elapsed += async (sender, e) =>
            {
                await RefreshState();
                await InvokeAsync(StateHasChanged);
            };
            pollingTimer.AutoReset = true;
            pollingTimer.Start();
            
            animationTimer = new System.Timers.Timer(3000); // Update animation every 3s
            animationTimer.Elapsed += (sender, e) => 
            {
                if (isNanoleafOn && currentPalette.Any())
                {
                    AnimatePalette();
                    InvokeAsync(StateHasChanged);
                }
            };
            animationTimer.AutoReset = true;
            animationTimer.Start();
        }

        public void Dispose()
        {
            debounceTimer?.Dispose();
            pollingTimer?.Dispose();
            animationTimer?.Dispose();
        }

        private async Task RefreshState()
        {
            var state = await NanoleafService.GetStateAsync();
            if (state != null)
            {
                isNanoleafOn = state.IsOn;
                brightness = state.Brightness;
                
                if (!string.IsNullOrEmpty(state.SelectedEffect))
                {
                    selectedEffect = state.SelectedEffect;
                }
                
                if (state.Effects != null)
                {
                    effects = state.Effects;
                }
                
                hexColor = HsvToHex(state.Hue, state.Saturation, 100);
                colorMode = state.ColorMode;
                
                await UpdateDisplayColor(state.IsOn, state.ColorMode, state.Hue, state.Saturation, state.ColorTemperature, state.SelectedEffect);

                statusMessage = null;
            }
            else
            {
                statusMessage = "Unable to connect to Nanoleaf controller.";
            }
        }
        
        private void AnimatePalette()
        {
            if (layout?.PositionData == null || !currentPalette.Any()) return;

            // smooth random transition
            // To make it less chaotic, maybe only change a subset of panels? 
            // Or just re-roll all. With CSS transition it looks like a cross-fade.
            foreach(var p in layout.PositionData)
            {
                panelColors[p.PanelId] = currentPalette[random.Next(currentPalette.Count)];
            }
        }

        private async Task UpdateDisplayColor(bool isOn, string? colorMode, int hue, int sat, int temp, string? currentEffectName)
        {
            if (layout?.PositionData == null) return;

            if (!isOn)
            {
                currentPalette.Clear();
                foreach(var p in layout.PositionData)
                {
                    panelColors[p.PanelId] = "#343a40"; // Dark gray for off
                }
            }
            else
            {
                if (colorMode == "effect" && !string.IsNullOrEmpty(currentEffectName))
                {
                    // If we already have the palette for this effect, keep animating? 
                    // But we might need to fetch if effect changed. 
                    // Optimization: check if currentEffectName changed or currentPalette is empty.
                    // For now, let's fetch to be safe (or we can trust the caller).
                    
                    var effectDetails = await NanoleafService.GetEffectDetailsAsync(currentEffectName);
                    
                    if (effectDetails?.Palette != null && effectDetails.Palette.Any())
                    {
                        currentPalette = effectDetails.Palette
                            .Select(c => HsvToHex(c.Hue, c.Saturation, c.Brightness))
                            .ToList();

                        // Initial distribution
                        AnimatePalette();
                    }
                    else
                    {
                        currentPalette.Clear();
                        // Fallback
                        var color = HsvToHex(hue, sat, 100);
                        foreach(var p in layout.PositionData) panelColors[p.PanelId] = color;
                    }
                }
                else
                {
                    currentPalette.Clear();
                    string color;
                    if (colorMode == "ct")
                    {
                        color = ColorTemperatureToHex(temp);
                    }
                    else
                    {
                        // Fallback for 'hs'
                        color = HsvToHex(hue, sat, 100);
                    }
                    foreach(var p in layout.PositionData) panelColors[p.PanelId] = color;
                }
            }
        }

        private async Task RefreshLayout()
        {
            layout = await NanoleafService.GetPanelLayoutAsync();
            if (layout?.PositionData != null && layout.PositionData.Any())
            {
                CalculateViewBox();
                // Initialize panel colors if not set
                foreach(var p in layout.PositionData)
                {
                    if (!panelColors.ContainsKey(p.PanelId))
                    {
                        panelColors[p.PanelId] = "#e9ecef";
                    }
                }
            }
        }

        private void CalculateViewBox()
        {
            if (layout?.PositionData == null) return;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            int padding = 100; // Extra padding

            foreach (var p in layout.PositionData)
            {
                // Approximate bounding box of a panel based on SideLength
                // We use -p.Y for SVG coordinates (flip Y axis)
                int svgY = -p.Y;
                
                int r = layout.SideLength; 
                if (p.X - r < minX) minX = p.X - r;
                if (p.X + r > maxX) maxX = p.X + r;
                if (svgY - r < minY) minY = svgY - r;
                if (svgY + r > maxY) maxY = svgY + r;
            }

            viewBoxX = minX - padding;
            viewBoxY = minY - padding;
            viewBoxWidth = (maxX - minX) + (padding * 2);
            viewBoxHeight = (maxY - minY) + (padding * 2);
        }

        private string GetSvgPoints(PanelPosition p)
        {
            // Simple logic: return points based on ShapeType
            // 0: Triangle, 1: Rhythm, 2: Square, 3: Control Square, 4: Hexagon, 7: Mini Triangle, 8: Large Triangle, 12: Elements Hexagon
            
            double cx = p.X;
            double cy = p.Y; 
                             
            double side = layout?.SideLength ?? 100;
            // Radius of circumcircle
            double r = side / Math.Sqrt(3); 
            
            // Adjust radius based on shape
            if (p.ShapeType == 0 || p.ShapeType == 7 || p.ShapeType == 8) // Triangles
            {
                r = side * 0.57735; // side / sqrt(3)
                // 3 vertices
                return GetPolygonPoints(cx, -cy, r, 3, p.O);
            }
            else if (p.ShapeType == 4 || p.ShapeType == 12) // Hexagons
            {
                r = side; // side length is radius for hexagon
                return GetPolygonPoints(cx, -cy, r, 6, p.O);
            }
             else if (p.ShapeType == 2 || p.ShapeType == 3) // Squares
            {
                // Square: side is side length. circumradius = side / sqrt(2)
                r = side * 0.707;
                return GetPolygonPoints(cx, -cy, r, 4, p.O + 45); // Offset by 45 to align flat sides
            }

            // Default circle-ish (10-gon)
            return GetPolygonPoints(cx, -cy, side/2, 10, 0);
        }

        private string GetPolygonPoints(double cx, double cy, double r, int sides, double rotationDeg)
        {
            var points = new StringBuilder();
            double angleStep = 2 * Math.PI / sides;
            double rotationRad = rotationDeg * Math.PI / 180.0;
            
            // Triangle specific adjustment to match visual expectation if needed
            if (sides == 3) rotationRad += Math.PI / 6; 

            for (int i = 0; i < sides; i++)
            {
                double angle = i * angleStep + rotationRad;
                double x = cx + r * Math.Cos(angle);
                double y = cy + r * Math.Sin(angle); // SVG Y is down
                points.Append($"{x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)},{y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} ");
            }
            return points.ToString().Trim();
        }
        
        private string GetPanelColor(int panelId)
        {
             if (panelColors.TryGetValue(panelId, out var color))
             {
                 return color;
             }
             return "#e9ecef";
        }

        private async Task SendPowerCommand(bool on)
        {
            await NanoleafService.SetPowerAsync(on);
            isNanoleafOn = on;
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

        private async Task SetColor(ChangeEventArgs e)
        {
            hexColor = e.Value?.ToString() ?? "#ffffff";
            
            if (!isNanoleafOn)
            {
                await NanoleafService.SetPowerAsync(true);
                isNanoleafOn = true;
            }

            var (h, s, v) = HexToHsv(hexColor);
            
            await NanoleafService.SetHueAsync(h);
            await NanoleafService.SetSaturationAsync(s);
            
            // Immediate feedback: Set all panels to this color
            if (layout?.PositionData != null)
            {
                foreach(var p in layout.PositionData) panelColors[p.PanelId] = hexColor;
            }
        }

        // Helper methods for Color Conversion
        private string HsvToHex(double h, double s, double v)
        {
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
        
        private string ColorTemperatureToHex(int kelvin)
        {
            double temp = kelvin / 100.0;
            double r, g, b;

            // Red
            if (temp <= 66)
            {
                r = 255;
            }
            else
            {
                r = 329.698727446 * Math.Pow(temp - 60, -0.1332047592);
                r = Clamp(r, 0, 255);
            }

            // Green
            if (temp <= 66)
            {
                g = 99.4708025861 * Math.Log(temp) - 161.1195681661;
            }
            else
            {
                g = 288.1221695283 * Math.Pow(temp - 60, -0.0755148492);
            }
            g = Clamp(g, 0, 255);

            // Blue
            if (temp >= 66)
            {
                b = 255;
            }
            else if (temp <= 19)
            {
                b = 0;
            }
            else
            {
                b = 138.5177312231 * Math.Log(temp - 10) - 305.0447927307;
                b = Clamp(b, 0, 255);
            }

            return $"#{(int)r:X2}{(int)g:X2}{(int)b:X2}";
        }

        private double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }
    }
}