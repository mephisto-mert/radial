using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RadialLauncher.Models;
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

        [Fact]
        public void Translations_ZeroMixedLanguageFragments_AcrossAll11Locales()
        {
            var service = new LocalizationService();
            var forbiddenEnglishTerms = new[] { "Density", "Reduce Motion", "Final Release" };

            foreach (var lang in service.SupportedLanguages)
            {
                if (lang.Code == "en") continue;
                var dict = service.GetDictionaryForLanguage(lang.Code);
                Assert.NotNull(dict);

                foreach (var kvp in dict)
                {
                    foreach (var term in forbiddenEnglishTerms)
                    {
                        Assert.False(kvp.Value.Contains($"({term})", StringComparison.OrdinalIgnoreCase),
                            $"Language '{lang.Code}' key '{kvp.Key}' contains mixed English fragment '({term})': '{kvp.Value}'");
                    }
                }
            }
        }

        [Fact]
        public void UserCreatedCategory_WithoutSystemKey_IsNeverAutoTranslated()
        {
            var loc = LocalizationService.Instance;

            // User-created categories with names matching system concepts without SystemKey
            var userGames = new Category { Id = 991, Name = "Games", SystemKey = null };
            var userSystem = new Category { Id = 992, Name = "System", SystemKey = null };
            var userWindows = new Category { Id = 993, Name = "Open Windows", SystemKey = null };
            var userClipboard = new Category { Id = 994, Name = "Clipboard History", SystemKey = null };
            var userCustom = new Category { Id = 995, Name = "My Custom Category", SystemKey = null };

            foreach (var lang in loc.SupportedLanguages)
            {
                loc.SetLanguage(lang.Code);
                Assert.Equal("Games", userGames.DisplayName);
                Assert.Equal("System", userSystem.DisplayName);
                Assert.Equal("Open Windows", userWindows.DisplayName);
                Assert.Equal("Clipboard History", userClipboard.DisplayName);
                Assert.Equal("My Custom Category", userCustom.DisplayName);
            }

            loc.SetLanguage("en");
        }

        [Fact]
        public void BuiltinCategory_WithSystemKey_IsProperlyTranslatedInAll11Languages()
        {
            var loc = LocalizationService.Instance;

            var sysMostUsed = new Category { Id = 1, Name = "Most Used", SystemKey = "Cat_MostUsed" };
            var sysWindows = new Category { Id = 2, Name = "Open Windows", SystemKey = "Cat_OpenWindows" };
            var sysClipboard = new Category { Id = 3, Name = "Clipboard History", SystemKey = "Cat_ClipboardHistory" };
            var sysSystem = new Category { Id = 4, Name = "System", SystemKey = "Cat_System" };
            var sysGames = new Category { Id = 5, Name = "Games", SystemKey = "Cat_Games" };

            // English
            loc.SetLanguage("en");
            Assert.Equal("⭐ Most Used", sysMostUsed.DisplayName);
            Assert.Equal("🪟 Open Windows", sysWindows.DisplayName);
            Assert.Equal("📋 Clipboard History", sysClipboard.DisplayName);
            Assert.Equal("⚡ System", sysSystem.DisplayName);
            Assert.Equal("🎮 Games", sysGames.DisplayName);

            // Turkish
            loc.SetLanguage("tr");
            Assert.Equal("⭐ Sık Kullanılanlar", sysMostUsed.DisplayName);
            Assert.Equal("🪟 Açık Pencereler", sysWindows.DisplayName);
            Assert.Equal("📋 Pano Geçmişi", sysClipboard.DisplayName);
            Assert.Equal("⚡ Sistem", sysSystem.DisplayName);
            Assert.Equal("🎮 Oyunlar", sysGames.DisplayName);

            // German
            loc.SetLanguage("de");
            Assert.Equal("⭐ Meistgenutzt", sysMostUsed.DisplayName);
            Assert.Equal("🪟 Offene Fenster", sysWindows.DisplayName);
            Assert.Equal("📋 Zwischenablage", sysClipboard.DisplayName);
            Assert.Equal("⚡ System", sysSystem.DisplayName);
            Assert.Equal("🎮 Spiele", sysGames.DisplayName);

            // Verify all 11 languages have non-empty values
            foreach (var lang in loc.SupportedLanguages)
            {
                loc.SetLanguage(lang.Code);
                Assert.False(string.IsNullOrWhiteSpace(sysMostUsed.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(sysWindows.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(sysClipboard.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(sysSystem.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(sysGames.DisplayName));
            }

            loc.SetLanguage("en");
        }

        [Fact]
        public void HardcodedUiStrings_StaticAudit_PassesClean()
        {
            // Verify that critical files do not contain unlocalized UI strings
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Walk up to find repo root
            string repoRoot = baseDir;
            while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "RadialLauncher.sln")))
            {
                var parent = Directory.GetParent(repoRoot);
                if (parent == null) break;
                repoRoot = parent.FullName;
            }

            if (!File.Exists(Path.Combine(repoRoot, "RadialLauncher.sln"))) return;

            // 1. ProcessRunner.cs should not have hardcoded MessageBox string
            string prPath = Path.Combine(repoRoot, "Services", "Processes", "ProcessRunner.cs");
            if (File.Exists(prPath))
            {
                string prContent = File.ReadAllText(prPath);
                Assert.DoesNotContain("MessageBox.Show($\"Could not launch", prContent);
            }

            // 2. RadialMenuWindow.xaml.cs should not have hardcoded Turkish Sayfa string
            string rmPath = Path.Combine(repoRoot, "UI", "Windows", "RadialMenuWindow.xaml.cs");
            if (File.Exists(rmPath))
            {
                string rmContent = File.ReadAllText(rmPath);
                Assert.DoesNotContain("ToolTip = $\"Sayfa ", rmContent);
            }

            // 3. ManagementWindow.xaml should not have hardcoded static ComboBoxItems in ShortcutCombo
            string mwXamlPath = Path.Combine(repoRoot, "UI", "Windows", "ManagementWindow.xaml");
            if (File.Exists(mwXamlPath))
            {
                string mwContent = File.ReadAllText(mwXamlPath);
                Assert.DoesNotContain("<ComboBoxItem Content=\"Middle Click", mwContent);
                Assert.DoesNotContain("ToolTip=\"Rename Category\"", mwContent);
            }
        }
    }
}
