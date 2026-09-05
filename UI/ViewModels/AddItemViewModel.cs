using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;

namespace RadialLauncher.UI.ViewModels
{
    public partial class AddItemViewModel : ObservableObject
    {
        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _type = "EXE";

        [ObservableProperty]
        private string _target = string.Empty;

        [ObservableProperty]
        private string _arguments = string.Empty;

        [ObservableProperty]
        private string _workingDirectory = string.Empty;

        [ObservableProperty]
        private string _iconPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = new();

        [ObservableProperty]
        private Category? _selectedCategory;

        public event System.Action? RequestClose;

        public AddItemViewModel(IItemRepository itemRepo, ICategoryRepository categoryRepo)
        {
            _itemRepo = itemRepo;
            _categoryRepo = categoryRepo;

            Categories = new ObservableCollection<Category>(_categoryRepo.GetAll());
            SelectedCategory = Categories.FirstOrDefault();
        }

        [RelayCommand]
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Target))
                return;

            int catId = SelectedCategory?.Id ?? 0;
            int maxPos = _itemRepo.GetByCategoryId(catId).Select(i => i.Position).DefaultIfEmpty(0).Max();

            var item = new LauncherItem
            {
                Name = Name.Trim(),
                Type = Type,
                Target = Target.Trim(),
                Arguments = Arguments.Trim(),
                WorkingDirectory = WorkingDirectory.Trim(),
                IconPath = IconPath.Trim(),
                CategoryId = catId,
                Position = maxPos + 1,
                IsFavorite = false,
                IsUserAdded = true
            };

            _itemRepo.Insert(item);
            RequestClose?.Invoke();
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke();
        }
    }
}
