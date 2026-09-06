using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace RadialLauncher.Installer
{
    public class InstallerService
    {
        public const string AppName = "Radial Launcher";
        public const string RegUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RadialLauncher";
        public const string RegRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string GetDefaultInstallPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", "RadialLauncher");
        }

        public static void ExtractPayload(string targetDirectory, Action<int, string>? reportProgress = null)
        {
            Directory.CreateDirectory(targetDirectory);
            reportProgress?.Invoke(10, "Preparing installation directory...");

            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream("RadialLauncher.Installer.payload.zip");

            if (resourceStream != null)
            {
                using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);
                int totalEntries = archive.Entries.Count;
                int count = 0;

                foreach (var entry in archive.Entries)
                {
                    count++;
                    string destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
                    if (!destinationPath.StartsWith(Path.GetFullPath(targetDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Path traversal protection
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }

                    int pct = 10 + (int)((count / (double)Math.Max(1, totalEntries)) * 70);
                    reportProgress?.Invoke(pct, $"Extracting files: {entry.Name}");
                }
            }
            else
            {
                // Fallback: If running in dev/local mode without embedded zip, copy from current directory
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] files = Directory.GetFiles(currentDir);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string name = Path.GetFileName(file);
                    if (!name.Equals("RadialLauncher.Installer.exe", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("RadialLauncher-Setup-v1.0.0.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(file, Path.Combine(targetDirectory, name), true);
                    }
                }
            }

            // Copy self as uninstaller
            try
            {
                string currentExe = Environment.ProcessPath ?? assembly.Location;
                string uninstallerPath = Path.Combine(targetDirectory, "Uninstall.exe");
                if (File.Exists(currentExe))
                {
                    File.Copy(currentExe, uninstallerPath, overwrite: true);
                }
            }
            catch
            {
                // Ignore copy errors if running in restricted mode
            }

            reportProgress?.Invoke(85, "Installation files extracted.");
        }

        public static void CreateShortcuts(string targetDirectory, bool createDesktop, bool createStartMenu)
        {
            string exePath = Path.Combine(targetDirectory, "RadialLauncher.exe");
            string iconPath = Path.Combine(targetDirectory, "app.ico");
            if (!File.Exists(iconPath)) iconPath = exePath;

            if (createDesktop)
            {
                string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktopFolder, "Radial Launcher.lnk");
                CreateShortcutFile(shortcutPath, exePath, targetDirectory, iconPath, "Radial Launcher - Ultra Fast Circular App & Game Launcher");
            }

            if (createStartMenu)
            {
                string startMenuFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Radial Launcher");
                Directory.CreateDirectory(startMenuFolder);

                string appShortcut = Path.Combine(startMenuFolder, "Radial Launcher.lnk");
                CreateShortcutFile(appShortcut, exePath, targetDirectory, iconPath, "Radial Launcher");

                string uninstallerExe = Path.Combine(targetDirectory, "Uninstall.exe");
                if (File.Exists(uninstallerExe))
                {
                    string uninstallShortcut = Path.Combine(startMenuFolder, "Uninstall Radial Launcher.lnk");
                    CreateShortcutFile(uninstallShortcut, uninstallerExe, targetDirectory, uninstallerExe, "Uninstall Radial Launcher");
                }
            }
        }

        private static void CreateShortcutFile(string shortcutPath, string targetPath, string workingDir, string iconLocation, string description)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    shortcut.IconLocation = $"{iconLocation},0";
                    shortcut.Save();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Shortcut creation error: {ex.Message}");
            }
        }

        public static void RegisterInWindows(string targetDirectory)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegUninstallKey);
                if (key != null)
                {
                    string exePath = Path.Combine(targetDirectory, "RadialLauncher.exe");
                    string uninstallerPath = Path.Combine(targetDirectory, "Uninstall.exe");
                    string iconPath = Path.Combine(targetDirectory, "app.ico");

                    key.SetValue("DisplayName", AppName);
                    key.SetValue("DisplayVersion", "1.0.0");
                    key.SetValue("Publisher", "Radial Launcher Team");
                    key.SetValue("DisplayIcon", iconPath);
                    key.SetValue("InstallLocation", targetDirectory);
                    key.SetValue("UninstallString", $"\"{uninstallerPath}\" /uninstall");
                    key.SetValue("QuietUninstallString", $"\"{uninstallerPath}\" /uninstall /silent");
                    key.SetValue("URLInfoAbout", "https://github.com/mephisto-mert/radial");
                    key.SetValue("HelpLink", "https://github.com/mephisto-mert/radial/issues");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Registry registration error: {ex.Message}");
            }
        }

        public static void SetStartup(string targetDirectory, bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegRunKey, writable: true);
                if (key != null)
                {
                    if (enable)
                    {
                        string exePath = Path.Combine(targetDirectory, "RadialLauncher.exe");
                        key.SetValue("RadialLauncher", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("RadialLauncher", throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Startup registration error: {ex.Message}");
            }
        }

        public static void PerformUninstall(bool removeUserData)
        {
            // 1. Kill running Radial Launcher processes
            try
            {
                var processes = Process.GetProcessesByName("RadialLauncher");
                foreach (var p in processes)
                {
                    p.Kill();
                    p.WaitForExit(2000);
                }
            }
            catch { }

            // 2. Remove registry keys
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(RegRunKey, writable: true);
                runKey?.DeleteValue("RadialLauncher", throwOnMissingValue: false);
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegUninstallKey, throwOnMissingSubKey: false);
            }
            catch { }

            // 3. Remove shortcuts
            try
            {
                string desktopShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "Radial Launcher.lnk");
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                string startMenuFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Radial Launcher");
                if (Directory.Exists(startMenuFolder)) Directory.Delete(startMenuFolder, recursive: true);
            }
            catch { }

            // 4. Optionally remove user data
            if (removeUserData)
            {
                try
                {
                    string localData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "RadialLauncher");
                    if (Directory.Exists(localData))
                    {
                        Directory.Delete(localData, recursive: true);
                    }
                }
                catch { }
            }

            // 5. Self-delete installation directory
            try
            {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                string cmd = $"/c timeout /t 2 /nobreak > NUL & rmdir /s /q \"{currentDir}\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }
    }
}
