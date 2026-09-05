using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RadialLauncher.Services
{
    public class SystemActionInfo
    {
        public string ActionKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string IconSymbol { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    public static class SystemActionService
    {
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
        };

        public static void ExecuteAction(string actionKey)
        {
            try
            {
                switch (actionKey.ToUpperInvariant())
                {
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
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("action_error.log", $"{DateTime.Now}: {ex}\n");
            }
        }

        private static void SendKey(byte vkCode)
        {
            keybd_event(vkCode, 0, 0, UIntPtr.Zero);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static string GetIconForAction(string actionKey)
        {
            var match = AvailableActions.Find(a => string.Equals(a.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase));
            return match?.IconSymbol ?? "⚡";
        }
    }
}
