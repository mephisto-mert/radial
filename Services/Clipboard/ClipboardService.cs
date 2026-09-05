using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Serilog;

namespace RadialLauncher.Services.Clipboard
{
    public class ClipboardService : IClipboardService
    {
        private readonly List<ClipboardItem> _history = new();
        private const int MaxHistory = 10;

        public IReadOnlyList<ClipboardItem> GetRecentHistory(int limit = 5)
        {
            RecordCurrentClipboard();
            return _history.Take(limit).ToList();
        }

        public void CopyToClipboard(string text)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                RecordCurrentClipboard();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set clipboard text");
            }
        }

        public void RecordCurrentClipboard()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var existing = _history.FirstOrDefault(h => h.Text == text);
                        if (existing != null) _history.Remove(existing);

                        _history.Insert(0, new ClipboardItem { Text = text, Timestamp = DateTime.UtcNow });
                        if (_history.Count > MaxHistory) _history.RemoveAt(_history.Count - 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Clipboard access not available: {Message}", ex.Message);
            }
        }
    }
}
