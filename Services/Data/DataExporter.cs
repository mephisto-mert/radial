using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RadialLauncher.Data;
using RadialLauncher.Models;
using Serilog;

namespace RadialLauncher.Services.Data
{
    public class DataExporter : IDataExporter
    {
        private readonly IDatabaseManager _db;

        public DataExporter(IDatabaseManager db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public void Export(string path)
        {
            try
            {
                var items = _db.GetAllItems();
                string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export data to {Path}", path);
                throw;
            }
        }

        public void Import(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<LauncherItem>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        _db.InsertItem(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to import data from {Path}", path);
                throw;
            }
        }
    }
}
