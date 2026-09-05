using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Icons;
using RadialLauncher.Services.Scanning;
using RadialLauncher.Services.Themes;
using RadialLauncher.Services.Windows;
using RadialLauncher.UI.ViewModels;
using Serilog;

namespace RadialLauncher.UI.Windows
{
    public class LauncherItemViewModel
    {
        private readonly IIconExtractor? _iconExtractor;
        public LauncherItem Item { get; set; }
        public string IsFavoriteText => Item.IsFavorite ? "⭐" : "—";
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

        public ManagementWindow() : this(
            App.ServiceProvider?.GetService(typeof(ManagementViewModel)) as ManagementViewModel,
            App.ServiceProvider?.GetService(typeof(IStartupManager)) as IStartupManager,
            App.ServiceProvider?.GetService(typeof(IThemeService)) as IThemeService)
        {
        }

        public ManagementWindow(ManagementViewModel? viewModel, IStartupManager? startupManager = null, IThemeService? themeService = null)
        {
            InitializeComponent();

            _themeService = themeService ?? ThemeService.Instance;
            _startupManager = startupManager ?? new StartupManager();
            _viewModel = viewModel
                         ?? (App.ServiceProvider?.GetService(typeof(ManagementViewModel)) as ManagementViewModel)
                         ?? new ManagementViewModel(
                             new ItemRepository(new Data.DatabaseManager()),
                             new CategoryRepository(new Data.DatabaseManager()),
                             _themeService,
                             (App.ServiceProvider?.GetService(typeof(IPCScannerService)) as IPCScannerService) ?? new Services.Scanning.PCScannerService(),
                             new Services.Sync.SyncService(new ItemRepository(new Data.DatabaseManager()), new CategoryRepository(new Data.DatabaseManager())),
                             new Data.DatabaseManager());

            DataContext = _viewModel;
            Loaded += ManagementWindow_Loaded;
        }

        private void ManagementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCategories();
            LoadThemes();
            RefreshGrid();
            LoadStartupState();
            LoadShortcutState();
        }

        private void LoadCategories()
        {
            CategoryFilterCombo.Items.Clear();
            CategoryFilterCombo.Items.Add(new ComboBoxItem { Content = "Tüm Kategoriler", Tag = 0 });

            foreach (var cat in _viewModel.Categories)
            {
                CategoryFilterCombo.Items.Add(new ComboBoxItem { Content = cat.Name, Tag = cat.Id });
            }
            CategoryFilterCombo.SelectedIndex = 0;
        }

        private void LoadThemes()
        {
            ThemesListBox.Items.Clear();
            foreach (var t in _viewModel.Themes)
            {
                ThemesListBox.Items.Add(t.Name);
            }
            ThemesListBox.SelectedItem = _viewModel.SelectedTheme?.Name ?? "Dark";
            LivePreviewControl.Theme = _viewModel.SelectedTheme;

            FollowWindowsThemeCheck.IsChecked = _viewModel.FollowWindowsTheme;
            ExtractAccentCheck.IsChecked = _viewModel.ExtractAccentFromWallpaper;
        }

        private void LoadShortcutState()
        {
            string sc = _viewModel.ActivationShortcut;
            ShortcutCombo.SelectedIndex = sc switch
            {
                "MiddleClick" => 0,
                "CtrlRightClick" => 1,
                "ShiftRightClick" => 2,
                "AltRightClick" => 3,
                "AltSpace" => 4,
                "CtrlSpace" => 5,
                "F4" => 6,
                "Tilde" => 7,
                _ => 0
            };
        }

        private void LoadStartupState()
        {
            RunOnStartupCheck.IsChecked = _startupManager.IsRunOnStartup();
        }

        private void RefreshGrid()
        {
            var catMap = _viewModel.Categories.ToDictionary(c => c.Id, c => c.Name);
            var query = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
            int selectedCatId = 0;
            if (CategoryFilterCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is int id)
            {
                selectedCatId = id;
            }

            var items = _viewModel.Items.Select(i => new LauncherItemViewModel(i, catMap.GetValueOrDefault(i.CategoryId, "Genel"))).ToList();
            if (selectedCatId > 0)
            {
                items = items.Where(i => i.Item.CategoryId == selectedCatId).ToList();
            }
            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(i => i.Name.ToLowerInvariant().Contains(query) || i.Target.ToLowerInvariant().Contains(query)).ToList();
            }

            ItemsGrid.ItemsSource = items;
            StatusText.Text = $"Toplam {items.Count} öğe listelendi.";
        }

        private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshGrid();
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshGrid();

        private void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddItemWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                _viewModel.RefreshItems();
                RefreshGrid();
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
                    _viewModel.RefreshItems();
                    RefreshGrid();
                }
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LauncherItemViewModel lvm)
            {
                if (MessageBox.Show($"'{lvm.Name}' silinsin mi?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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
            if (ThemesListBox.SelectedItem is string themeName)
            {
                var theme = _viewModel.Themes.FirstOrDefault(t => t.Name == themeName);
                if (theme != null)
                {
                    _viewModel.ApplyTheme(theme);
                    LivePreviewControl.Theme = theme;
                }
            }
        }

        private void CustomColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var accent = (Color)ColorConverter.ConvertFromString(CustomAccentBox.Text.Trim());
                var bg = (Color)ColorConverter.ConvertFromString(CustomBgBox.Text.Trim());
                var card = (Color)ColorConverter.ConvertFromString(CustomCardBox.Text.Trim());

                var previewTheme = new Theme
                {
                    Name = CustomThemeNameBox.Text,
                    AccentR = accent.R, AccentG = accent.G, AccentB = accent.B,
                    BgR = bg.R, BgG = bg.G, BgB = bg.B,
                    IconBgR = card.R, IconBgG = card.G, IconBgB = card.B
                };
                LivePreviewControl.Theme = previewTheme;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Live preview color parse failed for partial input");
            }
        }

        private void SaveCustomTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var accent = (Color)ColorConverter.ConvertFromString(CustomAccentBox.Text.Trim());
                var bg = (Color)ColorConverter.ConvertFromString(CustomBgBox.Text.Trim());
                var card = (Color)ColorConverter.ConvertFromString(CustomCardBox.Text.Trim());

                _viewModel.CustomThemeName = CustomThemeNameBox.Text.Trim();
                _viewModel.CustomAccentColor = accent;
                _viewModel.CustomBgColor = bg;
                _viewModel.CustomCardColor = card;
                _viewModel.SaveCustomTheme();

                LoadThemes();
                StatusText.Text = _viewModel.StatusMessage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geçersiz renk kodu: {ex.Message}", "Hata");
            }
        }

        private void FollowWindowsTheme_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.FollowWindowsTheme = FollowWindowsThemeCheck.IsChecked ?? false;
            _themeService.SetFollowWindowsTheme(_viewModel.FollowWindowsTheme);
            LivePreviewControl.Theme = _themeService.GetCurrentTheme();
        }

        private void ExtractAccent_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExtractAccentFromWallpaper = ExtractAccentCheck.IsChecked ?? false;
            _themeService.SetExtractAccentFromWallpaper(_viewModel.ExtractAccentFromWallpaper);
            LivePreviewControl.Theme = _themeService.GetCurrentTheme();
        }

        private void ReduceMotion_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ReduceMotion = ReduceMotionCheck.IsChecked ?? false;
            var t = _themeService.GetCurrentTheme();
            t.ReduceMotion = _viewModel.ReduceMotion;
            _themeService.SaveCustomTheme(t);
        }

        private void DensityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DensityCombo.SelectedItem is ComboBoxItem cbi)
            {
                string mode = cbi.Content.ToString()!.Contains("Kompakt") ? "Compact" : "Expanded";
                _viewModel.DensityMode = mode;
                var t = _themeService.GetCurrentTheme();
                t.DensityMode = mode;
                _themeService.SaveCustomTheme(t);
            }
        }

        private void ShortcutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShortcutCombo.SelectedItem is ComboBoxItem cbi)
            {
                string sc = cbi.Content.ToString() switch
                {
                    "Orta Tuş (Fare Tekerleği)" => "MiddleClick",
                    "Ctrl + Sağ Tık" => "CtrlRightClick",
                    "Shift + Sağ Tık" => "ShiftRightClick",
                    "Alt + Sağ Tık" => "AltRightClick",
                    "Alt + Boşluk (Alt+Space)" => "AltSpace",
                    "Ctrl + Boşluk (Ctrl+Space)" => "CtrlSpace",
                    "F4 Tuşu" => "F4",
                    "~ (Tilde Tuşu)" => "Tilde",
                    _ => "MiddleClick"
                };
                _themeService.SetActivationShortcut(sc);
            }
        }

        private void RunOnStartupCheck_Click(object sender, RoutedEventArgs e)
        {
            bool enable = RunOnStartupCheck.IsChecked ?? false;
            _startupManager.SetRunOnStartup(enable);
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Tarama başlatılıyor...";
            await _viewModel.ScanPc();
            RefreshGrid();
            StatusText.Text = _viewModel.StatusMessage;
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "JSON Dosyası (*.json)|*.json", FileName = "radial_backup.json" };
            if (sfd.ShowDialog() == true)
            {
                await _viewModel.ExportData(sfd.FileName);
                StatusText.Text = _viewModel.StatusMessage;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "JSON Dosyası (*.json)|*.json" };
            if (ofd.ShowDialog() == true)
            {
                await _viewModel.ImportData(ofd.FileName);
                RefreshGrid();
                StatusText.Text = _viewModel.StatusMessage;
            }
        }
    }
}
