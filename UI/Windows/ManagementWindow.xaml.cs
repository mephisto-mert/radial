using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Commands;
using RadialLauncher.Services.Icons;
using RadialLauncher.Services.Import;
using RadialLauncher.Services.Localization;
using RadialLauncher.Services.Scanning;
using RadialLauncher.Services.Sync;
using RadialLauncher.Services.Themes;
using RadialLauncher.Services.Updates;
using RadialLauncher.Services.Windows;
using RadialLauncher.UI.Helpers;
using RadialLauncher.UI.ViewModels;
using Serilog;

namespace RadialLauncher.UI.Windows
{
    public class LauncherItemViewModel
    {
        private readonly IIconExtractor? _iconExtractor;
        public LauncherItem Item { get; set; }
        public string IsFavoriteText => Item.IsFavorite ? "⭐" : "—";
        public int LaunchCount => Math.Max(Item.UseCount, Item.LaunchCount);
        public int Position => Item.Position;
        public string Name => Item.Name;
        public string Type => Item.Type;
        public string Target => Item.Target;
        public string CategoryName { get; set; } = "Genel";

        public ImageSource? Icon
        {
            get
            {
                var extractor = _iconExtractor ?? (App.ServiceProvider?.GetService(typeof(IIconExtractor)) as IIconExtractor);
                if (extractor == null) return null;

                var action = extractor.GetActionIcon(Item.Target) ?? extractor.GetActionIcon(Item.Name);
                if (action != null) return action;

                if (!string.IsNullOrEmpty(Item.IconPath) && File.Exists(Item.IconPath))
                {
                    var f = extractor.GetIconForFile(Item.IconPath);
                    if (f != null) return f;
                }
                if (Item.Type == "URL")
                {
                    var fav = extractor.GetFaviconForUrl(Item.Target);
                    if (fav != null) return fav;
                }
                var brand = extractor.GetBrandIcon(Item.Name, Item.Target);
                if (brand != null) return brand;
                if (!string.IsNullOrEmpty(Item.Target))
                {
                    var tf = extractor.GetIconForFile(Item.Target);
                    if (tf != null) return tf;
                }
                return extractor.CreateMonogramIcon(Item.Name, Color.FromRgb(88, 140, 236));
            }
        }

        public LauncherItemViewModel(LauncherItem item, string categoryName, IIconExtractor? iconExtractor = null)
        {
            Item = item;
            CategoryName = categoryName;
            _iconExtractor = iconExtractor;
        }
    }

    public partial class ManagementWindow : Window
    {
        private readonly ManagementViewModel _viewModel;
        private readonly IStartupManager _startupManager;
        private readonly IThemeService _themeService;
        private readonly ISyncService _syncService;
        private readonly ICommandPaletteService? _commandPaletteService;
        private bool _isUpdatingShortcutCombo = false;

        public string ActionFavoriteToolTip => LocalizationService.Instance.GetString("Action_Favorite", "Favorite");
        public string ActionEditToolTip => LocalizationService.Instance.GetString("Action_Edit", "Edit");
        public string ActionDeleteToolTip => LocalizationService.Instance.GetString("Action_Delete", "Delete");
        public string RenameCategoryToolTip => LocalizationService.Instance.GetString("Rename_Category", "Rename Category");

        public ManagementWindow() : this(
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ManagementViewModel>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupManager>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISyncService>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ICommandPaletteService>(App.ServiceProvider)
                : null)
        {
        }

        private readonly Action<Theme> _onThemeChangedHandler;
        private readonly Action _onLanguageChangedHandler;

        public ManagementWindow(ManagementViewModel viewModel, IStartupManager startupManager, IThemeService themeService, ISyncService syncService, ICommandPaletteService? commandPaletteService = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
            _commandPaletteService = commandPaletteService ?? (App.ServiceProvider?.GetService(typeof(ICommandPaletteService)) as ICommandPaletteService);

            InitializeComponent();

            DataContext = _viewModel;
            Loaded += ManagementWindow_Loaded;

            _onThemeChangedHandler = t => Dispatcher.Invoke(() => ApplyThemeVisuals(t));
            _onLanguageChangedHandler = () => Dispatcher.Invoke(ApplyLocalization);

            _themeService.OnThemeChanged += _onThemeChangedHandler;
            LocalizationService.Instance.OnLanguageChanged += _onLanguageChangedHandler;

            Closed += (s, e) =>
            {
                _themeService.OnThemeChanged -= _onThemeChangedHandler;
                LocalizationService.Instance.OnLanguageChanged -= _onLanguageChangedHandler;
            };
        }

        private void ManagementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalization();
            ApplyThemeVisuals(_themeService.GetCurrentTheme());
            LoadCategories();
            LoadThemes();
            LoadOpacityState();
            RefreshGrid();
            LoadStartupState();
            LoadShortcutState();
            LoadDensityState();
            LoadLanguageState();
            LoadSteamGridState();
            UpdateBackupStatusLabel();
            if (AutoCheckUpdatesCheck != null)
            {
                AutoCheckUpdatesCheck.IsChecked = _themeService.GetAutoCheckUpdates();
            }
        }

        private void ApplyThemeVisuals(Theme theme)
        {
            if (theme == null) return;

            bool isLight = ThemeContrastHelper.IsLightColor(theme.BackgroundColor);
            var textBrush = ThemeContrastHelper.GetContrastTextBrush(theme.BackgroundColor);
            var borderBrush = ThemeContrastHelper.GetContrastBorderBrush(theme.BackgroundColor, 40, 50);

            var bgBrush = new SolidColorBrush(theme.BackgroundColor);
            var panelBrush = new SolidColorBrush(theme.IconBackgroundColor);

            var itemTextBrush = ThemeContrastHelper.GetContrastTextBrush(theme.IconBackgroundColor);
            var itemBorderBrush = ThemeContrastHelper.GetContrastBorderBrush(theme.IconBackgroundColor, 40, 50);

            this.Background = bgBrush;
            this.Foreground = textBrush;

            if (MainTabs != null)
            {
                MainTabs.Background = panelBrush;
                MainTabs.BorderBrush = borderBrush;
                MainTabs.Foreground = textBrush;
            }

            if (ItemsGrid != null)
            {
                ItemsGrid.Background = bgBrush;
                ItemsGrid.RowBackground = bgBrush;
                byte altR = (byte)Math.Clamp(theme.BgR + (isLight ? -10 : 10), 0, 255);
                byte altG = (byte)Math.Clamp(theme.BgG + (isLight ? -10 : 10), 0, 255);
                byte altB = (byte)Math.Clamp(theme.BgB + (isLight ? -10 : 10), 0, 255);
                ItemsGrid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(altR, altG, altB));
                ItemsGrid.Foreground = textBrush;
                ItemsGrid.BorderBrush = borderBrush;
                ItemsGrid.HorizontalGridLinesBrush = new SolidColorBrush(Color.FromArgb(20, theme.TextR, theme.TextG, theme.TextB));
            }

            if (CategoryFilterCombo != null)
            {
                CategoryFilterCombo.Background = panelBrush;
                CategoryFilterCombo.Foreground = itemTextBrush;
                CategoryFilterCombo.BorderBrush = itemBorderBrush;
            }
            if (SearchBox != null)
            {
                SearchBox.Background = panelBrush;
                SearchBox.Foreground = itemTextBrush;
                SearchBox.BorderBrush = itemBorderBrush;
            }
            if (ThemesListBox != null)
            {
                ThemesListBox.Background = panelBrush;
                ThemesListBox.Foreground = itemTextBrush;
                ThemesListBox.BorderBrush = itemBorderBrush;
            }
            if (DensityCombo != null)
            {
                DensityCombo.Background = panelBrush;
                DensityCombo.Foreground = itemTextBrush;
                DensityCombo.BorderBrush = itemBorderBrush;
            }
            if (ShortcutCombo != null)
            {
                ShortcutCombo.Background = panelBrush;
                ShortcutCombo.Foreground = itemTextBrush;
                ShortcutCombo.BorderBrush = itemBorderBrush;
            }
            if (LanguageCombo != null)
            {
                LanguageCombo.Background = panelBrush;
                LanguageCombo.Foreground = itemTextBrush;
                LanguageCombo.BorderBrush = itemBorderBrush;
            }
        }

        private void LoadCategories()
        {
            if (CategoryFilterCombo == null || _viewModel == null) return;
            var loc = LocalizationService.Instance;
            int currentSelectedId = 0;
            if (CategoryFilterCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is int id)
            {
                currentSelectedId = id;
            }

            CategoryFilterCombo.Items.Clear();
            CategoryFilterCombo.Items.Add(new ComboBoxItem { Content = loc.GetString("All_Categories", "All Categories"), Tag = 0 });

            int newIndex = 0;
            for (int i = 0; i < _viewModel.Categories.Count; i++)
            {
                var cat = _viewModel.Categories[i];
                CategoryFilterCombo.Items.Add(new ComboBoxItem { Content = loc.GetCategoryDisplayName(cat), Tag = cat.Id });
                if (cat.Id == currentSelectedId)
                {
                    newIndex = i + 1;
                }
            }
            CategoryFilterCombo.SelectedIndex = newIndex;
        }

        private void BtnRenameCategory_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            if (CategoryFilterCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is int catId && catId > 0)
            {
                var targetCat = _viewModel.Categories.FirstOrDefault(c => c.Id == catId);
                if (targetCat == null) return;

                var dlg = new CategoryRenameDialog(targetCat.Name, _viewModel.Categories.Select(c => c.Name))
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    string newName = dlg.NewCategoryName;
                    bool ok = _viewModel.RenameCategory(catId, newName);
                    if (ok)
                    {
                        LoadCategories();
                        for (int i = 0; i < CategoryFilterCombo.Items.Count; i++)
                        {
                            if (CategoryFilterCombo.Items[i] is ComboBoxItem item && item.Tag is int id && id == catId)
                            {
                                CategoryFilterCombo.SelectedIndex = i;
                                break;
                            }
                        }
                        RefreshGrid();
                        StatusText.Text = string.Format(loc.GetString("Cat_Renamed_Status", "Category renamed: {0}"), newName);
                    }
                    else
                    {
                        MessageBox.Show(loc.GetString("Cat_Rename_Failed", "Failed to rename category."), loc.GetString("Error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(loc.GetString("Cat_Select_To_Rename", "Please select a specific category from the dropdown to rename."), loc.GetString("Warning", "Warning"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadThemes()
        {
            if (ThemesListBox == null || _viewModel == null) return;
            string currentId = _viewModel.SelectedTheme?.Id ?? "Dark";
            ThemesListBox.Items.Clear();
            int selectedIdx = 0;
            for (int i = 0; i < _viewModel.Themes.Count; i++)
            {
                var t = _viewModel.Themes[i];
                var lbi = new ListBoxItem
                {
                    Content = t.DisplayName,
                    Tag = t.Id
                };
                ThemesListBox.Items.Add(lbi);
                if (string.Equals(t.Id, currentId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, currentId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIdx = i;
                }
            }
            ThemesListBox.SelectedIndex = selectedIdx;

            if (LivePreviewControl != null)
            {
                LivePreviewControl.Theme = _viewModel.SelectedTheme;
            }
            UpdatePaletteSwatches(_viewModel.SelectedTheme);
            if (ReduceMotionCheck != null)
            {
                ReduceMotionCheck.IsChecked = _viewModel.SelectedTheme?.ReduceMotion ?? false;
            }
        }

        private void LoadOpacityState()
        {
            double opacity = _themeService.GetRadialOpacity();
            int percent = (int)Math.Round(opacity * 100.0);
            if (OpacitySlider != null)
            {
                OpacitySlider.Value = Math.Clamp(percent, 20, 100);
            }
            if (OpacityValueText != null)
            {
                OpacityValueText.Text = $"%{percent}";
            }
        }

        private void LoadDensityState()
        {
            if (DensityCombo == null) return;
            string mode = _viewModel.SelectedTheme?.DensityMode ?? "Expanded";
            DensityCombo.SelectedIndex = (mode == "Compact") ? 1 : 0;
        }

        private void LoadShortcutState()
        {
            if (ShortcutCombo == null) return;
            _isUpdatingShortcutCombo = true;
            try
            {
                var loc = LocalizationService.Instance;
                int currentSelection = ShortcutCombo.SelectedIndex;
                ShortcutCombo.Items.Clear();
                ShortcutCombo.Items.Add(loc.GetString("Mouse_Middle", "Middle Click (Mouse Wheel)"));
                ShortcutCombo.Items.Add(loc.GetString("Mouse_XButton1", "Mouse 4 (Back Button - XButton1)"));
                ShortcutCombo.Items.Add(loc.GetString("Mouse_XButton2", "Mouse 5 (Forward Button - XButton2)"));
                ShortcutCombo.Items.Add(loc.GetString("Mouse_Ctrl_Right", "Ctrl + Right Click"));
                ShortcutCombo.Items.Add(loc.GetString("Mouse_Shift_Right", "Shift + Right Click"));
                ShortcutCombo.Items.Add(loc.GetString("Mouse_Alt_Right", "Alt + Right Click"));
                ShortcutCombo.Items.Add(loc.GetString("Mouse_Ctrl_XButton1", "Ctrl + Mouse 4"));
                ShortcutCombo.Items.Add(loc.GetString("Shortcut_AltSpace", "Alt + Space"));
                ShortcutCombo.Items.Add(loc.GetString("Shortcut_CtrlSpace", "Ctrl + Space"));
                ShortcutCombo.Items.Add(loc.GetString("Shortcut_F4", "F4 Key"));
                ShortcutCombo.Items.Add(loc.GetString("Shortcut_Tilde", "~ (Tilde Key)"));

                string sc = _themeService.GetActivationShortcut();
                ShortcutCombo.SelectedIndex = sc switch
                {
                    "MiddleClick" => 0,
                    "XButton1" => 1,
                    "XButton2" => 2,
                    "CtrlRightClick" => 3,
                    "ShiftRightClick" => 4,
                    "AltRightClick" => 5,
                    "Ctrl+XButton1" => 6,
                    "AltSpace" => 7,
                    "CtrlSpace" => 8,
                    "F4" => 9,
                    "Tilde" => 10,
                    _ => -1
                };

                if (ActiveShortcutLabel != null)
                {
                    ActiveShortcutLabel.Text = $"{loc.GetString("Active_Shortcut_Label", "Active Shortcut:")} {ShortcutAssignWindow.ToFriendlyName(sc)} ({sc})";
                }
            }
            finally
            {
                _isUpdatingShortcutCombo = false;
            }
        }

        private void LoadStartupState()
        {
            RunOnStartupCheck.IsChecked = _startupManager.IsRunOnStartup();
        }

        private void RefreshGrid()
        {
            if (_viewModel == null || CategoryFilterCombo == null || ItemsGrid == null || StatusText == null) return;
            var loc = LocalizationService.Instance;
            var catMap = _viewModel.Categories?.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => loc.GetCategoryDisplayName(g.First())) ?? new Dictionary<int, string>();
            var query = SearchBox?.Text?.Trim() ?? "";
            int selectedCatId = 0;
            if (CategoryFilterCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is int id)
            {
                selectedCatId = id;
            }

            if (selectedCatId == 0)
            {
                _viewModel.SelectedCategory = null;
            }
            else
            {
                _viewModel.SelectedCategory = _viewModel.Categories?.FirstOrDefault(c => c.Id == selectedCatId)
                    ?? new Category { Id = selectedCatId, Name = loc.GetString("Category", "Category") };
            }

            _viewModel.FilterQuery = query;
            _viewModel.RefreshItems();

            string defaultCatName = loc.GetString("Cat_MostUsed", "General");
            var items = _viewModel.Items.Select(i => new LauncherItemViewModel(i, catMap.GetValueOrDefault(i.CategoryId, defaultCatName))).ToList();
            ItemsGrid.ItemsSource = items;

            if (items.Count == 0)
            {
                StatusText.Text = loc.GetString("Status_No_Items", "No items to display.");
            }
            else
            {
                StatusText.Text = string.Format(loc.GetString("Status_Total_Items", "Total {0} items listed."), items.Count);
            }
        }

        private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshGrid();

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = SearchBox?.Text?.Trim() ?? "";
            if (_commandPaletteService != null && q.Length > 1 && q.StartsWith("=", StringComparison.Ordinal))
            {
                if (_commandPaletteService.TryHandle(q, out string msg))
                {
                    StatusText.Text = msg;
                    return;
                }
            }
            RefreshGrid();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _commandPaletteService != null && !string.IsNullOrWhiteSpace(SearchBox?.Text))
            {
                string q = SearchBox.Text.Trim();
                if (q.StartsWith("=", StringComparison.Ordinal) ||
                    q.StartsWith(">", StringComparison.Ordinal) ||
                    q.StartsWith("!", StringComparison.Ordinal) ||
                    q.StartsWith("?", StringComparison.Ordinal))
                {
                    if (_commandPaletteService.TryHandle(q, out string msg))
                    {
                        StatusText.Text = msg;
                        e.Handled = true;
                    }
                }
            }
        }

        private void ManagementWindow_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void ManagementWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    int addedCount = 0;
                    foreach (var file in files)
                    {
                        var (ok, _, item) = LauncherDropParser.BuildItem(file);
                        if (ok && item != null)
                        {
                            if (_viewModel.SelectedCategory != null && _viewModel.SelectedCategory.Id > 0)
                            {
                                item.CategoryId = _viewModel.SelectedCategory.Id;
                            }
                            _viewModel.AddItem(item);
                            addedCount++;
                        }
                    }
                    if (addedCount > 0)
                    {
                        RefreshGrid();
                        var loc = LocalizationService.Instance;
                        StatusText.Text = string.Format(loc.GetString("Status_Items_Dropped", "Added {0} dropped item(s)."), addedCount);
                    }
                }
            }
        }

        private void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddItemWindow();
            win.Owner = this;
            if (win.ShowDialog() == true && win.CreatedItem != null)
            {
                _viewModel.AddItem(win.CreatedItem);
                RefreshGrid();
                var loc = LocalizationService.Instance;
                StatusText.Text = string.Format(loc.GetString("Status_Item_Added", "Added '{0}' successfully."), win.CreatedItem.Name);
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LauncherItemViewModel lvm)
            {
                var win = new EditItemWindow(lvm.Item);
                win.Owner = this;
                if (win.ShowDialog() == true)
                {
                    _viewModel.UpdateItem(win.Item);
                    RefreshGrid();
                    var loc = LocalizationService.Instance;
                    StatusText.Text = string.Format(loc.GetString("Status_Item_Updated", "Updated '{0}' successfully."), win.Item.Name);
                }
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LauncherItemViewModel lvm)
            {
                var loc = LocalizationService.Instance;
                string confirmMsg = string.Format(loc.GetString("Delete_Confirm", "Delete '{0}'?"), lvm.Name);
                string confirmTitle = loc.GetString("Delete_Confirm_Title", "Delete Confirmation");

                if (MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _viewModel.DeleteItem(lvm.Item);
                    RefreshGrid();
                }
            }
        }

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LauncherItemViewModel lvm)
            {
                _viewModel.ToggleFavorite(lvm.Item);
                RefreshGrid();
            }
        }

        private void ThemesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null || LivePreviewControl == null) return;
            var lb = sender as ListBox ?? ThemesListBox;
            string? selectedId = null;
            if (lb?.SelectedItem is ListBoxItem lbi && lbi.Tag is string tid)
            {
                selectedId = tid;
            }
            else if (lb?.SelectedItem is string tname)
            {
                selectedId = tname;
            }

            if (!string.IsNullOrEmpty(selectedId))
            {
                var theme = _viewModel.Themes.FirstOrDefault(t => 
                    string.Equals(t.Id, selectedId, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(t.Name, selectedId, StringComparison.OrdinalIgnoreCase));
                if (theme != null)
                {
                    _viewModel.ApplyTheme(theme);
                    LivePreviewControl.Theme = theme;
                    UpdatePaletteSwatches(theme);
                    if (ReduceMotionCheck != null)
                    {
                        ReduceMotionCheck.IsChecked = theme.ReduceMotion;
                    }
                }
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_viewModel == null || _themeService == null) return;
            double opacity = e.NewValue / 100.0;
            _themeService.SetRadialOpacity(opacity);
            if (OpacityValueText != null)
            {
                OpacityValueText.Text = $"%{(int)e.NewValue}";
            }
            if (LivePreviewControl != null)
            {
                LivePreviewControl.Theme = _themeService.GetCurrentTheme();
            }
        }

        private void UpdatePaletteSwatches(Theme? theme)
        {
            if (theme == null) return;
            if (SwatchAccent1 != null) SwatchAccent1.Fill = new SolidColorBrush(theme.AccentColor);
            if (SwatchAccent2 != null) SwatchAccent2.Fill = new SolidColorBrush(theme.Accent2Color);
            if (SwatchBackground != null) SwatchBackground.Fill = new SolidColorBrush(theme.BackgroundColor);
            if (SwatchCard != null) SwatchCard.Fill = new SolidColorBrush(theme.IconBackgroundColor);
        }

        private void ReduceMotion_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _themeService == null) return;
            bool reduce = ReduceMotionCheck?.IsChecked ?? false;
            var t = _themeService.GetCurrentTheme();
            if (t != null)
            {
                t.ReduceMotion = reduce;
                _themeService.SaveCustomTheme(t);
            }
        }

        private void DensityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null || _themeService == null) return;
            var cb = sender as ComboBox ?? DensityCombo;
            if (cb != null)
            {
                string mode = cb.SelectedIndex == 1 ? "Compact" : "Expanded";
                var t = _themeService.GetCurrentTheme();
                if (t != null)
                {
                    t.DensityMode = mode;
                    _themeService.SaveCustomTheme(t);
                }
            }
        }

        private void ShortcutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingShortcutCombo) return;
            if (_viewModel == null || _themeService == null) return;
            var cb = sender as ComboBox ?? ShortcutCombo;
            if (cb != null && cb.SelectedIndex >= 0)
            {
                string sc = cb.SelectedIndex switch
                {
                    0 => "MiddleClick",
                    1 => "XButton1",
                    2 => "XButton2",
                    3 => "CtrlRightClick",
                    4 => "ShiftRightClick",
                    5 => "AltRightClick",
                    6 => "Ctrl+XButton1",
                    7 => "AltSpace",
                    8 => "CtrlSpace",
                    9 => "F4",
                    10 => "Tilde",
                    _ => "MiddleClick"
                };
                _themeService.SetActivationShortcut(sc);
                LoadShortcutState();
            }
        }

        private void AssignCustomShortcut_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            string current = _themeService.GetActivationShortcut();
            var win = new ShortcutAssignWindow(current);
            win.Owner = this;
            if (win.ShowDialog() == true && !string.IsNullOrWhiteSpace(win.SelectedShortcut))
            {
                string clean = win.SelectedShortcut.Trim();
                _themeService.SetActivationShortcut(clean);
                LoadShortcutState();
                StatusText.Text = string.Format(loc.GetString("Shortcut_Assigned_Status", "New shortcut assigned: {0}"), ShortcutAssignWindow.ToFriendlyName(clean));
                MessageBox.Show(string.Format(loc.GetString("Shortcut_Saved_Msg", "Shortcut saved successfully:\n\n{0}\n({1})"), ShortcutAssignWindow.ToFriendlyName(clean), clean), loc.GetString("Shortcut_Updated_Title", "Shortcut Updated"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RunOnStartupCheck_Click(object sender, RoutedEventArgs e)
        {
            bool enable = RunOnStartupCheck.IsChecked ?? false;
            _startupManager.SetRunOnStartup(enable);
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            StatusText.Text = loc.GetString("MsgScanningPc", "Scanning computer...");
            await _viewModel.ScanPc();
            RefreshGrid();
            StatusText.Text = _viewModel.StatusMessage;
        }

        private async void CreateLocalBackup_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            StatusText.Text = loc.GetString("MsgCreatingBackup", "Creating local backup...");
            var result = await _syncService.CreateLocalBackupAsync();
            if (result.success)
            {
                UpdateBackupStatusLabel();
                StatusText.Text = loc.GetString("MsgBackupDone", "Local backup completed.");
                MessageBox.Show(string.Format(loc.GetString("MsgBackupDoneDetails", "Backup completed successfully:\n{0}"), result.filePath), loc.GetString("MsgBackupTakenTitle", "Backup Created"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusText.Text = loc.GetString("MsgBackupFailed", "Failed to create backup.");
                MessageBox.Show(loc.GetString("MsgBackupFailedDetails", "An error occurred while creating local backup."), loc.GetString("Error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RestoreLocalBackup_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            string backupsDir = RadialLauncher.Services.Data.UserDataPathProvider.Instance.GetBackupsFolder();

            var ofd = new OpenFileDialog
            {
                Title = loc.GetString("Restore_Backup", "Restore from Backup"),
                Filter = "JSON (*.json)|*.json",
                InitialDirectory = Directory.Exists(backupsDir) ? backupsDir : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            if (ofd.ShowDialog() == true)
            {
                string confirmMsg = string.Format(loc.GetString("Restore_Confirm", "Restore backup '{0}'?\nThis will overwrite current items and settings."), Path.GetFileName(ofd.FileName));
                string confirmTitle = loc.GetString("Restore_Confirm_Title", "Restore Confirmation");

                var confirm = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    StatusText.Text = loc.GetString("Restoring", "Restoring backup...");
                    bool ok = await _syncService.RestoreFromLocalBackupAsync(ofd.FileName);
                    if (ok)
                    {
                        _viewModel.LoadInitialData();
                        LoadCategories();
                        LoadThemes();
                        LoadOpacityState();
                        LoadStartupState();
                        LoadShortcutState();
                        LoadDensityState();
                        LoadLanguageState();
                        ApplyThemeVisuals(_themeService.GetCurrentTheme());
                        ApplyLocalization();
                        RefreshGrid();
                        UpdateBackupStatusLabel();
                        if (AutoCheckUpdatesCheck != null)
                        {
                            AutoCheckUpdatesCheck.IsChecked = _themeService.GetAutoCheckUpdates();
                        }
                        string successMsg = loc.GetString("Restore_Success", "Backup successfully restored and applied.");
                        StatusText.Text = successMsg;
                        MessageBox.Show(successMsg, loc.GetString("Success", "Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusText.Text = loc.GetString("Error", "Restore failed.");
                        MessageBox.Show(loc.GetString("Restore_Error", "Backup file could not be read or format is invalid."), loc.GetString("Error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void UpdateBackupStatusLabel()
        {
            if (BackupStatusLabel == null) return;
            var loc = LocalizationService.Instance;
            var backups = _syncService.GetLocalBackups();
            if (backups.Count > 0)
            {
                var latest = backups[0];
                string timeStr = File.Exists(latest) ? File.GetCreationTime(latest).ToString("yyyy-MM-dd HH:mm:ss") : "Ready";
                BackupStatusLabel.Text = string.Format(loc.GetString("Backup_Status_Count", "Total {0} local backups available. Latest: {1} ({2})"), backups.Count, timeStr, Path.GetFileName(latest));
            }
            else
            {
                BackupStatusLabel.Text = loc.GetString("Backup_Status_None", "No local backups created yet.");
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "radial_backup.json" };
            if (sfd.ShowDialog() == true)
            {
                await _viewModel.ExportData(sfd.FileName);
                StatusText.Text = _viewModel.StatusMessage;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (ofd.ShowDialog() == true)
            {
                await _viewModel.ImportData(ofd.FileName);
                _viewModel.LoadInitialData();
                LoadCategories();
                LoadThemes();
                LoadOpacityState();
                LoadStartupState();
                LoadShortcutState();
                LoadDensityState();
                LoadLanguageState();
                ApplyThemeVisuals(_themeService.GetCurrentTheme());
                ApplyLocalization();
                RefreshGrid();
                UpdateBackupStatusLabel();
                if (AutoCheckUpdatesCheck != null)
                {
                    AutoCheckUpdatesCheck.IsChecked = _themeService.GetAutoCheckUpdates();
                }
                StatusText.Text = _viewModel.StatusMessage;
            }
        }

        private void AutoCheckUpdatesCheck_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            bool isChecked = AutoCheckUpdatesCheck.IsChecked == true;
            _themeService.SetAutoCheckUpdates(isChecked);
            StatusText.Text = isChecked ? loc.GetString("AutoCheck_Enabled", "Automatic update check enabled.") : loc.GetString("AutoCheck_Disabled", "Automatic update check disabled.");
        }

        private async void CheckUpdatesNowBtn_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            CheckUpdatesNowBtn.IsEnabled = false;
            UpdateCheckStatusLabel.Text = loc.GetString("Checking_Release", "Checking GitHub Releases...");
            StatusText.Text = loc.GetString("Checking_Updates", "Checking for updates...");

            try
            {
                var updateService = App.ServiceProvider?.GetService(typeof(IUpdateCheckService)) as IUpdateCheckService;
                if (updateService == null)
                {
                    UpdateCheckStatusLabel.Text = loc.GetString("Update_Service_NotFound", "Update service not found.");
                    return;
                }

                var info = await updateService.CheckForUpdatesAsync();
                if (info == null)
                {
                    UpdateCheckStatusLabel.Text = loc.GetString("Update_Server_Unreachable", "Could not reach update server. Please check internet connection.");
                    StatusText.Text = loc.GetString("Update_Check_Failed", "Update check failed.");
                }
                else if (info.IsUpdateAvailable)
                {
                    UpdateCheckStatusLabel.Text = string.Format(loc.GetString("Update_Available_Label", "🎉 A new version is available: v{0}\n{1}"), info.LatestVersion, info.ReleaseUrl);
                    StatusText.Text = string.Format(loc.GetString("Update_Available_Status", "New version v{0} available!"), info.LatestVersion);
                    var res = MessageBox.Show(string.Format(loc.GetString("Update_Dialog_Body", "A new version has been released (v{0}).\n\nWould you like to open the download page?"), info.LatestVersion), loc.GetString("Update_Dialog_Title", "Update Available"), MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (res == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(info.ReleaseUrl))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = info.ReleaseUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    UpdateCheckStatusLabel.Text = string.Format(loc.GetString("Update_Latest_Label", "✅ You are using the latest version (v{0})."), info.CurrentVersion);
                    StatusText.Text = loc.GetString("Update_App_UpToDate", "Application is up to date.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking for updates from UI");
                UpdateCheckStatusLabel.Text = loc.GetString("Update_Error_Label", "An error occurred during update check.");
                StatusText.Text = loc.GetString("Update_Error_Status", "Update error.");
            }
            finally
            {
                CheckUpdatesNowBtn.IsEnabled = true;
            }
        }

        private void OpenLogsFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            try
            {
                string logDir = RadialLauncher.Services.Data.UserDataPathProvider.Instance.GetLogsFolder();

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open logs folder");
                MessageBox.Show(loc.GetString("Logs_Open_Failed", "Failed to open logs folder."), "Radial Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyDiagnosticsBtn_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            try
            {
                var diag = new System.Text.StringBuilder();
                diag.AppendLine("=== Radial Launcher Diagnostics ===");
                diag.AppendLine($"App Version: 1.0.0");
                diag.AppendLine($"OS: {Environment.OSVersion}");
                diag.AppendLine($"64-Bit OS: {Environment.Is64BitOperatingSystem}");
                diag.AppendLine($"64-Bit Process: {Environment.Is64BitProcess}");
                diag.AppendLine($".NET Runtime: {Environment.Version}");
                diag.AppendLine($"Current Theme: {_themeService.GetCurrentTheme()?.Name}");
                diag.AppendLine($"Shortcut: {_themeService.GetActivationShortcut()}");
                diag.AppendLine($"Follow Windows Theme: {_themeService.GetFollowWindowsTheme()}");
                diag.AppendLine($"Extract Accent: {_themeService.GetExtractAccentFromWallpaper()}");
                diag.AppendLine($"Auto Check Updates: {_themeService.GetAutoCheckUpdates()}");
                diag.AppendLine($"Categories Count: {_viewModel.Categories.Count}");
                diag.AppendLine($"Total Items Count: {_viewModel.Items.Count}");
                diag.AppendLine($"Timestamp UTC: {DateTime.UtcNow:O}");

                Clipboard.SetText(diag.ToString());
                StatusText.Text = loc.GetString("Diag_Copied_Status", "Diagnostic information copied to clipboard!");
                MessageBox.Show(loc.GetString("Diag_Copied_Msg", "System diagnostic information copied to clipboard."), loc.GetString("Diag_Title_Short", "Diagnostics"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed copying diagnostics");
            }
        }

        private void ResetSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            string confirmMsg = loc.GetString("Reset_Confirm", "Reset all theme, shortcut, and appearance settings to defaults?\n(Your items and usage counts will be preserved)");
            string confirmTitle = loc.GetString("Reset_Confirm_Title", "Reset Settings");

            var res = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                _themeService.ResetSettingsToDefault();
                _viewModel.LoadInitialData();
                LoadCategories();
                LoadThemes();
                LoadOpacityState();
                LoadStartupState();
                LoadShortcutState();
                LoadDensityState();
                LoadLanguageState();
                ApplyThemeVisuals(_themeService.GetCurrentTheme());
                ApplyLocalization();
                RefreshGrid();
                UpdateBackupStatusLabel();
                if (AutoCheckUpdatesCheck != null)
                {
                    AutoCheckUpdatesCheck.IsChecked = _themeService.GetAutoCheckUpdates();
                }
                StatusText.Text = loc.GetString("Reset_Success_Status", "Settings successfully reset to defaults.");
                MessageBox.Show(loc.GetString("Reset_Success_Msg", "Settings reset to default values."), "Radial Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed resetting settings");
                MessageBox.Show(loc.GetString("Reset_Error_Msg", "An error occurred while resetting settings."), loc.GetString("Error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLanguageState()
        {
            try
            {
                if (LanguageCombo == null) return;
                LanguageCombo.SelectionChanged -= LanguageCombo_SelectionChanged;
                LanguageCombo.Items.Clear();

                var languages = LocalizationService.Instance.SupportedLanguages;
                int selectedIndex = 0;
                string currentLang = LocalizationService.Instance.CurrentLanguage;

                for (int i = 0; i < languages.Count; i++)
                {
                    var lang = languages[i];
                    var item = new ComboBoxItem
                    {
                        Content = $"{lang.Flag}  {lang.DisplayName}",
                        Tag = lang.Code
                    };
                    LanguageCombo.Items.Add(item);

                    if (lang.Code.Equals(currentLang, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                    }
                }

                LanguageCombo.SelectedIndex = selectedIndex;
                LanguageCombo.SelectionChanged += LanguageCombo_SelectionChanged;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed loading language state");
            }
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageCombo?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string langCode)
            {
                LocalizationService.Instance.SetLanguage(langCode);
                var s = _themeService.GetSettings();
                s.Language = langCode;
                _themeService.UpdateSettings(s);
                ApplyLocalization();
                StatusText.Text = string.Format(LocalizationService.Instance.GetString("Language_Changed_Status", "Language changed: {0}"), selectedItem.Content);
            }
        }

        public void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;

            this.Title = loc.GetString("App_Title", "Radial Launcher — Settings & Management");

            // Sidebar navigation
            if (TabItem_Apps != null) TabItem_Apps.Header = loc.GetString("Nav_Apps", "📋  Applications & Shortcuts");
            if (TabItem_Themes != null) TabItem_Themes.Header = loc.GetString("Nav_Themes", "🎨  Themes & Appearance");
            if (TabItem_Shortcuts != null) TabItem_Shortcuts.Header = loc.GetString("Nav_Shortcuts", "⚙️  Shortcuts & Startup");
            if (TabItem_Backups != null) TabItem_Backups.Header = loc.GetString("Nav_Backups", "💾  Backup & Data");
            if (TabItem_System != null) TabItem_System.Header = loc.GetString("Nav_System", "ℹ️  System & Diagnostics");

            // Tab 1: Apps & Items
            if (TxtTab1Title != null) TxtTab1Title.Text = loc.GetString("Tab1_Title", "Application & Shortcut Management");
            if (TxtTab1Sub != null) TxtTab1Sub.Text = loc.GetString("Tab1_Sub", "Manage all apps, games, websites, and folders listed in the radial menu.");
            if (BtnScanPC != null) BtnScanPC.Content = loc.GetString("Scan_PC", "🔍 Scan PC");
            if (BtnAddItem != null) BtnAddItem.Content = loc.GetString("Add_Item", "➕ Add New Item");
            if (TxtCategoryLabel != null) TxtCategoryLabel.Text = loc.GetString("Category", "Category:");
            if (TxtSearchLabel != null) TxtSearchLabel.Text = loc.GetString("Search", "Search:");
            if (ColOrder != null) ColOrder.Header = loc.GetString("Col_Order", "Order");
            if (ColIcon != null) ColIcon.Header = loc.GetString("Col_Icon", "Icon");
            if (ColName != null) ColName.Header = loc.GetString("Col_Name", "Name");
            if (ColType != null) ColType.Header = loc.GetString("Col_Type", "Type");
            if (ColCategory != null) ColCategory.Header = loc.GetString("Col_Category", "Category");
            if (ColLaunchCount != null) ColLaunchCount.Header = loc.GetString("Col_Launches", "Launches");
            if (ColTarget != null) ColTarget.Header = loc.GetString("Col_Target", "Target");
            if (ColActions != null) ColActions.Header = loc.GetString("Col_Actions", "Actions");

            // Tab 2: Themes & Visuals
            if (TxtTab2Title != null) TxtTab2Title.Text = loc.GetString("Tab2_Title", "Themes & Visual Customization");
            if (TxtTab2Sub != null) TxtTab2Sub.Text = loc.GetString("Tab2_Sub", "Select from 8 curated themes, customize radial opacity and density.");
            if (TxtThemesHeader != null) TxtThemesHeader.Text = loc.GetString("Themes_Header", "Curated Themes (8 Themes)");
            if (TxtPreviewHeader != null) TxtPreviewHeader.Text = loc.GetString("Preview_Header", "Live Radial Preview");
            if (TxtOpacityTitle != null) TxtOpacityTitle.Text = loc.GetString("Opacity_Title", "Radial Menu Opacity");
            if (TxtOpacitySub != null) TxtOpacitySub.Text = loc.GetString("Opacity_Desc", "Adjust background transparency level of the circular overlay.");
            if (TxtDensityTitle != null) TxtDensityTitle.Text = loc.GetString("Density_Title", "Ring Density Mode");
            if (TxtDensitySub != null) TxtDensitySub.Text = loc.GetString("Density_Desc", "Number of items displayed per circular ring page.");
            if (DensityItemExpanded != null) DensityItemExpanded.Content = loc.GetString("Density_Expanded", "Expanded (15 Items)");
            if (DensityItemCompact != null) DensityItemCompact.Content = loc.GetString("Density_Compact", "Compact (18 Items)");
            if (TxtAccessTitle != null) TxtAccessTitle.Text = loc.GetString("Access_Title", "Accessibility & Performance");
            if (TxtAccessSub != null) TxtAccessSub.Text = loc.GetString("Access_Sub", "Reduce motion and simplify animations for low-spec systems.");
            if (ReduceMotionCheck != null) ReduceMotionCheck.Content = loc.GetString("Reduce_Motion", "Reduce Motion / Simplified Animations");
            if (TxtPaletteTitle != null) TxtPaletteTitle.Text = loc.GetString("Palette_Title", "Active Theme Color Palette");
            if (TxtAccent1Label != null) TxtAccent1Label.Text = loc.GetString("Primary_Accent", "Primary Accent");
            if (TxtAccent2Label != null) TxtAccent2Label.Text = loc.GetString("Secondary_Accent", "Secondary Accent");
            if (TxtBgLabel != null) TxtBgLabel.Text = loc.GetString("Background", "Background");
            if (TxtCardLabel != null) TxtCardLabel.Text = loc.GetString("Icon_Bubble", "Icon Bubble");

            // Tab 3: Shortcuts & Startup
            if (TxtTab3Title != null) TxtTab3Title.Text = loc.GetString("Tab3_Title", "Trigger Shortcut & Startup");
            if (TxtTab3Sub != null) TxtTab3Sub.Text = loc.GetString("Tab3_Sub", "Set the mouse button or keyboard shortcut to open the radial menu.");
            if (TxtShortcutCardTitle != null) TxtShortcutCardTitle.Text = loc.GetString("Trigger_Title", "Menu Activation Shortcut");
            if (TxtShortcutCardSub != null) TxtShortcutCardSub.Text = loc.GetString("Trigger_Desc", "Select a mouse button or keyboard hotkey to summon Radial Launcher.");
            if (BtnAssignShortcut != null) BtnAssignShortcut.Content = loc.GetString("Assign_Shortcut", "🎯 Assign Custom Shortcut");
            if (TxtStartupCardTitle != null) TxtStartupCardTitle.Text = loc.GetString("Startup_Title", "Windows Startup");
            if (RunOnStartupCheck != null) RunOnStartupCheck.Content = loc.GetString("Startup_Check", "Automatically start Radial Launcher in tray on Windows startup");
            if (TxtBehaviorTitle != null) TxtBehaviorTitle.Text = loc.GetString("Behavior_Title", "🎯 Menu Behavior & Navigation Guidelines");
            if (TxtBehaviorBody != null) TxtBehaviorBody.Text = loc.GetString("Behavior_Body", "• Auto-Close: Menu closes automatically when cursor moves 330px away.\n• Global Navigation: Drag with middle-mouse or use scroll wheel to switch pages/categories.\n• Quick Actions: Hovering items reveals instant actions at the center.");

            // Tab 4: Backup & Data
            if (TxtTab4Title != null) TxtTab4Title.Text = loc.GetString("Tab4_Title", "Backup & Data Management");
            if (TxtTab4Sub != null) TxtTab4Sub.Text = loc.GetString("Tab4_Sub", "Safely backup and restore your shortcuts, stats, and theme settings.");
            if (TxtLocalBackupTitle != null) TxtLocalBackupTitle.Text = loc.GetString("LocalBackup_Title", "💾 Local Disk Backup & Restore");
            if (TxtLocalBackupSub != null) TxtLocalBackupSub.Text = loc.GetString("LocalBackup_Sub", "Automatically or manually backup shortcuts, categories, and settings to local storage. Last 10 backups are preserved.");
            if (BtnLocalBackupNow != null) BtnLocalBackupNow.Content = loc.GetString("Backup_Now", "💾 Create Local Backup");
            if (BtnRestoreBackup != null) BtnRestoreBackup.Content = loc.GetString("Restore_Backup", "📂 Restore from Backup");
            if (TxtJsonBackupTitle != null) TxtJsonBackupTitle.Text = loc.GetString("JsonBackup_Title", "📤 JSON Export / Import");
            if (TxtJsonBackupSub != null) TxtJsonBackupSub.Text = loc.GetString("JsonBackup_Sub", "Export or import your complete configuration and shortcuts as a JSON file.");
            if (BtnExportJson != null) BtnExportJson.Content = loc.GetString("Export", "📤 Export (JSON)");
            if (BtnImportJson != null) BtnImportJson.Content = loc.GetString("Import", "📥 Import (JSON)");

            // Tab 5: Updates & Diagnostics
            if (TxtTab5Title != null) TxtTab5Title.Text = loc.GetString("Tab5_Title", "Updates & Diagnostics");
            if (TxtTab5Sub != null) TxtTab5Sub.Text = loc.GetString("Tab5_Sub", "System diagnostic logs, error logs, and application updates.");
            if (TxtLanguageTitle != null) TxtLanguageTitle.Text = loc.GetString("Language", "🌐 Display Language");
            if (TxtLanguageSub != null) TxtLanguageSub.Text = loc.GetString("Language_Desc", "Select language for application UI and radial menu (Default: English). Sorted alphabetically.");
            if (TxtSteamGridTitle != null) TxtSteamGridTitle.Text = loc.GetString("SteamGrid_Title", "🎮 SteamGridDB Game Covers");
            if (TxtSteamGridSub != null) TxtSteamGridSub.Text = loc.GetString("SteamGrid_Sub", "Enter your SteamGridDB API key to automatically fetch 600x900 vertical poster cover art for Steam and Epic games.");
            if (BtnSaveSteamGridKey != null) BtnSaveSteamGridKey.Content = loc.GetString("SteamGrid_Save", "💾 Save Key");
            if (TxtUpdatesTitle != null) TxtUpdatesTitle.Text = loc.GetString("Updates_Title", "🚀 Application Updates");
            if (CurrentVersionText != null) CurrentVersionText.Text = loc.GetString("Installed_Version", "Installed Version: v1.0.0 (Final Release)");
            if (AutoCheckUpdatesCheck != null) AutoCheckUpdatesCheck.Content = loc.GetString("AutoCheck_Updates", "Automatically check for updates on startup");
            if (CheckUpdatesNowBtn != null) CheckUpdatesNowBtn.Content = loc.GetString("Check_Updates", "🔄 Check for Updates Now");
            if (TxtDiagTitle != null) TxtDiagTitle.Text = loc.GetString("Diag_Title", "📊 System Diagnostics & Logs");
            if (TxtDiagSub != null) TxtDiagSub.Text = loc.GetString("Diag_Sub", "Application logs and system diagnostic summary.");
            if (BtnOpenLogs != null) BtnOpenLogs.Content = loc.GetString("Open_Logs", "📁 Open Logs Folder");
            if (BtnCopyDiag != null) BtnCopyDiag.Content = loc.GetString("Copy_Diag", "📋 Copy Diagnostics");
            if (TxtResetTitle != null) TxtResetTitle.Text = loc.GetString("Reset_Title", "⚠️ Reset to Factory Defaults");
            if (TxtResetSub != null) TxtResetSub.Text = loc.GetString("Reset_Sub", "Restores all theme, shortcut, and visual settings to defaults. (User database is preserved)");
            if (BtnResetSettings != null) BtnResetSettings.Content = loc.GetString("Reset_Factory", "Reset to Factory Defaults");

            // Update Category combo "All Categories" header
            if (CategoryFilterCombo != null && CategoryFilterCombo.Items.Count > 0 && CategoryFilterCombo.Items[0] is ComboBoxItem firstItem && firstItem.Tag is int tagVal && tagVal == 0)
            {
                firstItem.Content = loc.GetString("All_Categories", "All Categories");
            }

            if (BtnRenameCategory != null)
            {
                BtnRenameCategory.ToolTip = loc.GetString("Rename_Category", "Rename Category");
            }

            LoadCategories();
            LoadThemes();
            LoadShortcutState();
            LoadSteamGridState();
            RefreshGrid();
        }

        private void LoadSteamGridState()
        {
            if (SteamGridKeyBox != null)
            {
                SteamGridKeyBox.Text = _themeService.GetSteamGridDbKey();
            }
        }

        private void SaveSteamGridKey_Click(object sender, RoutedEventArgs e)
        {
            if (SteamGridKeyBox == null || SteamGridKeyStatus == null) return;
            string key = SteamGridKeyBox.Text.Trim();
            _themeService.SetSteamGridDbKey(key);
            var loc = LocalizationService.Instance;
            SteamGridKeyStatus.Text = loc.GetString("SteamGrid_Saved", "API key saved successfully.");
        }
    }
}
