using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using RadialLauncher.Models;
using Serilog;

namespace RadialLauncher.Services.Apps
{
    public static class InstalledTargetValidator
    {
        public static bool IsItemStillInstalled(
            LauncherItem item,
            HashSet<string>? installedSteamAppIds = null,
            HashSet<string>? installedEpicAppNames = null)
        {
            if (item == null) return false;

            string type = (item.Type ?? string.Empty).Trim().ToUpperInvariant();
            string target = (item.Target ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(target)) return false;

            // Keep URLs, System Actions, Macros, and Virtual items untouched
            if (type == "URL" || type == "ACTION" || type == "MACRO" || type == "PLUGIN")
                return true;

            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("ms-", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 1. Steam Game URI: steam://rungameid/<AppId>
            if (target.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
            {
                string appId = target.Substring("steam://rungameid/".Length).Trim().Trim('/');
                if (string.IsNullOrEmpty(appId)) return false;

                var steamIds = installedSteamAppIds ?? GetInstalledSteamAppIds();
                // If Steam itself isn't detected or has 0 manifests, check if Steam is installed
                return steamIds.Contains(appId);
            }

            // 2. Epic Games URI: com.epicgames.launcher://apps/<AppName>
            if (target.StartsWith("com.epicgames.launcher://apps/", StringComparison.OrdinalIgnoreCase))
            {
                string rest = target.Substring("com.epicgames.launcher://apps/".Length);
                int qIdx = rest.IndexOf('?');
                string appName = (qIdx >= 0 ? rest.Substring(0, qIdx) : rest).Trim();
                if (string.IsNullOrEmpty(appName)) return false;

                var epicApps = installedEpicAppNames ?? GetInstalledEpicAppNames();
                return epicApps.Contains(appName);
            }

            // 3. Relative command name (e.g. "code.exe", "notepad.exe", "calc.exe")
            // Only validate rooted disk paths (e.g. "C:\..." or "\\...")
            if (!Path.IsPathRooted(target))
            {
                return true;
            }

            // 4. Folder items
            if (type == "FOLDER")
            {
                return Directory.Exists(target);
            }

            // 5. Rooted file path (.exe, .lnk, .bat, etc.)
            if (!File.Exists(target) && !Directory.Exists(target))
            {
                return false;
            }

            if (!item.IsUserAdded)
            {
                if (target.Contains(@"\Windows\Installer\", StringComparison.OrdinalIgnoreCase) ||
                    target.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(target).StartsWith("uninstall", StringComparison.OrdinalIgnoreCase) ||
                    target.Contains("Güncelleyicisi", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // 6. If it is a Windows Shortcut (.lnk), verify its underlying target if it points to a rooted path
            if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                string? resolved = ResolveShortcutTarget(target);
                if (!string.IsNullOrWhiteSpace(resolved) && Path.IsPathRooted(resolved))
                {
                    if (!File.Exists(resolved) && !Directory.Exists(resolved))
                    {
                        return false;
                    }
                    if (!item.IsUserAdded && (
                        resolved.Contains(@"\Windows\Installer\", StringComparison.OrdinalIgnoreCase) ||
                        resolved.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(resolved).StartsWith("uninstall", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static HashSet<string> GetInstalledSteamAppIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string? steamPath = null;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    steamPath = key?.GetValue("SteamPath") as string;
                }

                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                    return ids;

                var libraryFolders = new List<string> { steamPath };
                string libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFile))
                {
                    foreach (var line in File.ReadAllLines(libraryFile))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmed.Split('"');
                            if (parts.Length >= 4)
                            {
                                var path = parts[3].Replace(@"\\", @"\");
                                if (Directory.Exists(path) && !libraryFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
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
                        string fileName = Path.GetFileNameWithoutExtension(manifest); // appmanifest_12345
                        if (fileName.StartsWith("appmanifest_", StringComparison.OrdinalIgnoreCase))
                        {
                            string appId = fileName.Substring("appmanifest_".Length).Trim();
                            if (!string.IsNullOrEmpty(appId))
                                ids.Add(appId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed enumerating installed Steam App IDs");
            }
            return ids;
        }

        public static HashSet<string> GetInstalledEpicAppNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string manifestsDir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
                if (!Directory.Exists(manifestsDir))
                    return names;

                foreach (var itemFile in Directory.GetFiles(manifestsDir, "*.item"))
                {
                    try
                    {
                        var json = File.ReadAllText(itemFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        string installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";
                        string appName = root.TryGetProperty("AppName", out var an) ? an.GetString() ?? "" : "";

                        if (!string.IsNullOrEmpty(appName) && (string.IsNullOrEmpty(installLocation) || Directory.Exists(installLocation)))
                        {
                            names.Add(appName);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed enumerating installed Epic AppNames");
            }
            return names;
        }

        private static string? ResolveShortcutTarget(string shortcutPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                string target = shortcut.TargetPath;
                Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
                return target;
            }
            catch
            {
                return null;
            }
        }
    }
}
