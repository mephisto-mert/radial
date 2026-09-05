using System.Collections.Generic;

namespace RadialLauncher.Services.Clipboard
{
    public class ClipboardItem
    {
        public string Text { get; set; } = string.Empty;
        public string Preview => Text.Length > 24 ? Text.Substring(0, 24) + "..." : Text;
        public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;
    }

    public interface IClipboardService
    {
        IReadOnlyList<ClipboardItem> GetRecentHistory(int limit = 5);
        void CopyToClipboard(string text);
        void RecordCurrentClipboard();
    }
}
