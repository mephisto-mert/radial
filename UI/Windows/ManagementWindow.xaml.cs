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
        }

        private void ManagementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCategories();
            LoadThemes();
            RefreshGrid();
            LoadStartupState();
            LoadShortcutState();
            LoadDensityState();
            UpdateSyncUiState();
            if (AutoCheckUpdatesCheck != null)
            {
                AutoCheckUpdatesCheck.IsChecked = _themeService.GetAutoCheckUpdates();
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

            FollowWindowsThemeCheck.IsChecked = _viewModel.FollowWindowsTheme;
            ExtractAccentCheck.IsChecked = _viewModel.ExtractAccentFromWallpaper;
            ReduceMotionCheck.IsChecked = _viewModel.ReduceMotion;
        }

        private void LoadDensityState()
        {
            if (DensityCombo == null) return;
            string mode = _viewModel.SelectedTheme?.DensityMode ?? _viewModel.DensityMode;
            DensityCombo.SelectedIndex = (mode == "Compact") ? 1 : 0;
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
            if (_viewModel == null || CategoryFilterCombo == null || ItemsGrid == null || StatusText == null) return;
            var catMap = _viewModel.Categories?.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name) ?? new Dictionary<int, string>();
            var query = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
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
            if (_viewModel == null || LivePreviewControl == null) return;
            var lb = sender as ListBox ?? ThemesListBox;
            if (lb?.SelectedItem is string themeName)
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
            if (LivePreviewControl == null || CustomAccentBox == null || CustomBgBox == null || CustomCardBox == null || CustomThemeNameBox == null) return;
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
            if (_viewModel == null || _themeService == null) return;
            _viewModel.FollowWindowsTheme = FollowWindowsThemeCheck.IsChecked ?? false;
            _themeService.SetFollowWindowsTheme(_viewModel.FollowWindowsTheme);
            if (LivePreviewControl != null)
            {
                LivePreviewControl.Theme = _themeService.GetCurrentTheme();
            }
        }

        private void ExtractAccent_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _themeService == null) return;
            _viewModel.ExtractAccentFromWallpaper = ExtractAccentCheck.IsChecked ?? false;
            _themeService.SetExtractAccentFromWallpaper(_viewModel.ExtractAccentFromWallpaper);
            if (LivePreviewControl != null)
            {
                LivePreviewControl.Theme = _themeService.GetCurrentTheme();
            }
        }

        private void ReduceMotion_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _themeService == null) return;
            _viewModel.ReduceMotion = ReduceMotionCheck.IsChecked ?? false;
            var t = _themeService.GetCurrentTheme();
            if (t != null)
            {
                t.ReduceMotion = _viewModel.ReduceMotion;
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
                _viewModel.DensityMode = mode;
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

        private void UpdateSyncUiState()
        {
            if (SyncNowBtn == null || SyncPullBtn == null || PatStatusText == null) return;
            bool hasPat = _syncService.HasPatConfigured();
            SyncNowBtn.IsEnabled = hasPat;
            SyncPullBtn.IsEnabled = hasPat;

            if (hasPat)
            {
                string? gistId = _syncService.GetGistId();
                PatStatusText.Text = string.IsNullOrEmpty(gistId) 
                    ? "Durum: Token aktif (Gist henüz oluşturulmadı)" 
                    : $"Durum: Token aktif (Gist ID: {gistId})";
                PatStatusText.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                SyncNowBtn.ToolTip = "Ayarlarınızı ve kısayollarınızı GitHub Gist'e yedekler.";
                SyncPullBtn.ToolTip = "GitHub Gist'teki yedeği indirip uygular.";
            }
            else
            {
                PatStatusText.Text = "Durum: Token ayarlanmamış (Bulut senkronizasyonu devre dışı)";
                PatStatusText.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                string tooltip = "GitHub Gist senkronizasyonu için geçerli bir GitHub PAT (gist yetkili) kaydedin.";
                SyncNowBtn.ToolTip = tooltip;
                SyncPullBtn.ToolTip = tooltip;
            }
        }

        private void SavePatBtn_Click(object sender, RoutedEventArgs e)
        {
            string token = GistPatBox.Password?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Lütfen geçerli bir GitHub Personal Access Token girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _syncService.SavePat(token);
            GistPatBox.Clear();
            UpdateSyncUiState();
            MessageBox.Show("GitHub Token güvenli şekilde şifrelenerek kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void SyncNowBtn_Click(object sender, RoutedEventArgs e)
        {
            SyncNowBtn.IsEnabled = false;
            StatusText.Text = "GitHub Gist'e yedekleniyor...";
            try
            {
                var result = await _syncService.PushToGistAsync();
                if (result.success)
                {
                    UpdateSyncUiState();
                    StatusText.Text = result.message;
                    MessageBox.Show(result.message, "Eşitleme Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = result.message;
                    MessageBox.Show(result.message, "Eşitleme Başarısız", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                UpdateSyncUiState();
            }
        }

        private async void SyncPullBtn_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Gist'ten geri yüklemek mevcut tüm yerel ayarlarınızın ve kısayollarınızın üzerine yazacaktır.\nDevam etmek istiyor musunuz?", 
                "Geri Yükleme Onayı", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            SyncPullBtn.IsEnabled = false;
            StatusText.Text = "GitHub Gist'ten indiriliyor...";
            try
            {
                var result = await _syncService.PullFromGistAsync();
                if (result.success)
                {
                    _viewModel.LoadInitialData();
                    RefreshGrid();
                    LoadCategories();
                    LoadThemes();
                    StatusText.Text = result.message;
                    MessageBox.Show(result.message, "Geri Yükleme Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = result.message;
                    MessageBox.Show(result.message, "Geri Yükleme Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                UpdateSyncUiState();
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
                diag.AppendLine($"App Version: 1.0.0-rc1");
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
                "Tüm tema, kısayol ve görsel ayarlarınız fabrika varsayılanlarına sıfırlanacaktır.\nDevam etmek istiyor musunuz?",
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
