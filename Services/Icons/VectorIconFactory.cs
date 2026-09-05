using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RadialLauncher.Services.Icons
{
    public static class VectorIconFactory
    {
        private static readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? GetBrandIcon(string name, string target)
        {
            string key = (name + " " + target).ToLowerInvariant();

            if (key.Contains("youtube") || key.Contains("youtu.be"))
                return GetOrCache("youtube", CreateYouTubeIcon);
            if (key.Contains("chatgpt") || key.Contains("openai"))
                return GetOrCache("chatgpt", CreateChatGptIcon);
            if (key.Contains("github"))
                return GetOrCache("github", CreateGitHubIcon);
            if (key.Contains("gmail"))
                return GetOrCache("gmail", CreateGmailIcon);
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
            if (key.Contains("chrome"))
                return GetOrCache("chrome", CreateChromeIcon);
            if (key.Contains("firefox"))
                return GetOrCache("firefox", CreateFirefoxIcon);
            if (key.Contains("edge"))
                return GetOrCache("edge", CreateEdgeIcon);

            return null;
        }

        private static ImageSource GetOrCache(string key, Func<ImageSource> factory)
        {
            if (!_cache.TryGetValue(key, out var img))
            {
                img = factory();
                _cache[key] = img;
            }
            return img;
        }

        public static ImageSource CreateMonogramIcon(string name, Color bgColor)
        {
            string initials = string.IsNullOrWhiteSpace(name) ? "?" : 
                (name.Length <= 2 ? name.ToUpperInvariant() : name.Substring(0, 2).ToUpperInvariant());

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var bgBrush = new SolidColorBrush(bgColor);
                dc.DrawRoundedRectangle(bgBrush, null, new Rect(4, 4, 56, 56), 14, 14);

                var formattedText = new FormattedText(
                    initials,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    22,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                double tx = (64 - formattedText.Width) / 2;
                double ty = (64 - formattedText.Height) / 2;
                dc.DrawText(formattedText, new Point(tx, ty));
            }

            return RenderVisualToBitmap(visual);
        }

        public static ImageSource CreateIconFromVisual(Visual visual, int width = 64, int height = 64)
        {
            return RenderVisualToBitmap(visual, width, height);
        }

        private static RenderTargetBitmap RenderVisualToBitmap(Visual visual, int width = 64, int height = 64)
        {
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        // Brand Icon Renderers
        private static ImageSource CreateYouTubeIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255, 0, 0)), null, new Rect(6, 12, 52, 40), 12, 12);
                var triangle = new StreamGeometry();
                using (var gc = triangle.Open())
                {
                    gc.BeginFigure(new Point(27, 22), true, true);
                    gc.LineTo(new Point(42, 32), true, false);
                    gc.LineTo(new Point(27, 42), true, false);
                }
                triangle.Freeze();
                dc.DrawGeometry(Brushes.White, null, triangle);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateChatGptIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(16, 163, 127)), null, new Rect(6, 6, 52, 52), 14, 14);
                var formatted = new FormattedText("GPT", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal),
                    18, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateGitHubIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(36, 41, 47)), null, new Point(32, 32), 26, 26);
                var formatted = new FormattedText("GH", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    20, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateGmailIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(234, 67, 53)), null, new Rect(8, 12, 48, 40), 10, 10);
                var formatted = new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal),
                    24, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateDiscordIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(88, 101, 242)), null, new Rect(6, 6, 52, 52), 14, 14);
                var formatted = new FormattedText("🎮", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Emoji"), 26, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateSpotifyIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(30, 215, 96)), null, new Point(32, 32), 26, 26);
                var formatted = new FormattedText("♫", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    28, Brushes.Black, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateTelegramIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(42, 171, 238)), null, new Point(32, 32), 26, 26);
                var formatted = new FormattedText("✈", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Symbol"), 24, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateWhatsAppIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(37, 211, 102)), null, new Rect(6, 6, 52, 52), 14, 14);
                var formatted = new FormattedText("💬", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Emoji"), 26, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateBraveIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255, 118, 0)), null, new Rect(6, 6, 52, 52), 14, 14);
                var formatted = new FormattedText("🦁", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Emoji"), 26, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateChromeIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(66, 133, 244)), null, new Point(32, 32), 26, 26);
                var formatted = new FormattedText("🌐", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Emoji"), 26, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateFirefoxIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 113, 57)), null, new Point(32, 32), 26, 26);
                var formatted = new FormattedText("🦊", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Emoji"), 26, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateEdgeIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0, 120, 215)), null, new Point(32, 32), 26, 26);
                var formatted = new FormattedText("e", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Bold, FontStretches.Normal),
                    30, Brushes.White, 1.0);
                dc.DrawText(formatted, new Point((64 - formatted.Width) / 2, (64 - formatted.Height) / 2));
            }
            return RenderVisualToBitmap(visual);
        }
    }
}
