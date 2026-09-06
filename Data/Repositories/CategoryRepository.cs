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
    public class CategoryRepository : ICategoryRepository
    {
        private readonly IDatabaseManager _dbManager;

        public CategoryRepository(IDatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        private IDbConnection CreateConnection() => new SqliteConnection(_dbManager.GetConnectionString());

        public List<Category> GetAll()
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.Query<Category>("SELECT * FROM Categories ORDER BY Position ASC, Id ASC").ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get all categories");
                return new List<Category>();
            }
        }

        public async Task<List<Category>> GetAllAsync()
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                var list = await conn.QueryAsync<Category>("SELECT * FROM Categories ORDER BY Position ASC, Id ASC");
                return list.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get all categories async");
                return new List<Category>();
            }
        }

        public Category? GetById(int id)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.QueryFirstOrDefault<Category>("SELECT * FROM Categories WHERE Id = @id", new { id });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get category by ID {Id}", id);
                return null;
            }
        }

        public int Insert(Category category)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                string sql = @"
                    INSERT INTO Categories (Name, Color, Position, SystemKey) 
                    VALUES (@Name, @Color, @Position, @SystemKey);
                    SELECT last_insert_rowid();";
                int id = conn.ExecuteScalar<int>(sql, category);
                category.Id = id;
                return id;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to insert category {Name}", category.Name);
                return 0;
            }
        }

        public bool Update(Category category)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                return conn.Execute("UPDATE Categories SET Name = @Name, Color = @Color, Position = @Position, SystemKey = @SystemKey WHERE Id = @Id", category) > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update category ID {Id}", category.Id);
                return false;
            }
        }

        public bool Rename(int id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;
            string trimmed = newName.Trim();
            if (trimmed.Length > 50) trimmed = trimmed.Substring(0, 50);

            try
            {
                using var conn = CreateConnection();
                conn.Open();

                // Check for duplicate category name (case-insensitive) excluding this category
                int duplicate = conn.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM Categories WHERE LOWER(TRIM(Name)) = LOWER(@trimmed) AND Id != @id",
                    new { trimmed, id });

                if (duplicate > 0)
                {
                    Log.Warning("Category rename rejected: Category '{Name}' already exists", trimmed);
                    return false;
                }

                return conn.Execute("UPDATE Categories SET Name = @newName WHERE Id = @id", new { id, newName = trimmed }) > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to rename category ID {Id} to {Name}", id, newName);
                return false;
            }
        }

        public bool Delete(int id)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();
                conn.Execute("DELETE FROM Items WHERE CategoryId = @id", new { id }, trans);
                int affected = conn.Execute("DELETE FROM Categories WHERE Id = @id", new { id }, trans);
                trans.Commit();
                return affected > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete category ID {Id}", id);
                return false;
            }
        }

        public void UpdatePositions(IEnumerable<Category> categories)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();
                foreach (var cat in categories)
                {
                    conn.Execute("UPDATE Categories SET Position = @Position WHERE Id = @Id", new { cat.Position, cat.Id }, trans);
                }
                trans.Commit();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update category positions");
            }
        }

        public int GetOrCreateCategory(string name, string defaultColor)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                var existing = conn.QueryFirstOrDefault<Category>("SELECT * FROM Categories WHERE LOWER(TRIM(Name)) = LOWER(TRIM(@name))", new { name });
                if (existing != null) return existing.Id;

                int nextPos = conn.QuerySingle<int>("SELECT IFNULL(MAX(Position), 0) + 1 FROM Categories");
                return conn.ExecuteScalar<int>(
                    "INSERT INTO Categories (Name, Color, Position) VALUES (@name, @defaultColor, @nextPos); SELECT last_insert_rowid();",
                    new { name, defaultColor, nextPos });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get or create category {Name}", name);
                return 0;
            }
        }
    }
}
