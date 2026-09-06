using System;
using System.IO;
using RadialLauncher.Data;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ItemRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DatabaseManager _db;
        private readonly ItemRepository _repo;

        public ItemRepositoryTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_items_{Guid.NewGuid():N}.db");
            _db = new DatabaseManager(_testDbPath);
            _db.InitializeDatabase();
            _repo = new ItemRepository(_db);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            }
            catch (IOException)
            {
                // Best-effort test cleanup: temporary file may still be briefly locked by SQLite connection pool.
            }
        }

        [Fact]
        public void Insert_And_GetById_ReturnsItem()
        {
            var item = new LauncherItem
            {
                Name = "Visual Studio Code",
                Target = "code.exe",
                Type = "EXE",
                CategoryId = 1,
                Position = 0
            };

            int id = _repo.Insert(item);
            Assert.True(id > 0);

            var retrieved = _repo.GetById(id);
            Assert.NotNull(retrieved);
            Assert.Equal("Visual Studio Code", retrieved!.Name);
            Assert.Equal("code.exe", retrieved.Target);
        }

        [Fact]
        public void IncrementLaunchCount_IncreasesCountAndUpdatesLastLaunched()
        {
            var item = new LauncherItem
            {
                Name = "Notepad",
                Target = "notepad.exe",
                Type = "EXE",
                CategoryId = 1,
                LaunchCount = 0
            };

            int id = _repo.Insert(item);

            _repo.IncrementLaunchCount(id);
            _repo.IncrementLaunchCount(id);

            var updated = _repo.GetById(id);
            Assert.NotNull(updated);
            Assert.Equal(2, updated!.LaunchCount);
            Assert.NotNull(updated.LastLaunched);
        }

        [Fact]
        public void ToggleFavorite_FlipsIsFavorite()
        {
            var item = new LauncherItem
            {
                Name = "Calculator",
                Target = "calc.exe",
                Type = "EXE",
                CategoryId = 1,
                IsFavorite = false
            };

            int id = _repo.Insert(item);

            _repo.ToggleFavorite(id);
            var fav = _repo.GetById(id);
            Assert.True(fav!.IsFavorite);

            _repo.ToggleFavorite(id);
            var notFav = _repo.GetById(id);
            Assert.False(notFav!.IsFavorite);
        }

        [Fact]
        public void GetMostUsed_ReturnsItemsSortedByLaunchCount()
        {
            int id1 = _repo.Insert(new LauncherItem { Name = "A", Target = "a.exe", Type = "EXE", CategoryId = 1, LaunchCount = 50, IsFavorite = true });
            int id2 = _repo.Insert(new LauncherItem { Name = "B", Target = "b.exe", Type = "EXE", CategoryId = 1, LaunchCount = 200, IsFavorite = true });
            int id3 = _repo.Insert(new LauncherItem { Name = "C", Target = "c.exe", Type = "EXE", CategoryId = 1, LaunchCount = 100, IsFavorite = true });

            var mostUsed = _repo.GetMostUsed(2);
            Assert.Equal(2, mostUsed.Count);
            Assert.Equal("B", mostUsed[0].Name);
            Assert.Equal("C", mostUsed[1].Name);
        }

        [Fact]
        public void Insert_And_Update_UrlItem_PersistsSuccessfully()
        {
            var urlItem = new LauncherItem
            {
                Name = "YouTube",
                Target = "https://youtube.com",
                Type = "URL",
                CategoryId = 1,
                IsFavorite = true,
                IsUserAdded = true
            };

            int id = _repo.Insert(urlItem);
            Assert.True(id > 0);

            var retrieved = _repo.GetById(id);
            Assert.NotNull(retrieved);
            Assert.Equal("YouTube", retrieved!.Name);
            Assert.Equal("https://youtube.com", retrieved.Target);
            Assert.Equal("URL", retrieved.Type);
            Assert.True(retrieved.IsUserAdded);

            // Update URL
            retrieved.Target = "https://music.youtube.com";
            retrieved.Name = "YouTube Music";
            bool updated = _repo.Update(retrieved);
            Assert.True(updated);

            var afterUpdate = _repo.GetById(id);
            Assert.NotNull(afterUpdate);
            Assert.Equal("YouTube Music", afterUpdate!.Name);
            Assert.Equal("https://music.youtube.com", afterUpdate.Target);
        }
    }
}
