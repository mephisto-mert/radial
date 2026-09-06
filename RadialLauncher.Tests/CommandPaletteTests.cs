using System;
using System.Collections.Generic;
using Moq;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Clipboard;
using RadialLauncher.Services.Context;
using RadialLauncher.Services.Plugins;
using RadialLauncher.Services.Processes;
using RadialLauncher.Services.Themes;
using RadialLauncher.Services.VirtualDesktop;
using RadialLauncher.Services.Windows;
using RadialLauncher.UI.ViewModels;
using Xunit;

namespace RadialLauncher.Tests
{
    public class CommandPaletteTests : IDisposable
    {
        private readonly string _tempDir;

        public CommandPaletteTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cmd_test_root_{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(_tempDir);
            RadialLauncher.Services.Data.UserDataPathProvider.Instance.SetOverrideDataRoot(_tempDir);
        }

        public void Dispose()
        {
            RadialLauncher.Services.Data.UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            try
            {
                if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }

        private RadialMenuViewModel CreateTestViewModel(List<LauncherItem>? customItems = null)
        {
            var itemRepoMock = new Mock<IItemRepository>();
            var catRepoMock = new Mock<ICategoryRepository>();
            var processRunnerMock = new Mock<IProcessRunner>();
            var themeServiceMock = new Mock<IThemeService>();
            var clipboardServiceMock = new Mock<IClipboardService>();
            var desktopServiceMock = new Mock<IVirtualDesktopService>();
            var systemActionServiceMock = new Mock<ISystemActionService>();
            var windowSwitcherMock = new Mock<IWindowSwitcherService>();
            var pluginServiceMock = new Mock<IPluginService>();
            var contextualActionServiceMock = new Mock<IContextualActionService>();

            themeServiceMock.Setup(t => t.GetCurrentTheme()).Returns(new Theme { Name = "Dark" });
            themeServiceMock.Setup(t => t.GetAllThemes()).Returns(new List<Theme>
            {
                new Theme { Name = "Dark" },
                new Theme { Name = "Light" },
                new Theme { Name = "Midnight Blue" },
                new Theme { Name = "Purple Haze" },
                new Theme { Name = "Forest" }
            });

            pluginServiceMock.Setup(p => p.GetProviders()).Returns(new List<IRadialItemProvider>());
            clipboardServiceMock.Setup(c => c.GetRecentHistory(It.IsAny<int>())).Returns(new List<ClipboardItem>());
            windowSwitcherMock.Setup(w => w.GetForegroundProcessName()).Returns(string.Empty);
            windowSwitcherMock.Setup(w => w.GetOpenWindows()).Returns(new List<WindowInfo>());
            contextualActionServiceMock.Setup(c => c.GetContextualItems(It.IsAny<string>())).Returns(new List<LauncherItem>());

            var items = customItems ?? new List<LauncherItem>
            {
                new LauncherItem { Id = 1, Name = "Calculator", Target = "calc.exe", Type = "EXE" },
                new LauncherItem { Id = 2, Name = "Notepad", Target = "notepad.exe", Type = "EXE" },
                new LauncherItem { Id = 3, Name = "VS Code", Target = "code.exe", Type = "EXE" }
            };

            itemRepoMock.Setup(r => r.GetAll()).Returns(items);
            catRepoMock.Setup(c => c.GetAll()).Returns(new List<Category>());

            var vm = new RadialMenuViewModel(
                itemRepoMock.Object,
                catRepoMock.Object,
                processRunnerMock.Object,
                themeServiceMock.Object,
                clipboardServiceMock.Object,
                desktopServiceMock.Object,
                systemActionServiceMock.Object,
                windowSwitcherMock.Object,
                pluginServiceMock.Object,
                contextualActionServiceMock.Object
            );

            vm.InitializeForDisplay();
            return vm;
        }

        [Fact]
        public void SlashRoot_ReturnsBuiltInCommands()
        {
            var vm = CreateTestViewModel();
            var results = vm.GetCommandPaletteResults("/");

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Type == "COMMAND_THEME");
            Assert.Contains(results, r => r.Type == "COMMAND_RESTART");
            Assert.Contains(results, r => r.Type == "COMMAND_LOGS");
            Assert.Contains(results, r => r.Type == "COMMAND_SETTINGS");
        }

        [Fact]
        public void ThemeCommand_FiltersThemesByName()
        {
            var vm = CreateTestViewModel();

            var allThemes = vm.GetCommandPaletteResults("/theme");
            Assert.True(allThemes.Count >= 5);

            var purpleTheme = vm.GetCommandPaletteResults("/theme purple");
            Assert.Single(purpleTheme);
            Assert.Contains("Purple Haze", purpleTheme[0].Name);
        }

        [Fact]
        public void FuzzySearch_MatchesItemNames()
        {
            var vm = CreateTestViewModel();

            var calcResults = vm.GetCommandPaletteResults("/calc");
            Assert.Contains(calcResults, r => r.Name.Contains("Calculator"));

            var noteResults = vm.GetCommandPaletteResults("/note");
            Assert.Contains(noteResults, r => r.Name.Contains("Notepad"));
        }
    }
}
