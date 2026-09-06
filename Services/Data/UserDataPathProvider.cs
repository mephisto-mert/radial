using System;
using System.IO;

namespace RadialLauncher.Services.Data
{
    public class UserDataPathProvider : IUserDataPathProvider
    {
        private static UserDataPathProvider? _instance;
        public static UserDataPathProvider Instance => _instance ??= new UserDataPathProvider();

        private string? _overrideRoot;

        public UserDataPathProvider(string? overrideRoot = null)
        {
            _overrideRoot = overrideRoot;
        }

        public void SetOverrideDataRoot(string? rootPath)
        {
            _overrideRoot = rootPath;
        }

        public string GetDataDirectory() => GetAppDataFolder();

        public string GetAppDataFolder()
        {
            if (!string.IsNullOrEmpty(_overrideRoot))
            {
                if (!Directory.Exists(_overrideRoot)) Directory.CreateDirectory(_overrideRoot);
                return _overrideRoot;
            }

            string? envOverride = Environment.GetEnvironmentVariable("RADIAL_LAUNCHER_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                if (!Directory.Exists(envOverride)) Directory.CreateDirectory(envOverride);
                return envOverride;
            }

            string defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadialLauncher");
            if (!Directory.Exists(defaultFolder)) Directory.CreateDirectory(defaultFolder);
            return defaultFolder;
        }

        public string GetDatabasePath() => Path.Combine(GetAppDataFolder(), "launcher.db");
        public string GetSettingsPath() => Path.Combine(GetAppDataFolder(), "settings.json");
        
        public string GetBackupsFolder()
        {
            string dir = Path.Combine(GetAppDataFolder(), "Backups");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetLogsFolder()
        {
            string dir = Path.Combine(GetAppDataFolder(), "Logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetCustomThemesFolder()
        {
            string dir = Path.Combine(GetAppDataFolder(), "CustomThemes");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetFaviconCacheFolder()
        {
            string dir = Path.Combine(GetAppDataFolder(), "Favicons");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
