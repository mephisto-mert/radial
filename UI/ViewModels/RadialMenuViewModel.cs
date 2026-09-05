using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Clipboard;
using RadialLauncher.Services.Processes;
using RadialLauncher.Services.Themes;
using RadialLauncher.Services.VirtualDesktop;
using RadialLauncher.Services.Windows;
using Serilog;

namespace RadialLauncher.UI.ViewModels
{
    public partial class RadialMenuViewModel : ObservableObject
    {
        public static readonly Dictionary<string, ImageSource> WindowIcons = new();

        private readonly IItemRepository _itemRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IProcessRunner _processRunner;
        private readonly IThemeService _themeService;
        private readonly IClipboardService _clipboardService;
        private readonly IVirtualDesktopService _desktopService;
        private readonly ISystemActionService _systemActionService;
        private readonly IWindowSwitcherService _windowSwitcher;

        private readonly Stack<(int parentId, string title)> _navStack = new();
        private List<LauncherItem> _allItems = new();

        [ObservableProperty]
        private ObservableCollection<Category> _categories = new();

        [ObservableProperty]
        private Category? _currentCategory;

        [ObservableProperty]
        private int _currentCategoryIndex = 0;

        [ObservableProperty]
        private ObservableCollection<LauncherItem> _currentPageItems = new();

        [ObservableProperty]
        private int _currentPageIndex = 0;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearchMode = false;

        [ObservableProperty]
        private bool _isSubmenu = false;

        [ObservableProperty]
        private string _breadcrumbTitle = string.Empty;

        [ObservableProperty]
        private LauncherItem? _hoveredItem;

        [ObservableProperty]
        private string _hoveredItemTitle = string.Empty;

        [ObservableProperty]
        private bool _showTutorial = false;

        [ObservableProperty]
        private Theme _activeTheme;

        [ObservableProperty]
        private ObservableCollection<QuickActionItem> _quickActions = new();

        [ObservableProperty]
        private ObservableCollection<ClipboardItem> _recentClipboards = new();

        public event Action? RequestClose;
        public event Action? RequestLayoutUpdate;

        public RadialMenuViewModel(
            IItemRepository itemRepo,
            ICategoryRepository categoryRepo,
            IProcessRunner processRunner,
            IThemeService themeService,
            IClipboardService clipboardService,
            IVirtualDesktopService desktopService,
            ISystemActionService systemActionService,
            IWindowSwitcherService windowSwitcher)
        {
            _itemRepo = itemRepo;
            _categoryRepo = categoryRepo;
            _processRunner = processRunner;
            _themeService = themeService;
            _clipboardService = clipboardService;
            _desktopService = desktopService;
            _systemActionService = systemActionService;
            _windowSwitcher = windowSwitcher;

            _activeTheme = _themeService.GetCurrentTheme();
            _themeService.OnThemeChanged += (t) =>
            {
                ActiveTheme = t;
                RequestLayoutUpdate?.Invoke();
            };

            LoadDefaultQuickActions();
        }

        public void InitializeForDisplay()
        {
            SearchQuery = string.Empty;
            IsSearchMode = false;
            _navStack.Clear();
            IsSubmenu = false;
            BreadcrumbTitle = string.Empty;
            CurrentCategoryIndex = 0;
            CurrentPageIndex = 0;
            HoveredItem = null;
            HoveredItemTitle = string.Empty;

            ActiveTheme = _themeService.GetCurrentTheme();

            // Load data
            _allItems = _itemRepo.GetAll();
            var allDbCats = _categoryRepo.GetAll();

            // 1. Most Used / Favorites is first category
            var mostUsedCat = allDbCats.FirstOrDefault(c => c.Id <= 1 || c.Name.Contains("Kullanılanlar", StringComparison.OrdinalIgnoreCase))
                             ?? new Category { Id = 1, Name = "⭐ En Çok Kullanılanlar", Color = "#f39c12", Position = 0 };

            // 2. Open Windows category placed immediately next to Most Used
            var openWinCat = new Category { Id = -99, Name = "🪟 Açık Pencereler", Color = "#9b59b6", Position = 1 };

            var validCats = new List<Category> { mostUsedCat, openWinCat };

            foreach (var c in allDbCats)
            {
                if (c.Id == mostUsedCat.Id || c.Name.Contains("Kullanılanlar", StringComparison.OrdinalIgnoreCase)) continue;
                if (c.Name.Contains("Açık Pencereler", StringComparison.OrdinalIgnoreCase)) continue;
                if (_allItems.Any(i => i.CategoryId == c.Id && i.ParentId == 0))
                {
                    validCats.Add(c);
                }
            }

            Categories = new ObservableCollection<Category>(validCats);
            if (CurrentCategoryIndex >= Categories.Count) CurrentCategoryIndex = 0;
            CurrentCategory = Categories.Count > 0 ? Categories[CurrentCategoryIndex] : null;

            // Load recent clipboard
            RecentClipboards = new ObservableCollection<ClipboardItem>(_clipboardService.GetRecentHistory(5));

            RefreshPageItems();
        }

        private void LoadDefaultQuickActions()
        {
            QuickActions = new ObservableCollection<QuickActionItem>
            {
                new QuickActionItem { Id = "SETTINGS", Name = "Ayarlar", IconSymbol = "⚙️", ActionKey = "SETTINGS", Order = 0 },
                new QuickActionItem { Id = "SEARCH", Name = "Arama", IconSymbol = "🔍", ActionKey = "SEARCH", Order = 1 },
                new QuickActionItem { Id = "DESKTOP", Name = "Masaüstü", IconSymbol = "🖥️", ActionKey = "SHOW_DESKTOP", Order = 2 },
                new QuickActionItem { Id = "SNIP", Name = "Ekran Alıntısı", IconSymbol = "✂️", ActionKey = "SNIP_TOOL", Order = 3 },
                new QuickActionItem { Id = "MUTE", Name = "Sesi Kapat", IconSymbol = "🔇", ActionKey = "VOLUME_MUTE", Order = 4 }
            };
        }

        public void RefreshPageItems()
        {
            List<LauncherItem> filtered;

            if (IsSearchMode && !string.IsNullOrWhiteSpace(SearchQuery))
            {
                string q = SearchQuery.Trim().ToLowerInvariant();
                filtered = _allItems.Where(i =>
                    i.Name.ToLowerInvariant().Contains(q) ||
                    i.Target.ToLowerInvariant().Contains(q) ||
                    (i.Tags != null && i.Tags.ToLowerInvariant().Contains(q))
                ).ToList();
            }
            else if (IsSubmenu && _navStack.Count > 0)
            {
                int currentParentId = _navStack.Peek().parentId;
                filtered = _allItems.Where(i => i.ParentId == currentParentId).OrderBy(i => i.Position).ToList();
            }
            else
            {
                var cat = (Categories.Count > CurrentCategoryIndex) ? Categories[CurrentCategoryIndex] : null;
                if (cat == null)
                {
                    filtered = new List<LauncherItem>();
                }
                else if (cat.Id == -99 || cat.Name.Contains("Açık Pencereler", StringComparison.OrdinalIgnoreCase))
                {
                    var openWins = _windowSwitcher.GetOpenWindows();
                    WindowIcons.Clear();
                    foreach (var w in openWins)
                    {
                        if (w.Icon != null) WindowIcons[w.Handle.ToString()] = w.Icon;
                    }
                    filtered = openWins.Select((w, idx) => new LauncherItem
                    {
                        Id = -100 - idx,
                        Name = string.IsNullOrWhiteSpace(w.Title) ? w.ProcessName : w.Title,
                        Type = "WINDOW",
                        Target = w.Handle.ToString(),
                        IconPath = w.ProcessName,
                        CategoryId = cat.Id,
                        Position = idx
                    }).ToList();
                }
                else if (cat.Id <= 1 || cat.Name.Contains("Kullanılanlar", StringComparison.OrdinalIgnoreCase) || cat.Name.Contains("Hepsi", StringComparison.OrdinalIgnoreCase))
                {
                    // Smart usage tracking: page 1 prioritizes user-added items, then most used
                    filtered = _allItems.Where(i => i.CategoryId <= 1 || i.IsUserAdded || i.IsFavorite)
                                        .Where(i => i.ParentId == 0)
                                        .OrderByDescending(i => i.IsFavorite)
                                        .ThenByDescending(i => i.LaunchCount)
                                        .ThenBy(i => i.Position)
                                        .ToList();
                }
                else
                {
                    filtered = _allItems.Where(i => i.CategoryId == cat.Id && i.ParentId == 0)
                                        .OrderBy(i => i.Position)
                                        .ToList();
                }
            }

            int pageSize = ActiveTheme.DensityMode == "Compact" ? 18 : 15;
            TotalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
            if (CurrentPageIndex >= TotalPages) CurrentPageIndex = 0;

            var pageItems = filtered.Skip(CurrentPageIndex * pageSize).Take(pageSize).ToList();
            CurrentPageItems = new ObservableCollection<LauncherItem>(pageItems);

            RequestLayoutUpdate?.Invoke();
        }

        [RelayCommand]
        public void LaunchItem(LauncherItem item)
        {
            if (item == null) return;

            if (string.Equals(item.Type, "SUBMENU", StringComparison.OrdinalIgnoreCase))
            {
                // Enter submenu
                _navStack.Push((item.Id, item.Name));
                IsSubmenu = true;
                BreadcrumbTitle = item.Name;
                CurrentPageIndex = 0;
                RefreshPageItems();
                return;
            }

            if (string.Equals(item.Type, "WINDOW", StringComparison.OrdinalIgnoreCase) && long.TryParse(item.Target, out long hWndVal))
            {
                _windowSwitcher.SwitchToWindow((IntPtr)hWndVal);
                RequestClose?.Invoke();
                return;
            }

            _processRunner.Launch(item);
            RequestClose?.Invoke();
        }

        [RelayCommand]
        public void CenterButtonClick()
        {
            if (IsSubmenu && _navStack.Count > 0)
            {
                _navStack.Pop();
                if (_navStack.Count > 0)
                {
                    BreadcrumbTitle = _navStack.Peek().title;
                }
                else
                {
                    IsSubmenu = false;
                    BreadcrumbTitle = string.Empty;
                }
                CurrentPageIndex = 0;
                RefreshPageItems();
            }
            else
            {
                RequestClose?.Invoke();
            }
        }

        [RelayCommand]
        public void NextPage()
        {
            if (TotalPages > 1)
            {
                CurrentPageIndex = (CurrentPageIndex + 1) % TotalPages;
                RefreshPageItems();
            }
        }

        [RelayCommand]
        public void PrevPage()
        {
            if (TotalPages > 1)
            {
                CurrentPageIndex = (CurrentPageIndex - 1 + TotalPages) % TotalPages;
                RefreshPageItems();
            }
        }

        [RelayCommand]
        public void SwitchCategory(int index)
        {
            if (index >= 0 && index < Categories.Count)
            {
                CurrentCategoryIndex = index;
                CurrentCategory = Categories[index];
                CurrentPageIndex = 0;
                IsSubmenu = false;
                _navStack.Clear();
                RefreshPageItems();
            }
        }

        [RelayCommand]
        public void ApplySearch(string query)
        {
            SearchQuery = query;
            IsSearchMode = !string.IsNullOrWhiteSpace(query);
            CurrentPageIndex = 0;
            RefreshPageItems();
        }

        [RelayCommand]
        public void SwitchDesktop(string direction)
        {
            if (direction == "next") _desktopService.SwitchToNextDesktop();
            else if (direction == "prev") _desktopService.SwitchToPreviousDesktop();
        }

        [RelayCommand]
        public void ExecuteQuickAction(QuickActionItem action)
        {
            if (action == null) return;
            if (action.ActionKey == "SETTINGS")
            {
                RequestClose?.Invoke();
                RadialLauncher.App.Current.Dispatcher.Invoke(() =>
                {
                    ((App)App.Current).OpenSettings();
                });
            }
            else if (action.ActionKey == "SEARCH")
            {
                IsSearchMode = true;
            }
            else
            {
                _systemActionService.ExecuteAction(action.ActionKey);
            }
        }
    }
}
