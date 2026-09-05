using System.Collections.Generic;
using System.Threading.Tasks;
using RadialLauncher.Models;

namespace RadialLauncher.Data.Repositories
{
    public interface IItemRepository
    {
        List<LauncherItem> GetAll();
        Task<List<LauncherItem>> GetAllAsync();
        LauncherItem? GetById(int id);
        List<LauncherItem> GetByCategoryId(int categoryId);
        List<LauncherItem> GetMostUsed(int limit = 15);
        int Insert(LauncherItem item);
        bool Update(LauncherItem item);
        bool Delete(int id);
        void ToggleFavorite(int id);
        void IncrementLaunchCount(int id);
        void UpdatePositions(IEnumerable<LauncherItem> items);
        int DeleteScannedItems();
    }
}
