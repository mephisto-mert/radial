namespace RadialLauncher.Models
{
    public class LauncherItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "EXE"; // "EXE", "URL", "FILE", "FOLDER"
        public string Target { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int Position { get; set; }
        public bool IsFavorite { get; set; }
        public int ParentId { get; set; } = 0; // 0 = root level, >0 = sub-item of a SUBMENU
        public bool IsUserAdded { get; set; } = true;
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#3498db"; // Hex color
        public int Position { get; set; }
    }
}
