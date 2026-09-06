using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace RadialLauncher.Installer
{
    public partial class MainWindow : Window
    {
        private string _currentLang = "en";
        private bool _isInstalling = false;
        private bool _isCompleted = false;

        public MainWindow()
        {
            InitializeComponent();
            TxtInstallPath.Text = InstallerService.GetDefaultInstallPath();

            // Detect system culture for initial display
            string uiLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (uiLang == "tr")
            {
                SetLanguage("tr");
            }
            else
            {
                SetLanguage("en");
            }
        }

        private void SetLanguage(string lang)
        {
            _currentLang = lang;
            bool isTr = lang == "tr";

            Title = isTr ? "Radial Launcher v1.0.0 — Kurulum Sihirbazı" : "Radial Launcher v1.0.0 — Setup Wizard";
            TxtHeaderSub.Text = isTr ? "Windows için Profesyonel Dairesel Uygulama ve Oyun Başlatıcı" : "Professional Radial Application & Game Launcher for Windows";
            TxtInstallDirLabel.Text = isTr ? "Kurulum Klasörü:" : "Installation Directory:";
            BtnBrowse.Content = isTr ? "Gözat..." : "Browse...";
            TxtOptionsLabel.Text = isTr ? "Kurulum Seçenekleri:" : "Installation Options:";
            ChkDesktopShortcut.Content = isTr ? "Masaüstü Kısayolu Oluştur" : "Create Desktop Shortcut";
            ChkStartMenuShortcut.Content = isTr ? "Başlat Menüsü Kısayolu Oluştur" : "Create Start Menu Shortcut";
            ChkAutoStartup.Content = isTr ? "Windows başlangıcında otomatik çalıştır (Sistem Tepsisi)" : "Run automatically on Windows startup (Tray Mode)";
            ChkLaunchAfter.Content = isTr ? "Kurulum bittiğinde Radial Launcher'ı başlat" : "Launch Radial Launcher when setup completes";
            ChkCleanInstall.Content = isTr ? "Sıfırdan Temiz Kurulum (Mevcut ayarları ve kısayolları sıfırla)" : "Clean Install (Reset existing user data & shortcuts)";
            TxtCleanGuarantee.Text = isTr 
                ? "Temiz Kurulum: Ayarlarınız ve kısayollarınız %LOCALAPPDATA%\\RadialLauncher içinde güvenle saklanır"
                : "Clean Standalone Installation: Your settings and shortcuts will be safely stored in %LOCALAPPDATA%\\RadialLauncher";

            TxtFooterInfo.Text = isTr ? "Gereksinimler: Windows 10/11 x64" : "Requirements: Windows 10/11 x64";
            BtnCancel.Content = isTr ? "İptal" : "Cancel";

            if (!_isCompleted)
            {
                BtnInstall.Content = isTr ? "Şimdi Kur" : "Install Now";
            }
            else
            {
                BtnInstall.Content = isTr ? "Bitir" : "Finish";
            }

            if (isTr)
            {
                BtnLangTr.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));
                BtnLangEn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 38, 56));
            }
            else
            {
                BtnLangEn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));
                BtnLangTr.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 38, 56));
            }
        }

        private void BtnLangEn_Click(object sender, RoutedEventArgs e) => SetLanguage("en");
        private void BtnLangTr_Click(object sender, RoutedEventArgs e) => SetLanguage("tr");

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = _currentLang == "tr" ? "Kurulum Klasörünü Seçin" : "Select Installation Directory";
            dialog.SelectedPath = TxtInstallPath.Text;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtInstallPath.Text = dialog.SelectedPath;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isInstalling) return;
            Close();
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleted)
            {
                if (ChkLaunchAfter.IsChecked == true)
                {
                    string exePath = Path.Combine(TxtInstallPath.Text, "RadialLauncher.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                    }
                }
                Close();
                return;
            }

            string targetDir = TxtInstallPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                string msg = _currentLang == "tr" ? "Lütfen geçerli bir kurulum klasörü belirtin." : "Please specify a valid installation directory.";
                MessageBox.Show(msg, _currentLang == "tr" ? "Uyarı" : "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(targetDir);
            }
            catch (Exception ex)
            {
                string msg = _currentLang == "tr" ? $"Geçersiz klasör yolu: {ex.Message}" : $"Invalid directory path: {ex.Message}";
                MessageBox.Show(msg, _currentLang == "tr" ? "Hata" : "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _isInstalling = true;
            BtnInstall.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            BtnBrowse.IsEnabled = false;
            BtnLangEn.IsEnabled = false;
            BtnLangTr.IsEnabled = false;

            SetupPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;

            bool desktop = ChkDesktopShortcut.IsChecked == true;
            bool startMenu = ChkStartMenuShortcut.IsChecked == true;
            bool runStartup = ChkAutoStartup.IsChecked == true;
            bool cleanInstall = ChkCleanInstall.IsChecked == true;

            try
            {
                await Task.Run(() =>
                {
                    if (cleanInstall)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TxtStatusDetail.Text = _currentLang == "tr" ? "Eski kullanıcı verileri temizleniyor..." : "Resetting user data...";
                        });
                        InstallerService.ResetUserData();
                    }

                    InstallerService.ExtractPayload(targetDir, (pct, detail) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = pct;
                            TxtStatusDetail.Text = detail;
                        });
                    });

                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = 90;
                        TxtStatusDetail.Text = _currentLang == "tr" ? "Kısayollar ve kayıt defteri yapılandırılıyor..." : "Configuring system shortcuts and registry...";
                    });

                    InstallerService.CreateShortcuts(targetDir, desktop, startMenu);
                    InstallerService.RegisterInWindows(targetDir, runStartup);

                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = 100;
                        TxtStatus.Text = _currentLang == "tr" ? "Kurulum başarıyla tamamlandı!" : "Installation completed successfully!";
                        TxtStatusDetail.Text = _currentLang == "tr" ? "Radial Launcher kullanıma hazır." : "Radial Launcher is ready to use.";
                    });
                });

                _isCompleted = true;
                _isInstalling = false;
                BtnInstall.Content = _currentLang == "tr" ? "Bitir" : "Finish";
                BtnInstall.IsEnabled = true;
                BtnCancel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _isInstalling = false;
                BtnInstall.IsEnabled = true;
                BtnCancel.IsEnabled = true;
                SetupPanel.Visibility = Visibility.Visible;
                ProgressPanel.Visibility = Visibility.Collapsed;

                string msg = _currentLang == "tr" ? $"Kurulum başarısız oldu:\n{ex.Message}" : $"Installation failed:\n{ex.Message}";
                MessageBox.Show(msg, _currentLang == "tr" ? "Kurulum Hatası" : "Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
