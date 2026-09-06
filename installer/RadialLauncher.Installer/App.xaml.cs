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
                        "Are you sure you want to uninstall Radial Launcher from your computer?",
                        "Radial Launcher Uninstall Wizard",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        Shutdown();
                        return;
                    }

                    var removeDataResult = MessageBox.Show(
                        "Do you also want to delete your user settings, shortcuts database, and backups?\n\n(Choose 'No' if you plan to reinstall later)",
                        "User Data",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    bool removeData = (removeDataResult == MessageBoxResult.Yes);

                    InstallerService.PerformUninstall(removeData);

                    MessageBox.Show(
                        "Radial Launcher has been successfully removed from your computer.",
                        "Uninstall Complete",
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
