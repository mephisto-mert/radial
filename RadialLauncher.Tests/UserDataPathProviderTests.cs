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
        public void DefaultDataDirectory_ReturnsValidDataPath()
        {
            UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            string dataDir = UserDataPathProvider.Instance.GetDataDirectory();
            
            Assert.False(string.IsNullOrWhiteSpace(dataDir));
            Assert.True(Directory.Exists(dataDir));
        }

        [Fact]
        public void DefaultDataDirectory_PortableMode_ReturnsLocalDataDir()
        {
            string baseDir = AppContext.BaseDirectory;
            string portableMarker = Path.Combine(baseDir, "portable.mode");
            bool isPortable = File.Exists(portableMarker);

            UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            string dataDir = UserDataPathProvider.Instance.GetDataDirectory();

            if (isPortable)
            {
                Assert.Equal(Path.Combine(baseDir, "data"), dataDir);
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                Assert.StartsWith(localAppData, dataDir, StringComparison.OrdinalIgnoreCase);
            }
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
