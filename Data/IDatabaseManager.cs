using System.Collections.Generic;
using RadialLauncher.Models;

namespace RadialLauncher.Data
{
    public interface IDatabaseManager
    {
        string GetConnectionString();
        void InitializeDatabase();
        
        List<LauncherItem> GetAllItems();
        List<Category> GetAllCategories();
        int InsertItem(LauncherItem item);
        bool UpdateItem(LauncherItem item);
        bool DeleteItem(int id);
        void ToggleFavorite(int id);
        int InsertCategory(Category category);
        bool UpdateCategory(Category category);
        bool DeleteCategory(int id);
        void UpdateItemPositions(IEnumerable<LauncherItem> items);
        void UpdateCategoryPositions(IEnumerable<Category> categories);
        void UpdatePositions(IEnumerable<LauncherItem> items);
        int GetOrCreateCategory(string name, string defaultColor);
        int DeleteScannedItems();
    }
}
