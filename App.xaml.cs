using System;
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
                    var topCategoryItems = items.Where(i => i.CategoryId <= 1 || i.IsUserAdded)
                                                .OrderBy(i => i.IsFavorite ? 0 : 1)
                                                .ThenBy(i => i.Type == "URL" ? 0 : 1)
                                                .Take(15);
                    Console.WriteLine("PAGE_1_MOST_USED_ITEMS:");
                    foreach (var it in topCategoryItems)
                    {
                        Console.WriteLine($"  [{it.Type}] {it.Name} (CatId={it.CategoryId}, UserAdded={it.IsUserAdded}, Fav={it.IsFavorite})");
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
                
                Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();

                var dbManager = new Data.DatabaseManager();
                dbManager.InitializeDatabase();

                _radialMenu = new UI.Windows.RadialMenuWindow();
                _radialMenu.Show();
                _radialMenu.Hide();

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
                
                // Setup Global Mouse Hook
                mouseHook = new Hooks.GlobalMouseHook();
                mouseHook.OnMiddleMouseDown += (s, pt) => 
                {
                    // Run on UI Thread asynchronously to prevent blocking the hook
                    Current.Dispatcher.BeginInvoke(new Action(() => OpenLauncher(pt)));
                };
                mouseHook.Start();
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
            mouseHook?.Dispose();
            notifyIcon?.Dispose();
            Current.Shutdown();
        }
    }
}
