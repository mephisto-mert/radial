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
        public void SupportedLanguages_CountIsExactly2_EnglishAndTurkish()
        {
            var service = new LocalizationService();
            var languages = service.SupportedLanguages;

            Assert.Equal(2, languages.Count);

            // Assert English and Turkish are present
            Assert.Contains(languages, l => l.Code == "en");
            Assert.Contains(languages, l => l.Code == "tr");
        }

        [Fact]
        public void EnglishAndTurkish_Have100PercentKeyParity_WithoutAnyMissingKeys()
        {
            var service = new LocalizationService();
            var enDict = service.GetDictionaryForLanguage("en");
            var trDict = service.GetDictionaryForLanguage("tr");

            Assert.NotNull(enDict);
            Assert.NotNull(trDict);
            Assert.True(enDict.Count >= 250, $"English dictionary should have at least 250 keys, but has {enDict.Count}");
            Assert.True(trDict.Count >= 250, $"Turkish dictionary should have at least 250 keys, but has {trDict.Count}");

            // Verify 1-to-1 symmetric match
            foreach (var kvp in enDict)
            {
                string key = kvp.Key;
                Assert.True(service.HasKeyDirectly("tr", key),
                    $"Turkish dictionary is missing direct translation for key '{key}'");

                string val = trDict[key];
                Assert.False(string.IsNullOrWhiteSpace(val),
                    $"Turkish dictionary has empty value for key '{key}'");
            }

            foreach (var kvp in trDict)
            {
                string key = kvp.Key;
                Assert.True(service.HasKeyDirectly("en", key),
                    $"English dictionary is missing direct translation for key '{key}'");

                string val = enDict[key];
                Assert.False(string.IsNullOrWhiteSpace(val),
                    $"English dictionary has empty value for key '{key}'");
            }
        }

        [Fact]
        public void AllLanguages_FormatPlaceholders_MatchEnglish()
        {
            var service = new LocalizationService();
            var enDict = service.GetDictionaryForLanguage("en");
            var placeholderRegex = new Regex(@"\{(\d+)\}");

            foreach (var kvp in enDict!)
            {
                string key = kvp.Key;
                string enValue = kvp.Value;
                var enPlaceholders = placeholderRegex.Matches(enValue).Select(m => m.Value).OrderBy(p => p).ToList();

                if (enPlaceholders.Count > 0)
                {
                    foreach (var lang in service.SupportedLanguages)
                    {
                        var langDict = service.GetDictionaryForLanguage(lang.Code);
                        string langValue = langDict![key];
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
            Assert.Equal("➕ Yeni Öğe Ekle", service["Add_Item"]);

            service.SetLanguage("en");
            Assert.Equal("en", service.CurrentLanguage);
            Assert.Equal("🔍 Scan PC", service.GetString("Scan_PC"));
            Assert.Equal("➕ Add New Item", service["Add_Item"]);
        }

        [Fact]
        public void UserCreatedCategory_WithoutSystemKey_IsNeverAutoTranslated()
        {
            var loc = LocalizationService.Instance;

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
        public void BuiltinCategory_WithSystemKey_IsProperlyTranslatedInBothLanguages()
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
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string repoRoot = baseDir;
            while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "RadialLauncher.sln")))
            {
                var parent = Directory.GetParent(repoRoot);
                if (parent == null) break;
                repoRoot = parent.FullName;
            }

            if (!File.Exists(Path.Combine(repoRoot, "RadialLauncher.sln"))) return;

            string prPath = Path.Combine(repoRoot, "Services", "Processes", "ProcessRunner.cs");
            if (File.Exists(prPath))
            {
                string prContent = File.ReadAllText(prPath);
                Assert.DoesNotContain("MessageBox.Show($\"Could not launch", prContent);
            }

            string rmPath = Path.Combine(repoRoot, "UI", "Windows", "RadialMenuWindow.xaml.cs");
            if (File.Exists(rmPath))
            {
                string rmContent = File.ReadAllText(rmPath);
                Assert.DoesNotContain("ToolTip = $\"Sayfa ", rmContent);
            }

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
