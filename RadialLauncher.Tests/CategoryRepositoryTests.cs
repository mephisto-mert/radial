using System;
using System.IO;
using RadialLauncher.Data;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Services.Data;
using RadialLauncher.Services.Games;
using Xunit;

namespace RadialLauncher.Tests
{
    public class CategoryRepositoryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DatabaseManager _dbManager;
        private readonly CategoryRepository _categoryRepo;

        public CategoryRepositoryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"radial_cat_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            UserDataPathProvider.Instance.SetOverrideDataRoot(_tempDir);

            _dbManager = new DatabaseManager((IGameDetector?)null);
            _dbManager.InitializeDatabase();
            _categoryRepo = new CategoryRepository(_dbManager);
        }

        public void Dispose()
        {
            UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch { }
        }

        [Fact]
        public void RenameCategory_ValidName_UpdatesCategoryNameAndPreservesId()
        {
            var allCats = _categoryRepo.GetAll();
            Assert.NotEmpty(allCats);
            var target = allCats[0];
            int catId = target.Id;

            string newName = "Productivity Tools";
            bool success = _categoryRepo.Rename(catId, newName);

            Assert.True(success);
            var updated = _categoryRepo.GetById(catId);
            Assert.NotNull(updated);
            Assert.Equal(catId, updated.Id);
            Assert.Equal(newName, updated.Name);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void RenameCategory_EmptyOrWhitespace_FailsAndLeavesOriginalIntact(string? invalidName)
        {
            var target = _categoryRepo.GetAll()[0];
            string originalName = target.Name;

            bool success = _categoryRepo.Rename(target.Id, invalidName!);
            Assert.False(success);

            var current = _categoryRepo.GetById(target.Id);
            Assert.NotNull(current);
            Assert.Equal(originalName, current.Name);
        }

        [Fact]
        public void RenameCategory_NonExistentId_ReturnsFalse()
        {
            bool success = _categoryRepo.Rename(999999, "Non Existent Category");
            Assert.False(success);
        }

        [Fact]
        public void RenameCategory_DuplicateName_RejectsAndPreservesOriginal()
        {
            var allCats = _categoryRepo.GetAll();
            Assert.True(allCats.Count >= 2);

            var cat1 = allCats[0];
            var cat2 = allCats[1];
            string originalCat2Name = cat2.Name;

            // Attempt to rename cat2 to cat1's name (case-insensitive and trimmed)
            bool success = _categoryRepo.Rename(cat2.Id, $"  {cat1.Name.ToLowerInvariant()}  ");
            Assert.False(success);

            var current = _categoryRepo.GetById(cat2.Id);
            Assert.NotNull(current);
            Assert.Equal(originalCat2Name, current.Name);
        }

        [Fact]
        public void RenameCategory_SameNameWithDifferentCasing_Succeeds()
        {
            var target = _categoryRepo.GetAll()[0];
            string modifiedCasing = target.Name.ToUpperInvariant();

            bool success = _categoryRepo.Rename(target.Id, modifiedCasing);
            Assert.True(success);

            var current = _categoryRepo.GetById(target.Id);
            Assert.NotNull(current);
            Assert.Equal(modifiedCasing, current.Name);
        }
    }
}
