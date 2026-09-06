using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RadialLauncher.Models;

namespace RadialLauncher.UI.Controls
{
    public partial class RadialPreviewControl : UserControl
    {
        public RadialPreviewControl()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdatePreview();
        }

        public static readonly DependencyProperty ThemeProperty =
            DependencyProperty.Register(nameof(Theme), typeof(Theme), typeof(RadialPreviewControl),
                new PropertyMetadata(null, (d, e) => ((RadialPreviewControl)d).UpdatePreview()));

        public Theme? Theme
        {
            get => (Theme?)GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        public void UpdatePreview()
        {
            if (BaseFill == null || BaseStroke == null || GlowEffect == null || GlowStop == null || CenterFill == null || BubblesCanvas == null)
                return;

            var t = Theme ?? Services.Themes.ThemeService.Instance.GetCurrentTheme();
            if (t == null) return;

            byte a = (byte)(t.BackgroundOpacity * 255);
            BaseFill.Color = Color.FromArgb(a, t.BackgroundColor.R, t.BackgroundColor.G, t.BackgroundColor.B);
            BaseStroke.Color = t.AccentColor;
            GlowEffect.Color = t.AccentColor;
            GlowStop.Color = Color.FromArgb(40, t.AccentColor.R, t.AccentColor.G, t.AccentColor.B);
            CenterFill.Color = t.CenterButtonColor;

            BubblesCanvas.Children.Clear();
            int count = 8;
            double radius = 68.0;
            double cx = 110.0;
            double cy = 110.0;

            for (int i = 0; i < count; i++)
            {
                double angle = (2 * Math.PI / count) * i - (Math.PI / 2);
                double bx = cx + radius * Math.Cos(angle) - 10;
                double by = cy + radius * Math.Sin(angle) - 10;

                var bubble = new Ellipse
                {
                    Width = 20,
                    Height = 20,
                    Fill = new SolidColorBrush(t.IconBackgroundColor),
                    Stroke = (i == 0) ? t.AccentBrush : new SolidColorBrush(Color.FromArgb(60, t.TextR, t.TextG, t.TextB)),
                    StrokeThickness = 1.2
                };
                Canvas.SetLeft(bubble, bx);
                Canvas.SetTop(bubble, by);
                BubblesCanvas.Children.Add(bubble);
            }
        }
    }
}
