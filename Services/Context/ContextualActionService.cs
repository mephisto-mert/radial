using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using RadialLauncher.Models;
using Serilog;

namespace RadialLauncher.Services.Context
{
    public class ContextualRuleConfig
    {
        public string ProcessName { get; set; } = string.Empty;
        public List<ContextualItemConfig> Items { get; set; } = new();
    }

    public class ContextualItemConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string Type { get; set; } = "EXE";
        public string IconPath { get; set; } = string.Empty;
    }

    public class ContextualActionService : IContextualActionService
    {
        private readonly string _configPath;
        private List<ContextualRuleConfig> _rules = new();

        public ContextualActionService(string? customConfigPath = null)
        {
            if (!string.IsNullOrEmpty(customConfigPath))
            {
                _configPath = customConfigPath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(appData, "RadialLauncher");
                _configPath = Path.Combine(folder, "context_actions.json");
            }

            LoadOrCreateDefaultConfig();
        }

        public void Reload()
        {
            LoadOrCreateDefaultConfig();
        }

        private void LoadOrCreateDefaultConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (!File.Exists(_configPath))
                {
                    _rules = GetDefaultRules();
                    string json = JsonSerializer.Serialize(_rules, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_configPath, json);
                    Log.Information("Created default context actions config at {Path}", _configPath);
                }
                else
                {
                    string json = File.ReadAllText(_configPath);
                    var loaded = JsonSerializer.Deserialize<List<ContextualRuleConfig>>(json);
                    _rules = loaded ?? GetDefaultRules();
                    Log.Information("Loaded {Count} contextual action rules from {Path}", _rules.Count, _configPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed loading context actions from {Path}, using defaults", _configPath);
                _rules = GetDefaultRules();
            }
        }

        private static List<ContextualRuleConfig> GetDefaultRules()
        {
            return new List<ContextualRuleConfig>
            {
                new ContextualRuleConfig
                {
                    ProcessName = "code.exe",
                    Items = new List<ContextualItemConfig>
                    {
                        new ContextualItemConfig { Name = "Terminal Aç", Target = "cmd.exe", Arguments = "", Type = "EXE", IconPath = "cmd.exe" },
                        new ContextualItemConfig { Name = "Yeni Pencere", Target = "code", Arguments = "-n", Type = "EXE", IconPath = "code.exe" }
                    }
                },
                new ContextualRuleConfig
                {
                    ProcessName = "chrome.exe",
                    Items = new List<ContextualItemConfig>
                    {
                        new ContextualItemConfig { Name = "Yeni Sekme", Target = "https://www.google.com", Arguments = "", Type = "URL", IconPath = "" },
                        new ContextualItemConfig { Name = "GitHub", Target = "https://github.com", Arguments = "", Type = "URL", IconPath = "" }
                    }
                },
                new ContextualRuleConfig
                {
                    ProcessName = "msedge.exe",
                    Items = new List<ContextualItemConfig>
                    {
                        new ContextualItemConfig { Name = "Yeni Sekme", Target = "https://www.bing.com", Arguments = "", Type = "URL", IconPath = "" },
                        new ContextualItemConfig { Name = "GitHub", Target = "https://github.com", Arguments = "", Type = "URL", IconPath = "" }
                    }
                }
            };
        }

        public List<LauncherItem> GetContextualItems(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return new List<LauncherItem>();

            string cleaned = Path.GetFileName(processName).Trim().ToLowerInvariant();
            string cleanedWithExe = cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? cleaned : cleaned + ".exe";
            string cleanedWithoutExe = cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? cleaned[..^4] : cleaned;

            var matched = _rules.FirstOrDefault(r =>
            {
                string rName = r.ProcessName.Trim().ToLowerInvariant();
                return rName == cleanedWithExe || rName == cleanedWithoutExe;
            });

            if (matched == null || matched.Items == null || matched.Items.Count == 0)
                return new List<LauncherItem>();

            var results = new List<LauncherItem>();
            int count = Math.Min(3, matched.Items.Count);
            for (int i = 0; i < count; i++)
            {
                var cfg = matched.Items[i];
                results.Add(new LauncherItem
                {
                    Id = -300 - i,
                    Name = "⚡ " + cfg.Name,
                    Target = cfg.Target,
                    Arguments = cfg.Arguments,
                    Type = string.IsNullOrWhiteSpace(cfg.Type) ? "EXE" : cfg.Type.ToUpperInvariant(),
                    IconPath = cfg.IconPath,
                    CategoryId = -1,
                    Position = i
                });
            }

            return results;
        }

        public List<ItemContextAction> GetItemQuickActions(LauncherItem item)
        {
            var actions = new List<ItemContextAction>();
            if (item == null) return actions;

            var loc = Localization.LocalizationService.Instance;
            string type = (item.Type ?? "EXE").ToUpperInvariant();
            string target = item.Target ?? string.Empty;
            string name = item.Name ?? string.Empty;

            // 1. Steam Game (e.g. steam://rungameid/730)
            if (target.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
            {
                string appId = target.Substring("steam://rungameid/".Length).Trim();
                actions.Add(new ItemContextAction
                {
                    Id = "STEAM_PLAY",
                    Title = loc.GetString("Play", "Oyna"),
                    Icon = "▶",
                    ActionType = "LAUNCH",
                    Payload = target
                });
                if (!string.IsNullOrEmpty(appId))
                {
                    actions.Add(new ItemContextAction
                    {
                        Id = "STEAM_STORE",
                        Title = loc.GetString("Store", "Mağaza"),
                        Icon = "🛒",
                        ActionType = "URI",
                        Payload = $"https://store.steampowered.com/app/{appId}"
                    });
                    actions.Add(new ItemContextAction
                    {
                        Id = "STEAM_COMMUNITY",
                        Title = loc.GetString("Community", "Topluluk"),
                        Icon = "👥",
                        ActionType = "URI",
                        Payload = $"https://steamcommunity.com/app/{appId}"
                    });
                }
                return actions;
            }

            // 2. Steam App itself
            if (name.Contains("Steam", StringComparison.OrdinalIgnoreCase) || target.EndsWith("steam.exe", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(new ItemContextAction
                {
                    Id = "STEAM_OPEN",
                    Title = loc.GetString("Launch", "Başlat"),
                    Icon = "🚀",
                    ActionType = "LAUNCH",
                    Payload = target
                });
                actions.Add(new ItemContextAction
                {
                    Id = "STEAM_LIBRARY",
                    Title = loc.GetString("Library", "Kütüphane"),
                    Icon = "🎮",
                    ActionType = "URI",
                    Payload = "steam://open/games"
                });
                actions.Add(new ItemContextAction
                {
                    Id = "STEAM_STORE_MAIN",
                    Title = loc.GetString("Store", "Mağaza"),
                    Icon = "🛒",
                    ActionType = "URI",
                    Payload = "steam://open/store"
                });
                return actions;
            }

            // 3. Epic Games
            if (target.StartsWith("com.epicgames.launcher://", StringComparison.OrdinalIgnoreCase) || name.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(new ItemContextAction
                {
                    Id = "EPIC_PLAY",
                    Title = loc.GetString("Play", "Oyna"),
                    Icon = "▶",
                    ActionType = "LAUNCH",
                    Payload = target
                });
                actions.Add(new ItemContextAction
                {
                    Id = "EPIC_STORE",
                    Title = loc.GetString("Store", "Mağaza"),
                    Icon = "🛒",
                    ActionType = "URI",
                    Payload = "https://store.epicgames.com"
                });
                return actions;
            }

            // 4. URL / Website
            if (type == "URL" || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(new ItemContextAction
                {
                    Id = "URL_OPEN",
                    Title = loc.GetString("Open", "Aç"),
                    Icon = "🌐",
                    ActionType = "LAUNCH",
                    Payload = target
                });
                actions.Add(new ItemContextAction
                {
                    Id = "URL_COPY",
                    Title = loc.GetString("Copy", "Kopyala"),
                    Icon = "📋",
                    ActionType = "COPY_URL",
                    Payload = target
                });
                return actions;
            }

            // 5. Folder
            if (type == "FOLDER" || (Directory.Exists(target) && !File.Exists(target)))
            {
                actions.Add(new ItemContextAction
                {
                    Id = "FOLDER_OPEN",
                    Title = loc.GetString("Open", "Aç"),
                    Icon = "📂",
                    ActionType = "LAUNCH",
                    Payload = target
                });
                actions.Add(new ItemContextAction
                {
                    Id = "FOLDER_CMD",
                    Title = loc.GetString("Terminal", "Terminal"),
                    Icon = "⚡",
                    ActionType = "TERMINAL",
                    Payload = target
                });
                return actions;
            }

            // 6. File
            if (type == "FILE")
            {
                actions.Add(new ItemContextAction
                {
                    Id = "FILE_OPEN",
                    Title = loc.GetString("Open", "Aç"),
                    Icon = "📄",
                    ActionType = "LAUNCH",
                    Payload = target
                });
                if (File.Exists(target))
                {
                    actions.Add(new ItemContextAction
                    {
                        Id = "FILE_SHOW",
                        Title = loc.GetString("Location", "Konum"),
                        Icon = "📁",
                        ActionType = "EXPLORE",
                        Payload = target
                    });
                }
                return actions;
            }

            // 7. Normal Windows Application / EXE
            if (type == "EXE" || type == "APP" || type == "SHORTCUT")
            {
                actions.Add(new ItemContextAction
                {
                    Id = "APP_LAUNCH",
                    Title = loc.GetString("Launch", "Başlat"),
                    Icon = "▶",
                    ActionType = "LAUNCH",
                    Payload = target
                });

                if (File.Exists(target) || File.Exists(item.IconPath))
                {
                    string fileToLocate = File.Exists(target) ? target : item.IconPath;
                    actions.Add(new ItemContextAction
                    {
                        Id = "APP_EXPLORE",
                        Title = loc.GetString("Location", "Konum"),
                        Icon = "📁",
                        ActionType = "EXPLORE",
                        Payload = fileToLocate
                    });

                    actions.Add(new ItemContextAction
                    {
                        Id = "APP_ADMIN",
                        Title = loc.GetString("Run_Admin", "Yönetici"),
                        Icon = "⚡",
                        ActionType = "RUNAS_ADMIN",
                        Payload = target
                    });
                }
                return actions;
            }

            // 8. Default fallback for actions / system / plugins
            actions.Add(new ItemContextAction
            {
                Id = "DEFAULT_LAUNCH",
                Title = loc.GetString("Launch", "Başlat"),
                Icon = "▶",
                ActionType = "LAUNCH",
                Payload = target
            });

            return actions;
        }

        public bool ExecuteItemQuickAction(LauncherItem item, ItemContextAction action)
        {
            if (item == null || action == null) return false;

            try
            {
                switch (action.ActionType.ToUpperInvariant())
                {
                    case "LAUNCH":
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = !string.IsNullOrEmpty(action.Payload) ? action.Payload : item.Target,
                            Arguments = item.Arguments ?? "",
                            WorkingDirectory = !string.IsNullOrEmpty(item.WorkingDirectory) ? item.WorkingDirectory : "",
                            UseShellExecute = true
                        });
                        return true;

                    case "URI":
                        if (!string.IsNullOrEmpty(action.Payload))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = action.Payload,
                                UseShellExecute = true
                            });
                            return true;
                        }
                        break;

                    case "COPY_URL":
                        string textToCopy = !string.IsNullOrEmpty(action.Payload) ? action.Payload : item.Target;
                        if (!string.IsNullOrEmpty(textToCopy))
                        {
                            System.Windows.Clipboard.SetText(textToCopy);
                            return true;
                        }
                        break;

                    case "EXPLORE":
                        string exploreTarget = !string.IsNullOrEmpty(action.Payload) ? action.Payload : item.Target;
                        if (File.Exists(exploreTarget))
                        {
                            Process.Start("explorer.exe", $"/select,\"{exploreTarget}\"");
                            return true;
                        }
                        else if (Directory.Exists(exploreTarget))
                        {
                            Process.Start("explorer.exe", $"\"{exploreTarget}\"");
                            return true;
                        }
                        break;

                    case "RUNAS_ADMIN":
                        string adminTarget = !string.IsNullOrEmpty(action.Payload) ? action.Payload : item.Target;
                        if (!string.IsNullOrEmpty(adminTarget))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = adminTarget,
                                Arguments = item.Arguments ?? "",
                                UseShellExecute = true,
                                Verb = "runas"
                            });
                            return true;
                        }
                        break;

                    case "TERMINAL":
                        string termDir = !string.IsNullOrEmpty(action.Payload) ? action.Payload : item.Target;
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            WorkingDirectory = Directory.Exists(termDir) ? termDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            UseShellExecute = true
                        });
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to execute ItemContextAction {Id} ({Type}) on {Target}", action.Id, action.ActionType, item.Target);
            }

            return false;
        }
    }
}
