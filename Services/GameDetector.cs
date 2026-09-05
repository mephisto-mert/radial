using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace RadialLauncher.Services
{
    public class DetectedGame
    {
        public string Name { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
    }

    public static class GameDetector
    {
        public static List<DetectedGame> DetectAllGames()
        {
            var games = new List<DetectedGame>();
            games.AddRange(DetectSteamGames());
            games.AddRange(DetectEpicGames());
            return games;
        }

        public static List<DetectedGame> DetectSteamGames()
        {
            var games = new List<DetectedGame>();
            try
            {
                // Pre-scan Desktop and Start Menu for Steam shortcuts to get official .ico paths
                var shortcutIcons = ScanSteamShortcutIcons();

                // Get Steam install path from registry
                string? steamPath = null;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    steamPath = key?.GetValue("SteamPath") as string;
                }
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                    return games;

                // Find all library folders
                var libraryFolders = new List<string> { steamPath };
                string libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFile))
                {
                    var content = File.ReadAllText(libraryFile);
                    foreach (var line in content.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("\"path\""))
                        {
                            var parts = trimmed.Split('"');
                            if (parts.Length >= 4)
                            {
                                var path = parts[3].Replace("\\\\", "\\");
                                if (Directory.Exists(path) && !libraryFolders.Contains(path))
                                    libraryFolders.Add(path);
                            }
                        }
                    }
                }

                // Parse appmanifest files in each library
                foreach (var libFolder in libraryFolders)
                {
                    string appsDir = Path.Combine(libFolder, "steamapps");
                    if (!Directory.Exists(appsDir)) continue;

                    foreach (var manifest in Directory.GetFiles(appsDir, "appmanifest_*.acf"))
                    {
                        try
                        {
                            var acfContent = File.ReadAllText(manifest);
                            string? name = ExtractVdfValue(acfContent, "name");
                            string? appId = ExtractVdfValue(acfContent, "appid");
                            string? installdir = ExtractVdfValue(acfContent, "installdir");

                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(appId))
                            {
                                if (name.Contains("Steamworks") || name.Contains("Proton") || name.Contains("Redistribut"))
                                    continue;

                                // Find icon:
                                // 1. From Desktop/Start Menu shortcut
                                string iconPath = "";
                                if (shortcutIcons.TryGetValue(appId, out var scIcon) && File.Exists(scIcon))
                                {
                                    iconPath = scIcon;
                                }
                                else
                                {
                                    // 2. From game installation folder
                                    if (!string.IsNullOrEmpty(installdir))
                                    {
                                        string gameDir = Path.Combine(appsDir, "common", installdir);
                                        if (Directory.Exists(gameDir))
                                        {
                                            var exes = Directory.GetFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly);
                                            if (exes.Length > 0)
                                            {
                                                iconPath = exes[0];
                                            }
                                        }
                                    }
                                }

                                games.Add(new DetectedGame
                                {
                                    Name = name,
                                    ExePath = $"steam://rungameid/{appId}",
                                    Platform = "Steam",
                                    IconPath = iconPath
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return games;
        }

        public static Dictionary<string, string> ScanSteamShortcutIcons()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var searchDirs = new List<string>
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Steam"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs\Steam")
                };

                foreach (var dir in searchDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.GetFiles(dir, "*.url", SearchOption.AllDirectories))
                    {
                        try
                        {
                            string[] lines = File.ReadAllLines(file);
                            string? appId = null;
                            string? iconFile = null;

                            foreach (var line in lines)
                            {
                                var trimmed = line.Trim();
                                if (trimmed.StartsWith("URL=steam://rungameid/", StringComparison.OrdinalIgnoreCase))
                                {
                                    appId = trimmed.Substring("URL=steam://rungameid/".Length).Trim();
                                }
                                else if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                                {
                                    iconFile = trimmed.Substring("IconFile=".Length).Trim();
                                }
                            }

                            if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                            {
                                dict[appId] = iconFile;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return dict;
        }

        public static List<DetectedGame> DetectEpicGames()
        {
            var games = new List<DetectedGame>();
            try
            {
                string manifestsDir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
                if (!Directory.Exists(manifestsDir))
                    return games;

                foreach (var itemFile in Directory.GetFiles(manifestsDir, "*.item"))
                {
                    try
                    {
                        var json = File.ReadAllText(itemFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() ?? "" : "";
                        string installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";
                        string launchExe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() ?? "" : "";
                        string appName = root.TryGetProperty("AppName", out var an) ? an.GetString() ?? "" : "";

                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(appName))
                        {
                            string exePath = !string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(launchExe)
                                ? Path.Combine(installLocation, launchExe)
                                : $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true";

                            string iconPath = "";
                            if (!string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(launchExe))
                            {
                                string fullExe = Path.Combine(installLocation, launchExe);
                                if (File.Exists(fullExe))
                                {
                                    iconPath = fullExe;
                                }
                            }

                            games.Add(new DetectedGame
                            {
                                Name = displayName,
                                ExePath = exePath,
                                Platform = "Epic",
                                IconPath = iconPath
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return games;
        }

        private static string? ExtractVdfValue(string content, string key)
        {
            string searchKey = $"\"{key}\"";
            int keyIndex = content.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0) return null;

            int afterKey = keyIndex + searchKey.Length;
            int valueStart = content.IndexOf('"', afterKey);
            if (valueStart < 0) return null;
            valueStart++;

            int valueEnd = content.IndexOf('"', valueStart);
            if (valueEnd < 0) return null;

            return content.Substring(valueStart, valueEnd - valueStart);
        }
    }
}
