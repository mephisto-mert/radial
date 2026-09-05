using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RadialLauncher.Services
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private static string FaviconCacheDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RadialLauncher", "FaviconCache");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static readonly Dictionary<string, ImageSource> _brandCache = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? GetBrandIcon(string name, string target)
        {
            string nameLower = (name ?? "").Trim().ToLowerInvariant();
            string targetLower = (target ?? "").Trim().ToLowerInvariant();
            string key = (nameLower + " " + targetLower);

            if (key.Contains("youtube") || key.Contains("youtu.be"))
                return GetOrCache("youtube", CreateYouTubeIcon);
            if (key.Contains("chatgpt") || key.Contains("openai"))
                return GetOrCache("chatgpt", CreateChatGptIcon);
            if (key.Contains("github"))
                return GetOrCache("github", CreateGitHubIcon);
            if (key.Contains("gmail"))
                return GetOrCache("gmail", CreateGmailIcon);
            if (key.Contains("analytics"))
                return GetOrCache("analytics", CreateAnalyticsIcon);
            if (key.Contains("brave"))
                return GetOrCache("brave", CreateBraveIcon);
            if (key.Contains("discord"))
                return GetOrCache("discord", CreateDiscordIcon);
            if (key.Contains("spotify"))
                return GetOrCache("spotify", CreateSpotifyIcon);
            if (key.Contains("telegram"))
                return GetOrCache("telegram", CreateTelegramIcon);
            if (key.Contains("whatsapp"))
                return GetOrCache("whatsapp", CreateWhatsAppIcon);

            // Steam client only (NOT steam:// games!)
            if (!targetLower.StartsWith("steam://", StringComparison.OrdinalIgnoreCase) && 
                (nameLower == "steam" || targetLower.EndsWith("steam.exe", StringComparison.OrdinalIgnoreCase)))
            {
                return GetOrCache("steam", CreateSteamIcon);
            }

            // General mail client only (NOT custom domains like mephistomail)
            if (nameLower == "mail" || nameLower == "eposta" || nameLower == "e-posta" ||
                targetLower.Contains("mail.google.com") || targetLower.Contains("outlook.live.com"))
            {
                return GetOrCache("mail", CreateMailIcon);
            }

            if (key.Contains("chrome"))
                return GetOrCache("chrome", CreateChromeIcon);
            if (key.Contains("edge"))
                return GetOrCache("edge", CreateEdgeIcon);
            if (key.Contains("firefox"))
                return GetOrCache("firefox", CreateFirefoxIcon);
            if (key.Contains("opera"))
                return GetOrCache("opera", CreateOperaIcon);
            if (key.Contains("minecraft"))
                return GetOrCache("minecraft", CreateMinecraftIcon);
            if (nameLower.Contains("blitz") || nameLower.Contains("riot client") || nameLower.Contains("league of legends"))
                return GetOrCache("blitz", CreateBlitzIcon);
            if (nameLower.Contains("visual studio code") || nameLower == "vscode" || targetLower.EndsWith("\\code.exe", StringComparison.OrdinalIgnoreCase) || targetLower.EndsWith("/code.exe", StringComparison.OrdinalIgnoreCase))
                return GetOrCache("vscode", CreateVsCodeIcon);
            if (key.Contains("notepad"))
                return GetOrCache("notepad", CreateNotepadPlusIcon);
            if (key.Contains("mpc") || key.Contains("vlc"))
                return GetOrCache("mpc", CreateMpcIcon);
            if (nameLower == "rave" || targetLower.Contains("rave.exe"))
                return GetOrCache("rave", CreateRaveIcon);
            if (nameLower.Contains("belgeler") || nameLower.Contains("klasör"))
                return GetOrCache("folder", CreateFolderIcon);
            if (nameLower.Contains("ayar") || nameLower.Contains("setting"))
                return GetOrCache("settings", CreateSettingsIcon);
            if (nameLower == "google" || targetLower.Contains("google.com/search"))
                return GetOrCache("google", CreateGoogleIcon);

            return null;
        }

        private static ImageSource GetOrCache(string key, Func<ImageSource> factory)
        {
            if (_brandCache.TryGetValue(key, out var cached)) return cached;
            var created = factory();
            _brandCache[key] = created;
            return created;
        }

        public static ImageSource CreateMonogramIcon(string name, Color accentColor)
        {
            string label = name.Trim();
            if (label.Length > 2) label = label.Substring(0, 2);
            label = label.ToUpperInvariant();

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // Dark glass base disc
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(25, 25, 32)),
                    new Pen(new SolidColorBrush(Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B)), 1.5),
                    new Point(24, 24), 22, 22);

                var ft = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    14,
                    Brushes.White,
                    96.0);

                dc.DrawText(ft, new Point(24 - ft.Width / 2, 24 - ft.Height / 2));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateYouTubeIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // Dark base
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 20, 26)), null, new Point(24, 24), 23, 23);
                // Red badge
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(230, 33, 23)), null, new Rect(9, 14, 30, 20), 5, 5);
                // White play triangle
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(21, 19), true, true);
                    gc.LineTo(new Point(29, 24), true, false);
                    gc.LineTo(new Point(21, 29), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(Brushes.White, null, geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateChatGptIcon()
        {
            try
            {
                string cached = Path.Combine(FaviconCacheDir, "chatgpt_com.png");
                if (File.Exists(cached))
                {
                    var img = LoadImageFromFile(cached);
                    if (img != null) return img;
                }
            }
            catch { }

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // Dark obsidian disc
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(16, 16, 22)),
                    new Pen(new SolidColorBrush(Color.FromArgb(120, 16, 163, 127)), 1.5),
                    new Point(24, 24), 23, 23);

                // Teal center swirl
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(16, 163, 127)), null, new Point(24, 24), 13, 13);
                dc.DrawEllipse(Brushes.White, null, new Point(24, 24), 6, 6);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(16, 163, 127)), null, new Point(24, 24), 2.5, 2.5);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateGitHubIcon()
        {
            try
            {
                string cached = Path.Combine(FaviconCacheDir, "github_com.png");
                if (File.Exists(cached))
                {
                    var img = LoadImageFromFile(cached);
                    if (img != null) return img;
                }
            }
            catch { }

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // Dark disc
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(24, 24, 30)),
                    new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 1.2),
                    new Point(24, 24), 23, 23);

                // Octocat head silhouette
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(14, 15), true, true);
                    gc.LineTo(new Point(18, 20), true, false);
                    gc.LineTo(new Point(30, 20), true, false);
                    gc.LineTo(new Point(34, 15), true, false);
                    gc.LineTo(new Point(34, 27), true, false);
                    gc.LineTo(new Point(24, 35), true, false);
                    gc.LineTo(new Point(14, 27), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(Brushes.White, null, geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateGmailIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(22, 22, 28)), null, new Point(24, 24), 23, 23);
                // Envelope base
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(240, 240, 245)), null, new Rect(10, 15, 28, 18), 3, 3);
                // Red 'M' fold
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(10, 15), false, false);
                    gc.LineTo(new Point(24, 25), true, false);
                    gc.LineTo(new Point(38, 15), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(234, 67, 53)), 2.5), geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateAnalyticsIcon()
        {
            try
            {
                string cached = Path.Combine(FaviconCacheDir, "analytics_google_com.png");
                if (File.Exists(cached))
                {
                    var img = LoadImageFromFile(cached);
                    if (img != null) return img;
                }
            }
            catch { }

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(24, 24, 32)),
                    new Pen(new SolidColorBrush(Color.FromArgb(100, 244, 180, 0)), 1.2),
                    new Point(24, 24), 23, 23);

                // Bar chart
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(244, 180, 0)), null, new Rect(14, 26, 5, 10), 1.5, 1.5);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(244, 180, 0)), null, new Rect(21, 20, 5, 16), 1.5, 1.5);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(66, 133, 244)), null, new Rect(28, 14, 5, 22), 1.5, 1.5);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateBraveIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(22, 22, 28)),
                    new Pen(new SolidColorBrush(Color.FromArgb(100, 251, 84, 43)), 1.2),
                    new Point(24, 24), 23, 23);

                // Orange lion shield
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(24, 12), true, true);
                    gc.LineTo(new Point(34, 18), true, false);
                    gc.LineTo(new Point(31, 30), true, false);
                    gc.LineTo(new Point(24, 36), true, false);
                    gc.LineTo(new Point(17, 30), true, false);
                    gc.LineTo(new Point(14, 18), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(251, 84, 43)), null, geo);
                dc.DrawEllipse(Brushes.White, null, new Point(24, 24), 4, 4);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateDiscordIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(24, 24, 32)), null, new Point(24, 24), 23, 23);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(88, 101, 242)), null, new Rect(11, 15, 26, 18), 7, 7);
                dc.DrawEllipse(Brushes.White, null, new Point(19, 23), 2.5, 2.5);
                dc.DrawEllipse(Brushes.White, null, new Point(29, 23), 2.5, 2.5);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateSpotifyIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(29, 185, 84)), null, new Point(24, 24), 20, 20);
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(18, 18, 22)), 2.2);
                
                var g1 = new StreamGeometry();
                using (var gc = g1.Open()) { gc.BeginFigure(new Point(16, 20), false, false); gc.ArcTo(new Point(32, 20), new Size(11, 8), 0, false, SweepDirection.Clockwise, true, false); }
                g1.Freeze();
                dc.DrawGeometry(null, pen, g1);

                var g2 = new StreamGeometry();
                using (var gc = g2.Open()) { gc.BeginFigure(new Point(18, 24), false, false); gc.ArcTo(new Point(30, 24), new Size(9, 6), 0, false, SweepDirection.Clockwise, true, false); }
                g2.Freeze();
                dc.DrawGeometry(null, pen, g2);

                var g3 = new StreamGeometry();
                using (var gc = g3.Open()) { gc.BeginFigure(new Point(20, 28), false, false); gc.ArcTo(new Point(28, 28), new Size(6, 4), 0, false, SweepDirection.Clockwise, true, false); }
                g3.Freeze();
                dc.DrawGeometry(null, pen, g3);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateTelegramIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(38, 165, 228)), null, new Point(24, 24), 20, 20);
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(14, 23), true, true);
                    gc.LineTo(new Point(34, 15), true, false);
                    gc.LineTo(new Point(29, 32), true, false);
                    gc.LineTo(new Point(23, 27), true, false);
                    gc.LineTo(new Point(20, 29), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(Brushes.White, null, geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateWhatsAppIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(37, 211, 102)), null, new Point(24, 24), 20, 20);
                dc.DrawEllipse(Brushes.White, null, new Point(24, 23), 11, 11);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(37, 211, 102)), null, new Point(24, 23), 8, 8);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateSteamIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(23, 26, 33)),
                    new Pen(new SolidColorBrush(Color.FromArgb(100, 102, 192, 244)), 1.2),
                    new Point(24, 24), 22, 22);

                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(102, 192, 244)), null, new Point(27, 19), 6, 6);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(102, 192, 244)), null, new Point(18, 28), 4, 4);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(102, 192, 244)), 2.5), new Point(27, 19), new Point(18, 28));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateMailIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(41, 128, 185)), null, new Point(24, 24), 20, 20);
                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(12, 16, 24, 16), 2, 2);
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(12, 16), false, false);
                    gc.LineTo(new Point(24, 25), true, false);
                    gc.LineTo(new Point(36, 16), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(41, 128, 185)), 2), geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateGoogleIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(24, 24, 30)),
                    new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.2),
                    new Point(24, 24), 22, 22);

                var ft = new FormattedText("G",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    22,
                    new SolidColorBrush(Color.FromRgb(66, 133, 244)),
                    96.0);
                dc.DrawText(ft, new Point(24 - ft.Width / 2, 24 - ft.Height / 2));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateChromeIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 22, 28)),
                    new Pen(new SolidColorBrush(Color.FromArgb(100, 234, 67, 53)), 1.5),
                    new Point(24, 24), 22, 22);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(66, 133, 244)),
                    new Pen(Brushes.White, 2.0),
                    new Point(24, 24), 8, 8);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateEdgeIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(16, 24, 32)),
                    new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 1.5),
                    new Point(24, 24), 22, 22);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0, 180, 216)), null, new Point(24, 24), 10, 10);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateFirefoxIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 16, 24)),
                    new Pen(new SolidColorBrush(Color.FromRgb(255, 113, 57)), 1.5),
                    new Point(24, 24), 22, 22);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 148, 8)), null, new Point(24, 24), 10, 10);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateOperaIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(26, 18, 20)),
                    new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0),
                    new Point(24, 24), 22, 22);
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(255, 27, 45)), 3.5), new Point(24, 24), 9, 13);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateMinecraftIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 26, 20)),
                    new Pen(new SolidColorBrush(Color.FromRgb(77, 158, 55)), 1.5),
                    new Point(24, 24), 22, 22);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(134, 96, 67)), null, new Rect(14, 14, 20, 20), 3, 3);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(77, 158, 55)), null, new Rect(14, 14, 20, 8), 2, 2);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateBlitzIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(22, 22, 28)),
                    new Pen(new SolidColorBrush(Color.FromRgb(241, 196, 15)), 1.5),
                    new Point(24, 24), 22, 22);
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(25, 12), true, true);
                    gc.LineTo(new Point(18, 24), true, false);
                    gc.LineTo(new Point(23, 24), true, false);
                    gc.LineTo(new Point(21, 36), true, false);
                    gc.LineTo(new Point(30, 22), true, false);
                    gc.LineTo(new Point(25, 22), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(241, 196, 15)), null, geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateVsCodeIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(18, 22, 32)),
                    new Pen(new SolidColorBrush(Color.FromRgb(0, 122, 204)), 1.5),
                    new Point(24, 24), 22, 22);

                var ft = new FormattedText("</>",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    15,
                    new SolidColorBrush(Color.FromRgb(0, 152, 255)),
                    96.0);
                dc.DrawText(ft, new Point(24 - ft.Width / 2, 24 - ft.Height / 2));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateNotepadPlusIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 24, 20)),
                    new Pen(new SolidColorBrush(Color.FromRgb(144, 224, 80)), 1.2),
                    new Point(24, 24), 22, 22);

                var ft = new FormattedText("N++",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    12,
                    new SolidColorBrush(Color.FromRgb(144, 224, 80)),
                    96.0);
                dc.DrawText(ft, new Point(24 - ft.Width / 2, 24 - ft.Height / 2));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateMpcIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(24, 20, 28)),
                    new Pen(new SolidColorBrush(Color.FromRgb(168, 85, 247)), 1.2),
                    new Point(24, 24), 22, 22);

                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(21, 17), true, true);
                    gc.LineTo(new Point(30, 24), true, false);
                    gc.LineTo(new Point(21, 31), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(168, 85, 247)), null, geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateRaveIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 14, 28)),
                    new Pen(new SolidColorBrush(Color.FromRgb(255, 42, 109)), 1.5),
                    new Point(24, 24), 22, 22);

                var ft = new FormattedText("R",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    20,
                    new SolidColorBrush(Color.FromRgb(5, 217, 232)),
                    96.0);
                dc.DrawText(ft, new Point(24 - ft.Width / 2, 24 - ft.Height / 2));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateWoMicIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(18, 26, 30)),
                    new Pen(new SolidColorBrush(Color.FromRgb(0, 210, 211)), 1.2),
                    new Point(24, 24), 22, 22);

                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0, 210, 211)), null, new Rect(21, 15, 6, 11), 3, 3);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0, 210, 211)), 2), new Point(24, 26), new Point(24, 32));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0, 210, 211)), 2), new Point(20, 32), new Point(28, 32));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateFolderIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 26, 32)),
                    new Pen(new SolidColorBrush(Color.FromRgb(0, 180, 216)), 1.2),
                    new Point(24, 24), 22, 22);

                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(new Point(14, 18), true, true);
                    gc.LineTo(new Point(20, 18), true, false);
                    gc.LineTo(new Point(23, 21), true, false);
                    gc.LineTo(new Point(34, 21), true, false);
                    gc.LineTo(new Point(34, 31), true, false);
                    gc.LineTo(new Point(14, 31), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0, 180, 216)), null, geo);
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource CreateSettingsIcon()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(26, 26, 30)),
                    new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.2),
                    new Point(24, 24), 22, 22);

                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 210)), 2.5), new Point(24, 24), 7, 7);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 210)), 2.5), new Point(24, 13), new Point(24, 35));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 210)), 2.5), new Point(13, 24), new Point(35, 24));
            }
            return ConvertVisualToImage(dv, 48, 48);
        }

        private static ImageSource ConvertVisualToImage(DrawingVisual dv, int width, int height)
        {
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        public static ImageSource? GetFaviconForUrl(string urlTarget)
        {
            try
            {
                string domain = urlTarget;
                if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    domain = new Uri(domain).Host;
                }
                else
                {
                    try { domain = new Uri("https://" + domain).Host; }
                    catch { }
                }

                string safeName = domain.Replace(".", "_").Replace(":", "_");
                string cachePath = Path.Combine(FaviconCacheDir, safeName + ".png");

                if (File.Exists(cachePath))
                {
                    var fileAge = DateTime.Now - File.GetLastWriteTime(cachePath);
                    if (fileAge.TotalDays < 7)
                    {
                        return LoadImageFromFile(cachePath);
                    }
                }

                string faviconUrl = $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(domain)}&sz=64";
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    var data = client.GetByteArrayAsync(faviconUrl).GetAwaiter().GetResult();
                    if (data != null && data.Length > 100)
                    {
                        File.WriteAllBytes(cachePath, data);
                        return LoadImageFromFile(cachePath);
                    }
                }
            }
            catch { }
            return null;
        }

        public static ImageSource? LoadImageFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".ico")
                {
                    try
                    {
                        var decoder = new IconBitmapDecoder(
                            new Uri(path, UriKind.Absolute),
                            BitmapCreateOptions.None,
                            BitmapCacheOption.OnLoad);

                        if (decoder.Frames.Count > 0)
                        {
                            // Prefer crisp 48-128px frame, or highest resolution available
                            var best = decoder.Frames
                                .OrderByDescending(f => f.PixelWidth)
                                .FirstOrDefault(f => f.PixelWidth <= 128)
                                ?? decoder.Frames.OrderByDescending(f => f.PixelWidth).First();

                            return best;
                        }
                    }
                    catch { }

                    // GDI+ Icon decoder fallback for complex / unmanaged ICO headers
                    try
                    {
                        using var gdiIcon = new System.Drawing.Icon(path, 128, 128);
                        var bs = Imaging.CreateBitmapSourceFromHIcon(
                            gdiIcon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        return bs;
                    }
                    catch { }
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static readonly Dictionary<string, ImageSource?> _fileIconCache = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? GetIconForFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            if (_fileIconCache.TryGetValue(path, out var cached))
                return cached;

            var icon = ExtractIconForFileInternal(path);
            _fileIconCache[path] = icon;
            return icon;
        }

        private static ImageSource? ExtractIconForFileInternal(string path)
        {
            // Direct image file (.png, .jpg, .ico)
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".ico")
            {
                var img = LoadImageFromFile(path);
                if (img != null) return img;
                // Never fall through to SHGetFileInfo for image/ico files to prevent generic file association icons
                return null;
            }

            // URL files (Desktop / Steam shortcuts)
            if (ext == ".url" && File.Exists(path))
            {
                try
                {
                    var lines = File.ReadAllLines(path);
                    string? iconFile = null;
                    string? steamUrl = null;
                    foreach (var l in lines)
                    {
                        var trimmed = l.Trim();
                        if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                            iconFile = trimmed.Substring("IconFile=".Length).Trim();
                        else if (trimmed.StartsWith("URL=steam://rungameid/", StringComparison.OrdinalIgnoreCase))
                            steamUrl = trimmed.Substring("URL=".Length).Trim();
                    }
                    if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                    {
                        var img = LoadImageFromFile(iconFile);
                        if (img != null) return img;
                    }
                    if (!string.IsNullOrEmpty(steamUrl))
                    {
                        var img = GetIconForFile(steamUrl);
                        if (img != null) return img;
                    }
                }
                catch { }
                return null;
            }

            // Steam URL lookup (e.g. steam://rungameid/1174180)
            if (path.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
            {
                string appId = path.Substring("steam://rungameid/".Length).Trim();
                var steamIcons = GameDetector.ScanSteamShortcutIcons();
                if (steamIcons.TryGetValue(appId, out var iconFile) && File.Exists(iconFile))
                {
                    var img = LoadImageFromFile(iconFile);
                    if (img != null) return img;
                }

                // Check desktop shortcuts for matching Steam app id
                var desktopFolders = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                };
                foreach (var df in desktopFolders)
                {
                    if (!Directory.Exists(df)) continue;
                    foreach (var uf in Directory.GetFiles(df, "*.url"))
                    {
                        try
                        {
                            var lines = File.ReadAllLines(uf);
                            bool match = false;
                            string? ico = null;
                            foreach (var line in lines)
                            {
                                if (line.IndexOf("steam://rungameid/" + appId, StringComparison.OrdinalIgnoreCase) >= 0) match = true;
                                if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase)) ico = line.Substring(9).Trim();
                            }
                            if (match && !string.IsNullOrEmpty(ico) && File.Exists(ico))
                            {
                                var img = LoadImageFromFile(ico);
                                if (img != null) return img;
                            }
                        }
                        catch { }
                    }
                }

                // Never call SHGetFileInfo on steam:// protocol URLs to prevent blue globe icon
                return null;
            }

            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(path, 0, out shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
            
            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            {
                result = SHGetFileInfo(path, 0x80, out shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
            }

            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    ImageSource img = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    img.Freeze();
                    return img;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    DestroyIcon(shinfo.hIcon);
                }
            }

            // Fallback for Windows shortcuts and apps: ExtractAssociatedIcon
            if (File.Exists(path))
            {
                try
                {
                    using var assocIcon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (assocIcon != null)
                    {
                        var bs = Imaging.CreateBitmapSourceFromHIcon(
                            assocIcon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        return bs;
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
