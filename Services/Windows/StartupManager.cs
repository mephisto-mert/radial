using System;
using System.Reflection;
using Microsoft.Win32;
using Serilog;

namespace RadialLauncher.Services.Windows
{
    public class StartupManager : IStartupManager
    {
        private const string AppName = "RadialLauncher";

        public void SetRunOnStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        string path = Assembly.GetExecutingAssembly().Location;
                        if (path.EndsWith(".dll")) path = path.Replace(".dll", ".exe");
                        key.SetValue(AppName, path);
                        Log.Information("Added startup registry entry: {Path}", path);
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                        Log.Information("Removed startup registry entry for {AppName}", AppName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update startup registry entry (enable={Enable})", enable);
            }
        }

        public bool IsRunOnStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed reading startup registry key");
                return false;
            }
        }
    }
}
