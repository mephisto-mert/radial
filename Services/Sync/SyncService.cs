using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using Serilog;

namespace RadialLauncher.Services.Sync
{
    public class SyncService : ISyncService
    {
        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;

        public SyncService(IItemRepository itemRepo, ICategoryRepository categoryRepo)
        {
            _itemRepo = itemRepo;
            _categoryRepo = categoryRepo;
        }

        public class SyncPayload
        {
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
            public System.Collections.Generic.List<Category> Categories { get; set; } = new();
            public System.Collections.Generic.List<LauncherItem> Items { get; set; } = new();
        }

        public async Task<bool> ExportToFileAsync(string filePath)
        {
            try
            {
                var payload = new SyncPayload
                {
                    Categories = _categoryRepo.GetAll(),
                    Items = _itemRepo.GetAll()
                };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                Log.Information("Exported launcher data to {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export launcher data");
                return false;
            }
        }

        public async Task<bool> ImportFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                string json = await File.ReadAllTextAsync(filePath);
                var payload = JsonSerializer.Deserialize<SyncPayload>(json);
                if (payload != null)
                {
                    foreach (var cat in payload.Categories)
                    {
                        var existing = _categoryRepo.GetById(cat.Id);
                        if (existing == null) _categoryRepo.Insert(cat);
                        else _categoryRepo.Update(cat);
                    }
                    foreach (var item in payload.Items)
                    {
                        var existing = _itemRepo.GetById(item.Id);
                        if (existing == null) _itemRepo.Insert(item);
                        else _itemRepo.Update(item);
                    }
                    Log.Information("Successfully imported launcher data from {Path}", filePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to import launcher data");
            }
            return false;
        }

        public Task<bool> SyncToLocalNetworkFolderAsync(string sharedFolderPath)
        {
            string targetFile = Path.Combine(sharedFolderPath, "radial_sync.json");
            return ExportToFileAsync(targetFile);
        }

        public Task<bool> SyncFromLocalNetworkFolderAsync(string sharedFolderPath)
        {
            string targetFile = Path.Combine(sharedFolderPath, "radial_sync.json");
            return ImportFromFileAsync(targetFile);
        }
    }
}
