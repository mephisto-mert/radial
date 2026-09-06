using System;
using System.IO;
using System.Linq;
using RadialLauncher.Models;
using RadialLauncher.Services.Themes;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ThemeServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public ThemeServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"theme_test_root_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            RadialLauncher.Services.Data.UserDataPathProvider.Instance.SetOverrideDataRoot(_tempDir);
        }

        public void Dispose()
        {
            RadialLauncher.Services.Data.UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            try
            {
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
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

        [Theory]
        [InlineData("UnknownTheme123", "Dark")]
        [InlineData("InvalidTheme_XYZ", "Dark")]
        [InlineData("", "Dark")]
        [InlineData("Light", "White")]
        [InlineData("Crimson Red", "Red")]
        [InlineData("Midnight Blue", "Blue")]
        [InlineData("Purple Haze", "Purple")]
        public void GetTheme_WithLegacyOrInvalidNames_MapsOrFallsBackSafely(string input, string expected)
        {
            var themeService = ThemeService.Instance;
            var theme = themeService.GetTheme(input);

            Assert.NotNull(theme);
            Assert.Equal(expected, theme.Name);
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

        [Theory]
        [InlineData("MiddleClick", "Orta Tuş")]
        [InlineData("XButton1", "Fare 4")]
        [InlineData("XButton2", "Fare 5")]
        [InlineData("Ctrl+XButton1", "Ctrl + Fare 4")]
        [InlineData("AltSpace", "Alt + Boşluk")]
        [InlineData("CtrlSpace", "Ctrl + Boşluk")]
        public void ToFriendlyName_ReturnsReadableTurkishDescriptions(string code, string expectedSubstring)
        {
            var loc = RadialLauncher.Services.Localization.LocalizationService.Instance;
            loc.SetLanguage("tr");
            string friendly = RadialLauncher.UI.Windows.ShortcutAssignWindow.ToFriendlyName(code);
            Assert.Contains(expectedSubstring, friendly);
            loc.SetLanguage("en");
        }

        [Theory]
        [InlineData("MiddleClick", "Middle Click")]
        [InlineData("XButton1", "Mouse 4")]
        [InlineData("XButton2", "Mouse 5")]
        [InlineData("Ctrl+XButton1", "Ctrl + Mouse 4")]
        [InlineData("AltSpace", "Alt + Space")]
        [InlineData("CtrlSpace", "Ctrl + Space")]
        public void ToFriendlyName_ReturnsReadableEnglishDescriptions(string code, string expectedSubstring)
        {
            var loc = RadialLauncher.Services.Localization.LocalizationService.Instance;
            loc.SetLanguage("en");
            string friendly = RadialLauncher.UI.Windows.ShortcutAssignWindow.ToFriendlyName(code);
            Assert.Contains(expectedSubstring, friendly);
        }
    }
}
