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
        private bool _isCompleted = false;

        public MainWindow()
        {
            InitializeComponent();
            TxtInstallPath.Text = InstallerService.GetDefaultInstallPath();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Radial Launcher için kurulum klasörünü seçin:";
            dialog.UseDescriptionForTitle = true;
            dialog.SelectedPath = TxtInstallPath.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtInstallPath.Text = dialog.SelectedPath;
            }
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleted)
            {
                Close();
                return;
            }

            string targetDir = TxtInstallPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                MessageBox.Show("Lütfen geçerli bir kurulum dizini belirtin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Path.GetFullPath(targetDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geçersiz dizin yolu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool desktopIcon = ChkDesktopShortcut.IsChecked == true;
            bool startMenu = ChkStartMenuShortcut.IsChecked == true;
            bool autoStartup = ChkAutoStartup.IsChecked == true;
            bool launchAfter = ChkLaunchAfter.IsChecked == true;

            // Switch to progress view
            SetupPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = false;
            BtnInstall.IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    InstallerService.ExtractPayload(targetDir, (pct, status) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = pct;
                            TxtStatusDetail.Text = status;
                        });
                    });

                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = 90;
                        TxtStatusDetail.Text = "Kısayollar ve sistem ayarları yapılıyor...";
                    });

                    InstallerService.CreateShortcuts(targetDir, desktopIcon, startMenu);
                    InstallerService.RegisterInWindows(targetDir);
                    InstallerService.SetStartup(targetDir, autoStartup);
                });

                ProgressBar.Value = 100;
                TxtStatus.Text = "🎉 Kurulum Başarıyla Tamamlandı!";
                TxtStatusDetail.Text = $"Radial Launcher hazır: {targetDir}";

                if (launchAfter)
                {
                    string exePath = Path.Combine(targetDir, "RadialLauncher.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = targetDir,
                            UseShellExecute = true
                        });
                    }
                }

                _isCompleted = true;
                BtnCancel.Visibility = Visibility.Collapsed;
                BtnInstall.IsEnabled = true;
                BtnInstall.Content = "Kapat";
                TxtFooterInfo.Text = "Kurulum tamamlandı. Kısayol veya sistem tepsisinden erişebilirsiniz.";
            }
            catch (Exception ex)
            {
                ProgressBar.Value = 0;
                TxtStatus.Text = "❌ Kurulum Sırasında Hata Oluştu";
                TxtStatusDetail.Text = ex.Message;
                BtnCancel.IsEnabled = true;
                BtnInstall.IsEnabled = true;
                BtnInstall.Content = "Tekrar Dene";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
