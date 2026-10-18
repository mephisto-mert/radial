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
using RadialLauncher.Services.Input;
using RadialLauncher.Services.Audio;
using RadialLauncher.Services.SystemTools;
using RadialLauncher.Services.Intelligence;
using Microsoft.Extensions.DependencyInjection;
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

        private IHotkeyAndSnippetService HotkeyService => App.ServiceProvider?.GetService<IHotkeyAndSnippetService>() ?? _fallbackHotkeyService;
        private IWindowTilingAndBoostService TilingService => App.ServiceProvider?.GetService<IWindowTilingAndBoostService>() ?? _fallbackTilingService;
        private IPlaytimeTrackerService PlaytimeService => App.ServiceProvider?.GetService<IPlaytimeTrackerService>() ?? _fallbackPlaytimeService;
        private IInlineCalculatorService CalculatorService => App.ServiceProvider?.GetService<IInlineCalculatorService>() ?? _fallbackCalculatorService;
        private IAppContextProfileService ProfileService => App.ServiceProvider?.GetService<IAppContextProfileService>() ?? _fallbackProfileService;
        private IUiSoundEffectsService SoundService => App.ServiceProvider?.GetService<IUiSoundEffectsService>() ?? _fallbackSoundService;
        private IWorkspaceAndMonitorService WorkspaceService => App.ServiceProvider?.GetService<IWorkspaceAndMonitorService>() ?? _fallbackWorkspaceService;
        private IPerAppAudioAndDeviceService PerAppAudioService => App.ServiceProvider?.GetService<IPerAppAudioAndDeviceService>() ?? _fallbackPerAppAudioService;
        private IRichClipboardPreviewService RichClipboardService => App.ServiceProvider?.GetService<IRichClipboardPreviewService>() ?? _fallbackRichClipboardService;

        private static readonly IHotkeyAndSnippetService _fallbackHotkeyService = new HotkeyAndSnippetService();
        private static readonly IWindowTilingAndBoostService _fallbackTilingService = new WindowTilingAndBoostService();
        private static readonly IPlaytimeTrackerService _fallbackPlaytimeService = new PlaytimeTrackerService();
        private static readonly IInlineCalculatorService _fallbackCalculatorService = new InlineCalculatorService();
        private static readonly IAppContextProfileService _fallbackProfileService = new AppContextProfileService();
        private static readonly IUiSoundEffectsService _fallbackSoundService = new UiSoundEffectsService();
        private static readonly IWorkspaceAndMonitorService _fallbackWorkspaceService = new WorkspaceAndMonitorService();
        private static readonly IPerAppAudioAndDeviceService _fallbackPerAppAudioService = new PerAppAudioAndDeviceService();
        private static readonly IRichClipboardPreviewService _fallbackRichClipboardService = new RichClipboardPreviewService();

        public string ActiveProfileTitle { get; private set; } = "⚡ Akıllı Aksiyonlar";
        public event Action<string, string>? RequestShowToast;

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

            _itemRepo.OnItemsChanged += () =>
            {
                _allItems = _itemRepo.GetAll();
                _cachedDbCategories = _categoryRepo.GetAll();
                RefreshPageItems();
            };

            _allItems = _itemRepo.GetAll();
            _cachedDbCategories = _categoryRepo.GetAll();
            LoadDefaultQuickActions();
        }

        private List<Category> _cachedDbCategories = new();
        private DateTime _lastPruneUtc = DateTime.MinValue;
        private bool _isPruningInBackground = false;

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

            if (_allItems.Count == 0)
            {
                _allItems = _itemRepo.GetAll();
            }
            if (_cachedDbCategories.Count == 0)
            {
                _cachedDbCategories = _categoryRepo.GetAll();
            }
            var allDbCats = _cachedDbCategories;

            // Prune uninstalled apps/games asynchronously in the background (never block UI open!)
            if (!_isPruningInBackground && (DateTime.UtcNow - _lastPruneUtc).TotalSeconds >= 30)
            {
                _isPruningInBackground = true;
                _lastPruneUtc = DateTime.UtcNow;
                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        _itemRepo.PruneUninstalledItems();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Background prune failed");
                    }
                    finally
                    {
                        _isPruningInBackground = false;
                    }
                });
            }

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

            // 2c. Context-Aware Smart Profile category
            string fgProc = GetTargetProcessName();
            var activeProfile = ProfileService.GetProfileForProcess(fgProc);
            ActiveProfileTitle = activeProfile.ProfileTitle;
            var smartProfileCat = new Category
            {
                Id = -97,
                Name = "⚡ Akıllı Profil",
                SystemKey = "Cat_SmartProfile",
                Color = string.IsNullOrEmpty(activeProfile.AccentHex) ? "#38BDF8" : activeProfile.AccentHex,
                Position = 3
            };

            // 2d. Per-App Audio Mixer category
            var appAudioCat = new Category
            {
                Id = -96,
                Name = "🎚️ Ses Mikseri",
                SystemKey = "Cat_AppAudioMixer",
                Color = "#EC4899",
                Position = 4
            };

            var validCats = new List<Category> { mostUsedCat, openWinCat, clipboardCat, smartProfileCat, appAudioCat };

            foreach (var c in allDbCats)
            {
                if (c.Id == mostUsedCat.Id || c.SystemKey == "Cat_MostUsed" || c.SystemKey == "Cat_OpenWindows" || c.SystemKey == "Cat_ClipboardHistory" || c.SystemKey == "Cat_SmartProfile" || c.SystemKey == "Cat_AppAudioMixer") continue;
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

            RefreshPageItems(notifyLayout: false);
        }

        private string GetTargetProcessName()
        {
            try
            {
                var openWins = _windowSwitcher?.GetOpenWindows();
                if (openWins != null && openWins.Count > 0)
                {
                    IntPtr targetHwnd = TilingService.LastActiveWindow;
                    if (targetHwnd != IntPtr.Zero)
                    {
                        var match = openWins.FirstOrDefault(w => w.Handle == targetHwnd);
                        if (match != null && !string.IsNullOrWhiteSpace(match.ProcessName))
                            return match.ProcessName;
                    }
                    return openWins[0].ProcessName ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
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

        public void RefreshPageItems(bool notifyLayout = true)
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

                    var calc = CalculatorService.TryEvaluate(SearchQuery);
                    if (calc.IsValid)
                    {
                        filtered.Insert(0, new LauncherItem
                        {
                            Id = -888,
                            Name = $"🧮 {calc.Expression} = {calc.FormattedResult}",
                            Type = "CALCULATOR_RESULT",
                            Target = calc.RawValue,
                            CategoryId = -1,
                            Position = -1
                        });
                        RequestShowToast?.Invoke($"🧮 {calc.Expression} = {calc.FormattedResult}", "#10B981");
                    }
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
                    filtered = clips.Select((c, idx) =>
                    {
                        var meta = RichClipboardService.Analyze(c.Text);
                        string displayTitle = meta.Kind == RichClipboardKind.PlainText
                            ? c.Preview
                            : meta.FormattedTitle;
                        return new LauncherItem
                        {
                            Id = -400 - idx,
                            Name = displayTitle,
                            Type = "CLIPBOARD",
                            Target = c.Text,
                            CategoryId = cat.Id,
                            Position = idx
                        };
                    }).ToList();
                }
                else if (cat.SystemKey == "Cat_SmartProfile" || cat.Id == -97)
                {
                    string fgProc = GetTargetProcessName();
                    var profile = ProfileService.GetProfileForProcess(fgProc);
                    ActiveProfileTitle = profile.ProfileTitle;
                    filtered = profile.QuickItems ?? new List<LauncherItem>();
                }
                else if (cat.SystemKey == "Cat_AppAudioMixer" || cat.Id == -96)
                {
                    filtered = new List<LauncherItem>
                    {
                        new LauncherItem
                        {
                            Id = -960,
                            Name = "🎧 Ses Çıkışı Değiştir (Kulaklık ⇄ Hoparlör)",
                            Type = "AUDIO_DEVICE",
                            Target = "SWITCH_AUDIO_DEVICE",
                            CategoryId = cat.Id,
                            Position = 0
                        }
                    };
                    var sessions = PerAppAudioService.GetActiveAppAudioSessions();
                    int sIdx = 1;
                    foreach (var s in sessions)
                    {
                        filtered.Add(new LauncherItem
                        {
                            Id = -960 - sIdx,
                            Name = $"{(s.IsMuted ? "🔇" : "🔊")} {s.DisplayName} (%{s.VolumePercent})",
                            Type = "APP_AUDIO_SESSION",
                            Target = s.ProcessId.ToString(),
                            IconPath = s.ProcessName,
                            CategoryId = cat.Id,
                            Position = sIdx++
                        });
                    }
                }
                else if (cat.Id <= -200)
                {
                    int providerIndex = (-cat.Id) - 200;
                    filtered = _pluginService?.GetSafeItems(providerIndex)?.ToList() ?? new List<LauncherItem>();
                }
                else if (cat.SystemKey == "Cat_MostUsed" || cat.Id == 1)
                {
                    filtered = _allItems.Where(i => (i.CategoryId <= 1 || i.IsUserAdded || i.IsFavorite) && i.ParentId == 0)
                                        .OrderBy(i => i.Position)
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

            if (notifyLayout)
            {
                RequestLayoutUpdate?.Invoke();
            }
        }

        public List<LauncherItem> GetSubmenuPreviewItems(LauncherItem parentItem, int maxCount = 5)
        {
            if (parentItem == null) return new List<LauncherItem>();
            return _allItems
                .Where(i => i.ParentId == parentItem.Id)
                .OrderBy(i => i.Position)
                .Take(maxCount)
                .ToList();
        }

        public void ReorderCurrentPageItems(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= CurrentPageItems.Count ||
                targetIndex < 0 || targetIndex >= CurrentPageItems.Count ||
                sourceIndex == targetIndex)
                return;

            int pageSize = ActiveTheme.DensityMode == "Compact" ? 18 : 15;
            int baseOffset = CurrentPageIndex * pageSize;

            List<LauncherItem> activeList;
            if (IsSubmenu && _navStack.Count > 0)
            {
                int currentParentId = _navStack.Peek().parentId;
                activeList = _allItems.Where(i => i.ParentId == currentParentId).OrderBy(i => i.Position).ToList();
            }
            else
            {
                var cat = (Categories.Count > CurrentCategoryIndex) ? Categories[CurrentCategoryIndex] : null;
                if (cat != null && cat.Id > 1)
                {
                    activeList = _allItems.Where(i => i.CategoryId == cat.Id && i.ParentId == 0).OrderBy(i => i.Position).ToList();
                }
                else
                {
                    activeList = _allItems.Where(i => (i.CategoryId <= 1 || i.IsFavorite || i.IsUserAdded) && i.ParentId == 0).OrderBy(i => i.Position).ToList();
                }
            }

            int globalSource = baseOffset + sourceIndex;
            int globalTarget = baseOffset + targetIndex;

            if (globalSource >= 0 && globalSource < activeList.Count && globalTarget >= 0 && globalTarget < activeList.Count)
            {
                var moved = activeList[globalSource];
                activeList.RemoveAt(globalSource);
                activeList.Insert(globalTarget, moved);

                for (int p = 0; p < activeList.Count; p++)
                {
                    activeList[p].Position = p + 1;
                }

                _itemRepo.UpdatePositions(activeList);
            }

            _allItems = _itemRepo.GetAll();
            RefreshPageItems();
        }

        [RelayCommand]
        public void LaunchItem(LauncherItem item)
        {
            if (item == null) return;

            if (string.Equals(item.Type, "SUBMENU", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Type, "FOLDER", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlaySubmenuDive();
                _navStack.Push((item.Id, item.Name));
                IsSubmenu = true;
                BreadcrumbTitle = item.Name;
                CurrentPageIndex = 0;
                RefreshPageItems();
                return;
            }

            if (string.Equals(item.Type, "CALCULATOR_RESULT", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                _clipboardService?.PasteItem(item.Target);
                RequestShowToast?.Invoke($"✅ Sonuç Kopyalandı: {item.Target}", "#10B981");
                return;
            }

            if (string.Equals(item.Type, "HOTKEY", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                RequestClose?.Invoke();
                HotkeyService.SimulateHotkey(item.Target);
                return;
            }

            if (string.Equals(item.Type, "SNIPPET", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                RequestClose?.Invoke();
                HotkeyService.PasteSnippet(item.Target);
                return;
            }

            if (string.Equals(item.Type, "WINDOW_TILING", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                switch (item.Target)
                {
                    case "SNAP_LEFT":
                        TilingService.SnapActiveWindowLeft();
                        RequestShowToast?.Invoke("⬅️ Pencere Sola Yaslandı", "#38BDF8");
                        break;
                    case "SNAP_RIGHT":
                        TilingService.SnapActiveWindowRight();
                        RequestShowToast?.Invoke("➡️ Pencere Sağa Yaslandı", "#38BDF8");
                        break;
                    case "ALWAYS_ON_TOP":
                        bool pinned = TilingService.ToggleAlwaysOnTop();
                        RequestShowToast?.Invoke(pinned ? "📌 Pencere Üste Sabitlendi" : "📌 Üste Sabitleme Kaldırıldı", "#F59E0B");
                        break;
                    case "GLASS_OPACITY":
                        bool glass = TilingService.ToggleGlassTransparency();
                        RequestShowToast?.Invoke(glass ? "🔮 Cam Şeffaflığı (%84) Aktif" : "🔮 Pencere Opaklığı (%100) Normal", "#A855F7");
                        break;
                    case "NEXT_MONITOR":
                        string monMsg = WorkspaceService.MoveWindowToNextMonitor(TilingService.LastActiveWindow);
                        RequestShowToast?.Invoke(monMsg, "#38BDF8");
                        break;
                }
                return;
            }

            if (string.Equals(item.Type, "WORKSPACE_ACTION", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                if (item.Target == "SAVE_WORKSPACE")
                {
                    string saveMsg = WorkspaceService.SaveWorkspaceSnapshot("Varsayılan");
                    RequestShowToast?.Invoke(saveMsg, "#10B981");
                }
                else if (item.Target == "RESTORE_WORKSPACE")
                {
                    string restoreMsg = WorkspaceService.RestoreWorkspaceSnapshot("Varsayılan");
                    RequestShowToast?.Invoke(restoreMsg, "#38BDF8");
                }
                return;
            }

            if (string.Equals(item.Type, "AUDIO_DEVICE", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                string devMsg = PerAppAudioService.CycleDefaultPlaybackDevice();
                RequestShowToast?.Invoke(devMsg, "#EC4899");
                return;
            }

            if (string.Equals(item.Type, "APP_AUDIO_SESSION", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                if (int.TryParse(item.Target, out int pid))
                {
                    bool nowMuted = PerAppAudioService.ToggleAppMute(pid);
                    RequestShowToast?.Invoke(nowMuted ? "🔇 Uygulama Sesi Kapatıldı" : "🔊 Uygulama Sesi Açıldı", "#EC4899");
                    RefreshPageItems();
                }
                return;
            }

            if (string.Equals(item.Type, "GAMING_BOOST", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayGamingBoost();
                var boost = TilingService.RunGamingBoost();
                RequestShowToast?.Invoke($"🚀 Gaming Boost: +{boost.FreedMegabytes} MB RAM Temizlendi ({boost.TrimmedProcessCount} süreç)", "#10B981");
                return;
            }

            if (string.Equals(item.Type, "CLIPBOARD", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
                RequestClose?.Invoke();
                _clipboardService?.PasteItem(item.Target);
                return;
            }

            if (string.Equals(item.Type, "WINDOW", StringComparison.OrdinalIgnoreCase) && long.TryParse(item.Target, out long hWndVal))
            {
                SoundService.PlayLaunchConfirm();
                _windowSwitcher.SwitchToWindow((IntPtr)hWndVal);
                RequestClose?.Invoke();
                return;
            }

            if (string.Equals(item.Type, "COMMAND_THEME", StringComparison.OrdinalIgnoreCase))
            {
                SoundService.PlayLaunchConfirm();
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

            SoundService.PlayLaunchConfirm();
            if (item.CategoryId == 2 ||
                string.Equals(item.Type, "GAME", StringComparison.OrdinalIgnoreCase) ||
                (CurrentCategory?.Name?.Contains("Oyun", StringComparison.OrdinalIgnoreCase) == true))
            {
                PlaytimeService.RecordGameLaunch(item.Name, item.Target);
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
