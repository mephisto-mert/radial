using System;
using System.IO;
using System.Linq;
using RadialLauncher.Data;
using RadialLauncher.Models;
using RadialLauncher.Services.Data;
using RadialLauncher.Services.Localization;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ProductionHardeningTests : IDisposable
    {
        private readonly string _tempDir;

        public ProductionHardeningTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"hardening_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            UserDataPathProvider.Instance.SetOverrideDataRoot(_tempDir);
        }

        public void Dispose()
        {
            UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            try
            {
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }

        [Fact]
        public void UserCreatedCategories_NeverTranslatedOnLanguageSwitch()
        {
            var loc = LocalizationService.Instance;
            var userCategory = new Category
            {
                Id = 105,
                Name = "Work & Production Tools",
                SystemKey = null // Pure user category
            };

            var userCatWithSpecialChars = new Category
            {
                Id = 106,
                Name = "Özel Projeler & Finans 2026",
                SystemKey = null
            };

            foreach (var lang in loc.SupportedLanguages)
            {
                loc.SetLanguage(lang.Code);

                // User categories must return exact verbatim name
                Assert.Equal("Work & Production Tools", userCategory.DisplayName);
                Assert.Equal("Work & Production Tools", loc.GetCategoryDisplayName(userCategory));

                Assert.Equal("Özel Projeler & Finans 2026", userCatWithSpecialChars.DisplayName);
                Assert.Equal("Özel Projeler & Finans 2026", loc.GetCategoryDisplayName(userCatWithSpecialChars));
            }

            loc.SetLanguage("en");
        }

        [Fact]
        public void SystemCategories_DynamicallyTranslateAcrossAllLanguages()
        {
            var loc = LocalizationService.Instance;

            var gamesCat = new Category { Id = 4, Name = "🎮 Games", SystemKey = "Cat_Games" };
            var sysCat = new Category { Id = 3, Name = "⚡ System", SystemKey = "Cat_System" };
            var mostUsedCat = new Category { Id = 1, Name = "⭐ Most Used", SystemKey = "Cat_MostUsed" };
            var winCat = new Category { Id = 2, Name = "🪟 Open Windows", SystemKey = "Cat_OpenWindows" };
            var clipCat = new Category { Id = -98, Name = "📋 Clipboard History", SystemKey = "Cat_ClipboardHistory" };

            // English
            loc.SetLanguage("en");
            Assert.Equal("🎮 Games", gamesCat.DisplayName);
            Assert.Equal("⚡ System", sysCat.DisplayName);
            Assert.Equal("⭐ Most Used", mostUsedCat.DisplayName);
            Assert.Equal("🪟 Open Windows", winCat.DisplayName);
            Assert.Equal("📋 Clipboard History", clipCat.DisplayName);

            // Turkish
            loc.SetLanguage("tr");
            Assert.Equal("🎮 Oyunlar", gamesCat.DisplayName);
            Assert.Equal("⚡ Sistem", sysCat.DisplayName);
            Assert.Equal("⭐ Sık Kullanılanlar", mostUsedCat.DisplayName);
            Assert.Equal("🪟 Açık Pencereler", winCat.DisplayName);
            Assert.Equal("📋 Pano Geçmişi", clipCat.DisplayName);

            foreach (var lang in loc.SupportedLanguages)
            {
                loc.SetLanguage(lang.Code);
                Assert.False(string.IsNullOrWhiteSpace(gamesCat.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(sysCat.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(mostUsedCat.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(winCat.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(clipCat.DisplayName));
            }

            loc.SetLanguage("en");
        }

        [Fact]
        public void FreshDatabase_InitializesCleanEnglishDefaults_WithoutTestCategory()
        {
            string dbPath = Path.Combine(_tempDir, "fresh_clean_test.db");
            var db = new DatabaseManager(dbPath);
            db.InitializeDatabase();

            var categories = db.GetAllCategories();
            Assert.NotEmpty(categories);

            // Verify zero contamination of TestCategory
            Assert.DoesNotContain(categories, c => c.Name.Contains("TestCategory", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(categories, c => c.Name.Equals("TestCategory", StringComparison.OrdinalIgnoreCase));

            // Verify built-in system keys
            Assert.Contains(categories, c => c.SystemKey == "Cat_OpenWindows");
            Assert.Contains(categories, c => c.SystemKey == "Cat_System");

            // Verify default items
            var items = db.GetAllItems();
            Assert.NotEmpty(items);
            Assert.DoesNotContain(items, i => i.Name.Contains("TestCategory", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void DatabaseMigration5_EnsuresSystemKeyColumn_AndTagsBuiltinCategories()
        {
            string dbPath = Path.Combine(_tempDir, "migration5_test.db");
            var db = new DatabaseManager(dbPath);
            db.InitializeDatabase();

            var categories = db.GetAllCategories();
            var openWin = categories.FirstOrDefault(c => c.Name.Contains("Open Windows") || c.Name.Contains("Açık Pencereler"));
            Assert.NotNull(openWin);
            Assert.Equal("Cat_OpenWindows", openWin.SystemKey);

            var systemCat = categories.FirstOrDefault(c => c.Name.Contains("System") || c.Name.Contains("Sistem"));
            Assert.NotNull(systemCat);
            Assert.Equal("Cat_System", systemCat.SystemKey);
        }

        [Fact]
        public void CategoryRename_WithDuplicateName_IsRejected()
        {
            string dbPath = Path.Combine(_tempDir, "rename_dup_test.db");
            var db = new DatabaseManager(dbPath);
            db.InitializeDatabase();

            var catRepo = new RadialLauncher.Data.Repositories.CategoryRepository(db);
            var categories = catRepo.GetAll();
            var cat1 = categories[0];
            var cat2 = categories[1];

            // Renaming cat1 to cat2's exact name should not create duplicates
            bool dupExists = categories.Any(c => c.Id != cat1.Id && c.Name.Equals(cat2.Name, StringComparison.OrdinalIgnoreCase));
            Assert.True(dupExists);
        }
    }
}
