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
                BgR = 18, BgG = 18, BgB = 20,
                BackgroundOpacity = 0.94,
                IconBgR = 34, IconBgG = 34, IconBgB = 38,
                IconHoverR = 48, IconHoverG = 48, IconHoverB = 54,
                TextR = 244, TextG = 244, TextB = 245,
                AccentR = 129, AccentG = 140, AccentB = 248,
                Accent2R = 167, Accent2G = 139, Accent2B = 250,
                CenterR = 42, CenterG = 42, CenterB = 48
            },
            new Theme
            {
                Name = "Light",
                BgR = 244, BgG = 244, BgB = 246,
                BackgroundOpacity = 0.96,
                IconBgR = 255, IconBgG = 255, IconBgB = 255,
                IconHoverR = 228, IconHoverG = 228, IconHoverB = 231,
                TextR = 17, TextG = 24, TextB = 39,
                AccentR = 37, AccentG = 99, AccentB = 235,
                Accent2R = 79, Accent2G = 70, Accent2B = 229,
                CenterR = 228, CenterG = 228, CenterB = 231
            },
            new Theme
            {
                Name = "Midnight Blue",
                BgR = 11, BgG = 19, BgB = 43,
                BackgroundOpacity = 0.94,
                IconBgR = 28, IconBgG = 37, IconBgB = 65,
                IconHoverR = 45, IconHoverG = 60, IconHoverB = 98,
                TextR = 224, TextG = 242, TextB = 254,
                AccentR = 0, AccentG = 229, AccentB = 255,
                Accent2R = 59, Accent2G = 130, Accent2B = 246,
                CenterR = 28, CenterG = 37, CenterB = 65
            },
            new Theme
            {
                Name = "Purple Haze",
                BgR = 24, BgG = 11, BgB = 38,
                BackgroundOpacity = 0.94,
                IconBgR = 50, IconBgG = 22, IconBgB = 80,
                IconHoverR = 72, IconHoverG = 32, IconHoverB = 115,
                TextR = 253, TextG = 244, TextB = 255,
                AccentR = 217, AccentG = 70, AccentB = 239,
                Accent2R = 192, Accent2G = 132, Accent2B = 252,
                CenterR = 50, CenterG = 22, CenterB = 80
            },
            new Theme
            {
                Name = "Forest",
                BgR = 9, BgG = 31, BgB = 20,
                BackgroundOpacity = 0.94,
                IconBgR = 19, IconBgG = 62, IconBgB = 40,
                IconHoverR = 28, IconHoverG = 90, IconHoverB = 58,
                TextR = 236, TextG = 253, TextB = 245,
                AccentR = 16, AccentG = 185, AccentB = 129,
                Accent2R = 52, Accent2G = 211, Accent2B = 153,
                CenterR = 19, CenterG = 62, CenterB = 40
            },
            new Theme
            {
                Name = "Cyberpunk",
                BgR = 10, BgG = 9, BgB = 21,
                BackgroundOpacity = 0.95,
                IconBgR = 30, IconBgG = 27, IconBgB = 56,
                IconHoverR = 47, IconHoverG = 42, IconHoverB = 88,
                TextR = 254, TextG = 240, TextB = 138,
                AccentR = 255, AccentG = 230, AccentB = 0,
                Accent2R = 255, Accent2G = 0, Accent2B = 127,
                CenterR = 30, CenterG = 27, CenterB = 56
            },
            new Theme
            {
                Name = "Crimson Red",
                BgR = 31, BgG = 10, BgB = 13,
                BackgroundOpacity = 0.94,
                IconBgR = 64, IconBgG = 20, IconBgB = 26,
                IconHoverR = 92, IconHoverG = 29, IconHoverB = 38,
                TextR = 255, TextG = 241, TextB = 242,
                AccentR = 239, AccentG = 68, AccentB = 68,
                Accent2R = 249, Accent2G = 115, Accent2B = 22,
                CenterR = 64, CenterG = 20, CenterB = 26
            },
            new Theme
            {
                Name = "AMOLED Black",
                BgR = 0, BgG = 0, BgB = 0,
                BackgroundOpacity = 0.98,
                IconBgR = 20, IconBgG = 20, IconBgB = 20,
                IconHoverR = 38, IconHoverG = 38, IconHoverB = 38,
                TextR = 255, TextG = 255, TextB = 255,
                AccentR = 168, AccentG = 85, AccentB = 247,
                Accent2R = 59, Accent2G = 130, Accent2B = 246,
                CenterR = 24, CenterG = 24, CenterB = 24
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

        public bool GetAutoCheckUpdates() => LoadSettings().AutoCheckUpdates;

        public void SetAutoCheckUpdates(bool autoCheck)
        {
            var settings = LoadSettings();
            settings.AutoCheckUpdates = autoCheck;
            SaveSettings(settings);
        }

        public void ResetSettingsToDefault()
        {
            try
            {
                var defaultSettings = new AppSettings();
                SaveSettings(defaultSettings);
                SetCurrentTheme(defaultSettings.ThemeName);
                SetActivationShortcut(defaultSettings.ActivationShortcut);
                Log.Information("Reset settings to factory defaults");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reset settings to default");
            }
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
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch (JsonException jsonEx)
            {
                Log.Warning(jsonEx, "Corrupted settings file at {Path}. Creating backup and resetting to defaults.", SettingsPath);
                try
                {
                    string backupPath = $"{SettingsPath}.corrupt.{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
                    File.Copy(SettingsPath, backupPath, true);
                    Log.Information("Backed up corrupted settings to {BackupPath}", backupPath);
                }
                catch (Exception backupEx)
                {
                    Log.Warning(backupEx, "Failed to create backup copy of corrupted settings");
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
            public int SchemaVersion { get; set; } = 1;
            public string ThemeName { get; set; } = "Dark";
            public string ActivationShortcut { get; set; } = "MiddleClick";
            public bool FollowWindowsTheme { get; set; } = false;
            public bool ExtractAccentFromWallpaper { get; set; } = false;
            public string DensityMode { get; set; } = "Expanded";
            public bool HasSeenTutorial { get; set; } = false;
            public bool AutoCheckUpdates { get; set; } = true;
        }
    }
}
