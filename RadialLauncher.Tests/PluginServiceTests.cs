using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RadialLauncher.Models;
using RadialLauncher.Services.Plugins;
using Xunit;

namespace RadialLauncher.Tests
{
    public class MockItemProvider : IRadialItemProvider
    {
        public string ProviderName => "Test Provider";
        public string CategoryName => "Test Category";
        public string CategoryColor => "#123456";

        public IEnumerable<LauncherItem> GetItems()
        {
            return new List<LauncherItem>
            {
                new LauncherItem { Id = -901, Name = "Test Plugin Item", Type = "URL", Target = "https://example.com" }
            };
        }
    }

    public class PluginServiceTests
    {
        [Fact]
        public void RegisterProvider_AddsProviderSuccessfully()
        {
            var service = new PluginService();
            var provider = new MockItemProvider();

            service.RegisterProvider(provider);

            var providers = service.GetProviders();
            Assert.Single(providers);
            Assert.Equal("Test Provider", providers[0].ProviderName);
            Assert.Equal("Test Category", providers[0].CategoryName);
            Assert.Single(providers[0].GetItems());
        }

        [Fact]
        public void LoadPlugins_HandlesCorruptDllGracefully()
        {
            var service = new PluginService();
            string tempDir = Path.Combine(Path.GetTempPath(), "RadialLauncher_Test_CorruptPlugins_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Write a fake corrupt dll
                string corruptDll = Path.Combine(tempDir, "corrupt.dll");
                File.WriteAllBytes(corruptDll, new byte[] { 0x00, 0x01, 0x02 });

                // Must not throw
                var ex = Record.Exception(() => service.LoadPlugins(tempDir));
                Assert.Null(ex);
                Assert.Empty(service.GetProviders());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void LoadPluginAssembly_LoadsSamplePluginWhenBuilt()
        {
            var service = new PluginService();
            // Path where SamplePlugin builds
            string solutionDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.FullName;
            string samplePluginDll = Path.Combine(solutionDir, "Plugins", "SamplePlugin", "bin", "Debug", "net7.0-windows", "SamplePlugin.dll");

            if (File.Exists(samplePluginDll))
            {
                service.LoadPluginAssembly(samplePluginDll);
                var providers = service.GetProviders();
                Assert.Contains(providers, p => p.CategoryName == "🧩 Eklentiler");
            }
        }
    }
}
