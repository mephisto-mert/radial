using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Hardcodet.Wpf.TaskbarNotification;
using RadialLauncher.Data;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Clipboard;
using RadialLauncher.Services.Games;
using RadialLauncher.Services.Icons;
using RadialLauncher.Services.Logging;
using RadialLauncher.Services.Plugins;
using RadialLauncher.Services.Processes;
using RadialLauncher.Services.Scanning;
using RadialLauncher.Services.Sync;
using RadialLauncher.Services.Themes;
using RadialLauncher.Services.VirtualDesktop;
using RadialLauncher.Services.Windows;
using RadialLauncher.UI.ViewModels;
using RadialLauncher.UI.Windows;
using RadialLauncher.Services.Data;
using Serilog;

namespace RadialLauncher
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        private TaskbarIcon? notifyIcon;
        private Hooks.GlobalMouseHook? mouseHook;
        private RadialMenuWindow? _radialMenu;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Hooks.Point lpPoint);

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 1. Initialize Serilog rolling file logger
                AppLogger.Initialize();

                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    Log.Fatal(args.ExceptionObject as Exception, "AppDomain Unhandled Exception");
                };

                DispatcherUnhandledException += (s, args) =>
                {
                    Log.Error(args.Exception, "Dispatcher Unhandled Exception");
                    args.Handled = true; // Prevent abrupt termination
                };

                Exit += (s, args) =>
                {
                    Log.Information("Application exiting with code {Code}", args.ApplicationExitCode);
                    AppLogger.CloseAndFlush();
                };

                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                base.OnStartup(e);

                Log.Information("Configuring Dependency Injection services...");
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();

                // 2. Initialize Database and Repositories
                var dbManager = ServiceProvider.GetRequiredService<IDatabaseManager>();
                dbManager.InitializeDatabase();

                // 3. Initialize Radial Window
                _radialMenu = ServiceProvider.GetRequiredService<RadialMenuWindow>();
                _radialMenu.Show();
                _radialMenu.Hide();

                var themeService = ServiceProvider.GetRequiredService<IThemeService>();

                // 4. Setup Tray Icon
                notifyIcon = new TaskbarIcon();
                notifyIcon.ToolTipText = "Radial Launcher (Pro v2.0)";

                try 
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var icoPath = Path.Combine(baseDir, "app.ico");
                    var icon = RadialLauncher.Services.IconExtractor.GetIconForFile(icoPath);
                    if (icon != null)
                    {
                        notifyIcon.IconSource = icon;
                    }
                    else
                    {
                        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (exePath.EndsWith(".dll")) exePath = exePath.Replace(".dll", ".exe");
                        var fallbackIcon = RadialLauncher.Services.IconExtractor.GetIconForFile(exePath);
                        if (fallbackIcon != null) notifyIcon.IconSource = fallbackIcon;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not set tray icon image");
                }

                var menu = new System.Windows.Controls.ContextMenu();
                
                var openItem = new System.Windows.Controls.MenuItem { Header = "Menüyü Aç" };
                openItem.Click += (s, args) => OpenLauncher();
                
                var settingsItem = new System.Windows.Controls.MenuItem { Header = "Ayarlar & Yönetim" };
                settingsItem.Click += (s, args) => OpenSettings();
                
                var exitItem = new System.Windows.Controls.MenuItem { Header = "Çıkış" };
                exitItem.Click += (s, args) => ExitApplication();

                menu.Items.Add(openItem);
                menu.Items.Add(settingsItem);
                menu.Items.Add(new System.Windows.Controls.Separator());
                menu.Items.Add(exitItem);

                notifyIcon.ContextMenu = menu;
                notifyIcon.TrayMouseDoubleClick += (s, args) => OpenLauncher();

                // 5. Setup Global Mouse Hook
                mouseHook = new Hooks.GlobalMouseHook();
                mouseHook.TriggerMode = themeService.GetActivationShortcut();
                mouseHook.OnMiddleMouseDown += (s, pt) => 
                {
                    Current.Dispatcher.BeginInvoke(new Action(() => OpenLauncher(pt)));
                };
                mouseHook.Start();

                // 6. Setup Keyboard HotKey
                SetupHotKey(themeService.GetActivationShortcut());

                // Listen to Shortcut changes
                themeService.OnShortcutChanged += (newShortcut) =>
                {
                    if (mouseHook != null)
                    {
                        mouseHook.TriggerMode = newShortcut;
                    }
                    SetupHotKey(newShortcut);
                };

                Log.Information("Radial Launcher startup sequence completed successfully!");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal crash during application startup");
                Current.Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Http Client
            services.AddHttpClient("FaviconClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            });

            // Database & Repositories
            services.AddSingleton<IDatabaseManager, DatabaseManager>();
            services.AddSingleton<IItemRepository, ItemRepository>();
            services.AddSingleton<ICategoryRepository, CategoryRepository>();

            // Core Services
            services.AddSingleton<IThemeService>(sp => ThemeService.Instance);
            services.AddSingleton<ISystemActionService>(sp => SystemActionService.Instance);
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IPCScannerService, PCScannerService>();
            services.AddSingleton<IGameDetector, GameDetector>();
            services.AddSingleton<IWindowSwitcherService, WindowSwitcherService>();
            services.AddSingleton<IStartupManager, StartupManager>();
            services.AddSingleton<IDataExporter, DataExporter>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IVirtualDesktopService, VirtualDesktopService>();
            services.AddSingleton<IPluginService, PluginService>();
            services.AddSingleton<ISyncService, SyncService>();

            // ViewModels
            services.AddSingleton<RadialMenuViewModel>();
            services.AddTransient<ManagementViewModel>();

            // Windows
            services.AddSingleton<RadialMenuWindow>();
            services.AddTransient<ManagementWindow>();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9001;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_SPACE = 0x20;
        private const uint VK_F4 = 0x73;
        private const uint VK_OEM_3 = 0xC0; // ~ tilde

        private System.Windows.Interop.HwndSource? _hwndSource;

        private void SetupHotKey(string shortcut)
        {
            try
            {
                if (_hwndSource == null && _radialMenu != null)
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(_radialMenu);
                    _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(helper.EnsureHandle());
                    _hwndSource?.AddHook(HwndHook);
                }

                if (_hwndSource != null)
                {
                    UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);

                    uint mod = MOD_NOREPEAT;
                    uint vk = 0;

                    switch (shortcut)
                    {
                        case "AltSpace":
                            mod |= MOD_ALT;
                            vk = VK_SPACE;
                            break;
                        case "CtrlSpace":
                            mod |= MOD_CONTROL;
                            vk = VK_SPACE;
                            break;
                        case "F4":
                            vk = VK_F4;
                            break;
                        case "Tilde":
                            vk = VK_OEM_3;
                            break;
                    }

                    if (vk != 0)
                    {
                        RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, mod, vk);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register global hotkey {Shortcut}", shortcut);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OpenLauncher();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void OpenLauncher(Hooks.Point pt = default)
        {
            if (_radialMenu == null) return;
            if (pt.X == 0 && pt.Y == 0)
            {
                GetCursorPos(out pt);
            }
            _radialMenu.ShowAt(pt.X, pt.Y);
        }

        private ManagementWindow? _managementWindow;

        public void OpenSettings()
        {
            if (_managementWindow == null || !_managementWindow.IsLoaded)
            {
                _managementWindow = new ManagementWindow();
                _managementWindow.Closed += (s, e) => _managementWindow = null;
                _managementWindow.Show();
            }
            else
            {
                _managementWindow.Activate();
                if (_managementWindow.WindowState == WindowState.Minimized)
                    _managementWindow.WindowState = WindowState.Normal;
            }
        }

        private void ExitApplication()
        {
            try
            {
                if (_hwndSource != null)
                {
                    UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
                    _hwndSource.RemoveHook(HwndHook);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error releasing hotkey on exit");
            }
            mouseHook?.Dispose();
            notifyIcon?.Dispose();
            Current.Shutdown();
        }
    }
}
