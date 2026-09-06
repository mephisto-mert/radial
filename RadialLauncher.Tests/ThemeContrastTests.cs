using System;
using System.Windows.Media;
using RadialLauncher.Models;
using RadialLauncher.Services.Themes;
using RadialLauncher.UI.Helpers;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ThemeContrastTests
    {
        [Fact]
        public void AbsoluteBlack_HasZeroLuminance_AndRequiresLightText()
        {
            Color black = Color.FromRgb(0, 0, 0);
            double lum = ThemeContrastHelper.GetPerceivedLuminance(black);
            Assert.Equal(0.0, lum);
            Assert.False(ThemeContrastHelper.IsLightColor(black));

            Color text = ThemeContrastHelper.GetContrastTextColor(black);
            Assert.True(text.R > 200 && text.G > 200 && text.B > 200);
        }

        [Fact]
        public void PureWhite_HasFullLuminance_AndRequiresDarkText()
        {
            Color white = Color.FromRgb(255, 255, 255);
            double lum = ThemeContrastHelper.GetPerceivedLuminance(white);
            Assert.True(lum > 0.99);
            Assert.True(ThemeContrastHelper.IsLightColor(white));

            Color text = ThemeContrastHelper.GetContrastTextColor(white);
            Assert.True(text.R < 50 && text.G < 50 && text.B < 50);
        }

        [Fact]
        public void All8CuratedThemes_HaveSufficientContrastOnTheirBackgrounds()
        {
            var service = new ThemeService();
            var themes = service.GetAllThemes();

            Assert.Equal(8, themes.Count);

            foreach (var theme in themes)
            {
                Color bg = theme.BackgroundColor;
                Color text = ThemeContrastHelper.GetContrastTextColor(bg);

                double bgLum = ThemeContrastHelper.GetPerceivedLuminance(bg);
                double textLum = ThemeContrastHelper.GetPerceivedLuminance(text);

                // Contrast ratio formula: (L1 + 0.05) / (L2 + 0.05)
                double l1 = Math.Max(bgLum, textLum);
                double l2 = Math.Min(bgLum, textLum);
                double ratio = (l1 + 0.05) / (l2 + 0.05);

                Assert.True(ratio >= 4.5, $"Theme '{theme.Name}' must have at least 4.5:1 contrast ratio. Got {ratio:F2}");
            }
        }

        [Fact]
        public void HoverColor_IsVisuallyDistinctFromBaseColor()
        {
            Color darkBg = Color.FromRgb(20, 20, 26);
            Color darkHover = ThemeContrastHelper.GetHoverColor(darkBg);
            Assert.True(darkHover.R > darkBg.R && darkHover.G > darkBg.G && darkHover.B > darkBg.B);

            Color lightBg = Color.FromRgb(245, 245, 245);
            Color lightHover = ThemeContrastHelper.GetHoverColor(lightBg);
            Assert.True(lightHover.R < lightBg.R && lightHover.G < lightBg.G && lightHover.B < lightBg.B);
        }
    }
}
