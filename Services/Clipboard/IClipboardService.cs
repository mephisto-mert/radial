using System;
using System.Collections.Generic;

namespace RadialLauncher.Services.Clipboard
{
    public class ClipboardItem
    {
        public string Text { get; set; } = string.Empty;
        public string Preview => Text.Length > 28 ? Text.Substring(0, 28) + "..." : Text;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public interface IClipboardService
    {
        IReadOnlyList<ClipboardItem> GetRecentHistory(int limit = 20);
        void CopyToClipboard(string text);
        void PasteItem(string text);
        void RecordCurrentClipboard();
        void StartListening(IntPtr hwnd);
        void StopListening(IntPtr hwnd);
        void RemoveAt(int index);
        void ClearHistory();
    }
}
