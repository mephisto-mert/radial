using System;
using System.Data;
using Microsoft.Data.Sqlite;
using RadialLauncher.Data;
using RadialLauncher.Models;
using Xunit;

namespace RadialLauncher.Tests
{
    public class DatabaseMigrationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DatabaseManager _db;

        public DatabaseMigrationTests()
        {
            _testDbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_radial_{Guid.NewGuid():N}.db");
            _db = new DatabaseManager(_testDbPath);
        }

        public void Dispose()
        {
            try
            {
                if (System.IO.File.Exists(_testDbPath))
                {
                    System.IO.File.Delete(_testDbPath);
                }
            }
            catch (System.IO.IOException)
            {
                // Best-effort test cleanup: temporary file may still be briefly locked by SQLite connection pool.
            }
        }

        [Fact]
        public void InitializeDatabase_CreatesExpectedTablesAndRunsMigrations()
        {
            // Act
            _db.InitializeDatabase();

            // Assert
            using var conn = new SqliteConnection(_db.GetConnectionString());
            conn.Open();

            // Check PRAGMA user_version is at least 3
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            var version = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.True(version >= 3, $"Expected user_version >= 3, got {version}");

            // Verify Items columns
            using var cmdTable = conn.CreateCommand();
            cmdTable.CommandText = "PRAGMA table_info(Items);";
            using var reader = cmdTable.ExecuteReader();
            var columns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }

            Assert.Contains("Id", columns);
            Assert.Contains("Name", columns);
            Assert.Contains("Target", columns);
            Assert.Contains("LaunchCount", columns);
            Assert.Contains("LastLaunched", columns);
            Assert.Contains("Tags", columns);

            // Verify QuickActions and CustomThemes tables
            using var cmdTables = conn.CreateCommand();
            cmdTables.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var tableReader = cmdTables.ExecuteReader();
            var tables = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (tableReader.Read())
            {
                tables.Add(tableReader.GetString(0));
            }

            Assert.Contains("QuickActions", tables);
            Assert.Contains("CustomThemes", tables);
        }
    }
}
