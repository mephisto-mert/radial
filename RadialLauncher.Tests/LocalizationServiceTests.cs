using System;
using System.Linq;
using RadialLauncher.Services.Localization;
using Xunit;

namespace RadialLauncher.Tests
{
    public class LocalizationServiceTests
    {
        [Fact]
        public void SupportedLanguages_ContainsExactly2Languages()
        {
            var service = new LocalizationService();
            var languages = service.SupportedLanguages;

            Assert.Equal(2, languages.Count);

            Assert.Contains(languages, l => l.Code == "en");
            Assert.Contains(languages, l => l.Code == "tr");
        }

        [Fact]
        public void SetLanguage_UpdatesCurrentLanguage_AndFiresEvent()
        {
            var service = new LocalizationService();
            bool eventFired = false;
            service.OnLanguageChanged += () => eventFired = true;

            service.SetLanguage("tr");
            Assert.Equal("tr", service.CurrentLanguage);
            Assert.True(eventFired);

            // Restore English
            service.SetLanguage("en");
            Assert.Equal("en", service.CurrentLanguage);
        }

        [Fact]
        public void GetString_ReturnsLocalizedValue_ForSupportedLanguages()
        {
            var service = new LocalizationService();

            service.SetLanguage("en");
            Assert.Equal("🔍 Scan PC", service.GetString("Scan_PC"));
            Assert.Equal("➕ Add New Item", service["Add_Item"]);

            service.SetLanguage("tr");
            Assert.Equal("🔍 Bilgisayarı Tara", service.GetString("Scan_PC"));
            Assert.Equal("➕ Yeni Öğe Ekle", service["Add_Item"]);

            // Reset back to English
            service.SetLanguage("en");
        }

        [Fact]
        public void GetString_FallsBackToEnglish_WhenKeyMissingInTargetLanguage()
        {
            var service = new LocalizationService();
            service.SetLanguage("tr");

            string result = service.GetString("NonExistentKey_XYZ", "DefaultFallback");
            Assert.Equal("DefaultFallback", result);

            service.SetLanguage("en");
        }
    }
}
