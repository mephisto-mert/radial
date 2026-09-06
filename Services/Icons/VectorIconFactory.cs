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

        public static ImageSource? GetActionIcon(string actionKeyOrName)
        {
            if (string.IsNullOrWhiteSpace(actionKeyOrName)) return null;
            string key = actionKeyOrName.ToUpperInvariant();

            if (key.Contains("VOLUME_UP") || key.Contains("SES AÇ") || key.Contains("SES AC"))
                return GetOrCache("act_vol_up", CreateVolumeUpIcon);
            if (key.Contains("VOLUME_DOWN") || key.Contains("SES KIS"))
                return GetOrCache("act_vol_down", CreateVolumeDownIcon);
            if (key.Contains("VOLUME_MUTE") || key.Contains("MUTE") || key.Contains("SESİ KAPAT") || key.Contains("SESI KAPAT"))
                return GetOrCache("act_vol_mute", CreateVolumeMuteIcon);
            if (key.Contains("PLAY_PAUSE") || key.Contains("OYNAT") || key.Contains("DURAKLAT") || key.Contains("MEDIA_PLAY"))
                return GetOrCache("act_play_pause", CreateMediaPlayPauseIcon);
            if (key.Contains("MEDIA_NEXT") || key.Contains("SONRAKI") || key.Contains("SONRAKİ"))
                return GetOrCache("act_media_next", CreateMediaNextIcon);
            if (key.Contains("MEDIA_PREV") || key.Contains("ONCEKI") || key.Contains("ÖNCEKİ"))
                return GetOrCache("act_media_prev", CreateMediaPrevIcon);
            if (key.Contains("SNIP") || key.Contains("EKRAN ALINTISI") || key.Contains("SCREENSHOT"))
                return GetOrCache("act_snip", CreateScreenshotIcon);
            if (key.Contains("TASK_MANAGER") || key.Contains("GÖREV YÖNETİCİSİ") || key.Contains("GÖREV YÖNETICISI") || key.Contains("TASKMGR"))
                return GetOrCache("act_taskmgr", CreateTaskManagerIcon);
            if (key.Contains("LOCK") || key.Contains("KİLİTLE") || key.Contains("KILITLE"))
                return GetOrCache("act_lock", CreateLockIcon);
            if (key.Contains("RECYCLE") || key.Contains("GERİ DÖNÜŞÜM") || key.Contains("GERI DÖNÜŞÜM") || key.Contains("ÇÖP") || key.Contains("COP"))
                return GetOrCache("act_recycle", CreateRecycleBinIcon);
            if (key.Contains("SHOW_DESKTOP") || key.Contains("MASAÜSTÜNÜ GÖSTER") || key.Contains("MASAUSTUNU GOSTER"))
                return GetOrCache("act_show_desktop", CreateShowDesktopIcon);
            if (key.Contains("NEXT_DESKTOP") || key.Contains("PREV_DESKTOP") || key.Contains("DESKTOP") || key.Contains("MASAÜSTÜ"))
                return GetOrCache("act_vdesktop", CreateVirtualDesktopIcon);
            if (key.Contains("FOCUS") || key.Contains("ODAKLAN") || key.Contains("POMODORO"))
                return GetOrCache("act_focus", CreateFocusTimerIcon);
            if (key.Contains("MACRO") || key.Contains("MAKRO"))
                return GetOrCache("act_macro", CreateMacroIcon);

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

        // ==========================================
        // SYSTEM ACTIONS & MEDIA VECTOR GRAPHICS
        // ==========================================

        private static ImageSource CreateVolumeUpIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(37, 99, 235)), null, new Rect(4, 4, 56, 56), 14, 14);

                // Speaker body
                var speaker = new StreamGeometry();
                using (var gc = speaker.Open())
                {
                    gc.BeginFigure(new Point(14, 26), true, true);
                    gc.LineTo(new Point(22, 26), true, false);
                    gc.LineTo(new Point(32, 17), true, false);
                    gc.LineTo(new Point(32, 47), true, false);
                    gc.LineTo(new Point(22, 38), true, false);
                    gc.LineTo(new Point(14, 38), true, false);
                }
                speaker.Freeze();
                dc.DrawGeometry(Brushes.White, null, speaker);

                // Sound waves (Arcs)
                var pen = new Pen(Brushes.White, 3.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                
                var arc1 = new StreamGeometry();
                using (var gc = arc1.Open())
                {
                    gc.BeginFigure(new Point(38, 25), false, false);
                    gc.ArcTo(new Point(38, 39), new Size(10, 10), 0, false, SweepDirection.Clockwise, true, false);
                }
                arc1.Freeze();
                dc.DrawGeometry(null, pen, arc1);

                var arc2 = new StreamGeometry();
                using (var gc = arc2.Open())
                {
                    gc.BeginFigure(new Point(45, 19), false, false);
                    gc.ArcTo(new Point(45, 45), new Size(18, 18), 0, false, SweepDirection.Clockwise, true, false);
                }
                arc2.Freeze();
                dc.DrawGeometry(null, pen, arc2);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateVolumeDownIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(59, 130, 246)), null, new Rect(4, 4, 56, 56), 14, 14);

                var speaker = new StreamGeometry();
                using (var gc = speaker.Open())
                {
                    gc.BeginFigure(new Point(16, 26), true, true);
                    gc.LineTo(new Point(24, 26), true, false);
                    gc.LineTo(new Point(34, 17), true, false);
                    gc.LineTo(new Point(34, 47), true, false);
                    gc.LineTo(new Point(24, 38), true, false);
                    gc.LineTo(new Point(16, 38), true, false);
                }
                speaker.Freeze();
                dc.DrawGeometry(Brushes.White, null, speaker);

                var pen = new Pen(Brushes.White, 3.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                var arc = new StreamGeometry();
                using (var gc = arc.Open())
                {
                    gc.BeginFigure(new Point(40, 25), false, false);
                    gc.ArcTo(new Point(40, 39), new Size(10, 10), 0, false, SweepDirection.Clockwise, true, false);
                }
                arc.Freeze();
                dc.DrawGeometry(null, pen, arc);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateVolumeMuteIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(225, 29, 72)), null, new Rect(4, 4, 56, 56), 14, 14);

                var speaker = new StreamGeometry();
                using (var gc = speaker.Open())
                {
                    gc.BeginFigure(new Point(14, 26), true, true);
                    gc.LineTo(new Point(22, 26), true, false);
                    gc.LineTo(new Point(32, 17), true, false);
                    gc.LineTo(new Point(32, 47), true, false);
                    gc.LineTo(new Point(22, 38), true, false);
                    gc.LineTo(new Point(14, 38), true, false);
                }
                speaker.Freeze();
                dc.DrawGeometry(Brushes.White, null, speaker);

                var pen = new Pen(Brushes.White, 3.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                dc.DrawLine(pen, new Point(39, 25), new Point(49, 39));
                dc.DrawLine(pen, new Point(49, 25), new Point(39, 39));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateMediaPlayPauseIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(16, 185, 129)), null, new Rect(4, 4, 56, 56), 14, 14);

                var playTriangle = new StreamGeometry();
                using (var gc = playTriangle.Open())
                {
                    gc.BeginFigure(new Point(16, 20), true, true);
                    gc.LineTo(new Point(30, 32), true, false);
                    gc.LineTo(new Point(16, 44), true, false);
                }
                playTriangle.Freeze();
                dc.DrawGeometry(Brushes.White, null, playTriangle);

                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(36, 20, 4.5, 24), 2, 2);
                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(44.5, 20, 4.5, 24), 2, 2);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateMediaNextIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(99, 102, 241)), null, new Rect(4, 4, 56, 56), 14, 14);

                var t1 = new StreamGeometry();
                using (var gc = t1.Open())
                {
                    gc.BeginFigure(new Point(16, 20), true, true);
                    gc.LineTo(new Point(29, 32), true, false);
                    gc.LineTo(new Point(16, 44), true, false);
                }
                t1.Freeze();
                dc.DrawGeometry(Brushes.White, null, t1);

                var t2 = new StreamGeometry();
                using (var gc = t2.Open())
                {
                    gc.BeginFigure(new Point(29, 20), true, true);
                    gc.LineTo(new Point(42, 32), true, false);
                    gc.LineTo(new Point(29, 44), true, false);
                }
                t2.Freeze();
                dc.DrawGeometry(Brushes.White, null, t2);

                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(44, 20, 4, 24), 2, 2);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateMediaPrevIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(99, 102, 241)), null, new Rect(4, 4, 56, 56), 14, 14);

                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(16, 20, 4, 24), 2, 2);

                var t1 = new StreamGeometry();
                using (var gc = t1.Open())
                {
                    gc.BeginFigure(new Point(35, 20), true, true);
                    gc.LineTo(new Point(22, 32), true, false);
                    gc.LineTo(new Point(35, 44), true, false);
                }
                t1.Freeze();
                dc.DrawGeometry(Brushes.White, null, t1);

                var t2 = new StreamGeometry();
                using (var gc = t2.Open())
                {
                    gc.BeginFigure(new Point(48, 20), true, true);
                    gc.LineTo(new Point(35, 32), true, false);
                    gc.LineTo(new Point(48, 44), true, false);
                }
                t2.Freeze();
                dc.DrawGeometry(Brushes.White, null, t2);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateScreenshotIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(236, 72, 153)), null, new Rect(4, 4, 56, 56), 14, 14);

                var pen = new Pen(Brushes.White, 3.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

                dc.DrawLine(pen, new Point(17, 25), new Point(17, 18));
                dc.DrawLine(pen, new Point(17, 18), new Point(25, 18));

                dc.DrawLine(pen, new Point(39, 18), new Point(47, 18));
                dc.DrawLine(pen, new Point(47, 18), new Point(47, 25));

                dc.DrawLine(pen, new Point(17, 39), new Point(17, 46));
                dc.DrawLine(pen, new Point(17, 46), new Point(25, 46));

                dc.DrawLine(pen, new Point(39, 46), new Point(47, 46));
                dc.DrawLine(pen, new Point(47, 46), new Point(47, 39));

                dc.DrawEllipse(Brushes.White, null, new Point(32, 32), 4, 4);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateTaskManagerIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(2, 132, 199)), null, new Rect(4, 4, 56, 56), 14, 14);

                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.5);
                dc.DrawRoundedRectangle(null, borderPen, new Rect(14, 16, 36, 32), 4, 4);

                var graphPen = new Pen(Brushes.White, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                var pulse = new StreamGeometry();
                using (var gc = pulse.Open())
                {
                    gc.BeginFigure(new Point(16, 34), false, false);
                    gc.LineTo(new Point(24, 34), true, false);
                    gc.LineTo(new Point(28, 22), true, false);
                    gc.LineTo(new Point(33, 42), true, false);
                    gc.LineTo(new Point(37, 30), true, false);
                    gc.LineTo(new Point(41, 34), true, false);
                    gc.LineTo(new Point(48, 34), true, false);
                }
                pulse.Freeze();
                dc.DrawGeometry(null, graphPen, pulse);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateLockIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(245, 158, 11)), null, new Rect(4, 4, 56, 56), 14, 14);

                var shacklePen = new Pen(Brushes.White, 3.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                var shackle = new StreamGeometry();
                using (var gc = shackle.Open())
                {
                    gc.BeginFigure(new Point(24, 27), false, false);
                    gc.LineTo(new Point(24, 21), true, false);
                    gc.ArcTo(new Point(40, 21), new Size(8, 8), 0, false, SweepDirection.Clockwise, true, false);
                    gc.LineTo(new Point(40, 27), true, false);
                }
                shackle.Freeze();
                dc.DrawGeometry(null, shacklePen, shackle);

                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(20, 27, 24, 20), 4, 4);

                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(245, 158, 11)), null, new Point(32, 35), 2.5, 2.5);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 158, 11)), null, new Rect(31, 35, 2, 6));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateRecycleBinIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(100, 116, 139)), null, new Rect(4, 4, 56, 56), 14, 14);

                var pen = new Pen(Brushes.White, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

                dc.DrawLine(pen, new Point(28, 17), new Point(36, 17));
                dc.DrawLine(pen, new Point(19, 21), new Point(45, 21));

                var bin = new StreamGeometry();
                using (var gc = bin.Open())
                {
                    gc.BeginFigure(new Point(22, 23), true, false);
                    gc.LineTo(new Point(24, 46), true, false);
                    gc.LineTo(new Point(40, 46), true, false);
                    gc.LineTo(new Point(42, 23), true, false);
                }
                bin.Freeze();
                dc.DrawGeometry(null, pen, bin);

                dc.DrawLine(pen, new Point(29, 28), new Point(29, 41));
                dc.DrawLine(pen, new Point(35, 28), new Point(35, 41));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateShowDesktopIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(37, 99, 235)), null, new Rect(4, 4, 56, 56), 14, 14);

                var pen = new Pen(Brushes.White, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

                dc.DrawRoundedRectangle(null, pen, new Rect(15, 17, 34, 24), 3, 3);
                dc.DrawLine(pen, new Point(32, 41), new Point(32, 47));
                dc.DrawLine(pen, new Point(25, 47), new Point(39, 47));

                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), null, new Rect(19, 21, 14, 10), 1.5, 1.5);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateVirtualDesktopIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(139, 92, 246)), null, new Rect(4, 4, 56, 56), 14, 14);

                var pen = new Pen(Brushes.White, 2.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), pen, new Rect(23, 16, 25, 18), 3, 3);
                dc.DrawRoundedRectangle(Brushes.White, pen, new Rect(16, 24, 25, 18), 3, 3);
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateFocusTimerIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(239, 68, 68)), null, new Rect(4, 4, 56, 56), 14, 14);

                dc.DrawEllipse(Brushes.White, null, new Point(32, 35), 14, 14);
                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(30, 16, 4, 5), 1.5, 1.5);

                var handPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 2.0) { StartLineCap = PenLineCap.Round };
                dc.DrawLine(handPen, new Point(32, 35), new Point(32, 26));
                dc.DrawLine(handPen, new Point(32, 35), new Point(39, 35));
            }
            return RenderVisualToBitmap(visual);
        }

        private static ImageSource CreateMacroIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(249, 115, 22)), null, new Rect(4, 4, 56, 56), 14, 14);

                var bolt = new StreamGeometry();
                using (var gc = bolt.Open())
                {
                    gc.BeginFigure(new Point(34, 15), true, true);
                    gc.LineTo(new Point(22, 32), true, false);
                    gc.LineTo(new Point(31, 32), true, false);
                    gc.LineTo(new Point(29, 49), true, false);
                    gc.LineTo(new Point(43, 29), true, false);
                    gc.LineTo(new Point(33, 29), true, false);
                }
                bolt.Freeze();
                dc.DrawGeometry(Brushes.White, null, bolt);
            }
            return RenderVisualToBitmap(visual);
        }
    }
}
