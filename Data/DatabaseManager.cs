using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Dapper;
using RadialLauncher.Models;

namespace RadialLauncher.Data
{
    public class DatabaseManager
    {
        private readonly string _dbPath;

        public DatabaseManager()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadialLauncher");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
                
            _dbPath = Path.Combine(folder, "launcher.db");
        }

        public string GetConnectionString() => $"Data Source={_dbPath}";

        public void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                
                // Items table
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
                        IsFavorite INTEGER DEFAULT 0
                    );
                ");

                // Categories table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Categories (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Color TEXT DEFAULT '#3498db',
                        Position INTEGER DEFAULT 0
                    );
                ");

                // Add IsFavorite column if it doesn't exist (migration for existing DBs)
                try { connection.Execute("ALTER TABLE Items ADD COLUMN IsFavorite INTEGER DEFAULT 0;"); }
                catch { /* Column already exists */ }

                // Add ParentId column if it doesn't exist (migration for sub-menus)
                try { connection.Execute("ALTER TABLE Items ADD COLUMN ParentId INTEGER DEFAULT 0;"); }
                catch { /* Column already exists */ }

                // Add IsUserAdded column if it doesn't exist (0 = scanned, 1 = user added)
                try { connection.Execute("ALTER TABLE Items ADD COLUMN IsUserAdded INTEGER DEFAULT 1;"); }
                catch { /* Column already exists */ }

                // Check and add "🪟 Açık Pencereler" and "⚡ Sistem" categories
                try
                {
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

                        // Seed default system actions
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
                        connection.Execute("INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, ParentId) VALUES (@Name, @Type, @Target, @Arguments, @WorkingDirectory, @IconPath, @CategoryId, @Position, @IsFavorite, @ParentId)", sysActions);
                    }
                }
                catch { }

                // Auto-resolve missing icons for Steam games in DB
                try
                {
                    var steamIcons = Services.GameDetector.ScanSteamShortcutIcons();
                    var steamItems = connection.Query<LauncherItem>("SELECT * FROM Items WHERE Target LIKE 'steam://rungameid/%' AND (IconPath IS NULL OR IconPath = '')");
                    foreach (var sItem in steamItems)
                    {
                        string appId = sItem.Target.Replace("steam://rungameid/", "").Trim();
                        if (steamIcons.TryGetValue(appId, out var iconFile) && File.Exists(iconFile))
                        {
                            connection.Execute("UPDATE Items SET IconPath = @IconPath WHERE Id = @Id", new { IconPath = iconFile, sItem.Id });
                        }
                    }
                }
                catch { }

                // Seed default categories if empty
                int catCount = connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories");
                if (catCount == 0)
                {
                    var defaultCategories = new[]
                    {
                        new { Name = "Hepsi", Color = "#95a5a6", Position = 0 },
                        new { Name = "Uygulamalar", Color = "#3498db", Position = 1 },
                        new { Name = "Web Siteleri", Color = "#2ecc71", Position = 2 },
                        new { Name = "Oyunlar", Color = "#e74c3c", Position = 3 },
                        new { Name = "Araçlar", Color = "#f39c12", Position = 4 }
                    };
                    connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES (@Name, @Color, @Position)", defaultCategories);
                }

                // Ensure smart categories exist
                var defaultSmartCats = new[]
                {
                    new { Name = "🎮 Oyunlar", Color = "#e74c3c" },
                    new { Name = "🌐 İnternet & İletişim", Color = "#3498db" },
                    new { Name = "💼 Geliştirme & İş", Color = "#2ecc71" },
                    new { Name = "🛠️ Sistem & Araçlar", Color = "#e67e22" }
                };
                foreach (var sc in defaultSmartCats)
                {
                    string searchKey = sc.Name.Substring(2).Trim();
                    int exists = connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories WHERE Name LIKE @Search", new { Search = $"%{searchKey}%" });
                    if (exists == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES (@Name, @Color, @Position)",
                            new { sc.Name, sc.Color, Position = nextPos });
                    }
                }

                // Seed default items if empty
                int count = connection.QuerySingle<int>("SELECT COUNT(*) FROM Items");
                if (count == 0)
                {
                    var defaultItems = new[]
                    {
                        new { Name = "Notepad", Type = "EXE", Target = "notepad.exe", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = 1, Position = 0, IsFavorite = 0, IsUserAdded = 1 },
                        new { Name = "Calculator", Type = "EXE", Target = "calc.exe", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = 1, Position = 1, IsFavorite = 0, IsUserAdded = 1 },
                        new { Name = "Explorer", Type = "EXE", Target = "explorer.exe", Arguments = "", WorkingDirectory = "", IconPath = "", CategoryId = 1, Position = 2, IsFavorite = 0, IsUserAdded = 1 }
                    };
                    connection.Execute("INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, IsUserAdded) VALUES (@Name, @Type, @Target, @Arguments, @WorkingDirectory, @IconPath, @CategoryId, @Position, @IsFavorite, @IsUserAdded)", defaultItems);
                }
            }
        }

        // ---- Items ----

        public List<LauncherItem> GetAllItems()
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                return connection.Query<LauncherItem>("SELECT * FROM Items ORDER BY Position").ToList();
            }
        }

        public List<LauncherItem> GetItemsByCategory(int categoryId)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                if (categoryId <= 1) // "Hepsi" category or 0: ONLY user-added items!
                    return connection.Query<LauncherItem>("SELECT * FROM Items WHERE (CategoryId <= 1 OR IsUserAdded = 1) AND ParentId = 0 ORDER BY Position").ToList();
                return connection.Query<LauncherItem>("SELECT * FROM Items WHERE CategoryId = @CategoryId AND ParentId = 0 ORDER BY Position", new { CategoryId = categoryId }).ToList();
            }
        }

        public List<LauncherItem> GetFavoriteItems()
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                return connection.Query<LauncherItem>("SELECT * FROM Items WHERE IsFavorite = 1 ORDER BY Position").ToList();
            }
        }

        public List<LauncherItem> GetItemsByParent(int parentId)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                return connection.Query<LauncherItem>("SELECT * FROM Items WHERE ParentId = @ParentId ORDER BY Position", new { ParentId = parentId }).ToList();
            }
        }

        public void InsertItem(LauncherItem item)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                string query = "INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, ParentId, IsUserAdded) VALUES (@Name, @Type, @Target, @Arguments, @WorkingDirectory, @IconPath, @CategoryId, @Position, @IsFavorite, @ParentId, @IsUserAdded)";
                connection.Execute(query, item);
            }
        }

        public void UpdateItem(LauncherItem item)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Execute("UPDATE Items SET Name=@Name, Type=@Type, Target=@Target, Arguments=@Arguments, WorkingDirectory=@WorkingDirectory, IconPath=@IconPath, CategoryId=@CategoryId, Position=@Position, IsFavorite=@IsFavorite, ParentId=@ParentId, IsUserAdded=@IsUserAdded WHERE Id=@Id", item);
            }
        }

        public void ToggleFavorite(int id)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Execute("UPDATE Items SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END WHERE Id = @Id", new { Id = id });
            }
        }

        public void DeleteItem(int id)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Execute("DELETE FROM Items WHERE Id = @Id", new { Id = id });
            }
        }

        public void UpdatePositions(List<LauncherItem> items)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var item in items)
                    {
                        connection.Execute("UPDATE Items SET Position = @Position WHERE Id = @Id", new { item.Position, item.Id }, transaction);
                    }
                    transaction.Commit();
                }
            }
        }

        // ---- Categories ----

        public List<Category> GetAllCategories()
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                return connection.Query<Category>("SELECT * FROM Categories ORDER BY Position").ToList();
            }
        }

        public void InsertCategory(Category cat)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES (@Name, @Color, @Position)", cat);
            }
        }

        public void DeleteCategory(int id)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                // Move items in this category to "Hepsi" (id=1)
                connection.Execute("UPDATE Items SET CategoryId = 1 WHERE CategoryId = @Id", new { Id = id });
                connection.Execute("DELETE FROM Categories WHERE Id = @Id", new { Id = id });
            }
        }

        public void UpdateCategory(Category cat)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Execute("UPDATE Categories SET Name = @Name, Color = @Color, Position = @Position WHERE Id = @Id", cat);
            }
        }

        public int GetOrCreateCategory(string name, string defaultColor)
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                var existing = connection.QueryFirstOrDefault<Category>("SELECT * FROM Categories WHERE Name LIKE @Name", new { Name = $"%{name}%" });
                if (existing != null) return existing.Id;

                int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES (@Name, @Color, @Position)",
                    new { Name = name, Color = defaultColor, Position = nextPos });
                return connection.QuerySingle<int>("SELECT last_insert_rowid()");
            }
        }
    }
}
