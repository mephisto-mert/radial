using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
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

        public static string GetUserDataPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "RadialLauncher");
        }

        public static void ResetUserData()
        {
            try
            {
                string userData = GetUserDataPath();
                if (Directory.Exists(userData))
                {
                    string dbPath = Path.Combine(userData, "launcher.db");
                    if (File.Exists(dbPath))
                    {
                        try { File.Delete(dbPath); } catch { }
                    }
                    string dbWal = Path.Combine(userData, "launcher.db-wal");
                    if (File.Exists(dbWal))
                    {
                        try { File.Delete(dbWal); } catch { }
                    }
                    string dbShm = Path.Combine(userData, "launcher.db-shm");
                    if (File.Exists(dbShm))
                    {
                        try { File.Delete(dbShm); } catch { }
                    }
                    string settingsPath = Path.Combine(userData, "settings.json");
                    if (File.Exists(settingsPath))
                    {
                        try { File.Delete(settingsPath); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void ExtractPayload(string targetDirectory, Action<int, string>? reportProgress = null)
        {
            Directory.CreateDirectory(targetDirectory);
            reportProgress?.Invoke(10, "Preparing target directory...");

            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream("RadialLauncher.Installer.payload.zip");

            if (resourceStream == null)
            {
                throw new InvalidOperationException("Installer payload is missing or corrupted. Cannot continue installation.");
            }

            using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);
            int totalEntries = archive.Entries.Count;
            int count = 0;

            foreach (var entry in archive.Entries)
            {
                count++;
                string destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
                if (!destinationPath.StartsWith(Path.GetFullPath(targetDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Path traversal security protection
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
                reportProgress?.Invoke(pct, $"Extracting: {entry.Name}");
            }

            // Copy self as Uninstall.exe
            try
            {
                string currentExe = Environment.ProcessPath ?? System.AppContext.BaseDirectory;
                string uninstallerPath = Path.Combine(targetDirectory, "Uninstall.exe");
                if (File.Exists(currentExe))
                {
                    File.Copy(currentExe, uninstallerPath, overwrite: true);
                }
            }
            catch
            {
                // Ignore copy errors in restricted environments
            }

            reportProgress?.Invoke(85, "Files extracted successfully.");
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
                CreateShortcutFile(shortcutPath, exePath, targetDirectory, iconPath, "Radial Launcher - Circular App & Game Launcher");
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
                    if (File.Exists(iconLocation))
                    {
                        shortcut.IconLocation = iconLocation;
                    }
                    shortcut.Save();
                }
            }
            catch
            {
                // Ignore shortcut creation failure in restricted test environments
            }
        }

        public static void RegisterInWindows(string targetDirectory, bool runOnStartup)
        {
            string exePath = Path.Combine(targetDirectory, "RadialLauncher.exe");
            string uninstallerExe = Path.Combine(targetDirectory, "Uninstall.exe");
            string iconPath = Path.Combine(targetDirectory, "app.ico");
            if (!File.Exists(iconPath)) iconPath = exePath;

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegUninstallKey);
                if (key != null)
                {
                    key.SetValue("DisplayName", "Radial Launcher", RegistryValueKind.String);
                    key.SetValue("DisplayVersion", "1.0.0", RegistryValueKind.String);
                    key.SetValue("Publisher", "Radial Launcher Team", RegistryValueKind.String);
                    key.SetValue("InstallLocation", targetDirectory, RegistryValueKind.String);
                    key.SetValue("DisplayIcon", iconPath, RegistryValueKind.String);
                    key.SetValue("UninstallString", $"\"{uninstallerExe}\" --uninstall", RegistryValueKind.String);
                    key.SetValue("QuietUninstallString", $"\"{uninstallerExe}\" --uninstall --silent", RegistryValueKind.String);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                    long sizeKb = 0;
                    try
                    {
                        var di = new DirectoryInfo(targetDirectory);
                        foreach (var fi in di.EnumerateFiles("*", SearchOption.AllDirectories))
                        {
                            sizeKb += fi.Length;
                        }
                        key.SetValue("EstimatedSize", (int)(sizeKb / 1024), RegistryValueKind.DWord);
                    }
                    catch { }
                }
            }
            catch
            {
                // Ignore registry access errors
            }

            try
            {
                using var runKey = Registry.CurrentUser.CreateSubKey(RegRunKey);
                if (runKey != null)
                {
                    if (runOnStartup)
                    {
                        runKey.SetValue(AppName, $"\"{exePath}\"", RegistryValueKind.String);
                    }
                    else
                    {
                        if (runKey.GetValue(AppName) != null) runKey.DeleteValue(AppName, false);
                    }
                }
            }
            catch
            {
                // Ignore startup registration errors
            }
        }

        public static void PerformUninstall(string targetDirectory, bool removeUserData = false)
        {
            // 1. Terminate running instances of RadialLauncher
            try
            {
                foreach (var proc in Process.GetProcessesByName("RadialLauncher"))
                {
                    try { proc.Kill(); proc.WaitForExit(3000); } catch { }
                }
            }
            catch { }

            // 2. Remove Shortcuts
            try
            {
                string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Radial Launcher.lnk");
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                string startMenuFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Radial Launcher");
                if (Directory.Exists(startMenuFolder)) Directory.Delete(startMenuFolder, true);
            }
            catch { }

            // 3. Remove Registry keys
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegUninstallKey, false);
                using var runKey = Registry.CurrentUser.OpenSubKey(RegRunKey, true);
                runKey?.DeleteValue(AppName, false);
            }
            catch { }

            // 4. Optionally remove User Data
            if (removeUserData)
            {
                try
                {
                    string userData = GetUserDataPath();
                    if (Directory.Exists(userData)) Directory.Delete(userData, true);
                }
                catch { }
            }

            // 5. Delete installation directory files (schedule self-cleanup via cmd)
            try
            {
                if (Directory.Exists(targetDirectory))
                {
                    foreach (var file in Directory.GetFiles(targetDirectory))
                    {
                        if (Path.GetFileName(file).Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        try { File.Delete(file); } catch { }
                    }

                    // Delete self via background cmd
                    string cmd = $"/c timeout /t 1 /nobreak & rmdir /s /q \"{targetDirectory}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = cmd,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch { }
        }
    }
}
