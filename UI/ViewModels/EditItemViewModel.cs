using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;

namespace RadialLauncher.UI.ViewModels
{
    public partial class EditItemViewModel : ObservableObject
    {
        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly LauncherItem _originalItem;

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

        public EditItemViewModel(LauncherItem item, IItemRepository itemRepo, ICategoryRepository categoryRepo)
        {
            _originalItem = item;
            _itemRepo = itemRepo;
            _categoryRepo = categoryRepo;

            Name = item.Name;
            Type = item.Type;
            Target = item.Target;
            Arguments = item.Arguments;
            WorkingDirectory = item.WorkingDirectory;
            IconPath = item.IconPath;

            Categories = new ObservableCollection<Category>(_categoryRepo.GetAll());
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == item.CategoryId) ?? Categories.FirstOrDefault();
        }

        [RelayCommand]
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Target))
                return;

            _originalItem.Name = Name.Trim();
            _originalItem.Type = Type;
            _originalItem.Target = Target.Trim();
            _originalItem.Arguments = Arguments.Trim();
            _originalItem.WorkingDirectory = WorkingDirectory.Trim();
            _originalItem.IconPath = IconPath.Trim();
            if (SelectedCategory != null)
            {
                _originalItem.CategoryId = SelectedCategory.Id;
            }

            _itemRepo.Update(_originalItem);
            RequestClose?.Invoke();
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke();
        }
    }
}
