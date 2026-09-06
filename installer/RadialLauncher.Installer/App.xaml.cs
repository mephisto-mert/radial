using System;
using System.Linq;
using System.Windows;

namespace RadialLauncher.Installer
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string[] args = e.Args;

            if (args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                bool silent = args.Any(a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase) || a.Equals("/s", StringComparison.OrdinalIgnoreCase));
                
                if (!silent)
                {
                    var result = MessageBox.Show(
                        "Radial Launcher'ı bilgisayarınızdan kaldırmak istediğinize emin misiniz?",
                        "Radial Launcher Kaldırma Sihirbazı",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        Shutdown();
                        return;
                    }

                    var removeDataResult = MessageBox.Show(
                        "Kullanıcı ayarlarınızı, kısayol veritabanınızı ve yedeklerinizi de silmek istiyor musunuz?\n\n(Daha sonra tekrar kurmayı planlıyorsanız 'Hayır'ı seçin)",
                        "Kullanıcı Verileri",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    bool removeData = (removeDataResult == MessageBoxResult.Yes);

                    InstallerService.PerformUninstall(removeData);

                    MessageBox.Show(
                        "Radial Launcher sisteminizden başarıyla kaldırıldı.",
                        "Kaldırma Tamamlandı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    InstallerService.PerformUninstall(removeUserData: false);
                }

                Shutdown();
                return;
            }

            if (args.Any(a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase) || a.Equals("/s", StringComparison.OrdinalIgnoreCase)))
            {
                string targetDir = InstallerService.GetDefaultInstallPath();
                InstallerService.ExtractPayload(targetDir);
                InstallerService.CreateShortcuts(targetDir, createDesktop: true, createStartMenu: true);
                InstallerService.RegisterInWindows(targetDir);
                InstallerService.SetStartup(targetDir, true);
                Shutdown();
                return;
            }

            // Normal Interactive GUI Setup Wizard
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
