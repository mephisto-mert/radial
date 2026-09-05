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
            Assert.True(themes.Count >= 5);
            Assert.Contains(themes, t => t.Name == "Dark");
            Assert.Contains(themes, t => t.Name == "Light");
            Assert.Contains(themes, t => t.Name == "Midnight Blue");
            Assert.Contains(themes, t => t.Name == "Purple Haze");
            Assert.Contains(themes, t => t.Name == "Forest");
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
    }
}
