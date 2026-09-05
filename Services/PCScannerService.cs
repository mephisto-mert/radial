using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using RadialLauncher.Data;
using RadialLauncher.Models;

namespace RadialLauncher.Services
{
    public class ScannedApp
    {
        public string Name { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "🛠️ Sistem & Araçlar";
        public string Source { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;

        public System.Windows.Media.ImageSource? Icon
        {
            get
            {
                if (!string.IsNullOrEmpty(IconPath) && File.Exists(IconPath))
                {
                    var fileIcon = IconExtractor.GetIconForFile(IconPath);
                    if (fileIcon != null) return fileIcon;
                }
                var brand = IconExtractor.GetBrandIcon(Name, Target);
                if (brand != null) return brand;
                if (!string.IsNullOrEmpty(Target))
                {
                    var targetIcon = IconExtractor.GetIconForFile(Target);
                    if (targetIcon != null) return targetIcon;
                }
                return IconExtractor.CreateMonogramIcon(Name, System.Windows.Media.Color.FromRgb(88, 140, 236));
            }
        }
    }

    public class ScanSummary
    {
        public int TotalDiscovered { get; set; }
        public int TotalAdded { get; set; }
        public int GamesCount { get; set; }
        public int InternetCount { get; set; }
        public int DevCount { get; set; }
        public int SystemCount { get; set; }
    }

    public static class PCScannerService
    {
        public const string CatGames = "🎮 Oyunlar";
        public const string CatInternet = "🌐 Web & İnternet";
        public const string CatDev = "💼 Uygulamalar & İş";
        public const string CatTools = "🛠️ Sistem & Araçlar";

        private static readonly string[] IgnoreKeywords = new[]
        {
            "uninstall", "kaldır", "setup", "installer", "unins000",
            "vcredist", "directx", "updater", "crashpad", "help",
            "readme", "license", "documentation", "yardım", "kılavuz",
            "changelog", "release notes", "website", "support",
            "report", "telemetry", "crashhandler"
        };

        private static readonly string[] GameKeywords = new[]
        {
            "steam", "epic", "game", "riot", "valorant", "league", "minecraft",
            "roblox", "gta", "rdr", "red dead", "counter-strike", "csgo", "cs2",
            "blitz", "battle.net", "origin", "ea desktop", "ubisoft", "gog",
            "genshin", "star wars", "hitman", "spider-man", "cities", "zomboid",
            "kenshi", "worldbox", "half sword", "forest", "universe sandbox",
            "playstation", "xbox", "pubg", "apex", "overwatch", "witcher",
            "cyberpunk", "dota", "fortnite", "assassin", "far cry", "fifa",
            "pes", "simulator", "battlefront"
        };

        private static readonly string[] InternetKeywords = new[]
        {
            "chrome", "msedge", "edge", "brave", "firefox", "opera", "vivaldi", "tor",
            "discord", "telegram", "whatsapp", "spotify", "zoom", "teams", "skype",
            "slack", "thunderbird", "rave", "youtube", "twitch", "netflix",
            "viber", "signal", "messenger", "browser", "internet"
        };

        private static readonly string[] DevWorkKeywords = new[]
        {
            "code", "visual studio", "vs code", "git", "github", "android studio",
            "intellij", "pycharm", "webstorm", "rider", "sublime", "notepad++",
            "postman", "docker", "powershell", "terminal", "unity", "unreal",
            "blender", "figma", "excel", "word", "powerpoint", "onenote", "office",
            "outlook", "acrobat", "pdf", "photoshop", "illustrator", "premiere",
            "after effects", "obs", "gimp", "canva", "davinci", "audacity",
            "godot", "dbeaver", "insomnia", "workbench"
        };

        public static List<ScannedApp> ScanAll()
        {
            var results = new List<ScannedApp>();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddIfValid(ScannedApp app)
            {
                if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.Target))
                    return;

                string lowerName = app.Name.ToLowerInvariant();
                foreach (var ignore in IgnoreKeywords)
                {
                    if (lowerName.Contains(ignore)) return;
                }

                string key = app.Target.ToLowerInvariant();
                if (seenTargets.Contains(key)) return;

                string nameKey = app.Name.Trim().ToLowerInvariant();
                if (seenNames.Contains(nameKey)) return;

                seenTargets.Add(key);
                seenNames.Add(nameKey);
                results.Add(app);
            }

            // 1. Steam & Epic Games
            try
            {
                var games = GameDetector.DetectAllGames();
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
            catch { }

            // 2. Desktop Shortcuts (.lnk and .url)
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
                        // .lnk shortcuts
                        foreach (var file in Directory.GetFiles(folder, "*.lnk"))
                        {
                            string appName = Path.GetFileNameWithoutExtension(file);
                            AddIfValid(new ScannedApp
                            {
                                Name = appName,
                                Target = file,
                                CategoryName = ClassifyCategory(appName, file),
                                Source = "Masaüstü"
                            });
                        }

                        // .url shortcuts (Steam games, websites, etc.)
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
                            catch { }

                            AddIfValid(new ScannedApp
                            {
                                Name = appName,
                                Target = target,
                                IconPath = iconPath,
                                CategoryName = ClassifyCategory(appName, target),
                                Source = "Masaüstü"
                            });
                        }
                    }
                }
            }
            catch { }

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
                            string appName = Path.GetFileNameWithoutExtension(file);
                            AddIfValid(new ScannedApp
                            {
                                Name = appName,
                                Target = file,
                                CategoryName = ClassifyCategory(appName, file),
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
                            catch { }

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
            catch { }

            // 4. Registry Installed Programs
            try
            {
                ScanRegistryKeys(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", AddIfValid);
                ScanRegistryKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", AddIfValid);
                ScanRegistryKeys(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", AddIfValid);
            }
            catch { }

            return results.OrderBy(a => a.CategoryName).ThenBy(a => a.Name).ToList();
        }

        private static void ScanRegistryKeys(RegistryKey rootKey, string subKeyPath, Action<ScannedApp> addAction)
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
                    catch { }
                }
            }
            catch { }
        }

        public static string ClassifyCategory(string name, string target)
        {
            if (target.StartsWith("steam://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("com.epicgames.", StringComparison.OrdinalIgnoreCase))
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

        public static ScanSummary ImportToDatabase(DatabaseManager db, List<ScannedApp> apps)
        {
            var summary = new ScanSummary { TotalDiscovered = apps.Count };

            // Ensure categories exist and get their IDs
            int gamesCatId = db.GetOrCreateCategory(CatGames, "#e74c3c");
            int internetCatId = db.GetOrCreateCategory(CatInternet, "#3498db");
            int devCatId = db.GetOrCreateCategory(CatDev, "#2ecc71");
            int toolsCatId = db.GetOrCreateCategory(CatTools, "#e67e22");

            var existingItems = db.GetAllItems();
            var existingTargets = new HashSet<string>(existingItems.Select(i => i.Target), StringComparer.OrdinalIgnoreCase);
            var existingNames = new HashSet<string>(existingItems.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);

            int maxPos = existingItems.Count > 0 ? existingItems.Max(i => i.Position) : 0;

            foreach (var app in apps)
            {
                if (!app.IsSelected) continue;

                if (existingTargets.Contains(app.Target) || existingNames.Contains(app.Name))
                    continue;

                int targetCatId = app.CategoryName switch
                {
                    CatGames => gamesCatId,
                    CatInternet => internetCatId,
                    CatDev => devCatId,
                    _ => toolsCatId
                };

                maxPos++;
                db.InsertItem(new LauncherItem
                {
                    Name = app.Name,
                    Type = "EXE",
                    Target = app.Target,
                    Arguments = app.Arguments,
                    IconPath = app.IconPath,
                    CategoryId = targetCatId,
                    Position = maxPos,
                    IsFavorite = false,
                    ParentId = 0,
                    IsUserAdded = false
                });

                existingTargets.Add(app.Target);
                existingNames.Add(app.Name);
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
