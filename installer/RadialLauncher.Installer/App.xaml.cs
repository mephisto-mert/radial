using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace RadialLauncher.Installer
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string[] args = e.Args;

            // Handle Uninstaller mode
            if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase) || 
                Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "").Equals("Uninstall", StringComparison.OrdinalIgnoreCase))
            {
                HandleUninstall(args.Contains("--silent", StringComparer.OrdinalIgnoreCase));
                return;
            }

            // Handle Silent install mode
            if (args.Contains("--silent", StringComparer.OrdinalIgnoreCase) || args.Contains("-s", StringComparer.OrdinalIgnoreCase))
            {
                HandleSilentInstall(args);
                return;
            }

            // Normal Interactive GUI Setup Wizard
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void HandleSilentInstall(string[] args)
        {
            try
            {
                string targetDir = InstallerService.GetDefaultInstallPath();
                int dirIdx = Array.FindIndex(args, a => a.Equals("--dir", StringComparison.OrdinalIgnoreCase));
                if (dirIdx >= 0 && dirIdx < args.Length - 1)
                {
                    targetDir = args[dirIdx + 1];
                }

                bool desktop = !args.Contains("--no-desktop", StringComparer.OrdinalIgnoreCase);
                bool startMenu = !args.Contains("--no-start", StringComparer.OrdinalIgnoreCase);
                bool runStartup = !args.Contains("--no-startup", StringComparer.OrdinalIgnoreCase);
                bool launch = !args.Contains("--no-launch", StringComparer.OrdinalIgnoreCase);

                InstallerService.ExtractPayload(targetDir);
                InstallerService.CreateShortcuts(targetDir, desktop, startMenu);
                InstallerService.RegisterInWindows(targetDir, runStartup);

                if (launch)
                {
                    string exe = Path.Combine(targetDir, "RadialLauncher.exe");
                    if (File.Exists(exe))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = exe, UseShellExecute = true });
                    }
                }

                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Silent install error: {ex.Message}");
                Shutdown(1);
            }
        }

        private void HandleUninstall(bool silent)
        {
            string installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');

            if (!silent)
            {
                string msg = "Are you sure you want to completely uninstall Radial Launcher from your system?";
                string title = "Uninstall Radial Launcher";

                var result = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    Shutdown(0);
                    return;
                }

                bool removeData = false;
                var dataResult = MessageBox.Show(
                    "Would you also like to delete your personal settings, categories, and database?",
                    "Remove Personal Data",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (dataResult == MessageBoxResult.Yes)
                {
                    removeData = true;
                }

                InstallerService.PerformUninstall(installDir, removeData);

                MessageBox.Show(
                    "Radial Launcher has been successfully uninstalled from your computer.",
                    "Uninstall Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                InstallerService.PerformUninstall(installDir, false);
            }

            Shutdown(0);
        }
    }
}
