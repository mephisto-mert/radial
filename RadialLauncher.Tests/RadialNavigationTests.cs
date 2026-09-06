using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    public class RadialNavigationTests : IDisposable
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly string _tempDir;

        public RadialNavigationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"nav_test_root_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            RadialLauncher.Services.Data.UserDataPathProvider.Instance.SetOverrideDataRoot(_tempDir);

            var itemRepoMock = new Mock<IItemRepository>();
            var catRepoMock = new Mock<ICategoryRepository>();
            var procRunnerMock = new Mock<IProcessRunner>();
            var themeServiceMock = new Mock<IThemeService>();
            var clipMock = new Mock<IClipboardService>();
            var desktopMock = new Mock<IVirtualDesktopService>();
            var actionMock = new Mock<ISystemActionService>();
            var switcherMock = new Mock<IWindowSwitcherService>();
            var pluginMock = new Mock<IPluginService>();
            var contextMock = new Mock<IContextualActionService>();

            themeServiceMock.Setup(t => t.GetCurrentTheme()).Returns(new Theme { Name = "Dark" });
            switcherMock.Setup(s => s.GetOpenWindows()).Returns(new List<WindowInfo>());
            clipMock.Setup(c => c.GetRecentHistory(Moq.It.IsAny<int>())).Returns(new List<ClipboardItem>());
            catRepoMock.Setup(c => c.GetAll()).Returns(new List<Category>
            {
                new Category { Id = 1, Name = "⭐ Most Used", SystemKey = "Cat_MostUsed", Position = 0 },
                new Category { Id = 2, Name = "🎮 Games", SystemKey = "Cat_Games", Position = 1 },
                new Category { Id = 3, Name = "Tools", Position = 2 }
            });

            var dummyItems = new List<LauncherItem>();
            for (int i = 0; i < 40; i++)
            {
                dummyItems.Add(new LauncherItem
                {
                    Id = i + 1,
                    Name = $"Item {i + 1}",
                    CategoryId = 1,
                    Position = i
                });
            }
            itemRepoMock.Setup(r => r.GetAll()).Returns(dummyItems);

            _viewModel = new RadialMenuViewModel(
                itemRepoMock.Object,
                catRepoMock.Object,
                procRunnerMock.Object,
                themeServiceMock.Object,
                clipMock.Object,
                desktopMock.Object,
                actionMock.Object,
                switcherMock.Object,
                pluginMock.Object,
                contextMock.Object);

            _viewModel.InitializeForDisplay();
        }

        [Fact]
        public void NavigateNextGlobal_IncrementsPage_ThenCyclesToNextCategory()
        {
            _viewModel.CurrentPageIndex = 0;
            Assert.True(_viewModel.TotalPages > 1);

            int initialCat = _viewModel.CurrentCategoryIndex;
            _viewModel.NavigateNextGlobal();
            Assert.Equal(1, _viewModel.CurrentPageIndex);

            // Move to last page
            _viewModel.CurrentPageIndex = _viewModel.TotalPages - 1;
            _viewModel.NavigateNextGlobal();

            // When at last page, advancing next should switch to next category
            Assert.NotEqual(initialCat, _viewModel.CurrentCategoryIndex);
        }

        [Fact]
        public void NavigatePrevGlobal_DecrementsPage_ThenCyclesToPrevCategory()
        {
            _viewModel.CurrentPageIndex = 1;
            _viewModel.NavigatePrevGlobal();
            Assert.Equal(0, _viewModel.CurrentPageIndex);

            // When at first page, advancing prev should switch category
            int initialCat = _viewModel.CurrentCategoryIndex;
            _viewModel.NavigatePrevGlobal();
            Assert.NotEqual(initialCat, _viewModel.CurrentCategoryIndex);
        }

        public void Dispose()
        {
            RadialLauncher.Services.Data.UserDataPathProvider.Instance.SetOverrideDataRoot(null);
            try
            {
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
    }
}
