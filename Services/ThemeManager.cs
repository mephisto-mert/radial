using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace RadialLauncher.Services
{
    public class Theme
    {
        public string Name { get; set; } = "Dark";
        public byte BgR { get; set; }
        public byte BgG { get; set; }
        public byte BgB { get; set; }
        public double BackgroundOpacity { get; set; } = 0.85;
        public byte IconBgR { get; set; }
        public byte IconBgG { get; set; }
        public byte IconBgB { get; set; }
        public byte IconHoverR { get; set; }
        public byte IconHoverG { get; set; }
        public byte IconHoverB { get; set; }
        public byte TextR { get; set; } = 255;
        public byte TextG { get; set; } = 255;
        public byte TextB { get; set; } = 255;
        public byte AccentR { get; set; }
        public byte AccentG { get; set; }
        public byte AccentB { get; set; }
        public byte CenterR { get; set; }
        public byte CenterG { get; set; }
        public byte CenterB { get; set; }

        // Helper properties (not serialized, computed)
        public Color BackgroundColor => Color.FromRgb(BgR, BgG, BgB);
        public Color IconBackgroundColor => Color.FromRgb(IconBgR, IconBgG, IconBgB);
        public Color IconHoverColor => Color.FromRgb(IconHoverR, IconHoverG, IconHoverB);
        public Color TextColor => Color.FromRgb(TextR, TextG, TextB);
        public Color AccentColor => Color.FromRgb(AccentR, AccentG, AccentB);
        public Color CenterButtonColor => Color.FromRgb(CenterR, CenterG, CenterB);
    }

    public static class ThemeManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher", "settings.json");

        private static readonly List<Theme> _themes = new()
        {
            new Theme
            {
                Name = "Dark",
                BgR = 18, BgG = 18, BgB = 22,
                BackgroundOpacity = 0.88,
                IconBgR = 38, IconBgG = 38, IconBgB = 42,
                IconHoverR = 58, IconHoverG = 58, IconHoverB = 65,
                TextR = 230, TextG = 230, TextB = 235,
                AccentR = 88, AccentG = 140, AccentB = 236,
                CenterR = 50, CenterG = 50, CenterB = 55
            },
            new Theme
            {
                Name = "Light",
                BgR = 240, BgG = 240, BgB = 245,
                BackgroundOpacity = 0.92,
                IconBgR = 255, IconBgG = 255, IconBgB = 255,
                IconHoverR = 220, IconHoverG = 225, IconHoverB = 235,
                TextR = 30, TextG = 30, TextB = 35,
                AccentR = 50, AccentG = 120, AccentB = 220,
                CenterR = 230, CenterG = 230, CenterB = 235
            },
            new Theme
            {
                Name = "Midnight Blue",
                BgR = 12, BgG = 20, BgB = 40,
                BackgroundOpacity = 0.90,
                IconBgR = 20, IconBgG = 35, IconBgB = 60,
                IconHoverR = 30, IconHoverG = 50, IconHoverB = 85,
                TextR = 200, TextG = 220, TextB = 255,
                AccentR = 0, AccentG = 180, AccentB = 255,
                CenterR = 25, CenterG = 40, CenterB = 70
            },
            new Theme
            {
                Name = "Purple Haze",
                BgR = 25, BgG = 12, BgB = 35,
                BackgroundOpacity = 0.88,
                IconBgR = 45, IconBgG = 25, IconBgB = 55,
                IconHoverR = 65, IconHoverG = 35, IconHoverB = 80,
                TextR = 230, TextG = 210, TextB = 255,
                AccentR = 200, AccentG = 80, AccentB = 255,
                CenterR = 50, CenterG = 30, CenterB = 65
            },
            new Theme
            {
                Name = "Forest",
                BgR = 12, BgG = 28, BgB = 18,
                BackgroundOpacity = 0.88,
                IconBgR = 25, IconBgG = 48, IconBgB = 32,
                IconHoverR = 35, IconHoverG = 68, IconHoverB = 45,
                TextR = 210, TextG = 245, TextB = 220,
                AccentR = 80, AccentG = 220, AccentB = 100,
                CenterR = 30, CenterG = 55, CenterB = 38
            }
        };

        public static List<Theme> GetAllThemes() => _themes;

        public static Theme GetTheme(string name)
        {
            return _themes.Find(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? _themes[0];
        }

        public static Theme GetCurrentTheme()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null && !string.IsNullOrEmpty(settings.ThemeName))
                        return GetTheme(settings.ThemeName);
                }
            }
            catch { }
            return _themes[0]; // Dark default
        }

        public static void SetCurrentTheme(string name)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                AppSettings settings;
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    settings = new AppSettings();
                }
                settings.ThemeName = name;
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private class AppSettings
        {
            public string ThemeName { get; set; } = "Dark";
        }
    }
}
