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
using RadialLauncher.Services.Localization;
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
    public class VisualRenderTests : IDisposable
    {
        private readonly string _docsImagesDir = @"C:\Users\pc\Desktop\RadialLauncher\docs\images";
        private readonly string _tempDir;

        public VisualRenderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"visual_test_root_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            RadialLauncher.Services.Data.UserDataPathProvider.Instance.SetOverrideDataRoot(_tempDir);
            Directory.CreateDirectory(_docsImagesDir);
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
                        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    }
                    else
                    {
                        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    }

                    // Enforce pure English localization for screenshots
                    LocalizationService.Instance.SetLanguage("en");

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
                        new Category { Id = 1, Name = "General", Position = 0 },
                        new Category { Id = 2, Name = "Development", Position = 1 },
                        new Category { Id = 3, Name = "Games", Position = 2 },
                        new Category { Id = 4, Name = "System Tools", Position = 3 }
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
                    dbMock.Setup(d => d.GetAllCategories()).Returns(testCategories);
                    dbMock.Setup(d => d.GetAllItems()).Returns(testItems);
                    winMock.Setup(s => s.GetOpenWindows()).Returns(new List<WindowInfo>());
                    clipMock.Setup(c => c.GetRecentHistory(It.IsAny<int>())).Returns(new List<ClipboardItem>());
                    syncMock.Setup(s => s.GetLocalBackups()).Returns(new List<string>
                    {
                        @"C:\Users\pc\AppData\Local\RadialLauncher\Backups\backup_20260907.json",
                        @"C:\Users\pc\AppData\Local\RadialLauncher\Backups\backup_20260906.json"
                    });

                    var vm = new ManagementViewModel(itemRepoMock.Object, catRepoMock.Object, themeService, scannerMock.Object, syncMock.Object, dbMock.Object);
                    var managementWin = new ManagementWindow(vm, startupMock.Object, themeService, syncMock.Object);

                    managementWin.Width = 1050;
                    managementWin.Height = 720;
                    managementWin.Left = -2000; // Position off-screen
                    managementWin.Top = -2000;
                    managementWin.Show();

                    var mainTabs = managementWin.FindName("MainTabs") as TabControl;
                    Assert.NotNull(mainTabs);

                    void FlushDispatcher()
                    {
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.Background);
                    }

                    // Tab 1: Apps & Scanner
                    mainTabs.SelectedIndex = 0;
                    managementWin.UpdateLayout();
                    FlushDispatcher();
                    SaveWindowToPng(managementWin, Path.Combine(_docsImagesDir, "03_settings_apps_scanner.png"), 1050, 720);

                    // Tab 2: Themes & Contrast
                    mainTabs.SelectedIndex = 1;
                    managementWin.UpdateLayout();
                    FlushDispatcher();
                    SaveWindowToPng(managementWin, Path.Combine(_docsImagesDir, "04_settings_themes_contrast.png"), 1050, 720);

                    // Tab 3: Shortcuts & Startup
                    mainTabs.SelectedIndex = 2;
                    managementWin.UpdateLayout();
                    FlushDispatcher();
                    SaveWindowToPng(managementWin, Path.Combine(_docsImagesDir, "05_settings_hotkeys_startup.png"), 1050, 720);

                    // Tab 4: Backups & Portability
                    mainTabs.SelectedIndex = 3;
                    managementWin.UpdateLayout();
                    FlushDispatcher();
                    SaveWindowToPng(managementWin, Path.Combine(_docsImagesDir, "06_settings_backups_portable.png"), 1050, 720);

                    // Tab 5: Language & Diagnostics
                    mainTabs.SelectedIndex = 4;
                    managementWin.UpdateLayout();
                    FlushDispatcher();
                    SaveWindowToPng(managementWin, Path.Combine(_docsImagesDir, "07_settings_language_diagnostics.png"), 1050, 720);

                    managementWin.Close();

                    // Render Add Item Dialog
                    var addItemWin = new AddItemWindow(dbMock.Object, iconExtractorMock.Object, actionMock.Object);
                    addItemWin.Width = 520;
                    addItemWin.Height = 440;
                    addItemWin.Left = -2000;
                    addItemWin.Top = -2000;
                    addItemWin.Show();
                    addItemWin.UpdateLayout();
                    FlushDispatcher();
                    SaveWindowToPng(addItemWin, Path.Combine(_docsImagesDir, "08_add_item_dialog.png"), 520, 440);
                    addItemWin.Close();

                    // Render Setup Wizard
                    var setupWin = new RadialLauncher.Installer.MainWindow();
                    setupWin.Width = 640;
                    setupWin.Height = 470;
                    setupWin.Left = -2000;
                    setupWin.Top = -2000;
                    setupWin.Show();
                    setupWin.UpdateLayout();
                    FlushDispatcher();
                    SaveWindowToPng(setupWin, Path.Combine(_docsImagesDir, "09_setup_wizard.png"), 640, 470);
                    setupWin.Close();

                    // Render RadialMenuWindow HUD
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

                    SaveWindowToPng(radialWin, Path.Combine(_docsImagesDir, "01_radial_hud_main.png"), 600, 600);

                    // Simulate hover on item 3 (Cyberpunk / Steam game with Quick Actions Micro HUD)
                    radialWin.ShowContextActions(testItems[2]);
                    radialWin.UpdateLayout();
                    SaveWindowToPng(radialWin, Path.Combine(_docsImagesDir, "02_radial_hud_actions.png"), 600, 600);
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
                finally
                {
                    try
                    {
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                    }
                    catch { }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
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
                // Draw sleek dark backdrop matching Radial Launcher aesthetic
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(11, 14, 23)), null, new Rect(0, 0, width, height));
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
