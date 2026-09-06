using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using Serilog;

namespace RadialLauncher.Services.Processes
{
    public class ProcessRunner : IProcessRunner
    {
        private const int MaxMacroDelayMs = 60000; // Max 60 seconds delay per step

        private readonly IItemRepository? _itemRepo;
        private readonly ISystemActionService _actionService;
        private readonly ConcurrentDictionary<CancellationTokenSource, byte> _activeMacroTokens = new();

        public ProcessRunner(IItemRepository? itemRepo = null, ISystemActionService? actionService = null)
        {
            _itemRepo = itemRepo;
            _actionService = actionService ?? Actions.SystemActionService.Instance;
        }

        public void CancelAllRunningMacros()
        {
            try
            {
                foreach (var cts in _activeMacroTokens.Keys)
                {
                    try
                    {
                        cts.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error canceling macro token during bulk cancel");
                    }
                }
                _activeMacroTokens.Clear();
                Log.Information("Cancelled all running macro tasks");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in CancelAllRunningMacros");
            }
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

                if (string.Equals(item.Type, "MACRO", StringComparison.OrdinalIgnoreCase))
                {
                    var cts = new CancellationTokenSource();
                    _activeMacroTokens.TryAdd(cts, 0);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteMacroAsync(item.Target, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            Log.Information("Macro execution cancelled for item: {Name}", item.Name);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Unhandled exception in background macro task for item: {Name}", item.Name);
                        }
                        finally
                        {
                            _activeMacroTokens.TryRemove(cts, out _);
                            cts.Dispose();
                        }
                    });
                    return;
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
                MessageBox.Show($"'{item.Name}' öğesi başlatılamadı. Lütfen hedef dosya yolunu kontrol edin.", "Radial Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public async Task ExecuteMacroAsync(string jsonSteps, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jsonSteps)) return;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var steps = JsonSerializer.Deserialize<List<MacroStep>>(jsonSteps);
                if (steps == null || steps.Count == 0) return;

                Log.Information("Executing macro with {Count} steps", steps.Count);
                foreach (var step in steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var stepItem = new LauncherItem
                        {
                            Name = step.Name,
                            Type = string.IsNullOrWhiteSpace(step.Type) ? "EXE" : step.Type,
                            Target = step.Target,
                            Arguments = step.Arguments
                        };

                        Launch(stepItem);

                        // Clamp delay: negative -> 0, max -> 60000ms
                        int delay = Math.Clamp(step.DelayMs, 0, MaxMacroDelayMs);
                        if (delay > 0)
                        {
                            await Task.Delay(delay, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception stepEx)
                    {
                        Log.Warning(stepEx, "Error executing macro step: {StepName}", step.Name);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to deserialize and execute macro");
            }
        }
    }
}
