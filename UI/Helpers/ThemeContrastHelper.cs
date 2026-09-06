using System;
using System.Windows.Media;

namespace RadialLauncher.UI.Helpers
{
    public static class ThemeContrastHelper
    {
        /// <summary>
        /// Calculates relative perceived luminance of a Color (0.0 = darkest black, 1.0 = brightest white).
        /// Standard ITU-R BT.709 / sRGB formula.
        /// </summary>
        public static double GetPerceivedLuminance(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            // sRGB gamma expansion
            r = (r <= 0.03928) ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            g = (g <= 0.03928) ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            b = (b <= 0.03928) ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>
        /// Returns whether a background color is considered "light" (requiring dark text).
        /// </summary>
        public static bool IsLightColor(Color background)
        {
            return GetPerceivedLuminance(background) > 0.40;
        }

        /// <summary>
        /// Returns optimal high-contrast foreground text color for a given background color.
        /// </summary>
        public static Color GetContrastTextColor(Color background)
        {
            return IsLightColor(background) 
                ? Color.FromRgb(15, 20, 28)     // Deep rich dark charcoal
                : Color.FromRgb(248, 250, 252); // Crisp bright white
        }

        /// <summary>
        /// Returns optimal high-contrast foreground Brush for a given background.
        /// </summary>
        public static SolidColorBrush GetContrastTextBrush(Color background)
        {
            return new SolidColorBrush(GetContrastTextColor(background));
        }

        /// <summary>
        /// Returns a subtle, visible border brush with appropriate alpha based on background lightness.
        /// </summary>
        public static SolidColorBrush GetContrastBorderBrush(Color background, byte alphaDark = 40, byte alphaLight = 60)
        {
            if (IsLightColor(background))
            {
                return new SolidColorBrush(Color.FromArgb(alphaLight, 0, 0, 0));
            }
            return new SolidColorBrush(Color.FromArgb(alphaDark, 255, 255, 255));
        }

        /// <summary>
        /// Returns a high-contrast hover surface color for a button or card.
        /// </summary>
        public static Color GetHoverColor(Color baseColor)
        {
            if (IsLightColor(baseColor))
            {
                // Darken slightly for light themes
                byte r = (byte)Math.Max(0, baseColor.R - 20);
                byte g = (byte)Math.Max(0, baseColor.G - 20);
                byte b = (byte)Math.Max(0, baseColor.B - 20);
                return Color.FromRgb(r, g, b);
            }
            else
            {
                // Lighten slightly for dark themes
                byte r = (byte)Math.Min(255, baseColor.R + 24);
                byte g = (byte)Math.Min(255, baseColor.G + 24);
                byte b = (byte)Math.Min(255, baseColor.B + 28);
                return Color.FromRgb(r, g, b);
            }
        }
    }
}
