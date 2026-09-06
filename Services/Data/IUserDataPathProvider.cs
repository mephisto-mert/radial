using System;

namespace RadialLauncher.Services.Data
{
    public interface IUserDataPathProvider
    {
        string GetAppDataFolder();
        string GetDataDirectory();
        void SetOverrideDataRoot(string? rootPath);
        string GetDatabasePath();
        string GetSettingsPath();
        string GetBackupsFolder();
        string GetLogsFolder();
        string GetCustomThemesFolder();
        string GetFaviconCacheFolder();
    }
}
