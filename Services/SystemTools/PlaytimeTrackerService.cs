using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace RadialLauncher.Services.SystemTools;

public sealed class GamePlaytimeRecord
{
    public string Key { get; set; } = string.Empty;
    public double TotalMinutes { get; set; }
    public DateTime LastPlayedUtc { get; set; }
    public int Sessions { get; set; }

    public GamePlaytimeRecord()
    {
    }

    public GamePlaytimeRecord(string key, double totalMinutes, DateTime lastPlayedUtc, int sessions)
    {
        Key = key;
        TotalMinutes = totalMinutes;
        LastPlayedUtc = lastPlayedUtc;
        Sessions = sessions;
    }
}

public interface IPlaytimeTrackerService
{
    void RecordGameLaunch(string name, string target);
    string GetPlaytimeBadge(string name, string target);
}

public sealed class PlaytimeTrackerService : IPlaytimeTrackerService, IDisposable
{
    private readonly string _storagePath;
    private readonly object _sync = new();
    private readonly Dictionary<string, GamePlaytimeRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActiveSessionMonitor> _activeMonitors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _monitorTimer;

    private sealed class ActiveSessionMonitor
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string ProcessCandidate { get; init; } = string.Empty;
        public DateTime StartedUtc { get; init; }
        public int MissedTicks { get; set; }
    }

    public PlaytimeTrackerService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher");
        Directory.CreateDirectory(folder);
        _storagePath = Path.Combine(folder, "playtime.json");

        LoadFromDisk();
        _monitorTimer = new Timer(OnMonitorTick, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void RecordGameLaunch(string name, string target)
    {
        string key = BuildKey(name, target);
        if (string.IsNullOrWhiteSpace(key))
            return;

        lock (_sync)
        {
            if (!_records.TryGetValue(key, out var record))
            {
                record = new GamePlaytimeRecord(key, 0, DateTime.UtcNow, 0);
                _records[key] = record;
            }

            record.Sessions += 1;
            record.TotalMinutes = Math.Round(record.TotalMinutes + 1.0, 1);
            record.LastPlayedUtc = DateTime.UtcNow;

            _activeMonitors[key] = new ActiveSessionMonitor
            {
                Key = key,
                Name = name ?? string.Empty,
                ProcessCandidate = ExtractProcessCandidate(name ?? string.Empty, target ?? string.Empty),
                StartedUtc = DateTime.UtcNow,
                MissedTicks = 0
            };

            SaveToDiskLocked();
        }
    }

    public string GetPlaytimeBadge(string name, string target)
    {
        string key = BuildKey(name, target);
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        lock (_sync)
        {
            if (!_records.TryGetValue(key, out var record) || record.Sessions <= 0)
                return string.Empty;

            if (record.TotalMinutes >= 60.0)
            {
                double hours = record.TotalMinutes / 60.0;
                string hoursText = hours.ToString("0.0", CultureInfo.InvariantCulture);
                string lastPlayedText = FormatRelativeDay(record.LastPlayedUtc);
                return $"⏱ {hoursText} sa · Son: {lastPlayedText}";
            }

            int mins = Math.Max(1, (int)Math.Round(record.TotalMinutes));
            return $"⏱ {mins} dk · {record.Sessions} oturum";
        }
    }

    private void OnMonitorTick(object? state)
    {
        List<ActiveSessionMonitor> snapshot;
        lock (_sync)
        {
            if (_activeMonitors.Count == 0)
                return;
            snapshot = _activeMonitors.Values.ToList();
        }

        var runningNames = CaptureRunningProcessNames();
        bool anyUpdated = false;

        lock (_sync)
        {
            foreach (var monitor in snapshot)
            {
                bool isRunning = IsMonitorActive(monitor, runningNames);
                if (isRunning)
                {
                    monitor.MissedTicks = 0;
                    if (_records.TryGetValue(monitor.Key, out var record))
                    {
                        record.TotalMinutes = Math.Round(record.TotalMinutes + 1.0, 1);
                        record.LastPlayedUtc = DateTime.UtcNow;
                        anyUpdated = true;
                    }
                }
                else
                {
                    monitor.MissedTicks++;
                    if (monitor.MissedTicks >= 3)
                        _activeMonitors.Remove(monitor.Key);
                }
            }

            if (anyUpdated)
                SaveToDiskLocked();
        }
    }

    private static HashSet<string> CaptureRunningProcessNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(proc.ProcessName))
                        set.Add(proc.ProcessName);
                }
                catch
                {
                    // Ignore inaccessible processes
                }
            }
        }
        return set;
    }

    private static bool IsMonitorActive(ActiveSessionMonitor monitor, HashSet<string> runningProcesses)
    {
        if (!string.IsNullOrWhiteSpace(monitor.ProcessCandidate))
        {
            if (runningProcesses.Contains(monitor.ProcessCandidate))
                return true;

            if (runningProcesses.Any(p => p.Contains(monitor.ProcessCandidate, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // For URI-launched games (steam://, com.epicgames.launcher://), grant a 5-minute startup grace window
        return (DateTime.UtcNow - monitor.StartedUtc).TotalMinutes <= 5.0 && monitor.MissedTicks == 0;
    }

    private static string FormatRelativeDay(DateTime lastPlayedUtc)
    {
        var localDate = lastPlayedUtc.ToLocalTime().Date;
        var today = DateTime.Now.Date;
        int days = (today - localDate).Days;

        return days switch
        {
            <= 0 => "Bugün",
            1 => "Dün",
            < 7 => $"{days} gün önce",
            _ => localDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
        };
    }

    private static string BuildKey(string name, string target)
    {
        string raw = !string.IsNullOrWhiteSpace(target) ? target.Trim() : (name?.Trim() ?? string.Empty);
        return raw.ToLowerInvariant();
    }

    private static string ExtractProcessCandidate(string name, string target)
    {
        if (!string.IsNullOrWhiteSpace(target) &&
            !target.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(target.Trim('"', ' '));
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }
            catch
            {
                // Ignore invalid path characters
            }
        }

        return (name ?? string.Empty).Replace(" ", string.Empty).Trim();
    }

    private void LoadFromDisk()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_storagePath))
                    return;

                string json = File.ReadAllText(_storagePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, GamePlaytimeRecord>>(json);
                if (loaded == null)
                    return;

                _records.Clear();
                foreach (var kvp in loaded)
                    _records[kvp.Key] = kvp.Value;
            }
            catch
            {
                // Start with empty dictionary if file is corrupt
            }
        }
    }

    private void SaveToDiskLocked()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_records, options);
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Ignore transient IO locks
        }
    }

    public void Dispose()
    {
        _monitorTimer.Dispose();
    }
}
