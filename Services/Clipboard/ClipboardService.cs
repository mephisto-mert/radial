using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Serilog;

namespace RadialLauncher.Services.Clipboard
{
    public class ClipboardService : IClipboardService
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private readonly List<ClipboardItem> _history = new();
        private readonly object _lock = new();
        private const int MaxHistory = 20;
        private const int MaxTextLength = 500000; // 500KB max per clipboard item

        private IntPtr _listeningHwnd = IntPtr.Zero;

        public IReadOnlyList<ClipboardItem> GetRecentHistory(int limit = 20)
        {
            lock (_lock)
            {
                int takeCount = Math.Clamp(limit, 0, MaxHistory);
                return _history.Take(takeCount).ToList();
            }
        }

        public void CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                System.Windows.Clipboard.SetText(text);
                AddToHistory(text);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set clipboard text");
            }
        }

        public void PasteItem(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                System.Windows.Clipboard.SetText(text);
                AddToHistory(text);

                // Small delay to let OS update clipboard before triggering Ctrl+V
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(60);
                    keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to paste clipboard text");
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
                        AddToHistory(text);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Clipboard access not available: {Message}", ex.Message);
            }
        }

        public void AddToHistory(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            
            // Truncate huge text to avoid memory bloating
            string safeText = text.Length > MaxTextLength ? text.Substring(0, MaxTextLength) : text;

            lock (_lock)
            {
                var existing = _history.FirstOrDefault(h => h.Text == safeText);
                if (existing != null) _history.Remove(existing);

                _history.Insert(0, new ClipboardItem { Text = safeText, Timestamp = DateTime.UtcNow });
                while (_history.Count > MaxHistory)
                {
                    _history.RemoveAt(_history.Count - 1);
                }
            }
        }

        public void StartListening(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            lock (_lock)
            {
                if (_listeningHwnd == hwnd) return; // Already listening on this HWND
                try
                {
                    if (_listeningHwnd != IntPtr.Zero)
                    {
                        RemoveClipboardFormatListener(_listeningHwnd);
                    }

                    bool success = AddClipboardFormatListener(hwnd);
                    if (success)
                    {
                        _listeningHwnd = hwnd;
                        Log.Information("Started clipboard format listener on HWND {Hwnd}", hwnd);
                    }
                    else
                    {
                        Log.Warning("AddClipboardFormatListener returned false on HWND {Hwnd}", hwnd);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to start clipboard format listener on HWND {Hwnd}", hwnd);
                }
            }
        }

        public void StopListening(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            lock (_lock)
            {
                if (_listeningHwnd != hwnd && _listeningHwnd != IntPtr.Zero) return;
                try
                {
                    RemoveClipboardFormatListener(hwnd);
                    _listeningHwnd = IntPtr.Zero;
                    Log.Information("Stopped clipboard format listener on HWND {Hwnd}", hwnd);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to stop clipboard format listener on HWND {Hwnd}", hwnd);
                }
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }
    }
}
