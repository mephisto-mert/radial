using System;
using System.Collections.Generic;
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
    }
}
