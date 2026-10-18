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
                CreateItem(-905, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 4),
                CreateItem(-906, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 5),
                CreateItem(-907, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 6),
                CreateItem(-908, "🔮 Pencere Şeffaflığı (%84)", "WINDOW_TILING", "GLASS_OPACITY", 7),
                CreateItem(-909, "📋 Hızlı Metin Yapıştır", "SNIPPET", "Merhaba, iyi çalışmalar dilerim.", 8),
                CreateItem(-910, "🚀 Gaming Boost (RAM Temizle)", "GAMING_BOOST", "BOOST_RAM", 9)
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
                CreateItem(-913, "Dosya Ara (Ctrl+P)", "HOTKEY", "Ctrl+P", 2),
                CreateItem(-914, "📋 Git Durum Komutu", "SNIPPET", "git status && git diff --stat", 3),
                CreateItem(-915, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 4),
                CreateItem(-916, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 5),
                CreateItem(-917, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 6),
                CreateItem(-918, "🔮 Pencere Şeffaflığı (%84)", "WINDOW_TILING", "GLASS_OPACITY", 7),
                CreateItem(-919, "🚀 Gaming Boost (RAM Temizle)", "GAMING_BOOST", "BOOST_RAM", 8)
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
                CreateItem(-924, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 3),
                CreateItem(-925, "🔮 Pencere Şeffaflığı (%84)", "WINDOW_TILING", "GLASS_OPACITY", 4),
                CreateItem(-926, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 5),
                CreateItem(-927, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 6),
                CreateItem(-928, "📋 Hızlı Metin Yapıştır", "SNIPPET", "Merhaba, iyi çalışmalar dilerim.", 7),
                CreateItem(-929, "🚀 Gaming Boost (RAM Temizle)", "GAMING_BOOST", "BOOST_RAM", 8)
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
                CreateItem(-931, "⬅️ Pencereyi Sola Yasla", "WINDOW_TILING", "SNAP_LEFT", 0),
                CreateItem(-932, "➡️ Pencereyi Sağa Yasla", "WINDOW_TILING", "SNAP_RIGHT", 1),
                CreateItem(-933, "📌 Her Zaman Üstte Tut", "WINDOW_TILING", "ALWAYS_ON_TOP", 2),
                CreateItem(-934, "🔮 Pencere Şeffaflığı (%84)", "WINDOW_TILING", "GLASS_OPACITY", 3),
                CreateItem(-935, "🚀 Gaming Boost (RAM Temizle)", "GAMING_BOOST", "BOOST_RAM", 4),
                CreateItem(-936, "📋 Hızlı Metin Yapıştır", "SNIPPET", "Merhaba, iyi çalışmalar dilerim.", 5),
                CreateItem(-937, "📋 Git Durum Komutu", "SNIPPET", "git status && git diff --stat", 6),
                CreateItem(-938, "Yeni Sekme (Ctrl+T)", "HOTKEY", "Ctrl+T", 7),
                CreateItem(-939, "Komut Paleti (Ctrl+Shift+P)", "HOTKEY", "Ctrl+Shift+P", 8),
                CreateItem(-940, "Terminal Aç (Ctrl+J)", "HOTKEY", "Ctrl+J", 9)
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
