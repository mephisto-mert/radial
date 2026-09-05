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

                // Auto-resolve missing icons for all items in DB
                try
                {
                    var steamIcons = Services.GameDetector.ScanSteamShortcutIcons();
                    
                    // Also scan desktop .url and .lnk files to map icon paths
                    var desktopUrlIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var desktopUrlTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var desktopFolders = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                    };
                    foreach (var df in desktopFolders)
                    {
                        if (!Directory.Exists(df)) continue;
                        foreach (var urlFile in Directory.GetFiles(df, "*.url"))
                        {
                            try
                            {
                                string baseName = Path.GetFileNameWithoutExtension(urlFile);
                                var lines = File.ReadAllLines(urlFile);
                                string? url = null;
                                string? icon = null;
                                foreach (var l in lines)
                                {
                                    var tr = l.Trim();
                                    if (tr.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                                        url = tr.Substring("URL=".Length).Trim();
                                    else if (tr.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                                        icon = tr.Substring("IconFile=".Length).Trim();
                                }
                                if (!string.IsNullOrEmpty(icon) && File.Exists(icon))
                                {
                                    desktopUrlIcons[baseName] = icon;
                                    if (!string.IsNullOrEmpty(url))
                                        desktopUrlTargets[url] = icon;
                                }
                            }
                            catch { }
                        }
                    }

                    var allDbItems = connection.Query<LauncherItem>("SELECT * FROM Items WHERE IconPath IS NULL OR IconPath = ''");
                    foreach (var it in allDbItems)
                    {
                        string foundIcon = "";
                        if (it.Target.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
                        {
                            string appId = it.Target.Replace("steam://rungameid/", "").Trim();
                            if (steamIcons.TryGetValue(appId, out var iconFile) && File.Exists(iconFile))
                            {
                                foundIcon = iconFile;
                            }
                            else if (desktopUrlTargets.TryGetValue(it.Target, out var dtIcon) && File.Exists(dtIcon))
                            {
                                foundIcon = dtIcon;
                            }
                        }
                        
                        if (string.IsNullOrEmpty(foundIcon) && desktopUrlIcons.TryGetValue(it.Name, out var nameIcon) && File.Exists(nameIcon))
                        {
                            foundIcon = nameIcon;
                        }

                        if (string.IsNullOrEmpty(foundIcon))
                        {
                            if (it.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(it.Target))
                            {
                                foundIcon = it.Target;
                            }
                            else if (it.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(it.Target))
                            {
                                foundIcon = it.Target;
                            }
                        }

                        if (!string.IsNullOrEmpty(foundIcon))
                        {
                            connection.Execute("UPDATE Items SET IconPath = @IconPath WHERE Id = @Id", new { IconPath = foundIcon, it.Id });
                        }
                    }

                    // Auto-resolve favicon for URL items from FaviconCache
                    string faviconDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "RadialLauncher", "FaviconCache");
                    if (Directory.Exists(faviconDir))
                    {
                        var urlItems = connection.Query<LauncherItem>("SELECT * FROM Items WHERE Type = 'URL' AND (IconPath IS NULL OR IconPath = '')");
                        foreach (var u in urlItems)
                        {
                            try
                            {
                                string domain = u.Target;
                                if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                    domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                {
                                    domain = new Uri(domain).Host;
                                }
                                else
                                {
                                    try { domain = new Uri("https://" + domain).Host; } catch { }
                                }
                                string safeName = domain.Replace(".", "_").Replace(":", "_") + ".png";
                                string iconFile = Path.Combine(faviconDir, safeName);
                                if (File.Exists(iconFile))
                                {
                                    connection.Execute("UPDATE Items SET IconPath = @IconPath WHERE Id = @Id", new { IconPath = iconFile, u.Id });
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // --- CATEGORY HARMONIZATION & CONSOLIDATION ---
                try
                {
                    // 1. Rename Cat 1 to "⭐ En Çok Kullanılanlar"
                    connection.Execute("UPDATE Categories SET Name = '⭐ En Çok Kullanılanlar', Color = '#f1c40f' WHERE Id = 1;");

                    // 2. Ensure "🌐 Web & İnternet"
                    int webCatId = connection.QueryFirstOrDefault<int>("SELECT Id FROM Categories WHERE Name LIKE '%Web%' OR Name LIKE '%İnternet%' ORDER BY Id DESC");
                    if (webCatId == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES ('🌐 Web & İnternet', '#3498db', @nextPos)", new { nextPos });
                        webCatId = connection.QuerySingle<int>("SELECT last_insert_rowid()");
                    }
                    else
                    {
                        connection.Execute("UPDATE Categories SET Name = '🌐 Web & İnternet', Color = '#3498db' WHERE Id = @Id", new { Id = webCatId });
                    }

                    // Move all URL items and browser items to Web category
                    connection.Execute(@"
                        UPDATE Items 
                        SET CategoryId = @webCatId 
                        WHERE Type = 'URL' 
                           OR Target LIKE '%brave%' 
                           OR Target LIKE '%chrome%' 
                           OR Target LIKE '%edge%' 
                           OR Target LIKE '%firefox%' 
                           OR Target LIKE '%discord%' 
                           OR Target LIKE '%spotify%' 
                           OR Target LIKE '%telegram%' 
                           OR Target LIKE '%whatsapp%'", new { webCatId });

                    // 3. Ensure "🎮 Oyunlar"
                    int gamesCatId = connection.QueryFirstOrDefault<int>("SELECT Id FROM Categories WHERE Name LIKE '%Oyun%' ORDER BY (CASE WHEN Name LIKE '🎮%' THEN 0 ELSE 1 END), Id DESC");
                    if (gamesCatId == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES ('🎮 Oyunlar', '#e74c3c', @nextPos)", new { nextPos });
                        gamesCatId = connection.QuerySingle<int>("SELECT last_insert_rowid()");
                    }
                    else
                    {
                        connection.Execute("UPDATE Categories SET Name = '🎮 Oyunlar', Color = '#e74c3c' WHERE Id = @Id", new { Id = gamesCatId });
                    }
                    // Move games from duplicate categories into gamesCatId
                    connection.Execute("UPDATE Items SET CategoryId = @gamesCatId WHERE CategoryId IN (SELECT Id FROM Categories WHERE Id != @gamesCatId AND Name LIKE '%Oyun%')", new { gamesCatId });

                    // 4. Ensure "💼 Uygulamalar & İş"
                    int devCatId = connection.QueryFirstOrDefault<int>("SELECT Id FROM Categories WHERE Name LIKE '%Geliştirme%' OR (Name LIKE '%Uygulama%' AND Id > 1) ORDER BY (CASE WHEN Name LIKE '💼%' THEN 0 ELSE 1 END), Id DESC");
                    if (devCatId == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES ('💼 Uygulamalar & İş', '#2ecc71', @nextPos)", new { nextPos });
                        devCatId = connection.QuerySingle<int>("SELECT last_insert_rowid()");
                    }
                    else
                    {
                        connection.Execute("UPDATE Categories SET Name = '💼 Uygulamalar & İş', Color = '#2ecc71' WHERE Id = @Id", new { Id = devCatId });
                    }
                    connection.Execute("UPDATE Items SET CategoryId = @devCatId WHERE CategoryId IN (SELECT Id FROM Categories WHERE Id != @devCatId AND (Name LIKE '%Geliştirme%' OR (Name LIKE '%Uygulama%' AND Id > 1)))", new { devCatId });

                    // 5. Ensure "🛠️ Sistem & Araçlar"
                    int sysCatId = connection.QueryFirstOrDefault<int>("SELECT Id FROM Categories WHERE (Name LIKE '%Sistem%' OR Name LIKE '%Araç%') AND Id > 1 ORDER BY (CASE WHEN Name LIKE '🛠️%' THEN 0 ELSE 1 END), Id DESC");
                    if (sysCatId == 0)
                    {
                        int nextPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                        connection.Execute("INSERT INTO Categories (Name, Color, Position) VALUES ('🛠️ Sistem & Araçlar', '#e67e22', @nextPos)", new { nextPos });
                        sysCatId = connection.QuerySingle<int>("SELECT last_insert_rowid()");
                    }
                    else
                    {
                        connection.Execute("UPDATE Categories SET Name = '🛠️ Sistem & Araçlar', Color = '#e67e22' WHERE Id = @Id", new { Id = sysCatId });
                    }
                    connection.Execute("UPDATE Items SET CategoryId = @sysCatId WHERE Type = 'ACTION' OR CategoryId IN (SELECT Id FROM Categories WHERE Id != @sysCatId AND (Name LIKE '%Sistem%' OR Name LIKE '%Araç%'))", new { sysCatId });

                    // 6. Delete obsolete duplicate empty categories safely without array binding errors
                    connection.Execute(@"
                        DELETE FROM Categories 
                        WHERE Id > 1 
                          AND Id NOT IN (1, @webCatId, @gamesCatId, @devCatId, @sysCatId) 
                          AND Name NOT LIKE '%Açık Pencereler%'
                          AND (SELECT COUNT(*) FROM Items WHERE CategoryId = Categories.Id) = 0", 
                        new { webCatId, gamesCatId, devCatId, sysCatId });

                    // 7. Re-order canonical positions
                    connection.Execute("UPDATE Categories SET Position = 0 WHERE Id = 1;");
                    connection.Execute("UPDATE Categories SET Position = 1 WHERE Id = @webCatId;", new { webCatId });
                    connection.Execute("UPDATE Categories SET Position = 2 WHERE Id = @gamesCatId;", new { gamesCatId });
                    connection.Execute("UPDATE Categories SET Position = 3 WHERE Id = @devCatId;", new { devCatId });
                    connection.Execute("UPDATE Categories SET Position = 4 WHERE Id = @sysCatId;", new { sysCatId });
                    connection.Execute("UPDATE Categories SET Position = 5 WHERE Name LIKE '%Açık Pencereler%';");

                    // 8. Ensure only user's configured items, websites, and favorites appear in ⭐ En Çok Kullanılanlar
                    connection.Execute(@"
                        UPDATE Items SET IsUserAdded = 0;

                        UPDATE Items 
                        SET IsUserAdded = 1 
                        WHERE Id IN (8, 48, 9, 49, 10, 11, 51, 13, 14, 20, 12, 22, 54, 18, 55, 15, 36, 16, 23, 24, 58, 69, 70);

                        -- Page 1: Exact 15 items in user's requested order
                        UPDATE Items SET Position = 0 WHERE Id = 8;   -- youtube
                        UPDATE Items SET Position = 1 WHERE Id = 48;  -- Ses Aç
                        UPDATE Items SET Position = 2 WHERE Id = 9;   -- ChatGpt
                        UPDATE Items SET Position = 3 WHERE Id = 49;  -- Ses Kıs
                        UPDATE Items SET Position = 4 WHERE Id = 10;  -- Github
                        UPDATE Items SET Position = 5 WHERE Id = 11;  -- Gmail
                        UPDATE Items SET Position = 6 WHERE Id = 51;  -- Oynat/Durdur
                        UPDATE Items SET Position = 7 WHERE Id = 13;  -- Mephisto Mail
                        UPDATE Items SET Position = 8 WHERE Id = 14;  -- Mephisto Shares
                        UPDATE Items SET Position = 9 WHERE Id = 20;  -- Zen
                        UPDATE Items SET Position = 10 WHERE Id = 12; -- Analytics
                        UPDATE Items SET Position = 11 WHERE Id = 22; -- Google
                        UPDATE Items SET Position = 12 WHERE Id = 54; -- Masaüstü
                        UPDATE Items SET Position = 13 WHERE Id = 18; -- Brave
                        UPDATE Items SET Position = 14 WHERE Id = 55; -- Ekran Alıntısı

                        -- Page 2: Remaining user-added / desktop items
                        UPDATE Items SET Position = 15 WHERE Id = 15; -- Rave
                        UPDATE Items SET Position = 16 WHERE Id = 36; -- Counter-Strike 2
                        UPDATE Items SET Position = 17 WHERE Id = 16; -- Antigravity
                        UPDATE Items SET Position = 18 WHERE Id = 23; -- Steam
                        UPDATE Items SET Position = 19 WHERE Id = 24; -- Epic Games
                        UPDATE Items SET Position = 20 WHERE Id = 58; -- Discord
                        UPDATE Items SET Position = 21 WHERE Id = 69; -- Spotify
                        UPDATE Items SET Position = 22 WHERE Id = 70; -- Blitz
                    ");
                }
                catch { }

                // --- GUARANTEE ALL GAMES AND DESKTOP APPS ARE DISCOVERED & HAVE CRISP ICONS ---
                try
                {
                    int gamesCatId = connection.QueryFirstOrDefault<int>("SELECT Id FROM Categories WHERE Name LIKE '%Oyun%' ORDER BY Id DESC");
                    int maxPos = connection.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) FROM Items");

                    // 1. Detect all Steam & Epic games (from libraryfolders + uninstall registry)
                    var detectedGames = Services.GameDetector.DetectAllGames();
                    foreach (var g in detectedGames)
                    {
                        int exists = connection.QuerySingle<int>(
                            "SELECT COUNT(*) FROM Items WHERE Target = @ExePath OR (Name = @Name AND CategoryId = @gamesCatId)",
                            new { g.ExePath, g.Name, gamesCatId });

                        if (exists == 0)
                        {
                            maxPos++;
                            connection.Execute(@"
                                INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, ParentId, IsUserAdded)
                                VALUES (@Name, 'EXE', @ExePath, '', '', @IconPath, @gamesCatId, @maxPos, 0, 0, 0)",
                                new { g.Name, g.ExePath, g.IconPath, gamesCatId, maxPos });
                        }
                    }

                    // 2. Scan Desktop .url files for any missing games / web links
                    var desktopFolders = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                    };
                    foreach (var df in desktopFolders)
                    {
                        if (!Directory.Exists(df)) continue;
                        foreach (var urlFile in Directory.GetFiles(df, "*.url"))
                        {
                            try
                            {
                                string baseName = Path.GetFileNameWithoutExtension(urlFile);
                                var lines = File.ReadAllLines(urlFile);
                                string? url = null;
                                string? icon = null;
                                foreach (var l in lines)
                                {
                                    var tr = l.Trim();
                                    if (tr.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                                        url = tr.Substring("URL=".Length).Trim();
                                    else if (tr.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                                        icon = tr.Substring("IconFile=".Length).Trim();
                                }

                                if (!string.IsNullOrEmpty(url))
                                {
                                    int exists = connection.QuerySingle<int>(
                                        "SELECT COUNT(*) FROM Items WHERE Target = @url OR Name = @baseName",
                                        new { url, baseName });

                                    if (exists == 0)
                                    {
                                        maxPos++;
                                        int targetCat = url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase) ? gamesCatId : 1;
                                        connection.Execute(@"
                                            INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, ParentId, IsUserAdded)
                                            VALUES (@baseName, 'EXE', @url, '', '', @icon, @targetCat, @maxPos, 0, 0, 0)",
                                            new { baseName, url, icon = icon ?? "", targetCat, maxPos });
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    // 3. Backfill missing / invalid IconPath for ALL items in DB
                    var steamIcons = Services.GameDetector.ScanSteamShortcutIcons();
                    var allDbItems = connection.Query<LauncherItem>("SELECT * FROM Items").ToList();
                    foreach (var it in allDbItems)
                    {
                        if (string.IsNullOrEmpty(it.IconPath) || !File.Exists(it.IconPath))
                        {
                            string resolvedIcon = "";
                            if (it.Target.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
                            {
                                string appId = it.Target.Substring("steam://rungameid/".Length).Trim();
                                if (steamIcons.TryGetValue(appId, out var sIcon) && File.Exists(sIcon))
                                {
                                    resolvedIcon = sIcon;
                                }
                            }

                            if (string.IsNullOrEmpty(resolvedIcon) && it.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(it.Target))
                            {
                                try
                                {
                                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                                    if (shellType != null)
                                    {
                                        dynamic shell = Activator.CreateInstance(shellType)!;
                                        dynamic shortcut = shell.CreateShortcut(it.Target);
                                        string iconLoc = shortcut.IconLocation;
                                        string targetPath = shortcut.TargetPath;
                                        if (!string.IsNullOrEmpty(iconLoc))
                                        {
                                            string cl = iconLoc.Split(',')[0].Trim().Trim('"');
                                            if (File.Exists(cl)) resolvedIcon = cl;
                                        }
                                        if (string.IsNullOrEmpty(resolvedIcon) && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                                        {
                                            resolvedIcon = targetPath;
                                        }
                                    }
                                }
                                catch { }
                            }

                            if (string.IsNullOrEmpty(resolvedIcon) && it.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(it.Target))
                            {
                                resolvedIcon = it.Target;
                            }

                            if (string.IsNullOrEmpty(resolvedIcon) && it.Type == "URL")
                            {
                                string faviconDir = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    "RadialLauncher", "FaviconCache");
                                if (Directory.Exists(faviconDir))
                                {
                                    try
                                    {
                                        string domain = it.Target;
                                        if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                            domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            domain = new Uri(domain).Host;
                                        }
                                        else
                                        {
                                            try { domain = new Uri("https://" + domain).Host; } catch { }
                                        }
                                        string safeName = domain.Replace(".", "_").Replace(":", "_") + ".png";
                                        string iconFile = Path.Combine(faviconDir, safeName);
                                        if (File.Exists(iconFile)) resolvedIcon = iconFile;
                                    }
                                    catch { }
                                }
                            }

                            if (!string.IsNullOrEmpty(resolvedIcon))
                            {
                                connection.Execute("UPDATE Items SET IconPath = @resolvedIcon WHERE Id = @Id", new { resolvedIcon, it.Id });
                            }
                        }
                    }
                }
                catch { }

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
