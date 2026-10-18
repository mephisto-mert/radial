using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RadialLauncher.Services.SystemTools;

public sealed record SystemTelemetrySnapshot(
    int CpuPercent,
    int RamPercent,
    long UsedRamMb,
    long TotalRamMb,
    int BatteryPercent,
    bool IsCharging);

public interface ISystemTelemetryService
{
    SystemTelemetrySnapshot GetLatestSnapshot();
}

public sealed class SystemTelemetryService : ISystemTelemetryService
{
    private readonly object _sync = new();
    private ulong _prevIdleTime;
    private ulong _prevTotalTime;
    private long _lastSampleTick;
    private int _lastCpuPercent;

    public SystemTelemetryService()
    {
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            _prevIdleTime = ToUInt64(idle);
            _prevTotalTime = ToUInt64(kernel) + ToUInt64(user);
            _lastSampleTick = Environment.TickCount64;
        }
    }

    public SystemTelemetrySnapshot GetLatestSnapshot()
    {
        int cpuPercent = ReadCpuPercent();
        var (ramPercent, usedMb, totalMb) = ReadMemoryStatus();
        var (batteryPercent, isCharging) = ReadPowerStatus();

        return new SystemTelemetrySnapshot(
            cpuPercent,
            ramPercent,
            usedMb,
            totalMb,
            batteryPercent,
            isCharging);
    }

    private int ReadCpuPercent()
    {
        lock (_sync)
        {
            long now = Environment.TickCount64;
            if (now - _lastSampleTick < 150 && _prevTotalTime > 0)
            {
                return _lastCpuPercent;
            }

            if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            {
                return _lastCpuPercent;
            }

            ulong idle = ToUInt64(idleFt);
            ulong total = ToUInt64(kernelFt) + ToUInt64(userFt);

            ulong totalDelta = total >= _prevTotalTime ? total - _prevTotalTime : 0;
            ulong idleDelta = idle >= _prevIdleTime ? idle - _prevIdleTime : 0;

            _prevIdleTime = idle;
            _prevTotalTime = total;
            _lastSampleTick = now;

            if (totalDelta > 0 && totalDelta >= idleDelta)
            {
                ulong busy = totalDelta - idleDelta;
                _lastCpuPercent = (int)Math.Clamp((busy * 100UL) / totalDelta, 0UL, 100UL);
            }

            return _lastCpuPercent;
        }
    }

    private static (int RamPercent, long UsedMb, long TotalMb) ReadMemoryStatus()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref mem) && mem.ullTotalPhys > 0)
        {
            long totalMb = (long)(mem.ullTotalPhys / (1024UL * 1024UL));
            long availMb = (long)(mem.ullAvailPhys / (1024UL * 1024UL));
            long usedMb = Math.Max(0L, totalMb - availMb);
            int ramPercent = (int)Math.Clamp(mem.dwMemoryLoad, 0U, 100U);
            return (ramPercent, usedMb, totalMb);
        }

        return (0, 0L, 0L);
    }

    private static (int BatteryPercent, bool IsCharging) ReadPowerStatus()
    {
        try
        {
            var power = SystemInformation.PowerStatus;
            if (power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery))
            {
                return (100, true);
            }

            int pct = (int)Math.Clamp(Math.Round(power.BatteryLifePercent * 100f), 0, 100);
            bool charging = power.PowerLineStatus == PowerLineStatus.Online ||
                            power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging);
            return (pct, charging);
        }
        catch
        {
            return (100, true);
        }
    }

    private static ulong ToUInt64(FILETIME ft) =>
        ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out FILETIME lpIdleTime,
        out FILETIME lpKernelTime,
        out FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
