using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private bool _isInitializing = true;

        public ManagementWindow()
        {
            InitializeComponent();
            _exporter = new DataExporter(_dbManager);

            LoadThemes();
            LoadStartupState();
            LoadCategoriesAndItems();

            _isInitializing = false;
        }

        private void LoadThemes()
        {
            var themes = ThemeManager.GetAllThemes();
            ThemeComboBox.ItemsSource = themes.Select(t => t.Name).ToList();
            var current = ThemeManager.GetCurrentTheme();
            ThemeComboBox.SelectedItem = current.Name;
        }

        private void LoadStartupState()
        {
            StartupCheckBox.IsChecked = StartupManager.IsRunOnStartup();
        }

        private void LoadCategoriesAndItems()
        {
            _categories = _dbManager.GetAllCategories();
            _allItems = _dbManager.GetAllItems();

            if (CategoryFilterComboBox.ItemsSource == null)
            {
                var filterList = new List<Category> { new Category { Id = -1, Name = "Tümü" } };
                filterList.AddRange(_categories);
                CategoryFilterComboBox.ItemsSource = filterList;
                CategoryFilterComboBox.SelectedIndex = 0;
            }

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
                .Where(i => selectedCatId <= 0 || (selectedCatId == 1 ? true : i.CategoryId == selectedCatId))
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
            }
        }

        private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            StartupManager.SetRunOnStartup(StartupCheckBox.IsChecked == true);
        }

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
            if (ItemsListView.SelectedItem is LauncherItemViewModel vm)
            {
                var result = MessageBox.Show($"'{vm.Name}' öğesini silmek istediğinize emin misiniz?", "Silme Onayı",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _dbManager.DeleteItem(vm.Item.Id);
                    LoadCategoriesAndItems();
                }
            }
        }

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedItem is LauncherItemViewModel vm)
            {
                _dbManager.ToggleFavorite(vm.Item.Id);
                LoadCategoriesAndItems();
            }
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

        private void ScanSteam_Click(object sender, RoutedEventArgs e)
        {
            var games = GameDetector.DetectSteamGames();
            if (games.Count == 0)
            {
                MessageBox.Show("Steam veya yüklü Steam oyunu bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Find or create "Oyunlar" category
            int gamesCatId = _categories.FirstOrDefault(c => c.Name.Contains("Oyun", StringComparison.OrdinalIgnoreCase))?.Id ?? 3;

            int addedCount = 0;
            int maxPos = _allItems.Count > 0 ? _allItems.Max(i => i.Position) : 0;

            foreach (var g in games)
            {
                // Check if already added
                if (!_allItems.Any(i => i.Target.Equals(g.ExePath, StringComparison.OrdinalIgnoreCase) || i.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    maxPos++;
                    _dbManager.InsertItem(new LauncherItem
                    {
                        Name = g.Name,
                        Type = "EXE",
                        Target = g.ExePath,
                        CategoryId = gamesCatId,
                        Position = maxPos,
                        IsFavorite = false
                    });
                    addedCount++;
                }
            }

            LoadCategoriesAndItems();
            MessageBox.Show($"{addedCount} yeni Steam oyunu 'Oyunlar' kategorisine başarıyla eklendi!", "Steam Taraması Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ScanEpic_Click(object sender, RoutedEventArgs e)
        {
            var games = GameDetector.DetectEpicGames();
            if (games.Count == 0)
            {
                MessageBox.Show("Epic Games veya yüklü Epic oyunu bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int gamesCatId = _categories.FirstOrDefault(c => c.Name.Contains("Oyun", StringComparison.OrdinalIgnoreCase))?.Id ?? 3;

            int addedCount = 0;
            int maxPos = _allItems.Count > 0 ? _allItems.Max(i => i.Position) : 0;

            foreach (var g in games)
            {
                if (!_allItems.Any(i => i.Target.Equals(g.ExePath, StringComparison.OrdinalIgnoreCase) || i.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    maxPos++;
                    _dbManager.InsertItem(new LauncherItem
                    {
                        Name = g.Name,
                        Type = "EXE",
                        Target = g.ExePath,
                        CategoryId = gamesCatId,
                        Position = maxPos,
                        IsFavorite = false
                    });
                    addedCount++;
                }
            }

            LoadCategoriesAndItems();
            MessageBox.Show($"{addedCount} yeni Epic Games oyunu 'Oyunlar' kategorisine başarıyla eklendi!", "Epic Taraması Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

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

        // --- Drag & Drop Reordering ---
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
            var hitTest = System.Windows.Media.VisualTreeHelper.HitTest(ItemsListView, point);
            if (hitTest == null) return null;

            var visual = hitTest.VisualHit;
            while (visual != null && visual != ItemsListView)
            {
                if (visual is ListViewItem lvi && lvi.DataContext is LauncherItemViewModel vm)
                {
                    return vm;
                }
                visual = System.Windows.Media.VisualTreeHelper.GetParent(visual);
            }
            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
