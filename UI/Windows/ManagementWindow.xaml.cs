using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RadialLauncher.Data;
using RadialLauncher.Models;
using RadialLauncher.Services;

namespace RadialLauncher.UI.Windows
{
    public class LauncherItemViewModel
    {
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
                var brand = IconExtractor.GetBrandIcon(Item.Name, Item.Target);
                if (brand != null) return brand;
                if (!string.IsNullOrEmpty(Item.IconPath) && File.Exists(Item.IconPath))
                    return IconExtractor.GetIconForFile(Item.IconPath);
                if (!string.IsNullOrEmpty(Item.Target))
                {
                    var targetIcon = IconExtractor.GetIconForFile(Item.Target);
                    if (targetIcon != null) return targetIcon;
                }
                if (Item.Type == "URL")
                {
                    return IconExtractor.GetFaviconForUrl(Item.Target);
                }
                return IconExtractor.CreateMonogramIcon(Item.Name, Color.FromRgb(88, 140, 236));
            }
        }

        public LauncherItemViewModel(LauncherItem item, string categoryName)
        {
            Item = item;
            CategoryName = categoryName;
        }
    }

    public partial class ManagementWindow : Window
    {
        private readonly DatabaseManager _dbManager = new();
        private readonly DataExporter _exporter;
        private List<LauncherItem> _allItems = new();
        private List<Category> _categories = new();
        private List<ScannedApp> _scannedApps = new();
        private bool _isInitializing = true;

        public ManagementWindow()
        {
            InitializeComponent();

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath)) this.Icon = IconExtractor.GetIconForFile(iconPath);
            }
            catch { }

            _exporter = new DataExporter(_dbManager);

            LoadThemes();
            LoadStartupState();
            LoadShortcutState();
            LoadCategoriesAndItems();

            var currentTheme = ThemeManager.GetCurrentTheme();
            ApplyThemeToWindow(currentTheme);

            ThemeManager.OnThemeChanged += OnThemeManagerChanged;
            this.Closed += (s, e) => ThemeManager.OnThemeChanged -= OnThemeManagerChanged;

            _isInitializing = false;
        }

        private void OnThemeManagerChanged(Theme t)
        {
            Dispatcher.Invoke(() =>
            {
                ApplyThemeToWindow(t);
                UpdateThemePreview(t);
            });
        }

        private void LoadThemes()
        {
            var themes = ThemeManager.GetAllThemes();
            ThemeComboBox.ItemsSource = themes.Select(t => t.Name).ToList();
            var current = ThemeManager.GetCurrentTheme();
            ThemeComboBox.SelectedItem = current.Name;
            UpdateThemePreview(current);
        }

        private void UpdateThemePreview(Theme theme)
        {
            try
            {
                PreviewBgColor.Fill = new SolidColorBrush(theme.BackgroundColor);
                PreviewAccentColor.Fill = new SolidColorBrush(theme.AccentColor);
                PreviewIconBgColor.Fill = new SolidColorBrush(theme.IconBackgroundColor);
                PreviewCenterColor.Fill = new SolidColorBrush(theme.CenterButtonColor);
            }
            catch { }
        }

        private void LoadStartupState()
        {
            StartupCheckBox.IsChecked = StartupManager.IsRunOnStartup();
        }

        private void LoadCategoriesAndItems()
        {
            _categories = _dbManager.GetAllCategories();
            _allItems = _dbManager.GetAllItems();

            // Populate category filter
            int previousFilterId = -1;
            if (CategoryFilterComboBox.SelectedValue is int prevId)
                previousFilterId = prevId;

            var filterList = new List<Category> { new Category { Id = -1, Name = "Tümü" } };
            filterList.AddRange(_categories);
            CategoryFilterComboBox.ItemsSource = filterList;

            if (previousFilterId != -1 && filterList.Any(c => c.Id == previousFilterId))
            {
                CategoryFilterComboBox.SelectedValue = previousFilterId;
            }
            else
            {
                CategoryFilterComboBox.SelectedIndex = 0;
            }

            // Populate category manager list
            CategoriesListView.ItemsSource = null;
            CategoriesListView.ItemsSource = _categories;

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = SearchBox?.Text?.Trim().ToLower() ?? "";
            var categoryMap = _categories.ToDictionary(c => c.Id, c => c.Name);

            int selectedCatId = -1;
            if (CategoryFilterComboBox?.SelectedValue is int catId)
            {
                selectedCatId = catId;
            }

            var filtered = _allItems
                .Where(i => selectedCatId <= 0 || (selectedCatId == 1 ? (i.CategoryId <= 1 || i.IsUserAdded) : i.CategoryId == selectedCatId))
                .Where(i => string.IsNullOrEmpty(query) || i.Name.ToLower().Contains(query) || i.Target.ToLower().Contains(query))
                .Select(i => new LauncherItemViewModel(i, categoryMap.TryGetValue(i.CategoryId, out var name) ? name : "Genel"))
                .ToList();

            ItemsListView.ItemsSource = filtered;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyFilter();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ThemeComboBox.SelectedItem is string selectedTheme)
            {
                ThemeManager.SetCurrentTheme(selectedTheme);
                var theme = ThemeManager.GetTheme(selectedTheme);
                UpdateThemePreview(theme);
                ApplyThemeToWindow(theme);
            }
        }

        private void LoadShortcutState()
        {
            try
            {
                string currentShortcut = ThemeManager.GetActivationShortcut();
                foreach (ComboBoxItem item in ShortcutComboBox.Items)
                {
                    if (item.Tag?.ToString() == currentShortcut)
                    {
                        ShortcutComboBox.SelectedItem = item;
                        break;
                    }
                }
                if (ShortcutComboBox.SelectedItem == null && ShortcutComboBox.Items.Count > 0)
                {
                    ShortcutComboBox.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void ShortcutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ShortcutComboBox.SelectedItem is ComboBoxItem item && item.Tag is string shortcutKey)
            {
                ThemeManager.SetActivationShortcut(shortcutKey);
            }
        }

        private void ApplyThemeToWindow(Theme theme)
        {
            try
            {
                var darkBg = Color.FromRgb(
                    (byte)Math.Max(14, (int)(theme.BackgroundColor.R * 0.45)),
                    (byte)Math.Max(14, (int)(theme.BackgroundColor.G * 0.45)),
                    (byte)Math.Max(18, (int)(theme.BackgroundColor.B * 0.45))
                );
                var cardBg = Color.FromRgb(
                    (byte)Math.Min(255, darkBg.R + 10),
                    (byte)Math.Min(255, darkBg.G + 10),
                    (byte)Math.Min(255, darkBg.B + 14)
                );
                var borderCol = Color.FromArgb(90, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B);

                this.Background = new SolidColorBrush(darkBg);

                var cardBrush = new SolidColorBrush(cardBg);
                var accentBrush = new SolidColorBrush(theme.AccentColor);
                var borderBrush = new SolidColorBrush(borderCol);

                if (HeaderBadgeBorder != null)
                {
                    HeaderBadgeBorder.BorderBrush = borderBrush;
                    HeaderBadgeBorder.BorderThickness = new Thickness(1);
                }
                if (HeaderBadgeText != null)
                {
                    HeaderBadgeText.Foreground = accentBrush;
                }

                Border[] cards = new[] 
                { 
                    Tab1ActionBar, Tab2LeftBorder, Tab3InfoBorder, Tab3ActionBar, 
                    Tab4ThemeBar, Tab4PreviewCard, Tab5StartupCard, Tab5ShortcutCard, Tab5GuideCard 
                };
                foreach (var card in cards)
                {
                    if (card != null)
                    {
                        card.Background = cardBrush;
                        card.BorderBrush = borderBrush;
                    }
                }

                if (ItemsListView != null)
                {
                    ItemsListView.Background = new SolidColorBrush(Color.FromRgb((byte)Math.Max(10, darkBg.R - 4), (byte)Math.Max(10, darkBg.G - 4), (byte)Math.Max(12, darkBg.B - 4)));
                    ItemsListView.BorderBrush = borderBrush;
                }
                if (ScannedAppsListView != null)
                {
                    ScannedAppsListView.Background = new SolidColorBrush(Color.FromRgb((byte)Math.Max(10, darkBg.R - 4), (byte)Math.Max(10, darkBg.G - 4), (byte)Math.Max(12, darkBg.B - 4)));
                    ScannedAppsListView.BorderBrush = borderBrush;
                }
                if (CategoriesListView != null)
                {
                    CategoriesListView.BorderBrush = borderBrush;
                }
            }
            catch { }
        }

        private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            StartupManager.SetRunOnStartup(StartupCheckBox.IsChecked == true);
        }

        // ==================== TAB 1: ITEM OPERATIONS ====================

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var addWin = new AddItemWindow
            {
                Owner = this
            };

            if (addWin.ShowDialog() == true && addWin.CreatedItem != null)
            {
                int maxPos = _allItems.Count > 0 ? _allItems.Max(i => i.Position) : -1;
                addWin.CreatedItem.Position = maxPos + 1;
                addWin.CreatedItem.IsUserAdded = true;

                _dbManager.InsertItem(addWin.CreatedItem);
                LoadCategoriesAndItems();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            OpenEditDialog();
        }

        private void ItemsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenEditDialog();
        }

        private void OpenEditDialog()
        {
            if (ItemsListView.SelectedItem is LauncherItemViewModel vm)
            {
                var editWin = new EditItemWindow(vm.Item)
                {
                    Owner = this
                };

                if (editWin.ShowDialog() == true)
                {
                    _dbManager.UpdateItem(editWin.Item);
                    LoadCategoriesAndItems();
                    SelectById(editWin.Item.Id);
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ItemsListView.SelectedItems.OfType<LauncherItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;

            string confirmMsg = selectedItems.Count == 1
                ? $"'{selectedItems[0].Name}' öğesini silmek istediğinize emin misiniz?"
                : $"Seçilen {selectedItems.Count} öğeyi silmek istediğinize emin misiniz?";

            var result = MessageBox.Show(confirmMsg, "Silme Onayı",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                foreach (var vm in selectedItems)
                {
                    _dbManager.DeleteItem(vm.Item.Id);
                }
                LoadCategoriesAndItems();
            }
        }

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ItemsListView.SelectedItems.OfType<LauncherItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;

            foreach (var vm in selectedItems)
            {
                _dbManager.ToggleFavorite(vm.Item.Id);
            }
            LoadCategoriesAndItems();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedItem is LauncherItemViewModel vm)
            {
                int index = _allItems.FindIndex(i => i.Id == vm.Item.Id);
                if (index > 0)
                {
                    var current = _allItems[index];
                    var prev = _allItems[index - 1];

                    int tempPos = current.Position;
                    current.Position = prev.Position;
                    prev.Position = tempPos;

                    _dbManager.UpdatePositions(new List<LauncherItem> { current, prev });
                    LoadCategoriesAndItems();
                    SelectById(current.Id);
                }
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedItem is LauncherItemViewModel vm)
            {
                int index = _allItems.FindIndex(i => i.Id == vm.Item.Id);
                if (index >= 0 && index < _allItems.Count - 1)
                {
                    var current = _allItems[index];
                    var next = _allItems[index + 1];

                    int tempPos = current.Position;
                    current.Position = next.Position;
                    next.Position = tempPos;

                    _dbManager.UpdatePositions(new List<LauncherItem> { current, next });
                    LoadCategoriesAndItems();
                    SelectById(current.Id);
                }
            }
        }

        private void SelectById(int id)
        {
            foreach (var obj in ItemsListView.Items)
            {
                if (obj is LauncherItemViewModel vm && vm.Item.Id == id)
                {
                    ItemsListView.SelectedItem = vm;
                    ItemsListView.ScrollIntoView(vm);
                    break;
                }
            }
        }

        // ==================== TAB 2: CATEGORY MANAGEMENT ====================

        private void CategoriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriesListView.SelectedItem is Category cat)
            {
                EditCategoryNameBox.Text = cat.Name;
                EditCategoryColorBox.Text = cat.Color;
            }
        }

        private void UpdateCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesListView.SelectedItem is Category cat)
            {
                string newName = EditCategoryNameBox.Text.Trim();
                string newColor = EditCategoryColorBox.Text.Trim();

                if (string.IsNullOrEmpty(newName))
                {
                    MessageBox.Show("Kategori adı boş olamaz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                cat.Name = newName;
                if (!string.IsNullOrEmpty(newColor)) cat.Color = newColor;

                _dbManager.UpdateCategory(cat);
                LoadCategoriesAndItems();
                MessageBox.Show($"'{cat.Name}' kategorisi başarıyla güncellendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Lütfen önce listeden güncellenecek bir kategori seçin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            string name = NewCategoryNameBox.Text.Trim();
            string color = NewCategoryColorBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Lütfen kategori adı girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(color)) color = "#3498db";

            int nextPos = _categories.Count > 0 ? _categories.Max(c => c.Position) + 1 : 0;
            _dbManager.InsertCategory(new Category
            {
                Name = name,
                Color = color,
                Position = nextPos
            });

            NewCategoryNameBox.Text = "";
            LoadCategoriesAndItems();
            MessageBox.Show($"'{name}' kategorisi başarıyla oluşturuldu!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesListView.SelectedItem is Category cat)
            {
                if (cat.Id <= 1 || cat.Name.Equals("Hepsi", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("'Hepsi' ana kategorisi silinemez!", "Engellendi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var confirm = MessageBox.Show($"'{cat.Name}' kategorisini silmek istediğinize emin misiniz?\n\nBu kategorideki tüm öğeler 'Hepsi'ne aktarılacaktır.", "Kategori Silme", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    _dbManager.DeleteCategory(cat.Id);
                    EditCategoryNameBox.Text = "";
                    EditCategoryColorBox.Text = "";
                    LoadCategoriesAndItems();
                    MessageBox.Show("Kategori başarıyla silindi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Lütfen silmek için bir kategori seçin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ==================== TAB 3: SMART PC SCANNER ====================

        private async void RunFullScan_Click(object sender, RoutedEventArgs e)
        {
            ScanStatusText.Text = "⏳ Bilgisayar taranıyor, lütfen bekleyin...";
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            try
            {
                _scannedApps = await Task.Run(() => PCScannerService.ScanAll());
                ScannedAppsListView.ItemsSource = null;
                ScannedAppsListView.ItemsSource = _scannedApps;

                int gamesCount = _scannedApps.Count(a => a.CategoryName == PCScannerService.CatGames);
                int internetCount = _scannedApps.Count(a => a.CategoryName == PCScannerService.CatInternet);
                int devCount = _scannedApps.Count(a => a.CategoryName == PCScannerService.CatDev);
                int toolsCount = _scannedApps.Count(a => a.CategoryName == PCScannerService.CatTools);

                ScanStatusText.Text = $"✅ {_scannedApps.Count} uygulama bulundu ({gamesCount} Oyun, {internetCount} İnternet, {devCount} Geliştirme, {toolsCount} Sistem)";
            }
            catch (Exception ex)
            {
                ScanStatusText.Text = "❌ Tarama sırasında hata oluştu: " + ex.Message;
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        private void SelectAllScanned_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _scannedApps) app.IsSelected = true;
            ScannedAppsListView.Items.Refresh();
        }

        private void DeselectAllScanned_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _scannedApps) app.IsSelected = false;
            ScannedAppsListView.Items.Refresh();
        }

        private int _lastScannedClickIndex = -1;

        private void ScannedAppsListView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = GetScannedAppAtPoint(e.GetPosition(ScannedAppsListView));
            if (item != null)
            {
                int curIndex = _scannedApps.IndexOf(item);
                if ((System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || 
                     System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift)) &&
                    _lastScannedClickIndex >= 0 && _lastScannedClickIndex < _scannedApps.Count)
                {
                    int start = Math.Min(_lastScannedClickIndex, curIndex);
                    int end = Math.Max(_lastScannedClickIndex, curIndex);
                    bool newState = true;
                    for (int i = start; i <= end; i++)
                    {
                        _scannedApps[i].IsSelected = newState;
                    }
                    ScannedAppsListView.Items.Refresh();
                }
                _lastScannedClickIndex = curIndex;
            }
        }

        private ScannedApp? GetScannedAppAtPoint(Point point)
        {
            var hitTest = VisualTreeHelper.HitTest(ScannedAppsListView, point);
            if (hitTest == null) return null;

            var visual = hitTest.VisualHit;
            while (visual != null && visual != ScannedAppsListView)
            {
                if (visual is ListViewItem lvi && lvi.DataContext is ScannedApp app)
                {
                    return app;
                }
                visual = VisualTreeHelper.GetParent(visual);
            }
            return null;
        }

        private void ScannedAppsListView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space)
            {
                var selected = ScannedAppsListView.SelectedItems.OfType<ScannedApp>().ToList();
                if (selected.Count > 0)
                {
                    bool targetState = !selected[0].IsSelected;
                    foreach (var app in selected) app.IsSelected = targetState;
                    ScannedAppsListView.Items.Refresh();
                    e.Handled = true;
                }
            }
        }

        private void CheckSelectedScanned_Click(object sender, RoutedEventArgs e)
        {
            var selected = ScannedAppsListView.SelectedItems.OfType<ScannedApp>().ToList();
            if (selected.Count == 0) return;
            foreach (var app in selected) app.IsSelected = true;
            ScannedAppsListView.Items.Refresh();
        }

        private void UncheckSelectedScanned_Click(object sender, RoutedEventArgs e)
        {
            var selected = ScannedAppsListView.SelectedItems.OfType<ScannedApp>().ToList();
            if (selected.Count == 0) return;
            foreach (var app in selected) app.IsSelected = false;
            ScannedAppsListView.Items.Refresh();
        }

        private void ImportScanned_Click(object sender, RoutedEventArgs e)
        {
            if (_scannedApps.Count == 0)
            {
                MessageBox.Show("Önce 'Tüm Bilgisayarı Tara' butonuna basarak tarama yapmalısınız.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selected = _scannedApps.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Lütfen aktarılacak en az bir uygulama seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var summary = PCScannerService.ImportToDatabase(_dbManager, selected);
            LoadCategoriesAndItems();

            MessageBox.Show(
                $"Toplam {summary.TotalAdded} yeni uygulama başarıyla eklendi!\n\n" +
                $"🎮 Oyunlar: {summary.GamesCount}\n" +
                $"🌐 İnternet & İletişim: {summary.InternetCount}\n" +
                $"💼 Geliştirme & İş: {summary.DevCount}\n" +
                $"🛠️ Sistem & Araçlar: {summary.SystemCount}\n\n" +
                $"💡 Not: Ana 'Hepsi' menünüz tertemiz kaldı; tarananlar yalnızca kendi sekmelerinde listelenir.",
                "Aktarım Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==================== TAB 5: BACKUP & GENERAL ====================

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Öğeleri Dışa Aktar (JSON)",
                Filter = "JSON Dosyaları (*.json)|*.json",
                FileName = "RadialLauncher_Items.json"
            };

            if (dialog.ShowDialog() == true)
            {
                _exporter.Export(dialog.FileName);
                MessageBox.Show("Öğeler başarıyla dışa aktarıldı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Öğeleri İçe Aktar (JSON)",
                Filter = "JSON Dosyaları (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                _exporter.Import(dialog.FileName);
                LoadCategoriesAndItems();
                MessageBox.Show("Öğeler başarıyla içe aktarıldı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- Drag & Drop Reordering in Tab 1 ---
        private Point _startPoint;
        private LauncherItemViewModel? _draggedItem;

        private void ItemsListView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _draggedItem = GetItemAtPoint(e.GetPosition(ItemsListView));
        }

        private void ItemsListView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _draggedItem != null)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(ItemsListView, _draggedItem, DragDropEffects.Move);
                }
            }
        }

        private void ItemsListView_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(LauncherItemViewModel)))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void ItemsListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(LauncherItemViewModel)))
            {
                var droppedData = e.Data.GetData(typeof(LauncherItemViewModel)) as LauncherItemViewModel;
                var target = GetItemAtPoint(e.GetPosition(ItemsListView));

                if (droppedData != null && target != null && droppedData.Item.Id != target.Item.Id)
                {
                    int oldIndex = _allItems.FindIndex(i => i.Id == droppedData.Item.Id);
                    int newIndex = _allItems.FindIndex(i => i.Id == target.Item.Id);

                    if (oldIndex >= 0 && newIndex >= 0)
                    {
                        var item = _allItems[oldIndex];
                        _allItems.RemoveAt(oldIndex);
                        _allItems.Insert(newIndex, item);

                        for (int i = 0; i < _allItems.Count; i++)
                        {
                            _allItems[i].Position = i;
                        }

                        _dbManager.UpdatePositions(_allItems);
                        LoadCategoriesAndItems();
                        SelectById(item.Id);
                    }
                }
            }
        }

        private LauncherItemViewModel? GetItemAtPoint(Point point)
        {
            var hitTest = VisualTreeHelper.HitTest(ItemsListView, point);
            if (hitTest == null) return null;

            var visual = hitTest.VisualHit;
            while (visual != null && visual != ItemsListView)
            {
                if (visual is ListViewItem lvi && lvi.DataContext is LauncherItemViewModel vm)
                {
                    return vm;
                }
                visual = VisualTreeHelper.GetParent(visual);
            }
            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
