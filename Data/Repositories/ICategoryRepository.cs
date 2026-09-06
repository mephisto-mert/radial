using System.Collections.Generic;
using System.Threading.Tasks;
using RadialLauncher.Models;

namespace RadialLauncher.Data.Repositories
{
    public interface ICategoryRepository
    {
        List<Category> GetAll();
        Task<List<Category>> GetAllAsync();
        Category? GetById(int id);
        int Insert(Category category);
        bool Update(Category category);
        bool Rename(int id, string newName);
        bool Delete(int id);
        void UpdatePositions(IEnumerable<Category> categories);
        int GetOrCreateCategory(string name, string defaultColor);
    }
}
