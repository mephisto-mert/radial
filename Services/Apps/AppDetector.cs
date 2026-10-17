using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using RadialLauncher.Services.Icons;
using Serilog;

namespace RadialLauncher.Services.Apps
{
    public class AppDetector : IAppDetector
    {
        private static AppDetector? _instance;
        public static AppDetector Instance => _instance ??= new AppDetector();

        private readonly IIconExtractor? _iconExtractor;

        public AppDetector(IIconExtractor? iconExtractor = null)
        {
            _iconExtractor = iconExtractor;
        }

        private static readonly HashSet<string> IgnoredProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "svchost", "dwm", "explorer", "taskhostw", "devenv", "csrss", "conhost",
            "system", "registry", "smss", "lsass", "wininit", "services", "spoolsv",
            "ctfmon", "runtimebroker", "searchhost", "startmenuexperiencehost",
            "shellexperiencehost", "securityhealthsystray", "radiallauncher",
            "radiallauncher.installer", "testhost", "vstest.console", "msbuild",
            "dotnet", "wmiapsrv", "fontdrvhost", "audiodg", "sihost",
            "compattelrunner", "dllhost", "cmd", "powershell", "wsl",
            "smartscreen", "backgroundtaskhost", "textinputhost", "crossdeviceresume",
            "secd", "applephotostreams", "apsdaemon", "icloudckks", "icloudservices",
            "gameinputredistservice", "spacedeskservicetray", "windowspackagemanagerserver",
            "nvcontainer", "language_server", "msedgewebview2", "storedesktopextension",
            "python", "pythonw", "node", "java", "javaw", "ealauncher", "eaupdater",
            "epiconlineserviceshost", "eosbootstrapper", "desktopoverlayhost"
        };

        private static readonly string[] IgnoreSubstrings = new[]
        {
            "crashpad", "crashhandler", "helper", "renderer", "gpu-process",
            "utility", "broker", "redist", "updater", "güncelleyici", "guncelleyici",
            "unins", "uninstall", "setup", "installer", "vcredist", "directx",
            "kaldır", "kaldir", "remove", "readme", "license", "documentation",
            "changelog", "release notes", "yardım", "kılavuz", "tanılaması",
            "dil ayarları", "error reporter", "bootstrapper", "app recovery",
            "native tools command prompt", "developer command prompt", "developer powershell"
        };

        private static readonly string[] GameKeywords = new[]
        {
            "steam", "epic", "game", "riot", "valorant", "league", "minecraft",
            "roblox", "gta", "rdr", "red dead", "counter-strike", "csgo", "cs2",
            "blitz", "battle.net", "origin", "ea desktop", "ea games", "electronic arts",
            "ealauncher", "battlefield", "bf2042", "jedi", "fallen order", "star wars",
            "rockstar", "curseforge", "overwolf", "ubisoft", "gog", "genshin",
            "hitman", "spider-man", "cities", "zomboid", "kenshi", "worldbox",
            "half sword", "forest", "universe sandbox", "playstation", "xbox",
            "pubg", "apex", "overwatch", "witcher", "cyberpunk", "dota", "fortnite",
            "assassin", "far cry", "fifa", "fc 24", "fc 25", "pes", "simulator",
            "battlefront", "call of duty", "metin2", "gameforge", "oyunindir",
            "blackmyth", "wukong", "bodycam", "diablo", "dying light", "ready or not", "sims 4"
        };

        private static readonly string[] InternetKeywords = new[]
        {
            "chrome", "msedge", "edge", "brave", "firefox", "opera", "vivaldi", "tor browser",
            "discord", "telegram", "whatsapp", "spotify", "zoom", "teams", "skype",
            "slack", "thunderbird", "rave", "youtube", "twitch", "netflix", "zen browser"
        };

        private static readonly string[] DevToolKeywords = new[]
        {
            "code", "visual studio", "vs code", "git", "github", "android studio",
            "intellij", "pycharm", "webstorm", "rider", "sublime", "notepad++",
            "postman", "docker", "terminal", "unity", "unreal", "blender", "figma",
            "excel", "word", "powerpoint", "onenote", "office", "outlook", "acrobat",
            "photoshop", "illustrator", "premiere", "obs", "gimp", "canva", "davinci",
            "audacity", "godot", "dbeaver", "insomnia", "sharex", "ditto", "textify",
            "flow.launcher", "unigetui", "qbittorrent", "veracrypt", "antigravity", "cursor"
        };

        public List<DetectedApp> DetectRunningAndCommonApps()
        {
            var detected = new Dictionary<string, DetectedApp>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. Detect Running User Processes (Task Manager Apps with visible main window)
                DetectFromRunningProcesses(detected);

                // 2. Detect Common Desktop & Start Menu Shortcuts
                DetectFromShortcuts(detected);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error detecting running and common apps");
            }

            return detected.Values.ToList();
        }

        private void DetectFromRunningProcesses(Dictionary<string, DetectedApp> dict)
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    if (proc.MainWindowHandle == IntPtr.Zero) continue;

                    string pName = proc.ProcessName;
                    if (IgnoredProcessNames.Contains(pName)) continue;
                    if (IgnoreSubstrings.Any(s => pName.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;

                    string? exePath = null;
                    try
                    {
                        exePath = proc.MainModule?.FileName;
                    }
                    catch
                    {
                        // Access denied on some elevated system procs - ignore
                    }

                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) continue;

                    // Exclude Windows OS system directories and installer caches
                    if (exePath.Contains(@"\Windows\System32", StringComparison.OrdinalIgnoreCase) ||
                        exePath.Contains(@"\Windows\SysWOW64", StringComparison.OrdinalIgnoreCase) ||
                        exePath.Contains(@"\Windows\Installer", StringComparison.OrdinalIgnoreCase) ||
                        exePath.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Check if already in dict
                    if (dict.ContainsKey(exePath)) continue;

                    string displayName = CleanAppName(proc, exePath);
                    if (string.IsNullOrWhiteSpace(displayName)) continue;
                    if (IgnoreSubstrings.Any(s => displayName.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;

                    var app = new DetectedApp
                    {
                        Name = displayName,
                        ExePath = exePath,
                        IconPath = exePath,
                        IsRunning = true
                    };
                    AssignCategory(app);

                    dict[exePath] = app;
                }
                catch
                {
                    // Ignore transient process exit exceptions
                }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }
        }

        private void DetectFromShortcuts(Dictionary<string, DetectedApp> dict)
        {
            var searchDirs = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs")
            };

            dynamic? shell = null;
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    shell = Activator.CreateInstance(shellType);
                }
            }
            catch { }

            string desktop1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string desktop2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                bool isDesktopDir = string.Equals(dir, desktop1, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(dir, desktop2, StringComparison.OrdinalIgnoreCase);
                try
                {
                    var lnkFiles = Directory.GetFiles(dir, "*.lnk", isDesktopDir ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
                    foreach (var lnk in lnkFiles)
                    {
                        try
                        {
                            if (!isDesktopDir && (
                                lnk.Contains(@"\Administrative Tools\", StringComparison.OrdinalIgnoreCase) ||
                                lnk.Contains(@"\Microsoft DirectX SDK", StringComparison.OrdinalIgnoreCase) ||
                                lnk.Contains(@"\Visual Studio Tools\", StringComparison.OrdinalIgnoreCase) ||
                                lnk.Contains(@"\Accessibility\", StringComparison.OrdinalIgnoreCase) ||
                                lnk.Contains(@"\K-Lite Codec Pack\", StringComparison.OrdinalIgnoreCase) ||
                                lnk.Contains(@"\System Tools\", StringComparison.OrdinalIgnoreCase) ||
                                lnk.Contains(@"\Maintenance\", StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            string fileName = Path.GetFileNameWithoutExtension(lnk);
                            if (string.Equals(fileName, "Belgeler", StringComparison.OrdinalIgnoreCase)) continue;
                            if (IgnoreSubstrings.Any(s => fileName.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;

                            string? target = ResolveShortcutTarget(shell, lnk);
                            string effectiveTarget;
                            if (!string.IsNullOrEmpty(target) && File.Exists(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                effectiveTarget = target;
                            }
                            else if (isDesktopDir && string.IsNullOrEmpty(target))
                            {
                                // UWP / Xbox / Store Desktop shortcut (e.g. Minecraft Launcher.lnk)
                                effectiveTarget = lnk;
                            }
                            else
                            {
                                continue;
                            }

                            if (IgnoreSubstrings.Any(s => effectiveTarget.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;

                            if (effectiveTarget.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase) ||
                                effectiveTarget.Contains(@"\Windows\SysWOW64\", StringComparison.OrdinalIgnoreCase) ||
                                effectiveTarget.Contains(@"\Windows\Installer\", StringComparison.OrdinalIgnoreCase) ||
                                effectiveTarget.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (dict.ContainsKey(effectiveTarget)) continue;

                            var app = new DetectedApp
                            {
                                Name = fileName,
                                ExePath = effectiveTarget,
                                IconPath = effectiveTarget,
                                IsRunning = false
                            };
                            AssignCategory(app);

                            dict[effectiveTarget] = app;
                        }
                        catch
                        {
                            // Ignore single shortcut resolution error
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed scanning shortcuts in {Dir}", dir);
                }
            }

            if (shell != null)
            {
                try
                {
                    Marshal.FinalReleaseComObject(shell);
                }
                catch { }
            }
        }

        private static string CleanAppName(Process proc, string exePath)
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(vi.FileDescription) && vi.FileDescription.Length > 1 && vi.FileDescription.Length < 40)
                {
                    return vi.FileDescription.Trim();
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(proc.MainWindowTitle) && proc.MainWindowTitle.Length < 40 && !proc.MainWindowTitle.Contains('\\'))
            {
                return proc.MainWindowTitle.Trim();
            }

            string rawName = Path.GetFileNameWithoutExtension(exePath);
            if (rawName.EndsWith(".Root", StringComparison.OrdinalIgnoreCase))
                rawName = rawName.Substring(0, rawName.Length - 5);

            return rawName;
        }

        private static void AssignCategory(DetectedApp app)
        {
            string lowerCombined = (app.Name + " " + app.ExePath).ToLowerInvariant();
            bool isExactEa = string.Equals(app.Name.Trim(), "EA", StringComparison.OrdinalIgnoreCase);

            if (isExactEa || GameKeywords.Any(k => lowerCombined.Contains(k)))
            {
                app.CategoryKey = "Cat_Games";
                app.CategoryName = "🎮 Oyunlar";
                app.CategoryColor = "#e74c3c";
            }
            else if (InternetKeywords.Any(k => lowerCombined.Contains(k)))
            {
                app.CategoryKey = "Cat_Internet";
                app.CategoryName = "🌐 Web & İnternet";
                app.CategoryColor = "#3498db";
            }
            else if (DevToolKeywords.Any(k => lowerCombined.Contains(k)))
            {
                app.CategoryKey = "Cat_Apps";
                app.CategoryName = "💼 Uygulamalar & İş";
                app.CategoryColor = "#2ecc71";
            }
            else
            {
                app.CategoryKey = "Cat_Apps";
                app.CategoryName = "💼 Uygulamalar & İş";
                app.CategoryColor = "#2ecc71";
            }
        }

        private static string? ResolveShortcutTarget(dynamic? shell, string shortcutPath)
        {
            if (shell == null) return null;
            try
            {
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                string target = shortcut.TargetPath;
                Marshal.FinalReleaseComObject(shortcut);
                return target;
            }
            catch
            {
                return null;
            }
        }
    }
}
