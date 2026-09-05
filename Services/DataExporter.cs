using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RadialLauncher.Data;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Data
{
    public class DataExporter : IDataExporter
    {
        private readonly IDatabaseManager _db;

        public DataExporter(IDatabaseManager db)
        {
            _db = db;
        }

        public void Export(string path)
        {
            var items = _db.GetAllItems();
            string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public void Import(string path)
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
    }
}
