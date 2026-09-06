using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Moq;
using RadialLauncher.Data;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Clipboard;
using RadialLauncher.Services.Context;
using RadialLauncher.Services.Icons;
using RadialLauncher.Services.Plugins;
using RadialLauncher.Services.Processes;
using RadialLauncher.Services.Scanning;
using RadialLauncher.Services.Sync;
using RadialLauncher.Services.Themes;
using RadialLauncher.Services.Updates;
using RadialLauncher.Services.VirtualDesktop;
using RadialLauncher.Services.Windows;
using RadialLauncher.UI.ViewModels;
using RadialLauncher.UI.Windows;
using Xunit;

namespace RadialLauncher.Tests
{
    public class VisualRenderTests
    {
        private readonly string _outputDir = @"C:\Users\pc\.gemini\antigravity\brain\ec9198f0-b139-40e5-9f59-4bd46bf8bf13\scratch";

        [Fact]
        public void RenderAllManagementTabsAndRadialOverlay_ToPngs()
        {
            Exception? threadEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        new Application();
                    }

                    // Setup Mocks and Services
                    var itemRepoMock = new Mock<IItemRepository>();
                    var catRepoMock = new Mock<ICategoryRepository>();
                    var startupMock = new Mock<IStartupManager>();
                    var syncMock = new Mock<ISyncService>();
                    var scannerMock = new Mock<IPCScannerService>();
                    var dbMock = new Mock<IDatabaseManager>();
                    var themeService = ThemeService.Instance;
                    var procMock = new Mock<IProcessRunner>();
                    var clipMock = new Mock<IClipboardService>();
                    var vDeskMock = new Mock<IVirtualDesktopService>();
                    var plugMock = new Mock<IPluginService>();
                    var actionMock = new Mock<ISystemActionService>();
                    var winMock = new Mock<IWindowSwitcherService>();
                    var iconExtractorMock = new Mock<IIconExtractor>();
                    var ctxService = new ContextualActionService();

                    var testCategories = new List<Category>
                    {
                        new Category { Id = 1, Name = "Genel", Position = 0 },
                        new Category { Id = 2, Name = "Geliştirme", Position = 1 },
                        new Category { Id = 3, Name = "Oyunlar", Position = 2 },
                        new Category { Id = 4, Name = "Sistem", Position = 3 }
                    };

                    var testItems = new List<LauncherItem>
                    {
                        new LauncherItem { Id = 1, Name = "Google Chrome", Target = "chrome.exe", Type = "EXE", CategoryId = 1, Position = 1, LaunchCount = 42, UseCount = 42, IsFavorite = true },
                        new LauncherItem { Id = 2, Name = "Visual Studio Code", Target = "code.exe", Type = "EXE", CategoryId = 2, Position = 2, LaunchCount = 99, UseCount = 99, IsFavorite = true },
                        new LauncherItem { Id = 3, Name = "Cyberpunk 2077", Target = "steam://rungameid/1091500", Type = "Steam", CategoryId = 3, Position = 3, LaunchCount = 15, UseCount = 15, IsFavorite = false },
                        new LauncherItem { Id = 4, Name = "Windows Terminal", Target = "wt.exe", Type = "EXE", CategoryId = 4, Position = 4, LaunchCount = 55, UseCount = 55, IsFavorite = true },
                        new LauncherItem { Id = 5, Name = "GitHub Desktop", Target = "github.exe", Type = "EXE", CategoryId = 2, Position = 5, LaunchCount = 20, UseCount = 20, IsFavorite = false },
                        new LauncherItem { Id = 6, Name = "Spotify", Target = "spotify.exe", Type = "EXE", CategoryId = 1, Position = 6, LaunchCount = 30, UseCount = 30, IsFavorite = true },
                        new LauncherItem { Id = 7, Name = "Discord", Target = "discord.exe", Type = "EXE", CategoryId = 1, Position = 7, LaunchCount = 60, UseCount = 60, IsFavorite = true },
                        new LauncherItem { Id = 8, Name = "Calculator", Target = "calc.exe", Type = "EXE", CategoryId = 4, Position = 8, LaunchCount = 12, UseCount = 12, IsFavorite = false },
                        new LauncherItem { Id = 9, Name = "Notepad++", Target = "notepad++.exe", Type = "EXE", CategoryId = 2, Position = 9, LaunchCount = 18, UseCount = 18, IsFavorite = false },
                        new LauncherItem { Id = 10, Name = "File Explorer", Target = "explorer.exe", Type = "Folder", CategoryId = 4, Position = 10, LaunchCount = 40, UseCount = 40, IsFavorite = true }
                    };

                    catRepoMock.Setup(c => c.GetAll()).Returns(testCategories);
                    itemRepoMock.Setup(i => i.GetAll()).Returns(testItems);
                    itemRepoMock.Setup(i => i.GetByCategoryId(It.IsAny<int>())).Returns(testItems);
                    winMock.Setup(s => s.GetOpenWindows()).Returns(new List<WindowInfo>());
                    clipMock.Setup(c => c.GetRecentHistory(It.IsAny<int>())).Returns(new List<ClipboardItem>());
                    syncMock.Setup(s => s.GetLocalBackups()).Returns(new List<string>
                    {
                        @"C:\Users\pc\AppData\Local\RadialLauncher\Backups\backup_20260906.json"
                    });

                    var vm = new ManagementViewModel(itemRepoMock.Object, catRepoMock.Object, themeService, scannerMock.Object, syncMock.Object, dbMock.Object);
                    var managementWin = new ManagementWindow(vm, startupMock.Object, themeService, syncMock.Object);

                    managementWin.Width = 1050;
                    managementWin.Height = 720;
                    managementWin.Measure(new Size(1050, 720));
                    managementWin.Arrange(new Rect(0, 0, 1050, 720));
                    managementWin.Show();
                    managementWin.UpdateLayout();

                    var mainTabs = managementWin.FindName("MainTabs") as TabControl;
                    Assert.NotNull(mainTabs);

                    // Tab 1: Apps
                    mainTabs.SelectedIndex = 0;
                    managementWin.UpdateLayout();
                    SaveWindowToPng(managementWin, Path.Combine(_outputDir, "visual_tab1_apps.png"), 1050, 720);

                    // Tab 2: Themes
                    mainTabs.SelectedIndex = 1;
                    managementWin.UpdateLayout();
                    SaveWindowToPng(managementWin, Path.Combine(_outputDir, "visual_tab2_themes.png"), 1050, 720);

                    // Tab 3: Shortcuts
                    mainTabs.SelectedIndex = 2;
                    managementWin.UpdateLayout();
                    SaveWindowToPng(managementWin, Path.Combine(_outputDir, "visual_tab3_shortcuts.png"), 1050, 720);

                    // Tab 4: Backups
                    mainTabs.SelectedIndex = 3;
                    managementWin.UpdateLayout();
                    SaveWindowToPng(managementWin, Path.Combine(_outputDir, "visual_tab4_backups.png"), 1050, 720);

                    // Tab 5: Diagnostics
                    mainTabs.SelectedIndex = 4;
                    managementWin.UpdateLayout();
                    SaveWindowToPng(managementWin, Path.Combine(_outputDir, "visual_tab5_diagnostics.png"), 1050, 720);

                    managementWin.Close();

                    // Now Render RadialMenuWindow
                    var radialVm = new RadialMenuViewModel(
                        itemRepoMock.Object,
                        catRepoMock.Object,
                        procMock.Object,
                        themeService,
                        clipMock.Object,
                        vDeskMock.Object,
                        actionMock.Object,
                        winMock.Object,
                        plugMock.Object,
                        ctxService
                    );
                    radialVm.InitializeForDisplay();

                    var radialWin = new RadialMenuWindow(radialVm, iconExtractorMock.Object, ctxService);
                    radialWin.Width = 600;
                    radialWin.Height = 600;
                    radialWin.Measure(new Size(600, 600));
                    radialWin.Arrange(new Rect(0, 0, 600, 600));
                    radialWin.Show();

                    var rootGrid = radialWin.FindName("RootGrid") as FrameworkElement;
                    if (rootGrid != null)
                    {
                        rootGrid.Opacity = 1.0;
                        rootGrid.RenderTransform = Transform.Identity;
                        rootGrid.Measure(new Size(600, 600));
                        rootGrid.Arrange(new Rect(0, 0, 600, 600));
                    }

                    radialWin.ApplyThemeVisuals(themeService.GetCurrentTheme());
                    radialWin.RenderLayout();
                    radialWin.UpdateLayout();

                    SaveWindowToPng(radialWin, Path.Combine(_outputDir, "visual_radial_default.png"), 600, 600);

                    // Simulate hover on item 3 (Cyberpunk / Steam item)
                    radialWin.ShowContextActions(testItems[2]);
                    radialWin.UpdateLayout();
                    SaveWindowToPng(radialWin, Path.Combine(_outputDir, "visual_radial_hovered.png"), 600, 600);

                    radialWin.Close();
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(15000);

            if (threadEx != null)
            {
                throw new Exception("STA Visual Test Exception: " + threadEx.ToString(), threadEx);
            }
        }

        private static void SaveWindowToPng(FrameworkElement element, string filename, int width, int height)
        {
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var visualToRender = (element is Window win && win.Content is Visual visualContent) ? visualContent : element;
            
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // Draw subtle dark background so transparent overlay elements pop cleanly
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 17, 23)), null, new Rect(0, 0, width, height));
                var brush = new VisualBrush(visualToRender);
                dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            }
            rtb.Render(drawingVisual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.OpenWrite(filename))
            {
                encoder.Save(fs);
            }
        }
    }
}
