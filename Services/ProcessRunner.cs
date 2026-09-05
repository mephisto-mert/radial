using System;
using System.Diagnostics;
using RadialLauncher.Models;
using System.Windows;

namespace RadialLauncher.Services
{
    public class ProcessRunner
    {
        public void Launch(LauncherItem item)
        {
            try
            {
                if (string.Equals(item.Type, "ACTION", StringComparison.OrdinalIgnoreCase))
                {
                    SystemActionService.ExecuteAction(item.Target);
                    return;
                }

                string target = item.Target;
                
                // Web siteleri için başında http/https yoksa otomatik ekle
                if (item.Type == "URL" && !target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    target = "https://" + target;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                };

                if (!string.IsNullOrEmpty(item.Arguments))
                    psi.Arguments = item.Arguments;

                if (!string.IsNullOrEmpty(item.WorkingDirectory))
                    psi.WorkingDirectory = item.WorkingDirectory;

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch {item.Name}:\n{ex.Message}", "Error");
            }
        }
    }
}
