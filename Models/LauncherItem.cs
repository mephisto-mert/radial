using System;

namespace RadialLauncher.Models
{
    public class LauncherItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "EXE"; // "EXE", "URL", "FILE", "FOLDER", "ACTION", "SUBMENU", "CLIPBOARD", "WINDOW"
        public string Target { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int Position { get; set; }
        public bool IsFavorite { get; set; }
        public int ParentId { get; set; } = 0; // 0 = root level, >0 = sub-item of a SUBMENU
        public bool IsUserAdded { get; set; } = true;
        
        // Smart usage tracking & tags
        public int LaunchCount { get; set; } = 0;
        public DateTime? LastLaunched { get; set; }
        public int UseCount { get; set; } = 0;
        public DateTime? LastUsedAt { get; set; }
        public string Tags { get; set; } = string.Empty;
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#3498db"; // Hex color
        public int Position { get; set; }
        public string? SystemKey { get; set; }

        public string DisplayName => RadialLauncher.Services.Localization.LocalizationService.Instance.GetCategoryDisplayName(this);
    }

    public class QuickActionItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string IconSymbol { get; set; } = string.Empty;
        public string ActionKey { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class MacroStep
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "EXE"; // "EXE", "URL", "ACTION"
        public string Target { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public int DelayMs { get; set; } = 200;
    }
}
