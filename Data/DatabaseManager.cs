using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Dapper;
using RadialLauncher.Models;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Services.Games;
using Serilog;

using RadialLauncher.Services.Data;

namespace RadialLauncher.Data
{
    public class DatabaseManager : IDatabaseManager
    {
        private readonly string _dbPath;
        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IGameDetector? _gameDetector;

        public DatabaseManager() : this(
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IGameDetector>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."))
        {
        }

        public DatabaseManager(IGameDetector? gameDetector)
        {
            _dbPath = UserDataPathProvider.Instance.GetDatabasePath();
            _itemRepo = new ItemRepository(this);
            _categoryRepo = new CategoryRepository(this);
            _gameDetector = gameDetector;
        }

        public DatabaseManager(string dbPath, IGameDetector? gameDetector = null)
        {
            _dbPath = dbPath;
            _itemRepo = new ItemRepository(this);
            _categoryRepo = new CategoryRepository(this);
            _gameDetector = gameDetector;
        }

        public string GetConnectionString() => $"Data Source={_dbPath}";

        public void InitializeDatabase()
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                int currentVersion = connection.QuerySingle<int>("PRAGMA user_version;");
                Log.Information("Database current user_version: {Version}", currentVersion);

                // Migration 1: Base Tables
                if (currentVersion < 1)
                {
                    connection.Execute(@"
                        CREATE TABLE IF NOT EXISTS Items (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            Type TEXT NOT NULL,
                            Target TEXT NOT NULL,
                            Arguments TEXT,
                            WorkingDirectory TEXT,
                            IconPath TEXT,
                            CategoryId INTEGER DEFAULT 0,
                            Position INTEGER DEFAULT 0,
                            IsFavorite INTEGER DEFAULT 0,
                            ParentId INTEGER DEFAULT 0,
                            IsUserAdded INTEGER DEFAULT 1
                        );

                        CREATE TABLE IF NOT EXISTS Categories (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            Color TEXT DEFAULT '#3498db',
                            Position INTEGER DEFAULT 0,
                            SystemKey TEXT
                        );
                    ");
                    connection.Execute("PRAGMA user_version = 1;");
                    currentVersion = 1;
                    Log.Information("Applied Database Migration 1: Base Tables");
                }

                // Migration 2: Categories, System Actions & Backfill Columns
                if (currentVersion < 2)
                {
                    EnsureColumnExists(connection, "Items", "IsFavorite", "INTEGER DEFAULT 0");
                    EnsureColumnExists(connection, "Items", "ParentId", "INTEGER DEFAULT 0");
                    EnsureColumnExists(connection, "Items", "IsUserAdded", "INTEGER DEFAULT 1");
                    EnsureColumnExists(connection, "Categories", "SystemKey", "TEXT");

                    int winCat = connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories WHERE SystemKey = 'Cat_OpenWindows'");
                    if (winCat == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position, SystemKey) VALUES ('🪟 Open Windows', '#9b59b6', @nextPos, 'Cat_OpenWindows')", new { nextPos });
                    }

                    int sysCat = connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories WHERE SystemKey = 'Cat_System'");
                    if (sysCat == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position, SystemKey) VALUES ('⚡ System', '#f1c40f', @nextPos, 'Cat_System')", new { nextPos });
                        int newSysCatId = connection.QuerySingle<int>("SELECT Id FROM Categories WHERE SystemKey = 'Cat_System'");

                        var sysActions = new[]
                        {
                            new { Name = "Volume Up", Type = "ACTION", Target = "VOLUME_UP", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 0, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Volume Down", Type = "ACTION", Target = "VOLUME_DOWN", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 1, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Mute", Type = "ACTION", Target = "VOLUME_MUTE", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 2, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Play / Pause", Type = "ACTION", Target = "MEDIA_PLAY_PAUSE", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 3, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Next Track", Type = "ACTION", Target = "MEDIA_NEXT", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 4, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Previous Track", Type = "ACTION", Target = "MEDIA_PREV", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 5, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Desktop", Type = "ACTION", Target = "SHOW_DESKTOP", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 6, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Snipping Tool", Type = "ACTION", Target = "SNIP_TOOL", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 7, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Task Manager", Type = "ACTION", Target = "TASK_MANAGER", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 8, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Lock PC", Type = "ACTION", Target = "LOCK_PC", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 9, IsFavorite = 0, ParentId = 0 },
                        };

                        foreach (var a in sysActions)
                        {
                            connection.Execute(@"
                                INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, ParentId, IsUserAdded)
                                VALUES (@Name, @Type, @Target, @Arguments, @WorkingDirectory, @IconPath, @CategoryId, @Position, @IsFavorite, @ParentId, 1)", a);
                        }
                    }

                    connection.Execute("PRAGMA user_version = 2;");
                    currentVersion = 2;
                    Log.Information("Applied Database Migration 2: System actions and Categories");
                }

                // Migration 3: Usage Frequency, Quick Actions, Custom Themes
                if (currentVersion < 3)
                {
                    EnsureColumnExists(connection, "Items", "LaunchCount", "INTEGER DEFAULT 0");
                    EnsureColumnExists(connection, "Items", "LastLaunched", "TEXT");
                    EnsureColumnExists(connection, "Items", "Tags", "TEXT");

                    connection.Execute(@"
                        CREATE TABLE IF NOT EXISTS QuickActions (
                            Id TEXT PRIMARY KEY,
                            Name TEXT NOT NULL,
                            IconSymbol TEXT NOT NULL,
                            ActionKey TEXT NOT NULL,
                            [Order] INTEGER DEFAULT 0
                        );

                        CREATE TABLE IF NOT EXISTS CustomThemes (
                            Name TEXT PRIMARY KEY,
                            JsonData TEXT NOT NULL
                        );
                    ");

                    int qCount = connection.QuerySingle<int>("SELECT COUNT(*) FROM QuickActions");
                    if (qCount == 0)
                    {
                        var defaults = new[]
                        {
                            new { Id = "SETTINGS", Name = "Settings", IconSymbol = "⚙️", ActionKey = "SETTINGS", Order = 0 },
                            new { Id = "SEARCH", Name = "Search", IconSymbol = "🔍", ActionKey = "SEARCH", Order = 1 },
                            new { Id = "DESKTOP", Name = "Desktop", IconSymbol = "🖥️", ActionKey = "SHOW_DESKTOP", Order = 2 },
                            new { Id = "SNIP", Name = "Snipping Tool", IconSymbol = "✂️", ActionKey = "SNIP_TOOL", Order = 3 },
                            new { Id = "MUTE", Name = "Mute", IconSymbol = "🔇", ActionKey = "VOLUME_MUTE", Order = 4 }
                        };
                        foreach (var q in defaults)
                        {
                            connection.Execute("INSERT INTO QuickActions (Id, Name, IconSymbol, ActionKey, [Order]) VALUES (@Id, @Name, @IconSymbol, @ActionKey, @Order)", q);
                        }
                    }

                    connection.Execute("PRAGMA user_version = 3;");
                    currentVersion = 3;
                    Log.Information("Applied Database Migration 3: Usage Frequency, Quick Actions, Custom Themes");
                }

                // Migration 4: Recency/Frequency-Aware Tracking (UseCount and LastUsedAt)
                if (currentVersion < 4)
                {
                    EnsureColumnExists(connection, "Items", "UseCount", "INTEGER DEFAULT 0");
                    EnsureColumnExists(connection, "Items", "LastUsedAt", "TEXT");

                    // Backfill from LaunchCount and LastLaunched if existing
                    connection.Execute("UPDATE Items SET UseCount = LaunchCount WHERE (UseCount IS NULL OR UseCount = 0) AND LaunchCount > 0;");
                    connection.Execute("UPDATE Items SET LastUsedAt = LastLaunched WHERE LastUsedAt IS NULL AND LastLaunched IS NOT NULL;");

                    connection.Execute("PRAGMA user_version = 4;");
                    currentVersion = 4;
                    Log.Information("Applied Database Migration 4: Recency/Frequency Tracking (UseCount and LastUsedAt)");
                }

                // Migration 5: Stable System Category Keys & Architecture
                if (currentVersion < 5)
                {
                    EnsureColumnExists(connection, "Categories", "SystemKey", "TEXT");

                    // Assign stable system keys to known built-in categories
                    connection.Execute("UPDATE Categories SET SystemKey = 'Cat_OpenWindows' WHERE (SystemKey IS NULL OR SystemKey = '') AND Name = '🪟 Open Windows';");
                    connection.Execute("UPDATE Categories SET SystemKey = 'Cat_System' WHERE (SystemKey IS NULL OR SystemKey = '') AND Name = '⚡ System';");
                    connection.Execute("UPDATE Categories SET SystemKey = 'Cat_Games' WHERE (SystemKey IS NULL OR SystemKey = '') AND Name = '🎮 Games';");
                    connection.Execute("UPDATE Categories SET SystemKey = 'Cat_MostUsed' WHERE Id = 1 AND (SystemKey IS NULL OR SystemKey = '');");

                    connection.Execute("PRAGMA user_version = 5;");
                    currentVersion = 5;
                    Log.Information("Applied Database Migration 5: Category SystemKey Schema");
                }

                BackfillGamesAndIcons(connection);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize database");
            }
        }

        private void EnsureColumnExists(SqliteConnection conn, string table, string column, string columnDef)
        {
            try
            {
                var colList = conn.Query($"PRAGMA table_info({table});").Select(r => (string)r.name).ToList();
                if (!colList.Contains(column, StringComparer.OrdinalIgnoreCase))
                {
                    conn.Execute($"ALTER TABLE {table} ADD COLUMN {column} {columnDef};");
                    Log.Information("Added missing column {Column} to table {Table}", column, table);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Column check/add {Column} in {Table} warning", column, table);
            }
        }

        private void BackfillGamesAndIcons(SqliteConnection connection)
        {
            try
            {
                int gamesCatId = 0;
                var gamesCat = connection.QueryFirstOrDefault<Category>("SELECT * FROM Categories WHERE SystemKey = 'Cat_Games'");
                if (gamesCat != null)
                {
                    gamesCatId = gamesCat.Id;
                }
                else
                {
                    int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                    connection.Execute("INSERT INTO Categories (Name, Color, Position, SystemKey) VALUES ('🎮 Games', '#e67e22', @nextPos, 'Cat_Games')", new { nextPos });
                    gamesCatId = connection.QuerySingle<int>("SELECT Id FROM Categories WHERE SystemKey = 'Cat_Games' LIMIT 1");
                }

                var detectedGames = _gameDetector != null ? _gameDetector.DetectAll() : new List<DetectedGame>();
                var existingItems = connection.Query<LauncherItem>("SELECT * FROM Items").ToList();

                int currentMaxPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) FROM Items WHERE CategoryId = @gamesCatId", new { gamesCatId });

                foreach (var g in detectedGames)
                {
                    var match = existingItems.FirstOrDefault(i => 
                        string.Equals(i.Target, g.ExePath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.Name, g.Name, StringComparison.OrdinalIgnoreCase));

                    if (match == null)
                    {
                        currentMaxPos++;
                        connection.Execute(@"
                            INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, ParentId, IsUserAdded)
                            VALUES (@Name, 'EXE', @ExePath, '', '', @IconPath, @gamesCatId, @currentMaxPos, 0, 0, 0)",
                            new { g.Name, g.ExePath, g.IconPath, gamesCatId, currentMaxPos });
                    }
                    else if (string.IsNullOrEmpty(match.IconPath) && !string.IsNullOrEmpty(g.IconPath) && File.Exists(g.IconPath))
                    {
                        connection.Execute("UPDATE Items SET IconPath = @IconPath WHERE Id = @Id", new { g.IconPath, match.Id });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to backfill games and icons");
            }
        }

        // Delegate CRUD methods
        public List<LauncherItem> GetAllItems() => _itemRepo.GetAll();
        public List<Category> GetAllCategories() => _categoryRepo.GetAll();
        public int InsertItem(LauncherItem item) => _itemRepo.Insert(item);
        public bool UpdateItem(LauncherItem item) => _itemRepo.Update(item);
        public bool DeleteItem(int id) => _itemRepo.Delete(id);
        public void ToggleFavorite(int id) => _itemRepo.ToggleFavorite(id);
        public int InsertCategory(Category category) => _categoryRepo.Insert(category);
        public bool UpdateCategory(Category category) => _categoryRepo.Update(category);
        public bool RenameCategory(int id, string newName) => _categoryRepo.Rename(id, newName);
        public bool DeleteCategory(int id) => _categoryRepo.Delete(id);
        public void UpdateItemPositions(IEnumerable<LauncherItem> items) => _itemRepo.UpdatePositions(items);
        public void UpdateCategoryPositions(IEnumerable<Category> categories) => _categoryRepo.UpdatePositions(categories);
        public void UpdatePositions(IEnumerable<LauncherItem> items) => _itemRepo.UpdatePositions(items);
        public int GetOrCreateCategory(string name, string defaultColor) => _categoryRepo.GetOrCreateCategory(name, defaultColor);
        public int DeleteScannedItems() => _itemRepo.DeleteScannedItems();
    }
}
