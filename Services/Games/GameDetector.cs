using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using Serilog;

namespace RadialLauncher.Services.Games
{
    public class DetectedGame
    {
        public string Name { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
    }

    public class GameDetector : IGameDetector
    {
        public List<DetectedGame> DetectAll()
        {
            var games = new List<DetectedGame>();
            games.AddRange(DetectSteam());
            games.AddRange(DetectEpic());
            return games;
        }

        public List<DetectedGame> DetectSteam()
        {
            var games = new List<DetectedGame>();
            try
            {
                var shortcutIcons = ScanSteamShortcutIcons();

                string? steamPath = null;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    steamPath = key?.GetValue("SteamPath") as string;
                }
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                    return games;

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
                                var path = parts[3].Replace(@"\\", @"\");
                                if (Directory.Exists(path) && !libraryFolders.Contains(path))
                                    libraryFolders.Add(path);
                            }
                        }
                    }
                }

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

                                string iconPath = "";
                                if (shortcutIcons.TryGetValue(appId, out var scIcon) && File.Exists(scIcon))
                                {
                                    iconPath = scIcon;
                                }
                                else if (!string.IsNullOrEmpty(installdir))
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

                                games.Add(new DetectedGame
                                {
                                    Name = name,
                                    ExePath = $"steam://rungameid/{appId}",
                                    Platform = "Steam",
                                    IconPath = iconPath
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to parse steam manifest {Manifest}", manifest);
                        }
                    }
                }

                var regRoots = new[]
                {
                    (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
                };

                foreach (var (root, subPath) in regRoots)
                {
                    try
                    {
                        using var baseKey = root.OpenSubKey(subPath);
                        if (baseKey == null) continue;
                        foreach (var subName in baseKey.GetSubKeyNames())
                        {
                            if (subName.StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
                            {
                                string appId = subName.Substring("Steam App ".Length).Trim();
                                string steamExePath = $"steam://rungameid/{appId}";
                                if (!games.Any(g => g.ExePath.Equals(steamExePath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    using var appKey = baseKey.OpenSubKey(subName);
                                    if (appKey != null)
                                    {
                                        string? displayName = appKey.GetValue("DisplayName") as string;
                                        string? displayIcon = appKey.GetValue("DisplayIcon") as string;
                                        string cleanIcon = "";
                                        if (!string.IsNullOrEmpty(displayIcon))
                                        {
                                            string cl = displayIcon.Split(',')[0].Trim().Trim('"');
                                            if (File.Exists(cl)) cleanIcon = cl;
                                        }

                                        if (string.IsNullOrEmpty(cleanIcon) && shortcutIcons.TryGetValue(appId, out var scIcon) && File.Exists(scIcon))
                                        {
                                            cleanIcon = scIcon;
                                        }

                                        if (!string.IsNullOrEmpty(displayName))
                                        {
                                            games.Add(new DetectedGame
                                            {
                                                Name = displayName,
                                                ExePath = steamExePath,
                                                Platform = "Steam",
                                                IconPath = cleanIcon
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed scanning registry path {Path} for Steam games", subPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed executing DetectSteam");
            }
            return games;
        }

        public Dictionary<string, string> ScanSteamShortcutIcons()
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
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed reading Steam URL shortcut {File}", file);
                        }
                    }
                }

                var regRoots = new[]
                {
                    (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
                };

                foreach (var (root, subPath) in regRoots)
                {
                    try
                    {
                        using var baseKey = root.OpenSubKey(subPath);
                        if (baseKey == null) continue;
                        foreach (var subName in baseKey.GetSubKeyNames())
                        {
                            if (subName.StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
                            {
                                string appId = subName.Substring("Steam App ".Length).Trim();
                                if (!dict.ContainsKey(appId))
                                {
                                    using var appKey = baseKey.OpenSubKey(subName);
                                    if (appKey != null)
                                    {
                                        string? displayIcon = appKey.GetValue("DisplayIcon") as string;
                                        if (!string.IsNullOrEmpty(displayIcon))
                                        {
                                            string clean = displayIcon.Split(',')[0].Trim().Trim('"');
                                            if (File.Exists(clean))
                                            {
                                                dict[appId] = clean;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed reading Steam App registry shortcut {Path}", subPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed in ScanSteamShortcutIcons");
            }
            return dict;
        }

        public List<DetectedGame> DetectEpic()
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
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed parsing Epic manifest {File}", itemFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed executing DetectEpic");
            }
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
