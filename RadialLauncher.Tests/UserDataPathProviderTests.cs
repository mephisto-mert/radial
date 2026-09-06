using System;
using System.IO;
using RadialLauncher.Services.Data;
using Xunit;

namespace RadialLauncher.Tests
{
    public class UserDataPathProviderTests : IDisposable
    {
        private readonly string _testRoot;

        public UserDataPathProviderTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), $"radial_path_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, recursive: true);
                }
            }
            catch { }
        }

        [Fact]
        public void DefaultDataDirectory_IsUnderLocalAppData()
        {
            UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            string dataDir = UserDataPathProvider.Instance.GetDataDirectory();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            Assert.StartsWith(localAppData, dataDir, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RadialLauncher", dataDir, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetOverrideDataRoot_RedirectsAllSubPaths()
        {
            UserDataPathProvider.Instance.SetOverrideDataRoot(_testRoot);

            Assert.Equal(_testRoot, UserDataPathProvider.Instance.GetDataDirectory());
            Assert.Equal(Path.Combine(_testRoot, "launcher.db"), UserDataPathProvider.Instance.GetDatabasePath());
            Assert.Equal(Path.Combine(_testRoot, "settings.json"), UserDataPathProvider.Instance.GetSettingsPath());
            Assert.Equal(Path.Combine(_testRoot, "Logs"), UserDataPathProvider.Instance.GetLogsFolder());
            Assert.Equal(Path.Combine(_testRoot, "Backups"), UserDataPathProvider.Instance.GetBackupsFolder());
            Assert.Equal(Path.Combine(_testRoot, "CustomThemes"), UserDataPathProvider.Instance.GetCustomThemesFolder());
        }
    }
}
