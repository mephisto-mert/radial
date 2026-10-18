using System;
using System.Collections.Generic;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Intelligence
{
    public class AppProfileInfo
    {
        public string ProfileTitle { get; set; } = string.Empty;
        public string AccentHex { get; set; } = "#8B5CF6";
        public List<LauncherItem> QuickItems { get; set; } = new();
    }

    public interface IAppContextProfileService
    {
        AppProfileInfo GetProfileForProcess(string processName);
    }

    public class AppContextProfileService : IAppContextProfileService
    {
        public AppProfileInfo GetProfileForProcess(string processName)
        {
            string normalized = (processName ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace(".exe", string.Empty);

            return normalized switch
            {
                "chrome" or "brave" or "msedge" or "firefox" => BuildBrowserProfile(),
                "code" or "devenv" or "rider64" or "cursor" => BuildDeveloperProfile(),
                "spotify" or "vlc" => BuildMediaProfile(),
                _ => BuildDefaultProfile()
            };
        }

        private static AppProfileInfo BuildBrowserProfile()
        {
            var items = new List<LauncherItem>
            {
                CreateItem(-901, "Yeni Sekme (Ctrl+T)", "HOTKEY", "Ctrl+T", 0),
                CreateItem(-902, "Kapatılan Sekme (Ctrl+Shift+T)", "HOTKEY", "Ctrl+Shift+T", 1),
                CreateItem(-903, "Geliştirici Araçları (F12)", "HOTKEY", "F12", 2),
                CreateItem(-904, "Gizli Pencere (Ctrl+Shift+N)", "HOTKEY", "Ctrl+Shift+N", 3),
                CreateItem(-905, "🖥️ Diğer Ekrana Gönder", "WINDOW_TILING", "NEXT_MONITOR", 4),
                CreateItem(-906, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 5),
                CreateItem(-907, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 6),
                CreateItem(-908, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 7),
                CreateItem(-909, "🔮 Pencere Şeffaflığı (%84)", "WINDOW_TILING", "GLASS_OPACITY", 8),
                CreateItem(-910, "🎧 Ses Çıkışı Değiştir", "AUDIO_DEVICE", "SWITCH_AUDIO_DEVICE", 9)
            };

            return new AppProfileInfo
            {
                ProfileTitle = "🌐 Tarayıcı Profili",
                AccentHex = "#38BDF8",
                QuickItems = items
            };
        }

        private static AppProfileInfo BuildDeveloperProfile()
        {
            var items = new List<LauncherItem>
            {
                CreateItem(-911, "Komut Paleti (Ctrl+Shift+P)", "HOTKEY", "Ctrl+Shift+P", 0),
                CreateItem(-912, "Terminal Aç (Ctrl+J)", "HOTKEY", "Ctrl+J", 1),
                CreateItem(-913, "📋 Git Durum Komutu", "SNIPPET", "git status && git diff --stat", 2),
                CreateItem(-914, "🖥️ Diğer Ekrana Gönder", "WINDOW_TILING", "NEXT_MONITOR", 3),
                CreateItem(-915, "💾 Çalışma Alanı Kaydet", "WORKSPACE_ACTION", "SAVE_WORKSPACE", 4),
                CreateItem(-916, "📐 Çalışma Alanı Yükle", "WORKSPACE_ACTION", "RESTORE_WORKSPACE", 5),
                CreateItem(-917, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 6),
                CreateItem(-918, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 7),
                CreateItem(-919, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 8),
                CreateItem(-920, "🚀 Gaming Boost (RAM Temizle)", "GAMING_BOOST", "BOOST_RAM", 9)
            };

            return new AppProfileInfo
            {
                ProfileTitle = "💻 Geliştirici Profili",
                AccentHex = "#10B981",
                QuickItems = items
            };
        }

        private static AppProfileInfo BuildMediaProfile()
        {
            var items = new List<LauncherItem>
            {
                CreateItem(-921, "Oynat / Duraklat (Space)", "HOTKEY", "Space", 0),
                CreateItem(-922, "Sonraki Parça (Ctrl+Right)", "HOTKEY", "Ctrl+Right", 1),
                CreateItem(-923, "Önceki Parça (Ctrl+Left)", "HOTKEY", "Ctrl+Left", 2),
                CreateItem(-924, "🎧 Ses Çıkışı Değiştir", "AUDIO_DEVICE", "SWITCH_AUDIO_DEVICE", 3),
                CreateItem(-925, "🖥️ Diğer Ekrana Gönder", "WINDOW_TILING", "NEXT_MONITOR", 4),
                CreateItem(-926, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 5),
                CreateItem(-927, "🔮 Pencere Şeffaflığı (%84)", "WINDOW_TILING", "GLASS_OPACITY", 6),
                CreateItem(-928, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 7),
                CreateItem(-929, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 8)
            };

            return new AppProfileInfo
            {
                ProfileTitle = "🎵 Medya Profili",
                AccentHex = "#F59E0B",
                QuickItems = items
            };
        }

        private static AppProfileInfo BuildDefaultProfile()
        {
            var items = new List<LauncherItem>
            {
                CreateItem(-931, "🖥️ Diğer Ekrana Gönder", "WINDOW_TILING", "NEXT_MONITOR", 0),
                CreateItem(-932, "💾 Çalışma Alanı Kaydet", "WORKSPACE_ACTION", "SAVE_WORKSPACE", 1),
                CreateItem(-933, "📐 Çalışma Alanı Yükle", "WORKSPACE_ACTION", "RESTORE_WORKSPACE", 2),
                CreateItem(-934, "🎧 Ses Çıkışı Değiştir", "AUDIO_DEVICE", "SWITCH_AUDIO_DEVICE", 3),
                CreateItem(-935, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 4),
                CreateItem(-936, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 5),
                CreateItem(-937, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 6),
                CreateItem(-938, "🔮 Pencere Şeffaflığı (%84)", "WINDOW_TILING", "GLASS_OPACITY", 7),
                CreateItem(-939, "🚀 Gaming Boost (RAM Temizle)", "GAMING_BOOST", "BOOST_RAM", 8),
                CreateItem(-940, "📋 Hızlı Metin Yapıştır", "SNIPPET", "Merhaba, iyi çalışmalar dilerim.", 9)
            };

            return new AppProfileInfo
            {
                ProfileTitle = "⚡ Akıllı Aksiyonlar",
                AccentHex = "#8B5CF6",
                QuickItems = items
            };
        }

        private static LauncherItem CreateItem(int id, string name, string type, string target, int position)
        {
            return new LauncherItem
            {
                Id = id,
                Name = name,
                Type = type,
                Target = target,
                Position = position,
                CategoryId = -99,
                IsUserAdded = false
            };
        }
    }
}
