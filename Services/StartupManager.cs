using Microsoft.Win32;
using System.Reflection;

namespace RadialLauncher.Services
{
    public static class StartupManager
    {
        private const string AppName = "RadialLauncher";

        public static void SetRunOnStartup(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enable)
                {
                    string path = Assembly.GetExecutingAssembly().Location;
                    if (path.EndsWith(".dll")) path = path.Replace(".dll", ".exe");
                    key.SetValue(AppName, path);
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }

        public static bool IsRunOnStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue(AppName) != null;
        }
    }
}
