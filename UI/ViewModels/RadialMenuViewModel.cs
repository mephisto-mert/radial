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
using RadialLauncher.Services.Context;
using RadialLauncher.Services.Localization;
using RadialLauncher.Services.Plugins;
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
        private readonly IPluginService _pluginService;
        private readonly IContextualActionService _contextualActionService;
        private List<LauncherItem> _contextualItems = new();

        public IVirtualDesktopService DesktopService => _desktopService;
        public IContextualActionService ContextualActionService => _contextualActionService;
        public IItemRepository ItemRepository => _itemRepo;
        public void ReloadCategoriesAndItems() => InitializeForDisplay();

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
            IWindowSwitcherService windowSwitcher,
            IPluginService pluginService,
            IContextualActionService contextualActionService)
        {
            _itemRepo = itemRepo;
            _categoryRepo = categoryRepo;
            _processRunner = processRunner;
            _themeService = themeService;
            _clipboardService = clipboardService;
            _desktopService = desktopService;
            _systemActionService = systemActionService;
            _windowSwitcher = windowSwitcher;
            _pluginService = pluginService;
            _contextualActionService = contextualActionService;

            _activeTheme = _themeService.GetCurrentTheme();
            _themeService.OnThemeChanged += (t) =>
            {
                ActiveTheme = t;
                RequestLayoutUpdate?.Invoke();
            };

            LocalizationService.Instance.OnLanguageChanged += () =>
            {
                LoadDefaultQuickActions();
                RefreshPageItems();
                RequestLayoutUpdate?.Invoke();
            };

            LoadDefaultQuickActions();
        }

        public void InitializeForDisplay()
        {
            var loc = LocalizationService.Instance;
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

            try
            {
                string fgProc = _windowSwitcher.GetForegroundProcessName();
                _contextualItems = _contextualActionService.GetContextualItems(fgProc);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed loading contextual items for foreground process");
                _contextualItems = new List<LauncherItem>();
            }

            // Load data
            _allItems = _itemRepo.GetAll();
            var allDbCats = _categoryRepo.GetAll();

            // 1. Most Used / Favorites is first category
            var mostUsedCat = allDbCats.FirstOrDefault(c => c.SystemKey == "Cat_MostUsed");
            if (mostUsedCat != null)
            {
                mostUsedCat.SystemKey = "Cat_MostUsed";
            }
            else
            {
                mostUsedCat = new Category { Id = 1, Name = "⭐ Most Used", SystemKey = "Cat_MostUsed", Color = "#f39c12", Position = 0 };
            }

            // 2. Open Windows category placed immediately next to Most Used
            var openWinCat = new Category { Id = -99, Name = "🪟 Open Windows", SystemKey = "Cat_OpenWindows", Color = "#9b59b6", Position = 1 };

            // 2b. Clipboard History category
            var clipboardCat = new Category { Id = -98, Name = "📋 Clipboard History", SystemKey = "Cat_ClipboardHistory", Color = "#e67e22", Position = 2 };

            var validCats = new List<Category> { mostUsedCat, openWinCat, clipboardCat };

            foreach (var c in allDbCats)
            {
                if (c.Id == mostUsedCat.Id || c.SystemKey == "Cat_MostUsed" || c.SystemKey == "Cat_OpenWindows" || c.SystemKey == "Cat_ClipboardHistory") continue;
                if (_allItems.Any(i => i.CategoryId == c.Id && i.ParentId == 0))
                {
                    validCats.Add(c);
                }
            }

            // 3. Plugin categories
            int pluginIdx = 0;
            var providers = _pluginService?.GetProviders() ?? new List<RadialLauncher.Services.Plugins.IRadialItemProvider>();
            foreach (var provider in providers)
            {
                validCats.Add(new Category
                {
                    Id = -200 - pluginIdx++,
                    Name = provider.CategoryName,
                    Color = string.IsNullOrEmpty(provider.CategoryColor) ? "#9b59b6" : provider.CategoryColor,
                    Position = 100 + pluginIdx
                });
            }

            Categories = new ObservableCollection<Category>(validCats);
            if (CurrentCategoryIndex >= Categories.Count) CurrentCategoryIndex = 0;
            CurrentCategory = Categories.Count > 0 ? Categories[CurrentCategoryIndex] : null;

            // Load recent clipboard
            var recentClips = _clipboardService?.GetRecentHistory(5) ?? new List<ClipboardItem>();
            RecentClipboards = new ObservableCollection<ClipboardItem>(recentClips);

            RefreshPageItems();
        }

        public void LoadDefaultQuickActions()
        {
            var loc = LocalizationService.Instance;
            QuickActions = new ObservableCollection<QuickActionItem>
            {
                new QuickActionItem { Id = "SETTINGS", Name = loc.GetString("Quick_Settings", "Settings"), IconSymbol = "⚙️", ActionKey = "SETTINGS", Order = 0 },
                new QuickActionItem { Id = "SEARCH", Name = loc.GetString("Quick_Search", "Search"), IconSymbol = "🔍", ActionKey = "SEARCH", Order = 1 },
                new QuickActionItem { Id = "DESKTOP", Name = loc.GetString("Quick_Desktop", "Desktop"), IconSymbol = "🖥️", ActionKey = "SHOW_DESKTOP", Order = 2 },
                new QuickActionItem { Id = "SNIP", Name = loc.GetString("Quick_Snip", "Snipping Tool"), IconSymbol = "✂️", ActionKey = "SNIP_TOOL", Order = 3 },
                new QuickActionItem { Id = "MUTE", Name = loc.GetString("Quick_Mute", "Mute"), IconSymbol = "🔇", ActionKey = "VOLUME_MUTE", Order = 4 }
            };
        }

        public void RefreshPageItems()
        {
            List<LauncherItem> filtered;

            if (IsSearchMode && !string.IsNullOrWhiteSpace(SearchQuery))
            {
                if (SearchQuery.StartsWith("/"))
                {
                    filtered = GetCommandPaletteResults(SearchQuery);
                }
                else
                {
                    string q = SearchQuery.Trim().ToLowerInvariant();
                    filtered = _allItems.Where(i =>
                        i.Name.ToLowerInvariant().Contains(q) ||
                        i.Target.ToLowerInvariant().Contains(q) ||
                        (i.Tags != null && i.Tags.ToLowerInvariant().Contains(q))
                    ).ToList();
                }
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
                else if (cat.SystemKey == "Cat_OpenWindows" || cat.Id == -99)
                {
                    var openWins = _windowSwitcher?.GetOpenWindows() ?? new List<WindowInfo>();
                    WindowIcons?.Clear();
                    foreach (var w in openWins)
                    {
                        if (w.Icon != null && WindowIcons != null) WindowIcons[w.Handle.ToString()] = w.Icon;
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
                else if (cat.SystemKey == "Cat_ClipboardHistory" || cat.Id == -98)
                {
                    var clips = _clipboardService?.GetRecentHistory(20) ?? new List<ClipboardItem>();
                    filtered = clips.Select((c, idx) => new LauncherItem
                    {
                        Id = -400 - idx,
                        Name = c.Preview,
                        Type = "CLIPBOARD",
                        Target = c.Text,
                        CategoryId = cat.Id,
                        Position = idx
                    }).ToList();
                }
                else if (cat.Id <= -200)
                {
                    int providerIndex = (-cat.Id) - 200;
                    filtered = _pluginService?.GetSafeItems(providerIndex)?.ToList() ?? new List<LauncherItem>();
                }
                else if (cat.SystemKey == "Cat_MostUsed" || cat.Id == 1)
                {
                    // Recency/frequency-aware weighted ranking:
                    DateTime now = DateTime.UtcNow;
                    filtered = _allItems.Where(i => i.CategoryId <= 1 || i.IsUserAdded || i.IsFavorite || i.UseCount > 0 || i.LaunchCount > 0)
                                        .Where(i => i.ParentId == 0)
                                        .OrderByDescending(i => CalculateUsageScore(i, now))
                                        .ThenBy(i => i.Position)
                                        .ToList();

                    if (_contextualItems != null && _contextualItems.Count > 0)
                    {
                        filtered.InsertRange(0, _contextualItems);
                    }
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

            if (string.Equals(item.Type, "CLIPBOARD", StringComparison.OrdinalIgnoreCase))
            {
                RequestClose?.Invoke();
                _clipboardService?.PasteItem(item.Target);
                return;
            }

            if (string.Equals(item.Type, "WINDOW", StringComparison.OrdinalIgnoreCase) && long.TryParse(item.Target, out long hWndVal))
            {
                _windowSwitcher.SwitchToWindow((IntPtr)hWndVal);
                RequestClose?.Invoke();
                return;
            }

            if (string.Equals(item.Type, "COMMAND_THEME", StringComparison.OrdinalIgnoreCase))
            {
                _themeService.SetCurrentTheme(item.Target);
                ActiveTheme = _themeService.GetCurrentTheme();
                RequestLayoutUpdate?.Invoke();
                RequestClose?.Invoke();
                return;
            }

            if (string.Equals(item.Type, "COMMAND_RESTART", StringComparison.OrdinalIgnoreCase))
            {
                RequestClose?.Invoke();
                _systemActionService.ExecuteAction("RESTART_APP");
                return;
            }

            if (string.Equals(item.Type, "COMMAND_LOGS", StringComparison.OrdinalIgnoreCase))
            {
                RequestClose?.Invoke();
                string logDir = RadialLauncher.Services.Data.UserDataPathProvider.Instance.GetLogsFolder();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = logDir, UseShellExecute = true });
                return;
            }

            if (string.Equals(item.Type, "COMMAND_SETTINGS", StringComparison.OrdinalIgnoreCase))
            {
                RequestClose?.Invoke();
                RadialLauncher.App.Current?.Dispatcher?.Invoke(() =>
                {
                    ((App)RadialLauncher.App.Current).OpenSettings();
                });
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
        public void NavigateNextGlobal()
        {
            if (TotalPages > 1 && CurrentPageIndex < TotalPages - 1)
            {
                CurrentPageIndex++;
                RefreshPageItems();
            }
            else if (Categories.Count > 1)
            {
                int nextCat = (CurrentCategoryIndex + 1) % Categories.Count;
                SwitchCategory(nextCat);
            }
            else if (TotalPages > 1)
            {
                CurrentPageIndex = 0;
                RefreshPageItems();
            }
        }

        [RelayCommand]
        public void NavigatePrevGlobal()
        {
            if (TotalPages > 1 && CurrentPageIndex > 0)
            {
                CurrentPageIndex--;
                RefreshPageItems();
            }
            else if (Categories.Count > 1)
            {
                int prevCat = (CurrentCategoryIndex - 1 + Categories.Count) % Categories.Count;
                SwitchCategory(prevCat);
                if (TotalPages > 1)
                {
                    CurrentPageIndex = TotalPages - 1;
                    RefreshPageItems();
                }
            }
            else if (TotalPages > 1)
            {
                CurrentPageIndex = TotalPages - 1;
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
            if (direction == "next") _desktopService?.SwitchToNextDesktop();
            else if (direction == "prev") _desktopService?.SwitchToPreviousDesktop();
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

        public static double CalculateUsageScore(LauncherItem item, DateTime? now = null)
        {
            if (item == null) return 0.0;
            DateTime current = now ?? DateTime.UtcNow;
            int count = Math.Max(item.UseCount, item.LaunchCount);
            DateTime? last = item.LastUsedAt ?? item.LastLaunched;

            double recencyFactor = 0.1;
            if (last.HasValue)
            {
                double hours = Math.Max(0.0, (current - last.Value).TotalHours);
                // Exponential decay: 7-day half-life (168 hours)
                recencyFactor = Math.Exp(-0.693147 * hours / 168.0);
            }

            double baseScore = (count + 1.0) * (0.3 + 0.7 * recencyFactor);
            double favoriteBonus = item.IsFavorite ? 10.0 : 0.0;
            double userAddedBonus = item.IsUserAdded ? 5.0 : 0.0;

            return baseScore + favoriteBonus + userAddedBonus;
        }

        public List<LauncherItem> GetCommandPaletteResults(string query)
        {
            var list = new List<LauncherItem>();
            string trimmed = (query ?? string.Empty).Trim();
            string cmd = trimmed.ToLowerInvariant();

            if (cmd == "/" || cmd.StartsWith("/theme") || cmd.StartsWith("/tema"))
            {
                string themeArg = cmd.Length > 6 ? cmd[6..].Trim() : string.Empty;
                var allThemes = _themeService.GetAllThemes();
                var matchingThemes = string.IsNullOrEmpty(themeArg) 
                    ? allThemes 
                    : allThemes.Where(t => 
                        (!string.IsNullOrEmpty(t.DisplayName) && t.DisplayName.ToLowerInvariant().Contains(themeArg)) || 
                        (!string.IsNullOrEmpty(t.Name) && t.Name.ToLowerInvariant().Contains(themeArg)) || 
                        (!string.IsNullOrEmpty(t.Id) && t.Id.ToLowerInvariant().Contains(themeArg))).ToList();

                int tIdx = 0;
                foreach (var t in matchingThemes)
                {
                    list.Add(new LauncherItem
                    {
                        Id = -500 - (tIdx++),
                        Name = $"🎨 {t.DisplayName}",
                        Type = "COMMAND_THEME",
                        Target = t.Id,
                        CategoryId = -1,
                        Position = tIdx
                    });
                }
            }

            var loc = LocalizationService.Instance;
            if (cmd == "/" || "/restart".StartsWith(cmd) || cmd.StartsWith("/restart") || cmd.StartsWith("/yeniden"))
            {
                list.Add(new LauncherItem
                {
                    Id = -550,
                    Name = loc.GetString("Cmd_Restart", "🔄 Restart App (/restart)"),
                    Type = "COMMAND_RESTART",
                    Target = "RESTART",
                    CategoryId = -1
                });
            }

            if (cmd == "/" || "/logs".StartsWith(cmd) || cmd.StartsWith("/logs") || "/log".StartsWith(cmd))
            {
                list.Add(new LauncherItem
                {
                    Id = -551,
                    Name = loc.GetString("Cmd_Logs", "📂 Open Logs (/logs)"),
                    Type = "COMMAND_LOGS",
                    Target = "LOGS",
                    CategoryId = -1
                });
            }

            if (cmd == "/" || "/settings".StartsWith(cmd) || cmd.StartsWith("/settings") || "/ayarlar".StartsWith(cmd))
            {
                list.Add(new LauncherItem
                {
                    Id = -552,
                    Name = loc.GetString("Cmd_Settings", "⚙️ Open Settings (/settings)"),
                    Type = "COMMAND_SETTINGS",
                    Target = "SETTINGS",
                    CategoryId = -1
                });
            }

            if (cmd.Length > 1 && !cmd.StartsWith("/theme") && !cmd.StartsWith("/tema"))
            {
                string rawSearch = cmd.TrimStart('/');
                if (!string.IsNullOrWhiteSpace(rawSearch))
                {
                    var itemMatches = _allItems.Where(i =>
                        i.Name.ToLowerInvariant().Contains(rawSearch) ||
                        i.Target.ToLowerInvariant().Contains(rawSearch) ||
                        (i.Tags != null && i.Tags.ToLowerInvariant().Contains(rawSearch))
                    ).Take(12).ToList();

                    list.AddRange(itemMatches);
                }
            }

            return list;
        }
    }
}
