using System;
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
                
                Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();

                var dbManager = new Data.DatabaseManager();
                dbManager.InitializeDatabase();

                _radialMenu = new UI.Windows.RadialMenuWindow();
                // Workaround for WPF exiting early when no windows are shown
                _radialMenu.Show();
                _radialMenu.Hide();

                // TEST: Open it immediately to ensure it displays!
                OpenLauncher(new Hooks.Point(500, 500));

                // Setup Tray Icon
                notifyIcon = new TaskbarIcon();
                notifyIcon.ToolTipText = "Radial Launcher";

                // Load app icon
                try 
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var icoPath = System.IO.Path.Combine(baseDir, "app.ico");
                    if (System.IO.File.Exists(icoPath))
                    {
                        notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri(icoPath));
                    }
                    else
                    {
                        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (exePath.EndsWith(".dll")) exePath = exePath.Replace(".dll", ".exe");
                        var icon = Services.IconExtractor.GetIconForFile(exePath);
                        if (icon != null) notifyIcon.IconSource = icon;
                    }
                }
                catch 
                {
                    // Ignore icon extraction failure
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
                    System.IO.File.AppendAllText("hook_debug.log", $"Middle mouse triggered at {pt.X}, {pt.Y}\n");
                    // Run on UI Thread asynchronously to prevent blocking the hook
                    Current.Dispatcher.BeginInvoke(new Action(() => OpenLauncher(pt)));
                };
                mouseHook.Start();
                
                System.IO.File.WriteAllText("success.log", "Started successfully!");
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("crash.log", ex.ToString());
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

        private void OpenSettings()
        {
            var managementWindow = new UI.Windows.ManagementWindow();
            managementWindow.Show();
        }

        private void ExitApplication()
        {
            mouseHook?.Dispose();
            notifyIcon?.Dispose();
            Current.Shutdown();
        }
    }
}
