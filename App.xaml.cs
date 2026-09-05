using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;

namespace RadialLauncher
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        private TaskbarIcon notifyIcon;
        private Hooks.GlobalMouseHook mouseHook;

        private UI.Windows.RadialMenuWindow _radialMenu;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Hooks.Point lpPoint);

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                var diagFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadialLauncher");
                if (!System.IO.Directory.Exists(diagFolder)) System.IO.Directory.CreateDirectory(diagFolder);

                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    try { System.IO.File.WriteAllText(System.IO.Path.Combine(diagFolder, "unhandled.log"), args.ExceptionObject?.ToString() ?? "null"); } catch { }
                };

                DispatcherUnhandledException += (s, args) =>
                {
                    try { System.IO.File.WriteAllText(System.IO.Path.Combine(diagFolder, "dispatcher_unhandled.log"), args.Exception?.ToString() ?? "null"); } catch { }
                };

                Exit += (s, args) =>
                {
                    try { System.IO.File.WriteAllText(System.IO.Path.Combine(diagFolder, "exit.log"), $"ExitCode={args.ApplicationExitCode}\nStackTrace:\n{Environment.StackTrace}"); } catch { }
                };

                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                base.OnStartup(e);

                if (e.Args.Length > 0 && e.Args[0] == "--test-scan")
                {
                    var db = new Data.DatabaseManager();
                    db.InitializeDatabase();
                    var apps = Services.PCScannerService.ScanAll();
                    Console.WriteLine($"DISCOVERED_APPS:{apps.Count}");
                    foreach (var g in apps.GroupBy(a => a.CategoryName))
                    {
                        Console.WriteLine($"GROUP:{g.Key}:{g.Count()}");
                    }
                    Current.Shutdown();
                    return;
                }

                if (e.Args.Length > 0 && e.Args[0] == "--test-theme")
                {
                    string originalTheme = Services.ThemeManager.GetCurrentTheme().Name;
                    var allThemes = Services.ThemeManager.GetAllThemes();
                    Console.WriteLine($"TESTING_THEMES_TOTAL:{allThemes.Count}");

                    int successCount = 0;
                    foreach (var theme in allThemes)
                    {
                        string eventReceivedName = "";
                        void Handler(Services.Theme t) { eventReceivedName = t.Name; }
                        Services.ThemeManager.OnThemeChanged += Handler;

                        Services.ThemeManager.SetCurrentTheme(theme.Name);
                        Services.ThemeManager.OnThemeChanged -= Handler;

                        var active = Services.ThemeManager.GetCurrentTheme();
                        if (active.Name == theme.Name && eventReceivedName == theme.Name)
                        {
                            Console.WriteLine($"THEME_OK: '{theme.Name}', Accent=#{theme.AccentColor.R:X2}{theme.AccentColor.G:X2}{theme.AccentColor.B:X2}");
                            successCount++;
                        }
                        else
                        {
                            Console.WriteLine($"THEME_FAIL: '{theme.Name}' (Active='{active.Name}', Event='{eventReceivedName}')");
                        }
                    }

                    // Restore original
                    Services.ThemeManager.SetCurrentTheme(originalTheme);
                    Console.WriteLine($"THEME_TEST_RESULT: {successCount}/{allThemes.Count} PASSED");
                    Current.Shutdown();
                    return;
                }

                if (e.Args.Length > 0 && e.Args[0] == "--test-shortcut")
                {
                    string orig = Services.ThemeManager.GetActivationShortcut();
                    string testVal = "AltRightClick";
                    string received = "";
                    void OnChange(string s) { received = s; }
                    Services.ThemeManager.OnShortcutChanged += OnChange;
                    Services.ThemeManager.SetActivationShortcut(testVal);
                    Services.ThemeManager.OnShortcutChanged -= OnChange;
                    string updated = Services.ThemeManager.GetActivationShortcut();
                    Console.WriteLine($"SHORTCUT_TEST: Set='{testVal}', Got='{updated}', Event='{received}'");
                    Services.ThemeManager.SetActivationShortcut(orig);
                    Current.Shutdown();
                    return;
                }

                if (e.Args.Length > 0 && e.Args[0] == "--test-db")
                {
                    var db = new Data.DatabaseManager();
                    db.InitializeDatabase();
                    var cats = db.GetAllCategories();
                    var items = db.GetAllItems();
                    Console.WriteLine($"TOTAL_CATEGORIES:{cats.Count}");
                    foreach (var c in cats)
                    {
                        int cCount = items.Count(i => (c.Id <= 1 || c.Name.Contains("Kullanılanlar") || c.Name.Contains("Hepsi")) ? (i.CategoryId <= 1 || i.IsUserAdded) : i.CategoryId == c.Id);
                        Console.WriteLine($"CAT_ID={c.Id}, NAME='{c.Name}', POS={c.Position}, ITEMS={cCount}");
                    }
                    Console.WriteLine($"TOTAL_ITEMS:{items.Count}");
                    var topCategoryItems = items.Where(i => (i.CategoryId <= 1 || i.IsUserAdded || i.IsFavorite) && i.ParentId == 0)
                                                .OrderBy(i => i.Position)
                                                .ThenBy(i => i.Id)
                                                .Take(15);
                    Console.WriteLine("PAGE_1_PRIMARY_ITEMS:");
                    int idx = 1;
                    foreach (var it in topCategoryItems)
                    {
                        Console.WriteLine($"  {idx++}. [{it.Type}] {it.Name} (Pos={it.Position}, Fav={it.IsFavorite})");
                    }
                    Current.Shutdown();
                    return;
                }

                if (e.Args.Length > 0 && e.Args[0] == "--test-icons")
                {
                    var db = new Data.DatabaseManager();
                    db.InitializeDatabase();
                    var items = db.GetAllItems();
                    Console.WriteLine($"TOTAL_ITEMS_TO_CHECK:{items.Count}");

                    string[] testGames = new[] { 
                        "Red Dead Redemption 2", "Hitman: Absolution", "Kenshi", "Half Sword", 
                        "Project Zomboid", "Sons Of The Forest", "Marvel's Spider-Man 2", "Cities: Skylines",
                        "Universe Sandbox", "WorldBox - God Simulator", "STAR WARS Battlefront II",
                        "Minecraft Launcher", "OpenCode", "ChatGpt", "Github", "Mephisto Mail", 
                        "Mephisto Shares", "Zen", "Google", "youtube" 
                    };
                    foreach (var gameName in testGames)
                    {
                        var it = items.FirstOrDefault(i => i.Name.Equals(gameName, StringComparison.OrdinalIgnoreCase));
                        if (it != null)
                        {
                            var vm = new UI.Windows.LauncherItemViewModel(it, "Test");
                            var icon = vm.Icon;
                            Console.WriteLine($"ICON_CHECK: '{it.Name}', Target='{it.Target}', HasIcon={icon != null}, IconType={icon?.GetType().Name}");
                        }
                        else
                        {
                            Console.WriteLine($"ITEM_NOT_FOUND: '{gameName}'");
                        }
                    }

                    Current.Shutdown();
                    return;
                }

                if (e.Args.Length > 0 && e.Args[0] == "--test-settings")
                {
                    var db = new Data.DatabaseManager();
                    db.InitializeDatabase();
                    var win = new UI.Windows.ManagementWindow();
                    win.Show();
                    win.Close();
                    Console.WriteLine("SETTINGS_WINDOW_SUCCESS");
                    Current.Shutdown();
                    return;
                }
                
                File.AppendAllText(Path.Combine(diagFolder, "live_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] Step 1: OnStartup entered\n");

                Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();

                File.AppendAllText(Path.Combine(diagFolder, "live_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] Step 2: Services configured\n");

                var dbManager = new Data.DatabaseManager();
                dbManager.InitializeDatabase();

                File.AppendAllText(Path.Combine(diagFolder, "live_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] Step 3: DB initialized\n");

                _radialMenu = new UI.Windows.RadialMenuWindow();
                _radialMenu.Show();
                _radialMenu.Hide();

                File.AppendAllText(Path.Combine(diagFolder, "live_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] Step 4: RadialMenu initialized\n");

                // Setup Tray Icon
                notifyIcon = new TaskbarIcon();
                notifyIcon.ToolTipText = "Radial Launcher";

                // Load app icon
                try 
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var icoPath = System.IO.Path.Combine(baseDir, "app.ico");
                    var icon = Services.IconExtractor.GetIconForFile(icoPath);
                    if (icon != null)
                    {
                        notifyIcon.IconSource = icon;
                    }
                    else
                    {
                        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (exePath.EndsWith(".dll")) exePath = exePath.Replace(".dll", ".exe");
                        var fallbackIcon = Services.IconExtractor.GetIconForFile(exePath);
                        if (fallbackIcon != null) notifyIcon.IconSource = fallbackIcon;
                    }
                }
                catch 
                {
                    // Fallback to default tray icon if any
                }

                var menu = new System.Windows.Controls.ContextMenu();
                
                var openItem = new System.Windows.Controls.MenuItem { Header = "Open Launcher" };
                openItem.Click += (s, args) => OpenLauncher();
                
                var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
                settingsItem.Click += (s, args) => OpenSettings();
                
                var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
                exitItem.Click += (s, args) => ExitApplication();

                menu.Items.Add(openItem);
                menu.Items.Add(settingsItem);
                menu.Items.Add(new System.Windows.Controls.Separator());
                menu.Items.Add(exitItem);

                notifyIcon.ContextMenu = menu;
                notifyIcon.TrayMouseDoubleClick += (s, args) => OpenLauncher();
                
                File.AppendAllText(Path.Combine(diagFolder, "live_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] Step 5: Tray icon configured\n");

                // Setup Global Mouse Hook
                mouseHook = new Hooks.GlobalMouseHook();
                mouseHook.TriggerMode = Services.ThemeManager.GetActivationShortcut();
                mouseHook.OnMiddleMouseDown += (s, pt) => 
                {
                    // Run on UI Thread asynchronously to prevent blocking the hook
                    Current.Dispatcher.BeginInvoke(new Action(() => OpenLauncher(pt)));
                };
                mouseHook.Start();

                File.AppendAllText(Path.Combine(diagFolder, "live_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] Step 6: Mouse hook started\n");

                // Setup Keyboard HotKey
                SetupHotKey(Services.ThemeManager.GetActivationShortcut());

                // Listen to Shortcut changes
                Services.ThemeManager.OnShortcutChanged += (newShortcut) =>
                {
                    if (mouseHook != null)
                    {
                        mouseHook.TriggerMode = newShortcut;
                    }
                    SetupHotKey(newShortcut);
                };

                File.AppendAllText(Path.Combine(diagFolder, "live_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] Step 7: Hotkey setup complete, app fully running!\n");
            }
            catch (Exception ex)
            {
                try
                {
                    var logFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadialLauncher");
                    if (!System.IO.Directory.Exists(logFolder)) System.IO.Directory.CreateDirectory(logFolder);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(logFolder, "crash.log"), ex.ToString());
                }
                catch { }
                Current.Shutdown();
            }
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
            catch { }
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

        private void ConfigureServices(IServiceCollection services)
        {
            // Register services here
        }

        private void OpenLauncher(Hooks.Point pt = default)
        {
            if (pt.X == 0 && pt.Y == 0)
            {
                GetCursorPos(out pt);
            }
            _radialMenu.ShowAt(pt.X, pt.Y);
        }

        private UI.Windows.ManagementWindow? _managementWindow;

        public void OpenSettings()
        {
            if (_managementWindow == null || !_managementWindow.IsLoaded)
            {
                _managementWindow = new UI.Windows.ManagementWindow();
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
            catch { }
            mouseHook?.Dispose();
            notifyIcon?.Dispose();
            Current.Shutdown();
        }
    }
}
