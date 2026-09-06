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
            dialog.Description = "Select installation directory for Radial Launcher:";
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
                MessageBox.Show("Please specify a valid installation directory.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Path.GetFullPath(targetDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid directory path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        TxtStatusDetail.Text = "Configuring shortcuts and system settings...";
                    });

                    InstallerService.CreateShortcuts(targetDir, desktopIcon, startMenu);
                    InstallerService.RegisterInWindows(targetDir);
                    InstallerService.SetStartup(targetDir, autoStartup);
                });

                ProgressBar.Value = 100;
                TxtStatus.Text = "🎉 Installation Completed Successfully!";
                TxtStatusDetail.Text = $"Radial Launcher is ready: {targetDir}";

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
                BtnInstall.Content = "Close";
                TxtFooterInfo.Text = "Installation complete. You can access it via shortcut or system tray.";
            }
            catch (Exception ex)
            {
                ProgressBar.Value = 0;
                TxtStatus.Text = "❌ Installation Failed";
                TxtStatusDetail.Text = ex.Message;
                BtnCancel.IsEnabled = true;
                BtnInstall.IsEnabled = true;
                BtnInstall.Content = "Retry";
                MessageBox.Show($"Installation failed:\n{ex.Message}", "Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
