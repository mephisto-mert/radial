using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace RadialLauncher.Models
{
    public class Theme
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = "Dark";
        public bool IsCustom { get; set; } = false;

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Id))
                {
                    string loc = RadialLauncher.Services.Localization.LocalizationService.Instance.GetString($"Theme_{Id}", string.Empty);
                    if (!string.IsNullOrEmpty(loc)) return loc;
                }
                return !string.IsNullOrEmpty(Name) ? Name : "Dark";
            }
        }

        // Background
        public byte BgR { get; set; } = 18;
        public byte BgG { get; set; } = 18;
        public byte BgB { get; set; } = 22;
        public double BackgroundOpacity { get; set; } = 0.88;

        // Icon Bubble
        public byte IconBgR { get; set; } = 38;
        public byte IconBgG { get; set; } = 38;
        public byte IconBgB { get; set; } = 42;

        public byte IconHoverR { get; set; } = 58;
        public byte IconHoverG { get; set; } = 58;
        public byte IconHoverB { get; set; } = 65;

        // Text
        public byte TextR { get; set; } = 230;
        public byte TextG { get; set; } = 230;
        public byte TextB { get; set; } = 235;

        // Accent Primary
        public byte AccentR { get; set; } = 88;
        public byte AccentG { get; set; } = 140;
        public byte AccentB { get; set; } = 236;

        // Accent Secondary (for gradients)
        public byte Accent2R { get; set; } = 140;
        public byte Accent2G { get; set; } = 90;
        public byte Accent2B { get; set; } = 245;
        public bool UseGradientAccent { get; set; } = true;

        // Center Button
        public byte CenterR { get; set; } = 50;
        public byte CenterG { get; set; } = 50;
        public byte CenterB { get; set; } = 55;

        // Options
        public bool EnableBlurBackdrop { get; set; } = true;
        public bool ReduceMotion { get; set; } = false;
        public string DensityMode { get; set; } = "Expanded"; // "Compact" or "Expanded"

        // Computed Wpf Colors & Brushes (Not Serialized)
        [JsonIgnore]
        public Color BackgroundColor => Color.FromRgb(BgR, BgG, BgB);

        [JsonIgnore]
        public Color IconBackgroundColor => Color.FromRgb(IconBgR, IconBgG, IconBgB);

        [JsonIgnore]
        public Color IconHoverColor => Color.FromRgb(IconHoverR, IconHoverG, IconHoverB);

        [JsonIgnore]
        public Color TextColor => Color.FromRgb(TextR, TextG, TextB);

        [JsonIgnore]
        public Color AccentColor => Color.FromRgb(AccentR, AccentG, AccentB);

        [JsonIgnore]
        public Color Accent2Color => Color.FromRgb(Accent2R, Accent2G, Accent2B);

        [JsonIgnore]
        public Color CenterButtonColor => Color.FromRgb(CenterR, CenterG, CenterB);

        [JsonIgnore]
        public Brush AccentBrush
        {
            get
            {
                if (!UseGradientAccent)
                {
                    return new SolidColorBrush(AccentColor);
                }

                var grad = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1)
                };
                grad.GradientStops.Add(new GradientStop(AccentColor, 0.0));
                grad.GradientStops.Add(new GradientStop(Accent2Color, 1.0));
                return grad;
            }
        }
    }
}
