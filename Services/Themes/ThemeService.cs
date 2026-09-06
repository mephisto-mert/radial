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
        private static string SettingsPath => RadialLauncher.Services.Data.UserDataPathProvider.Instance.GetSettingsPath();
        private static string CustomThemesDir => RadialLauncher.Services.Data.UserDataPathProvider.Instance.GetCustomThemesFolder();

        private static ThemeService? _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        private readonly List<Theme> _builtinThemes = new()
        {
            new Theme
            {
                Name = "Dark",
                BgR = 18, BgG = 18, BgB = 22,
                BackgroundOpacity = 0.90,
                IconBgR = 36, IconBgG = 36, IconBgB = 42,
                IconHoverR = 52, IconHoverG = 52, IconHoverB = 60,
                TextR = 240, TextG = 240, TextB = 245,
                AccentR = 88, AccentG = 140, AccentB = 236,
                Accent2R = 140, Accent2G = 90, Accent2B = 245,
                CenterR = 44, CenterG = 44, CenterB = 52
            },
            new Theme
            {
                Name = "White",
                BgR = 245, BgG = 245, BgB = 247,
                BackgroundOpacity = 0.95,
                IconBgR = 255, IconBgG = 255, IconBgB = 255,
                IconHoverR = 230, IconHoverG = 232, IconHoverB = 238,
                TextR = 18, TextG = 20, TextB = 26,
                AccentR = 37, AccentG = 99, AccentB = 235,
                Accent2R = 79, Accent2G = 70, Accent2B = 229,
                CenterR = 232, CenterG = 234, CenterB = 240
            },
            new Theme
            {
                Name = "Red",
                BgR = 28, BgG = 10, BgB = 14,
                BackgroundOpacity = 0.92,
                IconBgR = 58, IconBgG = 20, IconBgB = 26,
                IconHoverR = 84, IconHoverG = 28, IconHoverB = 38,
                TextR = 255, TextG = 245, TextB = 245,
                AccentR = 239, AccentG = 68, AccentB = 68,
                Accent2R = 249, Accent2G = 115, Accent2B = 22,
                CenterR = 58, CenterG = 20, CenterB = 26
            },
            new Theme
            {
                Name = "Blue",
                BgR = 10, BgG = 20, BgB = 42,
                BackgroundOpacity = 0.92,
                IconBgR = 24, IconBgG = 42, IconBgB = 80,
                IconHoverR = 36, IconHoverG = 62, IconHoverB = 118,
                TextR = 240, TextG = 248, TextB = 255,
                AccentR = 0, AccentG = 210, AccentB = 255,
                Accent2R = 59, Accent2G = 130, Accent2B = 246,
                CenterR = 24, CenterG = 42, CenterB = 80
            },
            new Theme
            {
                Name = "Purple",
                BgR = 22, BgG = 10, BgB = 36,
                BackgroundOpacity = 0.92,
                IconBgR = 48, IconBgG = 22, IconBgB = 78,
                IconHoverR = 72, IconHoverG = 34, IconHoverB = 116,
                TextR = 253, TextG = 245, TextB = 255,
                AccentR = 217, AccentG = 70, AccentB = 239,
                Accent2R = 192, Accent2G = 132, Accent2B = 252,
                CenterR = 48, CenterG = 22, CenterB = 78
            },
            new Theme
            {
                Name = "Forest",
                BgR = 10, BgG = 28, BgB = 18,
                BackgroundOpacity = 0.92,
                IconBgR = 20, IconBgG = 58, IconBgB = 38,
                IconHoverR = 30, IconHoverG = 84, IconHoverB = 56,
                TextR = 240, TextG = 253, TextB = 244,
                AccentR = 16, AccentG = 185, AccentB = 129,
                Accent2R = 52, Accent2G = 211, Accent2B = 153,
                CenterR = 20, CenterG = 58, CenterB = 38
            },
            new Theme
            {
                Name = "AMOLED Black",
                BgR = 0, BgG = 0, BgB = 0,
                BackgroundOpacity = 0.98,
                IconBgR = 18, IconBgG = 18, IconBgB = 18,
                IconHoverR = 34, IconHoverG = 34, IconHoverB = 34,
                TextR = 255, TextG = 255, TextB = 255,
                AccentR = 168, AccentG = 85, AccentB = 247,
                Accent2R = 59, Accent2G = 130, Accent2B = 246,
                CenterR = 22, CenterG = 22, CenterB = 22
            },
            new Theme
            {
                Name = "High Contrast",
                BgR = 0, BgG = 0, BgB = 0,
                BackgroundOpacity = 1.0,
                IconBgR = 0, IconBgG = 0, IconBgB = 0,
                IconHoverR = 40, IconHoverG = 40, IconHoverB = 40,
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
            return new List<Theme>(_builtinThemes);
        }

        public Theme GetTheme(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return _builtinThemes[0];
            string trimmed = name.Trim();
            
            // Map legacy / alternative names to 8 curated themes
            if (string.Equals(trimmed, "Light", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "White", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "White");
            if (string.Equals(trimmed, "Crimson Red", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "Red", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "Red");
            if (string.Equals(trimmed, "Midnight Blue", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "Blue", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "Blue");
            if (string.Equals(trimmed, "Purple Haze", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "Purple", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "Purple");
            if (string.Equals(trimmed, "Forest", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "Forest");
            if (string.Equals(trimmed, "AMOLED Black", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "AMOLED", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "Black", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "AMOLED Black");
            if (string.Equals(trimmed, "High Contrast", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "Contrast", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "High Contrast");
            if (string.Equals(trimmed, "Dark", StringComparison.OrdinalIgnoreCase))
                return _builtinThemes.First(t => t.Name == "Dark");

            return _builtinThemes.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? _builtinThemes[0];
        }

        public Theme GetCurrentTheme()
        {
            try
            {
                var settings = LoadSettings();
                Theme selected = !string.IsNullOrEmpty(settings.ThemeName)
                    ? GetTheme(settings.ThemeName)
                    : _builtinThemes[0];

                selected.BackgroundOpacity = Math.Clamp(settings.RadialOpacity, 0.20, 1.0);
                return selected;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get current theme, falling back to default Dark");
            }
            var fallback = _builtinThemes[0];
            fallback.BackgroundOpacity = 0.90;
            return fallback;
        }

        public double GetRadialOpacity()
        {
            try
            {
                var settings = LoadSettings();
                return Math.Clamp(settings.RadialOpacity, 0.20, 1.0);
            }
            catch
            {
                return 0.90;
            }
        }

        public void SetRadialOpacity(double opacity)
        {
            try
            {
                var settings = LoadSettings();
                settings.RadialOpacity = Math.Clamp(opacity, 0.20, 1.0);
                SaveSettings(settings);

                var updated = GetCurrentTheme();
                OnThemeChanged?.Invoke(updated);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set radial opacity: {Opacity}", opacity);
            }
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
                if (File.Exists(SettingsPath))
                {
                    string preReset = $"{SettingsPath}.prereset.{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
                    File.Copy(SettingsPath, preReset, true);
                    Log.Information("Created safety backup prior to reset: {Path}", preReset);
                }

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
            return null;
        }

        private AppSettings LoadSettings()
        {
            // 1. Try primary settings.json
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
                Log.Warning(jsonEx, "Corrupted primary settings file at {Path}. Attempting backup recovery.", SettingsPath);
                try
                {
                    string backupCorrupt = $"{SettingsPath}.corrupt.{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
                    File.Copy(SettingsPath, backupCorrupt, true);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read primary settings from {Path}", SettingsPath);
            }

            // 2. Try settings.json.bak
            string bakPath = $"{SettingsPath}.bak";
            try
            {
                if (File.Exists(bakPath))
                {
                    var bakJson = File.ReadAllText(bakPath);
                    var recovered = JsonSerializer.Deserialize<AppSettings>(bakJson);
                    if (recovered != null)
                    {
                        Log.Information("Successfully recovered settings from {BakPath}", bakPath);
                        SaveSettings(recovered);
                        return recovered;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to recover from {BakPath}", bakPath);
            }

            // 3. Fallback: return default settings
            var defaultFallback = new AppSettings();
            return defaultFallback;
        }

        private void SaveSettings(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmpPath = $"{SettingsPath}.tmp";
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, SettingsPath, overwrite: true);

                // Mirror to .bak for crash recovery
                string bakPath = $"{SettingsPath}.bak";
                File.Copy(SettingsPath, bakPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to write settings atomically to {Path}", SettingsPath);
            }
        }

        public AppSettings GetSettings() => LoadSettings();

        public void UpdateSettings(AppSettings settings)
        {
            if (settings == null) return;
            SaveSettings(settings);
            var updated = GetTheme(settings.ThemeName);
            OnThemeChanged?.Invoke(updated);
        }

        public class AppSettings
        {
            public int SchemaVersion { get; set; } = 1;
            public string ThemeName { get; set; } = "Dark";
            public double RadialOpacity { get; set; } = 0.90;
            public string ActivationShortcut { get; set; } = "MiddleClick";
            public bool FollowWindowsTheme { get; set; } = false;
            public bool ExtractAccentFromWallpaper { get; set; } = false;
            public string DensityMode { get; set; } = "Expanded";
            public bool HasSeenTutorial { get; set; } = false;
            public bool AutoCheckUpdates { get; set; } = true;
            public string Language { get; set; } = "en";
        }
    }
}
