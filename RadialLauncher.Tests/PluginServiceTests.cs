using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RadialLauncher.Models;
using RadialLauncher.Services.Plugins;
using Xunit;

namespace RadialLauncher.Tests
{
    public class MockValidItemProvider : IRadialItemProvider
    {
        public string ProviderName => "Valid Provider";
        public string CategoryName => "Valid Category";
        public string CategoryColor => "#123456";

        public IEnumerable<LauncherItem> GetItems()
        {
            return new List<LauncherItem>
            {
                new LauncherItem { Id = -901, Name = "Item 1", Type = "URL", Target = "https://example.com" },
                new LauncherItem { Id = -902, Name = "Item 2", Type = "EXE", Target = "notepad.exe" }
            };
        }
    }

    public class MockThrowingItemProvider : IRadialItemProvider
    {
        public string ProviderName => "Throwing Provider";
        public string CategoryName => "Throwing Category";
        public string CategoryColor => "#FF0000";

        public IEnumerable<LauncherItem> GetItems()
        {
            throw new InvalidOperationException("Simulated plugin crash in GetItems()");
        }
    }

    public class MockNullItemProvider : IRadialItemProvider
    {
        public string ProviderName => "Null Provider";
        public string CategoryName => "Null Category";
        public string CategoryColor => "#00FF00";

        public IEnumerable<LauncherItem> GetItems() => null!;
    }

    public class MockMalformedItemProvider : IRadialItemProvider
    {
        public string ProviderName => "Malformed Provider";
        public string CategoryName => "Malformed Category";
        public string CategoryColor => "#FFFF00";

        public IEnumerable<LauncherItem> GetItems()
        {
            return new List<LauncherItem?>
            {
                null,
                new LauncherItem { Id = 0, Name = "", Target = "" },
                new LauncherItem { Id = -950, Name = "Valid Item Inside", Type = "EXE", Target = "calc.exe" }
            }!;
        }
    }

    public class PluginServiceTests
    {
        [Fact]
        public void RegisterProvider_AddsProviderSuccessfully()
        {
            var service = new PluginService();
            var provider = new MockValidItemProvider();

            service.RegisterProvider(provider);

            var providers = service.GetProviders();
            Assert.Single(providers);
            Assert.Equal("Valid Provider", providers[0].ProviderName);
            Assert.Equal("Valid Category", providers[0].CategoryName);
            Assert.Equal(2, providers[0].GetItems().Count());
        }

        [Fact]
        public void GetSafeItems_WithThrowingProvider_ReturnsEmptyAndDoesNotThrow()
        {
            var service = new PluginService();
            var throwingProvider = new MockThrowingItemProvider();
            service.RegisterProvider(throwingProvider);

            var items = service.GetSafeItems(throwingProvider);

            Assert.NotNull(items);
            Assert.Empty(items);
        }

        [Fact]
        public void GetSafeItems_OneHealthyOneBroken_PreservesHealthyAndIgnoresBroken()
        {
            var service = new PluginService();
            var validProvider = new MockValidItemProvider();
            var throwingProvider = new MockThrowingItemProvider();

            service.RegisterProvider(validProvider);
            service.RegisterProvider(throwingProvider);

            var validItems = service.GetSafeItems(0);
            var brokenItems = service.GetSafeItems(1);

            Assert.Equal(2, validItems.Count);
            Assert.Empty(brokenItems);
        }

        [Fact]
        public void GetSafeItems_WithNullProviderOrNullResult_ReturnsEmptySafely()
        {
            var service = new PluginService();
            var nullProvider = new MockNullItemProvider();
            service.RegisterProvider(nullProvider);

            var itemsFromNullProvider = service.GetSafeItems(nullProvider);
            var itemsFromNullArg = service.GetSafeItems((IRadialItemProvider)null!);
            var itemsFromInvalidIndex = service.GetSafeItems(99);

            Assert.NotNull(itemsFromNullProvider);
            Assert.Empty(itemsFromNullProvider);

            Assert.NotNull(itemsFromNullArg);
            Assert.Empty(itemsFromNullArg);

            Assert.NotNull(itemsFromInvalidIndex);
            Assert.Empty(itemsFromInvalidIndex);
        }

        [Fact]
        public void GetSafeItems_WithMalformedItems_FiltersOutNullAndEmptyEntries()
        {
            var service = new PluginService();
            var malformedProvider = new MockMalformedItemProvider();
            service.RegisterProvider(malformedProvider);

            var items = service.GetSafeItems(malformedProvider);

            Assert.Single(items);
            Assert.Equal("Valid Item Inside", items[0].Name);
            Assert.Equal("calc.exe", items[0].Target);
        }

        [Fact]
        public void LoadPlugins_HandlesMissingDirectoryGracefully()
        {
            var service = new PluginService();
            string nonExistent = Path.Combine(Path.GetTempPath(), "NonExistentDir_" + Guid.NewGuid().ToString("N"));

            var ex = Record.Exception(() => service.LoadPlugins(nonExistent));
            Assert.Null(ex);
            Assert.Empty(service.GetProviders());

            if (Directory.Exists(nonExistent))
            {
                try { Directory.Delete(nonExistent, true); } catch { }
            }
        }

        [Fact]
        public void LoadPlugins_HandlesCorruptDllGracefully()
        {
            var service = new PluginService();
            string tempDir = Path.Combine(Path.GetTempPath(), "RadialLauncher_Test_CorruptPlugins_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string corruptDll = Path.Combine(tempDir, "corrupt.dll");
                File.WriteAllBytes(corruptDll, new byte[] { 0x00, 0x01, 0x02 });

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
            string solutionDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.FullName;
            string samplePluginDll = Path.Combine(solutionDir, "Plugins", "SamplePlugin", "bin", "Debug", "net7.0-windows", "SamplePlugin.dll");

            if (File.Exists(samplePluginDll))
            {
                service.LoadPluginAssembly(samplePluginDll);
                var providers = service.GetProviders();
                Assert.NotEmpty(providers);
                var items = service.GetSafeItems(providers[0]);
                Assert.NotEmpty(items);
            }
        }
    }
}