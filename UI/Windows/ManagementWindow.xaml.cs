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
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."))
        {
        }

        public ManagementWindow(ManagementViewModel viewModel, IStartupManager startupManager, IThemeService themeService, ISyncService syncService)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));

            InitializeComponent();

            DataContext = _viewModel;
            Loaded += ManagementWindow_Loaded;
            _themeService.OnThemeChanged += t => Dispatcher.Invoke(() => ApplyThemeVisuals(t));
        }

        private void ManagementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyThemeVisuals(_themeService.GetCurrentTheme());
            LoadCategories();
            LoadThemes();
            LoadOpacityState();
            RefreshGrid();
            LoadStartupState();
            LoadShortcutState();
            LoadDensityState();
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

            this.Background = bgBrush;
            this.Foreground = textBrush;

            if (MainTabs != null)
            {
                MainTabs.Background = panelBrush;
                MainTabs.BorderBrush = borderBrush;
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
                CategoryFilterCombo.Foreground = textBrush;
            }
            if (SearchBox != null)
            {
                SearchBox.Background = panelBrush;
                SearchBox.Foreground = textBrush;
                SearchBox.BorderBrush = borderBrush;
            }
            if (ThemesListBox != null)
            {
                ThemesListBox.Background = panelBrush;
                ThemesListBox.Foreground = textBrush;
                ThemesListBox.BorderBrush = borderBrush;
            }
            if (DensityCombo != null)
            {
                DensityCombo.Background = panelBrush;
                DensityCombo.Foreground = textBrush;
            }
            if (ShortcutCombo != null)
            {
                ShortcutCombo.Background = panelBrush;
                ShortcutCombo.Foreground = textBrush;
            }
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
                ActiveShortcutLabel.Text = $"Aktif Kısayol: {ShortcutAssignWindow.ToFriendlyName(sc)} ({sc})";
            }
        }

        private void LoadStartupState()
        {
            RunOnStartupCheck.IsChecked = _startupManager.IsRunOnStartup();
        }

        private void RefreshGrid()
        {
            if (_viewModel == null || CategoryFilterCombo == null || ItemsGrid == null || StatusText == null) return;
            var catMap = _viewModel.Categories?.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name) ?? new Dictionary<int, string>();
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
                    ?? new Category { Id = selectedCatId, Name = "Kategori" };
            }

            _viewModel.FilterQuery = query;
            _viewModel.RefreshItems();

            var items = _viewModel.Items.Select(i => new LauncherItemViewModel(i, catMap.GetValueOrDefault(i.CategoryId, "Genel"))).ToList();
            ItemsGrid.ItemsSource = items;

            if (items.Count == 0)
            {
                StatusText.Text = "Henüz kullanılan veya listelenecek öğe bulunmuyor.";
            }
            else
            {
                StatusText.Text = $"Toplam {items.Count} öğe listelendi.";
            }
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
            if (_viewModel == null || LivePreviewControl == null) return;
            var lb = sender as ListBox ?? ThemesListBox;
            if (lb?.SelectedItem is string themeName)
            {
                var theme = _viewModel.Themes.FirstOrDefault(t => t.Name == themeName);
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
            if (cb?.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string mode = cbi.Content.ToString()!.Contains("Kompakt") ? "Compact" : "Expanded";
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
            if (_viewModel == null || _themeService == null) return;
            var cb = sender as ComboBox ?? ShortcutCombo;
            if (cb?.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string sc = cbi.Content.ToString() switch
                {
                    "Orta Tuş (Fare Tekerleği)" => "MiddleClick",
                    "Fare 4 (Geri Tuşu - XButton1)" => "XButton1",
                    "Fare 5 (İleri Tuşu - XButton2)" => "XButton2",
                    "Ctrl + Sağ Tık" => "CtrlRightClick",
                    "Shift + Sağ Tık" => "ShiftRightClick",
                    "Alt + Sağ Tık" => "AltRightClick",
                    "Ctrl + Fare 4" => "Ctrl+XButton1",
                    "Alt + Boşluk (Alt+Space)" => "AltSpace",
                    "Ctrl + Boşluk (Ctrl+Space)" => "CtrlSpace",
                    "F4 Tuşu" => "F4",
                    "~ (Tilde Tuşu)" => "Tilde",
                    _ => "MiddleClick"
                };
                _themeService.SetActivationShortcut(sc);
                LoadShortcutState();
            }
        }

        private void AssignCustomShortcut_Click(object sender, RoutedEventArgs e)
        {
            string current = _themeService.GetActivationShortcut();
            var win = new ShortcutAssignWindow(current);
            win.Owner = this;
            if (win.ShowDialog() == true && !string.IsNullOrWhiteSpace(win.SelectedShortcut))
            {
                string clean = win.SelectedShortcut.Trim();
                _themeService.SetActivationShortcut(clean);
                LoadShortcutState();
                StatusText.Text = $"Yeni kısayol atandı: {ShortcutAssignWindow.ToFriendlyName(clean)} ({clean})";
                MessageBox.Show($"Kısayol başarıyla kaydedildi:\n\n{ShortcutAssignWindow.ToFriendlyName(clean)}\n({clean})", "Kısayol Güncellendi", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private async void CreateLocalBackup_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Yerel yedek oluşturuluyor...";
            var result = await _syncService.CreateLocalBackupAsync();
            if (result.success)
            {
                UpdateBackupStatusLabel();
                StatusText.Text = "Yerel yedekleme tamamlandı.";
                MessageBox.Show($"Yedekleme başarıyla tamamlandı:\n{result.filePath}", "Yedek Alındı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusText.Text = "Yedekleme oluşturulamadı.";
                MessageBox.Show("Yerel yedek oluşturulurken bir hata meydana geldi.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RestoreLocalBackup_Click(object sender, RoutedEventArgs e)
        {
            string backupsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RadialLauncher", "Backups");

            var ofd = new OpenFileDialog
            {
                Title = "Geri Yüklenecek Yedeği Seçin",
                Filter = "Yedek Dosyaları (*.json)|*.json",
                InitialDirectory = Directory.Exists(backupsDir) ? backupsDir : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            if (ofd.ShowDialog() == true)
            {
                var confirm = MessageBox.Show(
                    $"'{Path.GetFileName(ofd.FileName)}' yedeği geri yüklenecek.\nMevcut verilerinizin üzerine yazılacaktır. Onaylıyor musunuz?",
                    "Yedekten Geri Yükleme Onayı",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    StatusText.Text = "Yedek geri yükleniyor...";
                    bool ok = await _syncService.RestoreFromLocalBackupAsync(ofd.FileName);
                    if (ok)
                    {
                        _viewModel.LoadInitialData();
                        LoadCategories();
                        LoadThemes();
                        RefreshGrid();
                        UpdateBackupStatusLabel();
                        StatusText.Text = "Yedek başarıyla geri yüklendi.";
                        MessageBox.Show("Yedek başarıyla geri yüklendi ve uygulandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusText.Text = "Geri yükleme başarısız.";
                        MessageBox.Show("Yedek dosyası okunamadı veya biçimi geçersiz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void UpdateBackupStatusLabel()
        {
            if (BackupStatusLabel == null) return;
            var backups = _syncService.GetLocalBackups();
            if (backups.Count > 0)
            {
                var latest = backups[0];
                var time = File.GetCreationTime(latest);
                BackupStatusLabel.Text = $"Toplam {backups.Count} yerel yedek mevcut. En son yedek: {time:yyyy-MM-dd HH:mm:ss} ({Path.GetFileName(latest)})";
            }
            else
            {
                BackupStatusLabel.Text = "Henüz oluşturulmuş yerel yedek bulunmuyor.";
            }
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

        private void AutoCheckUpdatesCheck_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = AutoCheckUpdatesCheck.IsChecked == true;
            _themeService.SetAutoCheckUpdates(isChecked);
            StatusText.Text = isChecked ? "Otomatik güncelleme kontrolü etkinleştirildi." : "Otomatik güncelleme kontrolü devre dışı bırakıldı.";
        }

        private async void CheckUpdatesNowBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesNowBtn.IsEnabled = false;
            UpdateCheckStatusLabel.Text = "GitHub Release kontrol ediliyor...";
            StatusText.Text = "Güncellemeler kontrol ediliyor...";

            try
            {
                var updateService = App.ServiceProvider?.GetService(typeof(IUpdateCheckService)) as IUpdateCheckService;
                if (updateService == null)
                {
                    UpdateCheckStatusLabel.Text = "Güncelleme servisi bulunamadı.";
                    return;
                }

                var info = await updateService.CheckForUpdatesAsync();
                if (info == null)
                {
                    UpdateCheckStatusLabel.Text = "Güncelleme sunucusuna ulaşılamadı. Lütfen internet bağlantınızı kontrol edin.";
                    StatusText.Text = "Güncelleme kontrolü başarısız.";
                }
                else if (info.IsUpdateAvailable)
                {
                    UpdateCheckStatusLabel.Text = $"🎉 Yeni bir sürüm mevcut: v{info.LatestVersion}\n{info.ReleaseUrl}";
                    StatusText.Text = $"Yeni sürüm v{info.LatestVersion} mevcut!";
                    var res = MessageBox.Show($"Yeni bir sürüm yayınlandı (v{info.LatestVersion}).\n\nİndirme sayfasına gitmek ister misiniz?", "Güncelleme Mevcut", MessageBoxButton.YesNo, MessageBoxImage.Information);
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
                    UpdateCheckStatusLabel.Text = $"✅ En güncel sürümü kullanıyorsunuz (v{info.CurrentVersion}).";
                    StatusText.Text = "Uygulama güncel.";
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking for updates from UI");
                UpdateCheckStatusLabel.Text = "Güncelleme kontrolü sırasında bir sorun oluştu.";
                StatusText.Text = "Güncelleme hatası.";
            }
            finally
            {
                CheckUpdatesNowBtn.IsEnabled = true;
            }
        }

        private void OpenLogsFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RadialLauncher", "Logs");

                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open logs folder");
                MessageBox.Show("Log klasörü açılamadı.", "Radial Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyDiagnosticsBtn_Click(object sender, RoutedEventArgs e)
        {
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
                StatusText.Text = "Tanılama bilgileri panoya kopyalandı!";
                MessageBox.Show("Sistem tanılama bilgileri panoya kopyalandı.", "Tanılama", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed copying diagnostics");
            }
        }

        private void ResetSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "Tüm tema, kısayol ve görsel ayarlarınız varsayılan fabrika değerlerine sıfırlanacaktır.\n(Kullanıcı öğeleri, kısayollar ve kullanım sayaçları KORUNUR)\n\nDevam etmek istiyor musunuz?",
                "Ayarları Sıfırla",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                _themeService.ResetSettingsToDefault();
                _viewModel.LoadInitialData();
                LoadThemes();
                LoadShortcutState();
                LoadDensityState();
                if (AutoCheckUpdatesCheck != null)
                {
                    AutoCheckUpdatesCheck.IsChecked = _themeService.GetAutoCheckUpdates();
                }
                StatusText.Text = "Ayarlar başarıyla varsayılanlara sıfırlandı.";
                MessageBox.Show("Ayarlar varsayılan değerlere sıfırlandı.", "Radial Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed resetting settings");
                MessageBox.Show("Ayarlar sıfırlanırken bir sorun oluştu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
