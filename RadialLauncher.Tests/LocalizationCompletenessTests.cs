using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RadialLauncher.Services.Localization;
using Xunit;

namespace RadialLauncher.Tests
{
    public class LocalizationCompletenessTests
    {
        [Fact]
        public void SupportedLanguages_CountIs11_AndSortedAlphabetically()
        {
            var service = new LocalizationService();
            var languages = service.SupportedLanguages;

            Assert.Equal(11, languages.Count);

            // Assert English is default
            Assert.Contains(languages, l => l.Code == "en");

            // Assert list is sorted alphabetically by DisplayName
            var sortedDisplayNames = languages.Select(l => l.DisplayName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var actualDisplayNames = languages.Select(l => l.DisplayName).ToList();
            Assert.Equal(sortedDisplayNames, actualDisplayNames);
        }

        [Fact]
        public void All11Languages_HaveAllKeysDirectlyWithoutFallback()
        {
            var service = new LocalizationService();
            var enDict = service.GetDictionaryForLanguage("en");

            Assert.NotNull(enDict);
            Assert.True(enDict.Count >= 230, $"English dictionary should have at least 230 keys, but has {enDict.Count}");

            foreach (var lang in service.SupportedLanguages)
            {
                var langDict = service.GetDictionaryForLanguage(lang.Code);
                Assert.NotNull(langDict);

                foreach (var kvp in enDict)
                {
                    string key = kvp.Key;
                    Assert.True(service.HasKeyDirectly(lang.Code, key),
                        $"Language '{lang.Code}' ({lang.DisplayName}) is missing direct translation for key '{key}'");

                    string val = langDict[key];
                    Assert.False(string.IsNullOrWhiteSpace(val),
                        $"Language '{lang.Code}' ({lang.DisplayName}) has empty value for key '{key}'");
                }
            }
        }

        [Fact]
        public void AllLanguages_FormatPlaceholders_MatchEnglish()
        {
            var service = new LocalizationService();
            var enDict = service.GetDictionaryForLanguage("en");
            var placeholderRegex = new Regex(@"\{(\d+)\}");

            foreach (var kvp in enDict)
            {
                string key = kvp.Key;
                string enValue = kvp.Value;
                var enPlaceholders = placeholderRegex.Matches(enValue).Select(m => m.Value).OrderBy(p => p).ToList();

                if (enPlaceholders.Count > 0)
                {
                    foreach (var lang in service.SupportedLanguages)
                    {
                        var langDict = service.GetDictionaryForLanguage(lang.Code);
                        string langValue = langDict[key];
                        var langPlaceholders = placeholderRegex.Matches(langValue).Select(m => m.Value).OrderBy(p => p).ToList();

                        Assert.Equal(enPlaceholders, langPlaceholders);
                    }
                }
            }
        }

        [Fact]
        public void LanguageSwitch_UpdatesCurrentLanguage_AndTranslatesKeys()
        {
            var service = new LocalizationService();

            service.SetLanguage("tr");
            Assert.Equal("tr", service.CurrentLanguage);
            Assert.Equal("🔍 Bilgisayarı Tara", service.GetString("Scan_PC"));

            service.SetLanguage("de");
            Assert.Equal("de", service.CurrentLanguage);
            Assert.Equal("🔍 PC durchsuchen", service.GetString("Scan_PC"));

            service.SetLanguage("ja");
            Assert.Equal("ja", service.CurrentLanguage);
            Assert.Equal("🔍 PCをスキャン", service.GetString("Scan_PC"));

            service.SetLanguage("en");
            Assert.Equal("en", service.CurrentLanguage);
            Assert.Equal("🔍 Scan PC", service.GetString("Scan_PC"));
        }
    }
}
