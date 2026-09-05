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

namespace RadialLauncher.Data
{
    public class DatabaseManager : IDatabaseManager
    {
        private readonly string _dbPath;
        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IGameDetector _gameDetector;

        public DatabaseManager() : this((IGameDetector?)null)
        {
        }

        public DatabaseManager(IGameDetector? gameDetector)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadialLauncher");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
                
            _dbPath = Path.Combine(folder, "launcher.db");
            _itemRepo = new ItemRepository(this);
            _categoryRepo = new CategoryRepository(this);
            _gameDetector = gameDetector ?? new GameDetector();
        }

        public DatabaseManager(string dbPath, IGameDetector? gameDetector = null)
        {
            _dbPath = dbPath;
            _itemRepo = new ItemRepository(this);
            _categoryRepo = new CategoryRepository(this);
            _gameDetector = gameDetector ?? new GameDetector();
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
                            Position INTEGER DEFAULT 0
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

                    int winCat = connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories WHERE Name LIKE '%Açık Pencereler%'");
                    if (winCat == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES ('🪟 Açık Pencereler', '#9b59b6', @nextPos)", new { nextPos });
                    }

                    int sysCat = connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories WHERE Name LIKE '%Sistem%'");
                    if (sysCat == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES ('⚡ Sistem', '#f1c40f', @nextPos)", new { nextPos });
                        int newSysCatId = connection.QuerySingle<int>("SELECT Id FROM Categories WHERE Name LIKE '%Sistem%'");

                        var sysActions = new[]
                        {
                            new { Name = "Ses Aç", Type = "ACTION", Target = "VOLUME_UP", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 0, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Ses Kıs", Type = "ACTION", Target = "VOLUME_DOWN", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 1, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Sesi Kapat", Type = "ACTION", Target = "VOLUME_MUTE", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 2, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Oynat/Durdur", Type = "ACTION", Target = "MEDIA_PLAY_PAUSE", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 3, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Sonraki", Type = "ACTION", Target = "MEDIA_NEXT", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 4, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Önceki", Type = "ACTION", Target = "MEDIA_PREV", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 5, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Masaüstü", Type = "ACTION", Target = "SHOW_DESKTOP", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 6, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Ekran Alıntısı", Type = "ACTION", Target = "SNIP_TOOL", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 7, IsFavorite = 1, ParentId = 0 },
                            new { Name = "Görev Yöneticisi", Type = "ACTION", Target = "TASK_MANAGER", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 8, IsFavorite = 0, ParentId = 0 },
                            new { Name = "Kilitle", Type = "ACTION", Target = "LOCK_PC", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = newSysCatId, Position = 9, IsFavorite = 0, ParentId = 0 },
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
                            new { Id = "SETTINGS", Name = "Ayarlar", IconSymbol = "⚙️", ActionKey = "SETTINGS", Order = 0 },
                            new { Id = "SEARCH", Name = "Arama", IconSymbol = "🔍", ActionKey = "SEARCH", Order = 1 },
                            new { Id = "DESKTOP", Name = "Masaüstü", IconSymbol = "🖥️", ActionKey = "SHOW_DESKTOP", Order = 2 },
                            new { Id = "SNIP", Name = "Ekran Alıntısı", IconSymbol = "✂️", ActionKey = "SNIP_TOOL", Order = 3 },
                            new { Id = "MUTE", Name = "Sesi Kapat", IconSymbol = "🔇", ActionKey = "VOLUME_MUTE", Order = 4 }
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
                var gamesCat = connection.QueryFirstOrDefault<Category>("SELECT * FROM Categories WHERE Name LIKE '%Oyun%'");
                if (gamesCat != null)
                {
                    gamesCatId = gamesCat.Id;
                }
                else
                {
                    int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                    connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES ('🎮 Oyunlar', '#e67e22', @nextPos)", new { nextPos });
                    gamesCatId = connection.QuerySingle<int>("SELECT Id FROM Categories WHERE Name LIKE '%Oyun%'");
                }

                var detectedGames = _gameDetector.DetectAll();
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
        public bool DeleteCategory(int id) => _categoryRepo.Delete(id);
        public void UpdateItemPositions(IEnumerable<LauncherItem> items) => _itemRepo.UpdatePositions(items);
        public void UpdateCategoryPositions(IEnumerable<Category> categories) => _categoryRepo.UpdatePositions(categories);
        public void UpdatePositions(IEnumerable<LauncherItem> items) => _itemRepo.UpdatePositions(items);
        public int GetOrCreateCategory(string name, string defaultColor) => _categoryRepo.GetOrCreateCategory(name, defaultColor);
        public int DeleteScannedItems() => _itemRepo.DeleteScannedItems();
    }
}
