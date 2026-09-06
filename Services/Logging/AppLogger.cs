using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace RadialLauncher.Services.Logging
{
    public static class AppLogger
    {
        public static string LogDirectory => RadialLauncher.Services.Data.UserDataPathProvider.Instance.GetLogsFolder();

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string logFilePath = Path.Combine(LogDirectory, "launcher-.log");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        logFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                Log.Information("Radial Launcher logging initialized. Log folder: {LogDirectory}", LogDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize Serilog: {ex.Message}");
            }
        }

        public static void CloseAndFlush()
        {
            try
            {
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing Serilog: {ex.Message}");
            }
        }
    }
}
