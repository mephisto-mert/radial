using System;
using System.IO;
using System.Linq;
using RadialLauncher.Models;
using RadialLauncher.Services.Themes;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ThemeServiceTests
    {
        [Fact]
        public void GetAllThemes_ContainsDefaultThemes()
        {
            var themeService = ThemeService.Instance;
            var themes = themeService.GetAllThemes();

            Assert.NotNull(themes);
            Assert.True(themes.Count >= 8);
            Assert.Contains(themes, t => t.Name == "Dark");
            Assert.Contains(themes, t => t.Name == "White");
            Assert.Contains(themes, t => t.Name == "Red");
            Assert.Contains(themes, t => t.Name == "Blue");
            Assert.Contains(themes, t => t.Name == "Purple");
            Assert.Contains(themes, t => t.Name == "Forest");
            Assert.Contains(themes, t => t.Name == "AMOLED Black");
            Assert.Contains(themes, t => t.Name == "High Contrast");
        }

        [Fact]
        public void RadialOpacity_ClampsAndPersistsCorrectly()
        {
            var themeService = ThemeService.Instance;
            double original = themeService.GetRadialOpacity();

            try
            {
                themeService.SetRadialOpacity(0.85);
                Assert.Equal(0.85, themeService.GetRadialOpacity(), 2);

                // Test clamping below 0.20
                themeService.SetRadialOpacity(0.05);
                Assert.Equal(0.20, themeService.GetRadialOpacity(), 2);

                // Test clamping above 1.00
                themeService.SetRadialOpacity(1.50);
                Assert.Equal(1.00, themeService.GetRadialOpacity(), 2);
            }
            finally
            {
                themeService.SetRadialOpacity(original);
            }
        }

        [Fact]
        public void CreateAndSaveCustomTheme_AddsToCustomThemes()
        {
            var themeService = ThemeService.Instance;
            string themeName = $"Cyberpunk_{Guid.NewGuid():N}";

            var custom = new Theme
            {
                Name = themeName,
                BgR = 20, BgG = 10, BgB = 30,
                AccentR = 255, AccentG = 0, AccentB = 128,
                TextR = 255, TextG = 255, TextB = 0,
                IconBgR = 40, IconBgG = 20, IconBgB = 60,
                IconHoverR = 60, IconHoverG = 30, IconHoverB = 90,
                CenterR = 255, CenterG = 0, CenterB = 128
            };

            themeService.SaveCustomTheme(custom);

            var all = themeService.GetAllThemes();
            Assert.Contains(all, t => t.Name == themeName);
        }

        [Fact]
        public void AutoCheckUpdates_CanBeToggledAndPersisted()
        {
            var themeService = ThemeService.Instance;
            bool current = themeService.GetAutoCheckUpdates();

            themeService.SetAutoCheckUpdates(!current);
            Assert.Equal(!current, themeService.GetAutoCheckUpdates());

            // Revert
            themeService.SetAutoCheckUpdates(current);
            Assert.Equal(current, themeService.GetAutoCheckUpdates());
        }

        [Fact]
        public void ResetSettingsToDefault_RestoresDefaultValuesSafely()
        {
            var themeService = ThemeService.Instance;
            themeService.ResetSettingsToDefault();

            Assert.Equal("Dark", themeService.GetCurrentTheme().Name);
            Assert.Equal("MiddleClick", themeService.GetActivationShortcut());
            Assert.True(themeService.GetAutoCheckUpdates());
        }
    }
}
