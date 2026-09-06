using System;
using System.IO;

namespace RadialLauncher.Services.Data
{
    public class UserDataPathProvider : IUserDataPathProvider
    {
        private static UserDataPathProvider? _instance;
        public static UserDataPathProvider Instance => _instance ??= new UserDataPathProvider();

        private readonly System.Threading.AsyncLocal<string?> _asyncOverrideRoot = new();
        private volatile string? _overrideRoot;

        public UserDataPathProvider(string? overrideRoot = null)
        {
            _overrideRoot = overrideRoot;
        }

        public void SetOverrideDataRoot(string? rootPath)
        {
            _overrideRoot = rootPath;
            _asyncOverrideRoot.Value = rootPath;
        }

        public IDisposable SetScopedOverrideDataRoot(string rootPath)
        {
            var previous = _asyncOverrideRoot.Value;
            _asyncOverrideRoot.Value = rootPath;
            return new DisposableAction(() => _asyncOverrideRoot.Value = previous);
        }

        private sealed class DisposableAction : IDisposable
        {
            private readonly Action _action;
            public DisposableAction(Action action) => _action = action;
            public void Dispose() => _action();
        }

        public string GetDataDirectory() => GetAppDataFolder();

        public string GetAppDataFolder()
        {
            string? scoped = _asyncOverrideRoot.Value;
            if (!string.IsNullOrEmpty(scoped))
            {
                if (!Directory.Exists(scoped)) Directory.CreateDirectory(scoped);
                return scoped;
            }

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

            try
            {
                string baseDir = AppContext.BaseDirectory;
                string portableMarker = Path.Combine(baseDir, "portable.mode");
                string portableMarkerTxt = Path.Combine(baseDir, "portable.txt");
                string portableDataDir = Path.Combine(baseDir, "data");

                if (Directory.Exists(portableDataDir) || File.Exists(portableMarker) || File.Exists(portableMarkerTxt))
                {
                    if (!Directory.Exists(portableDataDir)) Directory.CreateDirectory(portableDataDir);
                    return portableDataDir;
                }
            }
            catch { }

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
