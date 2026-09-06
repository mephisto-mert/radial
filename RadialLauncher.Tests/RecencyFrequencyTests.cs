using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Dapper;
using RadialLauncher.Data;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.UI.ViewModels;
using Xunit;

namespace RadialLauncher.Tests
{
    public class RecencyFrequencyTests : IDisposable
    {
        private readonly string _testDbPath;

        public RecencyFrequencyTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"recency_test_{Guid.NewGuid()}.db");
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                try { File.Delete(_testDbPath); } catch { }
            }
        }

        [Fact]
        public void Migration4_AddsUseCountAndLastUsedAt_AndSetsUserVersion4()
        {
            var db = new DatabaseManager(_testDbPath);
            db.InitializeDatabase();

            using var conn = new SqliteConnection(db.GetConnectionString());
            conn.Open();

            int version = conn.QuerySingle<int>("PRAGMA user_version;");
            Assert.True(version >= 4);

            var columns = conn.Query("PRAGMA table_info(Items);");
            var colNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columns)
            {
                colNames.Add((string)col.name);
            }

            Assert.Contains("UseCount", colNames);
            Assert.Contains("LastUsedAt", colNames);
        }

        [Fact]
        public void IncrementLaunchCount_UpdatesBothUseCountAndLastUsedAt()
        {
            var db = new DatabaseManager(_testDbPath);
            db.InitializeDatabase();
            var repo = new ItemRepository(db);

            var item = new LauncherItem
            {
                Name = "Test App",
                Target = "notepad.exe",
                Type = "EXE",
                CategoryId = 1,
                Position = 0
            };
            int id = repo.Insert(item);
            Assert.True(id > 0);

            repo.IncrementLaunchCount(id);

            var retrieved = repo.GetById(id);
            Assert.NotNull(retrieved);
            Assert.Equal(1, retrieved.LaunchCount);
            Assert.Equal(1, retrieved.UseCount);
            Assert.NotNull(retrieved.LastLaunched);
            Assert.NotNull(retrieved.LastUsedAt);
        }

        [Fact]
        public void WeightedScore_AppliesRecencyDecayAndFrequency()
        {
            var now = DateTime.UtcNow;

            var recentHighFreq = new LauncherItem
            {
                Name = "Recent High",
                UseCount = 20,
                LastUsedAt = now.AddHours(-1)
            };

            var oldHighFreq = new LauncherItem
            {
                Name = "Old High",
                UseCount = 20,
                LastUsedAt = now.AddDays(-30) // 30 days ago
            };

            var recentLowFreq = new LauncherItem
            {
                Name = "Recent Low",
                UseCount = 2,
                LastUsedAt = now.AddHours(-1)
            };

            double scoreRecentHigh = RadialMenuViewModel.CalculateUsageScore(recentHighFreq, now);
            double scoreOldHigh = RadialMenuViewModel.CalculateUsageScore(oldHighFreq, now);
            double scoreRecentLow = RadialMenuViewModel.CalculateUsageScore(recentLowFreq, now);

            // Recency decay: Recent item with same count must score higher than old item
            Assert.True(scoreRecentHigh > scoreOldHigh, $"Expected {scoreRecentHigh} > {scoreOldHigh}");

            // Frequency: More used item with same recency must score higher
            Assert.True(scoreRecentHigh > scoreRecentLow, $"Expected {scoreRecentHigh} > {scoreRecentLow}");

            // Favorite bonus
            var favoriteItem = new LauncherItem
            {
                Name = "Fav",
                UseCount = 2,
                LastUsedAt = now.AddHours(-1),
                IsFavorite = true
            };
            double scoreFav = RadialMenuViewModel.CalculateUsageScore(favoriteItem, now);
            Assert.True(scoreFav > scoreRecentLow, "Favorite item should receive a significant score boost");
        }
    }
}
