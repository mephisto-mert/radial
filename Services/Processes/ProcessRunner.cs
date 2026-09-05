using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using Serilog;

namespace RadialLauncher.Services.Processes
{
    public class ProcessRunner : IProcessRunner
    {
        private readonly IItemRepository? _itemRepo;
        private readonly ISystemActionService _actionService;

        public ProcessRunner(IItemRepository? itemRepo = null, ISystemActionService? actionService = null)
        {
            _itemRepo = itemRepo;
            _actionService = actionService ?? Actions.SystemActionService.Instance;
        }

        public void Launch(LauncherItem item)
        {
            try
            {
                Log.Information("Launching item: {Name} (Type={Type}, Target={Target})", item.Name, item.Type, item.Target);

                if (item.Id > 0 && _itemRepo != null)
                {
                    _itemRepo.IncrementLaunchCount(item.Id);
                }

                if (string.Equals(item.Type, "ACTION", StringComparison.OrdinalIgnoreCase))
                {
                    _actionService.ExecuteAction(item.Target);
                    return;
                }

                string target = item.Target;
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

                if (!string.IsNullOrEmpty(item.WorkingDirectory) && Directory.Exists(item.WorkingDirectory))
                    psi.WorkingDirectory = item.WorkingDirectory;

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to launch item: {Name}", item.Name);
                MessageBox.Show($"Başlatılamadı: {item.Name}\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
