using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using RadialLauncher.Models;
using Serilog;

namespace RadialLauncher.Data.Repositories
{
    public class ItemRepository : IItemRepository
    {
        private readonly IDatabaseManager _dbManager;

        public ItemRepository(IDatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        private IDbConnection CreateConnection() => new SqliteConnection(_dbManager.GetConnectionString());

        public List<LauncherItem> GetAll()
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.Query<LauncherItem>("SELECT * FROM Items ORDER BY Position ASC, Id ASC").ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get all items from repository");
                return new List<LauncherItem>();
            }
        }

        public async Task<List<LauncherItem>> GetAllAsync()
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                var items = await conn.QueryAsync<LauncherItem>("SELECT * FROM Items ORDER BY Position ASC, Id ASC");
                return items.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get all items async from repository");
                return new List<LauncherItem>();
            }
        }

        public LauncherItem? GetById(int id)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.QueryFirstOrDefault<LauncherItem>("SELECT * FROM Items WHERE Id = @id", new { id });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get item by ID {Id}", id);
                return null;
            }
        }

        public List<LauncherItem> GetByCategoryId(int categoryId)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.Query<LauncherItem>(
                    "SELECT * FROM Items WHERE CategoryId = @categoryId AND ParentId = 0 ORDER BY Position ASC",
                    new { categoryId }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get items for CategoryId {CategoryId}", categoryId);
                return new List<LauncherItem>();
            }
        }

        public List<LauncherItem> GetMostUsed(int limit = 15)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.Query<LauncherItem>(
                    "SELECT * FROM Items WHERE ParentId = 0 ORDER BY IsFavorite DESC, LaunchCount DESC, Position ASC LIMIT @limit",
                    new { limit }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get most used items with limit {Limit}", limit);
                return new List<LauncherItem>();
            }
        }

        public int Insert(LauncherItem item)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                string sql = @"
                    INSERT INTO Items (Name, Type, Target, Arguments, WorkingDirectory, IconPath, CategoryId, Position, IsFavorite, ParentId, IsUserAdded, LaunchCount, LastLaunched, UseCount, LastUsedAt, Tags)
                    VALUES (@Name, @Type, @Target, @Arguments, @WorkingDirectory, @IconPath, @CategoryId, @Position, @IsFavorite, @ParentId, @IsUserAdded, @LaunchCount, @LastLaunched, @UseCount, @LastUsedAt, @Tags);
                    SELECT last_insert_rowid();";
                int id = conn.ExecuteScalar<int>(sql, item);
                item.Id = id;
                return id;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to insert item {Name}", item.Name);
                return 0;
            }
        }

        public bool Update(LauncherItem item)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                string sql = @"
                    UPDATE Items SET 
                        Name = @Name,
                        Type = @Type,
                        Target = @Target,
                        Arguments = @Arguments,
                        WorkingDirectory = @WorkingDirectory,
                        IconPath = @IconPath,
                        CategoryId = @CategoryId,
                        Position = @Position,
                        IsFavorite = @IsFavorite,
                        ParentId = @ParentId,
                        IsUserAdded = @IsUserAdded,
                        LaunchCount = @LaunchCount,
                        LastLaunched = @LastLaunched,
                        UseCount = @UseCount,
                        LastUsedAt = @LastUsedAt,
                        Tags = @Tags
                    WHERE Id = @Id;";
                return conn.Execute(sql, item) > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update item ID {Id} ({Name})", item.Id, item.Name);
                return false;
            }
        }

        public bool Delete(int id)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.Execute("DELETE FROM Items WHERE Id = @id OR ParentId = @id", new { id }) > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete item ID {Id}", id);
                return false;
            }
        }

        public void ToggleFavorite(int id)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                conn.Execute("UPDATE Items SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END WHERE Id = @id", new { id });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to toggle favorite for item ID {Id}", id);
            }
        }

        public void IncrementLaunchCount(int id)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                conn.Execute(@"
                    UPDATE Items 
                    SET LaunchCount = LaunchCount + 1, 
                        UseCount = UseCount + 1,
                        LastLaunched = @now,
                        LastUsedAt = @now 
                    WHERE Id = @id", 
                    new { id, now = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to increment launch count for item ID {Id}", id);
            }
        }

        public void UpdatePositions(IEnumerable<LauncherItem> items)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();
                foreach (var item in items)
                {
                    conn.Execute("UPDATE Items SET Position = @Position WHERE Id = @Id", new { item.Position, item.Id }, trans);
                }
                trans.Commit();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update item positions");
            }
        }

        public int DeleteScannedItems()
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.Execute("DELETE FROM Items WHERE IsUserAdded = 0");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete scanned items");
                return 0;
            }
        }
    }
}
