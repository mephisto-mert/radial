using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialLauncher.Data;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Scanning;
using RadialLauncher.Services.Sync;
using RadialLauncher.Services.Themes;
using Serilog;

namespace RadialLauncher.UI.ViewModels
{
    public partial class ManagementViewModel : ObservableObject
    {
        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IThemeService _themeService;
        private readonly IPCScannerService _scannerService;
        private readonly ISyncService _syncService;
        private readonly IDatabaseManager _db;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = new();

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private ObservableCollection<LauncherItem> _items = new();

        [ObservableProperty]
        private LauncherItem? _selectedItem;

        [ObservableProperty]
        private string _filterQuery = string.Empty;

        // Theme management
        [ObservableProperty]
        private ObservableCollection<Theme> _themes = new();

        [ObservableProperty]
        private Theme? _selectedTheme;

        [ObservableProperty]
        private double _radialOpacity = 0.90;

        [ObservableProperty]
        private string _activationShortcut = "MiddleClick";

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ManagementViewModel(
            IItemRepository itemRepo,
            ICategoryRepository categoryRepo,
            IThemeService themeService,
            IPCScannerService scannerService,
            ISyncService syncService,
            IDatabaseManager db)
        {
            _itemRepo = itemRepo;
            _categoryRepo = categoryRepo;
            _themeService = themeService;
            _scannerService = scannerService;
            _syncService = syncService;
            _db = db;

            LoadInitialData();
        }

        public void LoadInitialData()
        {
            Categories = new ObservableCollection<Category>(_categoryRepo.GetAll());
            SelectedCategory = Categories.FirstOrDefault();

            Themes = new ObservableCollection<Theme>(_themeService.GetAllThemes());
            SelectedTheme = _themeService.GetCurrentTheme();

            RadialOpacity = _themeService.GetRadialOpacity();
            ActivationShortcut = _themeService.GetActivationShortcut();

            RefreshItems();
        }

        public void RefreshItems()
        {
            var all = _itemRepo.GetAll();
            if (SelectedCategory != null && (SelectedCategory.Id <= 1 || SelectedCategory.Name.Contains("Kullanılanlar", StringComparison.OrdinalIgnoreCase)))
            {
                // Exact same recency/frequency weighted calculation as RadialMenuViewModel
                DateTime now = DateTime.UtcNow;
                all = all.Where(i => i.CategoryId <= 1 || i.IsUserAdded || i.IsFavorite || i.UseCount > 0 || i.LaunchCount > 0)
                         .Where(i => i.ParentId == 0)
                         .OrderByDescending(i => RadialMenuViewModel.CalculateUsageScore(i, now))
                         .ThenBy(i => i.Position)
                         .ToList();
            }
            else if (SelectedCategory != null && SelectedCategory.Id > 1)
            {
                all = all.Where(i => i.CategoryId == SelectedCategory.Id && i.ParentId == 0)
                         .OrderBy(i => i.Position)
                         .ToList();
            }
            else
            {
                all = all.Where(i => i.ParentId == 0)
                         .OrderBy(i => i.CategoryId)
                         .ThenBy(i => i.Position)
                         .ToList();
            }

            if (!string.IsNullOrWhiteSpace(FilterQuery))
            {
                string q = FilterQuery.Trim().ToLowerInvariant();
                all = all.Where(i => i.Name.ToLowerInvariant().Contains(q) || i.Target.ToLowerInvariant().Contains(q)).ToList();
            }

            Items = new ObservableCollection<LauncherItem>(all);
        }

        [RelayCommand]
        public void SelectCategory(Category cat)
        {
            SelectedCategory = cat;
            RefreshItems();
        }

        [RelayCommand]
        public void ToggleFavorite(LauncherItem item)
        {
            if (item == null) return;
            _itemRepo.ToggleFavorite(item.Id);
            RefreshItems();
        }

        [RelayCommand]
        public void DeleteItem(LauncherItem item)
        {
            if (item == null) return;
            _itemRepo.Delete(item.Id);
            RefreshItems();
            StatusMessage = $"{item.Name} silindi.";
        }

        [RelayCommand]
        public void ApplyTheme(Theme theme)
        {
            if (theme == null) return;
            SelectedTheme = theme;
            _themeService.SetCurrentTheme(theme.Name);
            StatusMessage = $"Tema '{theme.Name}' uygulandı.";
        }

        [RelayCommand]
        public void UpdateRadialOpacity(double opacity)
        {
            RadialOpacity = opacity;
            _themeService.SetRadialOpacity(opacity);
        }

        [RelayCommand]
        public async Task ScanPc()
        {
            StatusMessage = "Bilgisayar taranıyor...";
            await Task.Run(() =>
            {
                var scanned = _scannerService.ScanAllApps();
                var summary = _scannerService.SaveScannedApps(scanned, _db);
                StatusMessage = $"Tarama tamamlandı: {summary.TotalAdded} yeni uygulama eklendi.";
            });
            RefreshItems();
        }

        [RelayCommand]
        public async Task ExportData(string path)
        {
            bool ok = await _syncService.ExportToFileAsync(path);
            StatusMessage = ok ? "Yedek başarıyla dışa aktarıldı." : "Dışa aktarma başarısız.";
        }

        [RelayCommand]
        public async Task ImportData(string path)
        {
            bool ok = await _syncService.ImportFromFileAsync(path);
            if (ok)
            {
                LoadInitialData();
                StatusMessage = "Yedek başarıyla içe aktarıldı.";
            }
            else
            {
                StatusMessage = "İçe aktarma başarısız.";
            }
        }
    }
}
