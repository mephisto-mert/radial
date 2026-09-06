using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using Microsoft.Win32;
using RadialLauncher.Models;
using Serilog;

namespace RadialLauncher.Services.Themes
{
    public class ThemeService : IThemeService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher", "settings.json");

        private static readonly string CustomThemesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher", "CustomThemes");

        private static ThemeService? _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        private readonly List<Theme> _builtinThemes = new()
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
                Accent2R = 140, Accent2G = 90, Accent2B = 245,
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
                Accent2R = 0, Accent2G = 180, Accent2B = 255,
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
                Accent2R = 100, Accent2G = 220, Accent2B = 255,
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
                Accent2R = 255, Accent2G = 105, Accent2B = 180,
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
                Accent2R = 34, Accent2G = 197, Accent2B = 94,
                CenterR = 30, CenterG = 55, CenterB = 38
            },
            new Theme
            {
                Name = "Cyberpunk",
                BgR = 10, BgG = 10, BgB = 16,
                BackgroundOpacity = 0.90,
                IconBgR = 28, IconBgG = 24, IconBgB = 40,
                IconHoverR = 48, IconHoverG = 38, IconHoverB = 65,
                TextR = 255, TextG = 245, TextB = 180,
                AccentR = 255, AccentG = 230, AccentB = 0,
                Accent2R = 255, Accent2G = 0, Accent2B = 128,
                CenterR = 35, CenterG = 30, CenterB = 50
            },
            new Theme
            {
                Name = "Crimson Red",
                BgR = 20, BgG = 10, BgB = 12,
                BackgroundOpacity = 0.90,
                IconBgR = 42, IconBgG = 22, IconBgB = 26,
                IconHoverR = 65, IconHoverG = 30, IconHoverB = 35,
                TextR = 255, TextG = 230, TextB = 235,
                AccentR = 255, AccentG = 59, AccentB = 48,
                Accent2R = 255, Accent2G = 149, Accent2B = 0,
                CenterR = 50, CenterG = 25, CenterB = 30
            },
            new Theme
            {
                Name = "AMOLED Black",
                BgR = 0, BgG = 0, BgB = 0,
                BackgroundOpacity = 0.95,
                IconBgR = 22, IconBgG = 22, IconBgB = 26,
                IconHoverR = 38, IconHoverG = 38, IconHoverB = 44,
                TextR = 255, TextG = 255, TextB = 255,
                AccentR = 168, AccentG = 85, AccentB = 247,
                Accent2R = 59, Accent2G = 130, Accent2B = 246,
                CenterR = 28, CenterG = 28, CenterB = 32
            },
            new Theme
            {
                Name = "High Contrast",
                BgR = 0, BgG = 0, BgB = 0,
                BackgroundOpacity = 1.0,
                IconBgR = 0, IconBgG = 0, IconBgB = 0,
                IconHoverR = 30, IconHoverG = 30, IconHoverB = 30,
                TextR = 255, TextG = 255, TextB = 255,
                AccentR = 255, AccentG = 255, AccentB = 0,
                Accent2R = 255, Accent2G = 255, Accent2B = 255,
                CenterR = 0, CenterG = 0, CenterB = 0,
                ReduceMotion = true
            }
        };

        public event Action<Theme>? OnThemeChanged;
        public event Action<string>? OnShortcutChanged;

        public ThemeService()
        {
            if (!Directory.Exists(CustomThemesDir))
            {
                Directory.CreateDirectory(CustomThemesDir);
            }
            ListenWindowsThemeChanges();
        }

        public List<Theme> GetAllThemes()
        {
            var list = new List<Theme>(_builtinThemes);
            try
            {
                if (Directory.Exists(CustomThemesDir))
                {
                    foreach (var file in Directory.GetFiles(CustomThemesDir, "*.json"))
                    {
                        var json = File.ReadAllText(file);
                        var custom = JsonSerializer.Deserialize<Theme>(json);
                        if (custom != null)
                        {
                            custom.IsCustom = true;
                            list.Add(custom);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load custom themes from {Dir}", CustomThemesDir);
            }
            return list;
        }

        public Theme GetTheme(string name)
        {
            var all = GetAllThemes();
            return all.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? all[0];
        }

        public Theme GetCurrentTheme()
        {
            try
            {
                var settings = LoadSettings();
                if (settings.FollowWindowsTheme)
                {
                    bool isDark = IsWindowsInDarkMode();
                    var candidate = GetTheme(isDark ? "Dark" : "Light");
                    if (settings.ExtractAccentFromWallpaper)
                    {
                        var sysAccent = GetWindowsAccentColor();
                        if (sysAccent.HasValue)
                        {
                            candidate.AccentR = sysAccent.Value.R;
                            candidate.AccentG = sysAccent.Value.G;
                            candidate.AccentB = sysAccent.Value.B;
                        }
                    }
                    return candidate;
                }

                if (!string.IsNullOrEmpty(settings.ThemeName))
                {
                    var custom = GetTheme(settings.ThemeName);
                    if (settings.ExtractAccentFromWallpaper)
                    {
                        var sysAccent = GetWindowsAccentColor();
                        if (sysAccent.HasValue)
                        {
                            custom.AccentR = sysAccent.Value.R;
                            custom.AccentG = sysAccent.Value.G;
                            custom.AccentB = sysAccent.Value.B;
                        }
                    }
                    return custom;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get current theme, falling back to default Dark");
            }
            return _builtinThemes[0];
        }

        public void SetCurrentTheme(string name)
        {
            try
            {
                var settings = LoadSettings();
                settings.ThemeName = name;
                SaveSettings(settings);

                var updated = GetTheme(name);
                OnThemeChanged?.Invoke(updated);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set theme: {Name}", name);
            }
        }

        public void SaveCustomTheme(Theme theme)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(theme.Name)) return;
                theme.IsCustom = true;
                string safeName = Path.GetFileName(theme.Name).Trim();
                if (string.IsNullOrWhiteSpace(safeName)) return;

                string filePath = Path.Combine(CustomThemesDir, $"{safeName}.json");
                string json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                Log.Information("Saved custom theme: {Name}", safeName);
                SetCurrentTheme(safeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save custom theme {Name}", theme.Name);
            }
        }

        public void DeleteCustomTheme(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                string safeName = Path.GetFileName(name).Trim();
                if (string.IsNullOrWhiteSpace(safeName)) return;

                string filePath = Path.Combine(CustomThemesDir, $"{safeName}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Log.Information("Deleted custom theme: {Name}", safeName);
                    SetCurrentTheme("Dark");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete custom theme {Name}", name);
            }
        }

        public string GetActivationShortcut()
        {
            try
            {
                var settings = LoadSettings();
                return string.IsNullOrEmpty(settings.ActivationShortcut) ? "MiddleClick" : settings.ActivationShortcut;
            }
            catch
            {
                return "MiddleClick";
            }
        }

        public void SetActivationShortcut(string shortcut)
        {
            try
            {
                var settings = LoadSettings();
                settings.ActivationShortcut = shortcut;
                SaveSettings(settings);

                OnShortcutChanged?.Invoke(shortcut);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set activation shortcut: {Shortcut}", shortcut);
            }
        }

        public bool GetFollowWindowsTheme() => LoadSettings().FollowWindowsTheme;

        public void SetFollowWindowsTheme(bool follow)
        {
            var settings = LoadSettings();
            settings.FollowWindowsTheme = follow;
            SaveSettings(settings);
            OnThemeChanged?.Invoke(GetCurrentTheme());
        }

        public bool GetExtractAccentFromWallpaper() => LoadSettings().ExtractAccentFromWallpaper;

        public void SetExtractAccentFromWallpaper(bool extract)
        {
            var settings = LoadSettings();
            settings.ExtractAccentFromWallpaper = extract;
            SaveSettings(settings);
            OnThemeChanged?.Invoke(GetCurrentTheme());
        }

        private void ListenWindowsThemeChanges()
        {
            try
            {
                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (LoadSettings().FollowWindowsTheme || LoadSettings().ExtractAccentFromWallpaper)
                    {
                        OnThemeChanged?.Invoke(GetCurrentTheme());
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to hook Windows theme events");
            }
        }

        public static bool IsWindowsInDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var val = key?.GetValue("AppsUseLightTheme");
                if (val is int intVal) return intVal == 0;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed querying AppsUseLightTheme registry value");
            }
            return true;
        }

        public static Color? GetWindowsAccentColor()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                var val = key?.GetValue("ColorizationColor");
                if (val is int intVal)
                {
                    byte a = (byte)((intVal >> 24) & 0xFF);
                    byte r = (byte)((intVal >> 16) & 0xFF);
                    byte g = (byte)((intVal >> 8) & 0xFF);
                    byte b = (byte)(intVal & 0xFF);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed querying DWM ColorizationColor registry value");
            }
            return null;
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read settings from {Path}", SettingsPath);
            }
            return new AppSettings();
        }

        private void SaveSettings(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to write settings to {Path}", SettingsPath);
            }
        }

        public class AppSettings
        {
            public string ThemeName { get; set; } = "Dark";
            public string ActivationShortcut { get; set; } = "MiddleClick";
            public bool FollowWindowsTheme { get; set; } = false;
            public bool ExtractAccentFromWallpaper { get; set; } = false;
            public string DensityMode { get; set; } = "Expanded";
            public bool HasSeenTutorial { get; set; } = false;
        }
    }
}
