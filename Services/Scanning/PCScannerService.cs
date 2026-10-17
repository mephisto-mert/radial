using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Microsoft.Win32;
using RadialLauncher.Data;
using RadialLauncher.Models;
using RadialLauncher.Services.Games;
using RadialLauncher.Services.Icons;
using Serilog;

namespace RadialLauncher.Services.Scanning
{
    public class ScannedApp
    {
        public string Name { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string ResolvedTarget { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "🛠️ Sistem & Araçlar";
        public string Source { get; set; } = string.Empty;
        public bool IsOnDesktop { get; set; } = false;
        public bool IsSelected { get; set; } = true;
        public ImageSource? Icon { get; set; }
    }

    public class ScanSummary
    {
        public int TotalDiscovered { get; set; }
        public int TotalAdded { get; set; }
        public int TotalUpdated { get; set; }
        public int GamesCount { get; set; }
        public int InternetCount { get; set; }
        public int DevCount { get; set; }
        public int SystemCount { get; set; }
    }

    public class PCScannerService : IPCScannerService
    {
        public const string CatGames = "🎮 Oyunlar";
        public const string CatInternet = "🌐 Web & İnternet";
        public const string CatDev = "💼 Uygulamalar & İş";
        public const string CatTools = "🛠️ Sistem & Araçlar";

        private readonly IGameDetector _gameDetector;
        private readonly IIconExtractor _iconExtractor;

        public PCScannerService() : this(
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IGameDetector>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IIconExtractor>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."))
        {
        }

        public PCScannerService(IGameDetector gameDetector, IIconExtractor iconExtractor)
        {
            _gameDetector = gameDetector ?? throw new ArgumentNullException(nameof(gameDetector));
            _iconExtractor = iconExtractor ?? throw new ArgumentNullException(nameof(iconExtractor));
        }

        private static readonly string[] IgnoreKeywords = new[]
        {
            "uninstall", "kaldır", "kaldir", "setup", "installer", "unins000", "unins",
            "vcredist", "directx", "updater", "güncelleyici", "guncelleyici", "crashpad", "help",
            "readme", "license", "documentation", "yardım", "kılavuz",
            "changelog", "release notes", "website", "support",
            "report", "telemetry", "crashhandler", "createdump", "helper",
            "bootstrapper", "app recovery", "redist", "package cache",
            "windowsdesktop-runtime", "native tools command prompt",
            "developer command prompt", "developer powershell", "error reporter",
            "tanılaması", "dil ayarları", "safe_x64_injector"
        };

        private static readonly string[] GameKeywords = new[]
        {
            "steam", "epic", "game", "riot", "valorant", "league", "minecraft",
            "roblox", "gta", "rdr", "red dead", "counter-strike", "csgo", "cs2",
            "blitz", "battle.net", "origin", "ea desktop", "ea games", "electronic arts",
            "ealauncher", "battlefield", "bf2042", "jedi", "fallen order", "star wars",
            "rockstar", "curseforge", "overwolf", "ubisoft", "gog",
            "genshin", "hitman", "spider-man", "cities", "zomboid",
            "kenshi", "worldbox", "half sword", "forest", "universe sandbox",
            "playstation", "xbox", "pubg", "apex", "overwatch", "witcher",
            "cyberpunk", "dota", "fortnite", "assassin", "far cry", "fifa",
            "fc 24", "fc 25", "pes", "simulator", "battlefront",
            "call of duty", "metin2", "gameforge", "oyunindir",
            "blackmyth", "wukong", "bodycam", "diablo", "dying light", "ready or not", "sims 4"
        };

        private static readonly string[] InternetKeywords = new[]
        {
            "chrome", "msedge", "edge", "brave", "firefox", "opera", "vivaldi", "tor browser",
            "discord", "telegram", "whatsapp", "spotify", "zoom", "teams", "skype",
            "slack", "thunderbird", "rave", "youtube", "twitch", "netflix",
            "viber", "signal", "messenger", "browser", "internet", "zen browser"
        };

        private static readonly string[] DevWorkKeywords = new[]
        {
            "code", "visual studio", "vs code", "git", "github", "android studio",
            "intellij", "pycharm", "webstorm", "rider", "sublime", "notepad++",
            "postman", "docker", "powershell", "terminal", "unity", "unreal",
            "blender", "figma", "excel", "word", "powerpoint", "onenote", "office",
            "outlook", "acrobat", "pdf", "photoshop", "illustrator", "premiere",
            "after effects", "obs", "gimp", "canva", "davinci", "audacity",
            "godot", "dbeaver", "insomnia", "workbench", "antigravity", "cursor"
        };

        public List<ScannedApp> ScanAllApps()
        {
            var results = new List<ScannedApp>();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var installedSteamIds = RadialLauncher.Services.Apps.InstalledTargetValidator.GetInstalledSteamAppIds();
            var installedEpicNames = RadialLauncher.Services.Apps.InstalledTargetValidator.GetInstalledEpicAppNames();

            void AddIfValid(ScannedApp app)
            {
                if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.Target))
                    return;

                if (string.Equals(app.Name.Trim(), "Belgeler", StringComparison.OrdinalIgnoreCase))
                    return;

                if (app.Target.Contains(@"\Windows\Installer\", StringComparison.OrdinalIgnoreCase) ||
                    app.Target.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase))
                    return;

                string lowerCheck = (app.Name + " " + Path.GetFileNameWithoutExtension(app.Target) + " " + Path.GetFileNameWithoutExtension(app.ResolvedTarget ?? "")).ToLowerInvariant();
                foreach (var ignore in IgnoreKeywords)
                {
                    if (lowerCheck.Contains(ignore)) return;
                }

                // Do not add broken shortcuts or uninstalled Steam/Epic targets
                var tempItem = new LauncherItem { Name = app.Name, Target = app.Target, Type = "EXE" };
                if (!RadialLauncher.Services.Apps.InstalledTargetValidator.IsItemStillInstalled(tempItem, installedSteamIds, installedEpicNames))
                    return;

                string key = app.Target.ToLowerInvariant();
                string resolvedKey = (app.ResolvedTarget ?? "").ToLowerInvariant();
                string nameKey = NormalizeAppNameKey(app.Name);

                var existing = results.FirstOrDefault(r =>
                    string.Equals(r.Target, app.Target, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(resolvedKey) && (string.Equals(r.Target, app.ResolvedTarget, StringComparison.OrdinalIgnoreCase) || string.Equals(r.ResolvedTarget, app.ResolvedTarget, StringComparison.OrdinalIgnoreCase))) ||
                    NormalizeAppNameKey(r.Name) == nameKey);

                if (existing != null)
                {
                    if (app.IsOnDesktop) existing.IsOnDesktop = true;
                    if (app.CategoryName == CatGames) existing.CategoryName = CatGames;
                    if (string.IsNullOrEmpty(existing.IconPath) && !string.IsNullOrEmpty(app.IconPath))
                        existing.IconPath = app.IconPath;
                    return;
                }

                seenTargets.Add(key);
                if (!string.IsNullOrEmpty(resolvedKey)) seenTargets.Add(resolvedKey);
                seenNames.Add(nameKey);

                // Resolve Icon eagerly
                app.Icon = ResolveAppIcon(app.Name, app.Target, app.IconPath);

                results.Add(app);
            }

            // 1. Desktop Shortcuts FIRST (.lnk and .url) so user's exact Desktop names & targets take priority
            try
            {
                string[] desktopFolders = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                };

                foreach (var folder in desktopFolders)
                {
                    if (Directory.Exists(folder))
                    {
                        foreach (var file in Directory.GetFiles(folder, "*.lnk"))
                        {
                            string appName = Path.GetFileNameWithoutExtension(file);
                            var (resolvedTarget, resolvedArgs, resolvedIcon) = ResolveShortcutDetails(file);
                            string effectiveTarget = (!string.IsNullOrEmpty(resolvedTarget) && File.Exists(resolvedTarget) && string.IsNullOrEmpty(resolvedArgs))
                                ? resolvedTarget
                                : file;
                            string effectiveIcon = (!string.IsNullOrEmpty(resolvedIcon) && File.Exists(resolvedIcon))
                                ? resolvedIcon
                                : ((!string.IsNullOrEmpty(resolvedTarget) && File.Exists(resolvedTarget)) ? resolvedTarget : file);

                            AddIfValid(new ScannedApp
                            {
                                Name = appName,
                                Target = effectiveTarget,
                                ResolvedTarget = resolvedTarget ?? string.Empty,
                                Arguments = string.IsNullOrEmpty(resolvedTarget) ? string.Empty : (resolvedArgs ?? string.Empty),
                                IconPath = effectiveIcon,
                                CategoryName = ClassifyCategory(appName, file + " " + resolvedTarget),
                                Source = "Masaüstü",
                                IsOnDesktop = true
                            });
                        }

                        foreach (var file in Directory.GetFiles(folder, "*.url"))
                        {
                            string appName = Path.GetFileNameWithoutExtension(file);
                            string target = file;
                            string iconPath = "";
                            try
                            {
                                var lines = File.ReadAllLines(file);
                                foreach (var line in lines)
                                {
                                    var trimmed = line.Trim();
                                    if (trimmed.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                                    {
                                        target = trimmed.Substring("URL=".Length).Trim();
                                    }
                                    else if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                                    {
                                        iconPath = trimmed.Substring("IconFile=".Length).Trim();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(ex, "Failed parsing desktop URL shortcut {File}", file);
                            }

                            AddIfValid(new ScannedApp
                            {
                                Name = appName,
                                Target = target,
                                IconPath = iconPath,
                                CategoryName = ClassifyCategory(appName, target),
                                Source = "Masaüstü",
                                IsOnDesktop = true
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed scanning desktop shortcuts");
            }

            // 2. Steam, Epic & Installed Game Directories
            try
            {
                var games = _gameDetector.DetectAll();
                foreach (var g in games)
                {
                    AddIfValid(new ScannedApp
                    {
                        Name = g.Name,
                        Target = g.ExePath,
                        IconPath = g.IconPath,
                        CategoryName = CatGames,
                        Source = g.Platform
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed scanning games in PCScannerService");
            }

            // 3. Start Menu Shortcuts (.lnk and .url)
            try
            {
                string[] startMenuFolders = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                };

                foreach (var folder in startMenuFolders)
                {
                    if (Directory.Exists(folder))
                    {
                        foreach (var file in Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories))
                        {
                            if (file.Contains(@"\Administrative Tools\", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains(@"\Microsoft DirectX SDK", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains(@"\Visual Studio Tools\", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains(@"\Accessibility\", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains(@"\K-Lite Codec Pack\", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains(@"\System Tools\", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains(@"\Maintenance\", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            string appName = Path.GetFileNameWithoutExtension(file);
                            var (resolvedTarget, resolvedArgs, resolvedIcon) = ResolveShortcutDetails(file);
                            string effectiveTarget = (!string.IsNullOrEmpty(resolvedTarget) && File.Exists(resolvedTarget) && string.IsNullOrEmpty(resolvedArgs))
                                ? resolvedTarget
                                : file;
                            string effectiveIcon = (!string.IsNullOrEmpty(resolvedIcon) && File.Exists(resolvedIcon))
                                ? resolvedIcon
                                : ((!string.IsNullOrEmpty(resolvedTarget) && File.Exists(resolvedTarget)) ? resolvedTarget : file);

                            AddIfValid(new ScannedApp
                            {
                                Name = appName,
                                Target = effectiveTarget,
                                ResolvedTarget = resolvedTarget ?? string.Empty,
                                Arguments = string.IsNullOrEmpty(resolvedTarget) ? string.Empty : (resolvedArgs ?? string.Empty),
                                IconPath = effectiveIcon,
                                CategoryName = ClassifyCategory(appName, file + " " + resolvedTarget),
                                Source = "Başlat Menüsü"
                            });
                        }

                        foreach (var file in Directory.GetFiles(folder, "*.url", SearchOption.AllDirectories))
                        {
                            string appName = Path.GetFileNameWithoutExtension(file);
                            string target = file;
                            string iconPath = "";
                            try
                            {
                                var lines = File.ReadAllLines(file);
                                foreach (var line in lines)
                                {
                                    var trimmed = line.Trim();
                                    if (trimmed.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                                    {
                                        target = trimmed.Substring("URL=".Length).Trim();
                                    }
                                    else if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                                    {
                                        iconPath = trimmed.Substring("IconFile=".Length).Trim();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(ex, "Failed parsing start menu URL shortcut {File}", file);
                            }

                            AddIfValid(new ScannedApp
                            {
                                Name = appName,
                                Target = target,
                                IconPath = iconPath,
                                CategoryName = ClassifyCategory(appName, target),
                                Source = "Başlat Menüsü"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed scanning start menu shortcuts");
            }

            // 4. Registry Installed Programs
            try
            {
                ScanRegistryKeys(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", AddIfValid);
                ScanRegistryKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", AddIfValid);
                ScanRegistryKeys(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", AddIfValid);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed scanning registry uninstall programs");
            }

            // 5. Running Processes & Common Installed Directories (%LOCALAPPDATA%\Programs, Program Files)
            try
            {
                var runningAndCommon = RadialLauncher.Services.Apps.AppDetector.Instance.DetectRunningAndCommonApps();
                foreach (var rc in runningAndCommon)
                {
                    AddIfValid(new ScannedApp
                    {
                        Name = rc.Name,
                        Target = rc.ExePath,
                        IconPath = rc.IconPath,
                        CategoryName = ClassifyCategory(rc.Name, rc.ExePath),
                        Source = "Sistem Taraması"
                    });
                }

                string[] appDirs = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                var skipProgramFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Common Files", "Windows Defender", "Windows Defender Advanced Threat Protection",
                    "Windows Mail", "Windows Photo Viewer", "Windows NT", "Windows Kits", "WindowsPowerShell",
                    "Internet Explorer", "ModifiableWindowsApps", "Reference Assemblies", "MSBuild",
                    "dotnet", "Microsoft.NET", "Uninstall Information", "Package Cache", "RadialLauncher",
                    "Microsoft DirectX SDK (June 2010)", "Microsoft SDKs", "Microsoft Works", "Microsoft XNA",
                    "EasyAntiCheat_EOS", "Riot Vanguard", "Microsoft GameInput", "VulkanRT", "OpenAL",
                    "MSXML 4.0", "Mozilla Maintenance Service", "Application Verifier", "Bonjour"
                };

                foreach (var baseDir in appDirs)
                {
                    if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir)) continue;
                    foreach (var subDir in Directory.GetDirectories(baseDir))
                    {
                        try
                        {
                            string folderName = Path.GetFileName(subDir);
                            if (skipProgramFolders.Contains(folderName)) continue;

                            var exes = Directory.GetFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly);
                            if (exes.Length == 0) continue;

                            // Pick main executable matching folder name or first valid exe
                            string? bestExe = exes.FirstOrDefault(e =>
                                Path.GetFileNameWithoutExtension(e).Equals(folderName, StringComparison.OrdinalIgnoreCase))
                                ?? exes.FirstOrDefault(e =>
                                {
                                    string n = Path.GetFileNameWithoutExtension(e).ToLowerInvariant();
                                    return !IgnoreKeywords.Any(kw => n.Contains(kw));
                                });

                            if (!string.IsNullOrEmpty(bestExe) && File.Exists(bestExe))
                            {
                                string displayName = folderName;
                                try
                                {
                                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(bestExe);
                                    if (!string.IsNullOrWhiteSpace(vi.ProductName) && vi.ProductName.Length > 1 && vi.ProductName.Length < 45)
                                        displayName = vi.ProductName.Trim();
                                }
                                catch { }

                                AddIfValid(new ScannedApp
                                {
                                    Name = displayName,
                                    Target = bestExe,
                                    IconPath = bestExe,
                                    CategoryName = ClassifyCategory(displayName, bestExe),
                                    Source = "Program Klasörleri"
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed scanning program directories");
            }

            return results.OrderByDescending(a => a.IsOnDesktop).ThenBy(a => a.CategoryName).ThenBy(a => a.Name).ToList();
        }

        private ImageSource? ResolveAppIcon(string name, string target, string iconPath)
        {
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                var fileIcon = _iconExtractor.GetIconForFile(iconPath);
                if (fileIcon != null) return fileIcon;
            }
            var brand = _iconExtractor.GetBrandIcon(name, target);
            if (brand != null) return brand;
            if (!string.IsNullOrEmpty(target))
            {
                var targetIcon = _iconExtractor.GetIconForFile(target);
                if (targetIcon != null) return targetIcon;
            }
            return _iconExtractor.CreateMonogramIcon(name, Color.FromRgb(88, 140, 236));
        }

        private void ScanRegistryKeys(RegistryKey rootKey, string subKeyPath, Action<ScannedApp> addAction)
        {
            try
            {
                using var baseKey = rootKey.OpenSubKey(subKeyPath);
                if (baseKey == null) return;

                foreach (var subName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var key = baseKey.OpenSubKey(subName);
                        if (key == null) continue;

                        int systemComp = Convert.ToInt32(key.GetValue("SystemComponent") ?? 0);
                        if (systemComp == 1) continue;

                        string? displayName = key.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        string? displayIcon = key.GetValue("DisplayIcon") as string;
                        string? installLocation = key.GetValue("InstallLocation") as string;

                        string target = "";
                        string icon = "";

                        if (!string.IsNullOrEmpty(displayIcon))
                        {
                            string cleanIcon = displayIcon.Split(',')[0].Trim('"');
                            if (File.Exists(cleanIcon))
                            {
                                if (cleanIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    target = cleanIcon;
                                }
                                else if (cleanIcon.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                                {
                                    icon = cleanIcon;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        {
                            var exes = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                            if (exes.Length > 0)
                            {
                                target = exes.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e).Equals(displayName, StringComparison.OrdinalIgnoreCase)) ?? exes[0];
                            }
                        }

                        if (!string.IsNullOrEmpty(target) && File.Exists(target))
                        {
                            if (target.Contains(@"\Windows\Installer\", StringComparison.OrdinalIgnoreCase) ||
                                target.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            addAction(new ScannedApp
                            {
                                Name = displayName,
                                Target = target,
                                IconPath = icon,
                                CategoryName = ClassifyCategory(displayName, target),
                                Source = "Kayıt Defteri"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed inspecting registry subkey {SubName}", subName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed opening registry key {KeyPath}", subKeyPath);
            }
        }

        private static (string? targetPath, string? arguments, string? iconLocation) ResolveShortcutDetails(string shortcutPath)
        {
            dynamic? shell = null;
            dynamic? shortcut = null;
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return (null, null, null);
                shell = Activator.CreateInstance(shellType);
                if (shell == null) return (null, null, null);
                shortcut = shell.CreateShortcut(shortcutPath);
                string? target = shortcut.TargetPath as string;
                string? args = shortcut.Arguments as string;
                string? iconLoc = shortcut.IconLocation as string;
                if (!string.IsNullOrEmpty(iconLoc))
                {
                    iconLoc = iconLoc.Split(',')[0].Trim().Trim('"');
                }
                return (target, args, iconLoc);
            }
            catch
            {
                return (null, null, null);
            }
            finally
            {
                if (shortcut != null) { try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); } catch { } }
                if (shell != null) { try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); } catch { } }
            }
        }

        private static string NormalizeAppNameKey(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            string n = name.Trim().ToLowerInvariant();
            if (n == "epic games" || n == "epic games launcher") return "epic games launcher";
            if (n == "minecraft" || n == "minecraft launcher") return "minecraft launcher";
            if (n.StartsWith("curseforge ")) return "curseforge";
            return n;
        }

        public string ClassifyCategory(string name, string target)
        {
            if (target.StartsWith("steam://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("com.epicgames.", StringComparison.OrdinalIgnoreCase))
            {
                return CatGames;
            }

            if (string.Equals(name.Trim(), "EA", StringComparison.OrdinalIgnoreCase))
            {
                return CatGames;
            }

            string combined = (name + " " + target).ToLowerInvariant();

            foreach (var kw in GameKeywords)
            {
                if (combined.Contains(kw)) return CatGames;
            }

            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return CatInternet;
            }

            foreach (var kw in InternetKeywords)
            {
                if (combined.Contains(kw)) return CatInternet;
            }

            foreach (var kw in DevWorkKeywords)
            {
                if (combined.Contains(kw)) return CatDev;
            }

            return CatTools;
        }

        public ScanSummary SaveScannedApps(IEnumerable<ScannedApp> apps, IDatabaseManager db)
        {
            var appList = apps as List<ScannedApp> ?? apps.ToList();
            var summary = new ScanSummary { TotalDiscovered = appList.Count };

            int gamesCatId = db.GetOrCreateCategory(CatGames, "#e74c3c");
            int internetCatId = db.GetOrCreateCategory(CatInternet, "#3498db");
            int devCatId = db.GetOrCreateCategory(CatDev, "#2ecc71");
            int toolsCatId = db.GetOrCreateCategory(CatTools, "#e67e22");

            var existingItems = db.GetAllItems();
            int maxPos = existingItems.Count > 0 ? existingItems.Max(i => i.Position) : 0;

            foreach (var app in appList)
            {
                if (!app.IsSelected) continue;

                int targetCatId = app.CategoryName switch
                {
                    CatGames => gamesCatId,
                    CatInternet => internetCatId,
                    CatDev => devCatId,
                    _ => toolsCatId
                };

                string normName = NormalizeAppNameKey(app.Name);
                var existingMatch = existingItems.FirstOrDefault(i =>
                    string.Equals(i.Target, app.Target, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(app.ResolvedTarget) && string.Equals(i.Target, app.ResolvedTarget, StringComparison.OrdinalIgnoreCase)) ||
                    NormalizeAppNameKey(i.Name) == normName);

                if (existingMatch != null)
                {
                    if (existingMatch.Type != "URL" && existingMatch.Type != "ACTION" && existingMatch.Type != "MACRO")
                    {
                        bool changed = false;
                        if (targetCatId == gamesCatId && existingMatch.CategoryId != gamesCatId)
                        {
                            if (existingMatch.CategoryId == 0)
                            {
                                existingMatch.IsFavorite = true;
                            }
                            existingMatch.CategoryId = gamesCatId;
                            changed = true;
                        }
                        else if (!existingMatch.IsUserAdded && existingMatch.CategoryId != targetCatId && existingMatch.CategoryId > 1)
                        {
                            existingMatch.CategoryId = targetCatId;
                            changed = true;
                        }

                        if (app.IsOnDesktop && targetCatId == gamesCatId && !existingMatch.IsFavorite)
                        {
                            existingMatch.IsFavorite = true;
                            changed = true;
                        }

                        if (string.IsNullOrEmpty(existingMatch.IconPath) && !string.IsNullOrEmpty(app.IconPath))
                        {
                            existingMatch.IconPath = app.IconPath;
                            changed = true;
                        }

                        if (changed)
                        {
                            db.UpdateItem(existingMatch);
                            summary.TotalUpdated++;
                        }
                    }
                    continue;
                }

                maxPos++;
                var newItem = new LauncherItem
                {
                    Name = app.Name,
                    Type = "EXE",
                    Target = app.Target,
                    Arguments = app.Arguments,
                    IconPath = app.IconPath,
                    CategoryId = targetCatId,
                    Position = maxPos,
                    IsFavorite = app.IsOnDesktop && targetCatId == gamesCatId,
                    ParentId = 0,
                    IsUserAdded = false
                };
                db.InsertItem(newItem);
                existingItems.Add(newItem);
                summary.TotalAdded++;

                if (app.CategoryName == CatGames) summary.GamesCount++;
                else if (app.CategoryName == CatInternet) summary.InternetCount++;
                else if (app.CategoryName == CatDev) summary.DevCount++;
                else summary.SystemCount++;
            }

            return summary;
        }
    }
}
