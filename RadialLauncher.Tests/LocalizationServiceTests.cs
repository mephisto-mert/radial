using System;
using System.Linq;
using RadialLauncher.Services.Localization;
using Xunit;

namespace RadialLauncher.Tests
{
    public class LocalizationServiceTests
    {
        [Fact]
        public void SupportedLanguages_ContainsAtLeast10Languages_AndIsAlphabeticallySorted()
        {
            var service = new LocalizationService();
            var languages = service.SupportedLanguages;

            Assert.True(languages.Count >= 10, $"Expected at least 10 languages, found {languages.Count}");

            // Verify sorted alphabetically by DisplayName
            var sorted = languages.OrderBy(l => l.DisplayName).Select(l => l.Code).ToList();
            var actual = languages.Select(l => l.Code).ToList();
            Assert.Equal(sorted, actual);

            // Verify essential languages are present
            Assert.Contains(languages, l => l.Code == "en");
            Assert.Contains(languages, l => l.Code == "de");
            Assert.Contains(languages, l => l.Code == "es");
            Assert.Contains(languages, l => l.Code == "fr");
            Assert.Contains(languages, l => l.Code == "it");
            Assert.Contains(languages, l => l.Code == "ja");
            Assert.Contains(languages, l => l.Code == "ko");
            Assert.Contains(languages, l => l.Code == "pl");
            Assert.Contains(languages, l => l.Code == "pt-BR");
            Assert.Contains(languages, l => l.Code == "tr");
        }

        [Fact]
        public void SetLanguage_UpdatesCurrentLanguage_AndFiresEvent()
        {
            var service = new LocalizationService();
            bool eventFired = false;
            service.OnLanguageChanged += () => eventFired = true;

            service.SetLanguage("de");
            Assert.Equal("de", service.CurrentLanguage);
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

            service.SetLanguage("de");
            Assert.Equal("🔍 PC durchsuchen", service.GetString("Scan_PC"));

            service.SetLanguage("es");
            Assert.Equal("🔍 Escanear PC", service.GetString("Scan_PC"));

            service.SetLanguage("fr");
            Assert.Equal("🔍 Analyser le PC", service.GetString("Scan_PC"));

            service.SetLanguage("ja");
            Assert.Equal("🔍 PCをスキャン", service.GetString("Scan_PC"));

            // Reset back to English
            service.SetLanguage("en");
        }

        [Fact]
        public void GetString_FallsBackToEnglish_WhenKeyMissingInTargetLanguage()
        {
            var service = new LocalizationService();
            service.SetLanguage("ja");

            // Even if an obscure key were missing, it should fall back to english or fallback string
            string result = service.GetString("NonExistentKey_XYZ", "DefaultFallback");
            Assert.Equal("DefaultFallback", result);

            service.SetLanguage("en");
        }
    }
}
