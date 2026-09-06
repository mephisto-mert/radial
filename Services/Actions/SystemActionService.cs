using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace RadialLauncher.Services.Actions
{
    public class SystemActionService : ISystemActionService
    {
        private static SystemActionService? _instance;
        public static SystemActionService Instance => _instance ??= new SystemActionService();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_LWIN = 0x5B;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;

        public static readonly List<SystemActionInfo> AvailableActions = new()
        {
            new SystemActionInfo { ActionKey = "VOLUME_UP", DisplayName = "Ses Aç (+2%)", IconSymbol = "🔊", Category = "Medya" },
            new SystemActionInfo { ActionKey = "VOLUME_DOWN", DisplayName = "Ses Kıs (-2%)", IconSymbol = "🔉", Category = "Medya" },
            new SystemActionInfo { ActionKey = "VOLUME_MUTE", DisplayName = "Sesi Kapat / Aç", IconSymbol = "🔇", Category = "Medya" },
            new SystemActionInfo { ActionKey = "MEDIA_PLAY_PAUSE", DisplayName = "Oynat / Duraklat", IconSymbol = "⏯️", Category = "Medya" },
            new SystemActionInfo { ActionKey = "MEDIA_NEXT", DisplayName = "Sonraki Parça", IconSymbol = "⏭️", Category = "Medya" },
            new SystemActionInfo { ActionKey = "MEDIA_PREV", DisplayName = "Önceki Parça", IconSymbol = "⏮️", Category = "Medya" },
            new SystemActionInfo { ActionKey = "SHOW_DESKTOP", DisplayName = "Masaüstünü Göster (Win+D)", IconSymbol = "🖥️", Category = "Windows" },
            new SystemActionInfo { ActionKey = "SNIP_TOOL", DisplayName = "Ekran Alıntısı (Win+Shift+S)", IconSymbol = "✂️", Category = "Windows" },
            new SystemActionInfo { ActionKey = "TASK_MANAGER", DisplayName = "Görev Yöneticisi", IconSymbol = "⚙️", Category = "Windows" },
            new SystemActionInfo { ActionKey = "LOCK_PC", DisplayName = "Bilgisayarı Kilitle", IconSymbol = "🔒", Category = "Sistem" },
            new SystemActionInfo { ActionKey = "EMPTY_RECYCLE_BIN", DisplayName = "Geri Dönüşüm Kutusunu Boşalt", IconSymbol = "🗑️", Category = "Sistem" },
            new SystemActionInfo { ActionKey = "NEXT_DESKTOP", DisplayName = "Sonraki Masaüstü (Win+Ctrl+→)", IconSymbol = "➡️", Category = "Windows" },
            new SystemActionInfo { ActionKey = "PREV_DESKTOP", DisplayName = "Önceki Masaüstü (Win+Ctrl+←)", IconSymbol = "⬅️", Category = "Windows" },
            new SystemActionInfo { ActionKey = "FOCUS_25", DisplayName = "🍅 Odaklan (25dk)", IconSymbol = "🍅", Category = "Sistem" },
        };

        public event Action<TimeSpan>? FocusTimerTick;
        public event Action? FocusTimerCompleted;

        private System.Threading.Timer? _focusTimer;
        private DateTime _focusTimerEndTime;
        private readonly object _timerLock = new();

        public bool IsFocusTimerRunning { get; private set; }

        public TimeSpan FocusTimerRemaining
        {
            get
            {
                lock (_timerLock)
                {
                    if (!IsFocusTimerRunning) return TimeSpan.Zero;
                    var remaining = _focusTimerEndTime - DateTime.UtcNow;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
            }
        }

        public void StartFocusTimer(int minutes = 25)
        {
            lock (_timerLock)
            {
                _focusTimer?.Dispose();
                _focusTimerEndTime = DateTime.UtcNow.AddMinutes(minutes);
                IsFocusTimerRunning = true;

                _focusTimer = new System.Threading.Timer(OnTimerCallback, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                Log.Information("Started focus timer for {Minutes} minutes, ending at {EndTime}", minutes, _focusTimerEndTime);
            }
        }

        public void StopFocusTimer()
        {
            lock (_timerLock)
            {
                _focusTimer?.Dispose();
                _focusTimer = null;
                IsFocusTimerRunning = false;
                Log.Information("Stopped focus timer");
            }
        }

        private void OnTimerCallback(object? state)
        {
            TimeSpan remaining = FocusTimerRemaining;
            if (remaining <= TimeSpan.Zero)
            {
                StopFocusTimer();
                FocusTimerCompleted?.Invoke();
            }
            else
            {
                FocusTimerTick?.Invoke(remaining);
            }
        }

        public List<SystemActionInfo> GetAvailableActions() => AvailableActions;

        public void ExecuteAction(string actionKey)
        {
            try
            {
                Log.Information("Executing system action: {ActionKey}", actionKey);
                switch (actionKey.ToUpperInvariant())
                {
                    case "FOCUS_25":
                        if (IsFocusTimerRunning)
                        {
                            StopFocusTimer();
                        }
                        else
                        {
                            StartFocusTimer(25);
                        }
                        break;
                    case "VOLUME_UP":
                        SendKey(VK_VOLUME_UP);
                        break;
                    case "VOLUME_DOWN":
                        SendKey(VK_VOLUME_DOWN);
                        break;
                    case "VOLUME_MUTE":
                        SendKey(VK_VOLUME_MUTE);
                        break;
                    case "MEDIA_PLAY_PAUSE":
                        SendKey(VK_MEDIA_PLAY_PAUSE);
                        break;
                    case "MEDIA_NEXT":
                        SendKey(VK_MEDIA_NEXT_TRACK);
                        break;
                    case "MEDIA_PREV":
                        SendKey(VK_MEDIA_PREV_TRACK);
                        break;
                    case "SHOW_DESKTOP":
                        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                        keybd_event(0x44, 0, 0, UIntPtr.Zero); // 'D'
                        keybd_event(0x44, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        break;
                    case "SNIP_TOOL":
                        try
                        {
                            Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
                        }
                        catch
                        {
                            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
                            keybd_event(0x53, 0, 0, UIntPtr.Zero); // 'S'
                            keybd_event(0x53, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }
                        break;
                    case "TASK_MANAGER":
                        Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
                        break;
                    case "LOCK_PC":
                        LockWorkStation();
                        break;
                    case "EMPTY_RECYCLE_BIN":
                        SHEmptyRecycleBin(IntPtr.Zero, null, 7); // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
                        break;
                    case "NEXT_DESKTOP":
                        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                        keybd_event(0x11, 0, 0, UIntPtr.Zero); // VK_CONTROL
                        keybd_event(0x27, 0, 0, UIntPtr.Zero); // VK_RIGHT
                        keybd_event(0x27, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        break;
                    case "PREV_DESKTOP":
                        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                        keybd_event(0x11, 0, 0, UIntPtr.Zero); // VK_CONTROL
                        keybd_event(0x25, 0, 0, UIntPtr.Zero); // VK_LEFT
                        keybd_event(0x25, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        break;
                    case "RESTART_APP":
                        string? exe = Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exe))
                        {
                            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                        }
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => System.Windows.Application.Current.Shutdown());
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing system action {ActionKey}", actionKey);
                // Also write to full path action_error.log in appdata logs directory (fixes relative path issue)
                try
                {
                    string actionLogPath = Path.Combine(
                        RadialLauncher.Services.Data.UserDataPathProvider.Instance.GetLogsFolder(),
                        "action_error.log");
                    File.AppendAllText(actionLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {ex}\n");
                }
                catch (Exception writeEx)
                {
                    Log.Warning(writeEx, "Failed to append to action_error.log");
                }
            }
        }

        private static void SendKey(byte vkCode)
        {
            keybd_event(vkCode, 0, 0, UIntPtr.Zero);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public string GetIconForAction(string actionKey)
        {
            var match = AvailableActions.Find(a => string.Equals(a.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase));
            return match?.IconSymbol ?? "⚡";
        }
    }
}
