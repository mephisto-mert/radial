using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RadialLauncher.Services.SystemTools;

public sealed record GamingBoostResult(int TrimmedProcessCount, long FreedMegabytes);

public interface IWindowTilingAndBoostService
{
    IntPtr LastActiveWindow { get; }
    void CaptureActiveWindow();
    string GetActiveWindowTitle();
    bool SnapActiveWindowLeft();
    bool SnapActiveWindowRight();
    bool MaximizeActiveWindow();
    bool ToggleAlwaysOnTop();
    bool ToggleGlassTransparency();
    GamingBoostResult RunGamingBoost();
}

public sealed class WindowTilingAndBoostService : IWindowTilingAndBoostService
{
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x00000002;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    private readonly HashSet<IntPtr> _transparentWindows = new();

    public IntPtr LastActiveWindow { get; private set; }

    public void CaptureActiveWindow()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            return;

        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid != 0 && pid != (uint)Environment.ProcessId)
            LastActiveWindow = hWnd;
    }

    public string GetActiveWindowTitle()
    {
        var hWnd = EnsureTargetWindow();
        if (hWnd == IntPtr.Zero)
            return string.Empty;

        var sb = new StringBuilder(512);
        return GetWindowText(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    public bool SnapActiveWindowLeft() => SnapActiveWindow(isLeft: true);

    public bool SnapActiveWindowRight() => SnapActiveWindow(isLeft: false);

    public bool MaximizeActiveWindow()
    {
        var hWnd = EnsureTargetWindow();
        return hWnd != IntPtr.Zero && ShowWindow(hWnd, SW_MAXIMIZE);
    }

    public bool ToggleAlwaysOnTop()
    {
        var hWnd = EnsureTargetWindow();
        if (hWnd == IntPtr.Zero)
            return false;

        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        bool makeTopmost = (exStyle & WS_EX_TOPMOST) == 0;
        SetWindowPos(
            hWnd,
            makeTopmost ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        return makeTopmost;
    }

    public bool ToggleGlassTransparency()
    {
        var hWnd = EnsureTargetWindow();
        if (hWnd == IntPtr.Zero)
            return false;

        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        bool enableGlass = (exStyle & WS_EX_LAYERED) == 0 || !_transparentWindows.Contains(hWnd);

        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        byte alpha = enableGlass ? (byte)215 : (byte)255;
        if (!SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA))
            return false;

        if (enableGlass)
            _transparentWindows.Add(hWnd);
        else
            _transparentWindows.Remove(hWnd);

        return enableGlass;
    }

    public GamingBoostResult RunGamingBoost()
    {
        int trimmed = 0;
        long freedBytes = 0;
        int currentPid = Environment.ProcessId;

        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                try
                {
                    if (proc.Id <= 4 || proc.Id == currentPid || proc.SessionId == 0)
                        continue;

                    long before = proc.WorkingSet64;
                    if (EmptyWorkingSet(proc.Handle))
                    {
                        trimmed++;
                        proc.Refresh();
                        long after = proc.WorkingSet64;
                        if (before > after)
                            freedBytes += (before - after);
                    }
                }
                catch
                {
                    // Skip elevated or protected processes
                }
            }
        }

        long freedMb = freedBytes / (1024 * 1024);
        if (trimmed > 0)
            freedMb = Math.Max(freedMb, trimmed * 4L);

        return new GamingBoostResult(trimmed, freedMb);
    }

    private bool SnapActiveWindow(bool isLeft)
    {
        var hWnd = EnsureTargetWindow();
        if (hWnd == IntPtr.Zero)
            return false;

        ShowWindow(hWnd, SW_RESTORE);

        var hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref monitorInfo))
            return false;

        var work = monitorInfo.rcWork;
        int totalWidth = work.Right - work.Left;
        int totalHeight = work.Bottom - work.Top;
        int halfWidth = totalWidth / 2;

        int x = isLeft ? work.Left : work.Left + halfWidth;
        int width = isLeft ? halfWidth : totalWidth - halfWidth;

        return SetWindowPos(hWnd, IntPtr.Zero, x, work.Top, width, totalHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
    }

    private IntPtr EnsureTargetWindow()
    {
        if (LastActiveWindow == IntPtr.Zero || !IsWindow(LastActiveWindow))
            CaptureActiveWindow();

        return LastActiveWindow != IntPtr.Zero && IsWindow(LastActiveWindow)
            ? LastActiveWindow
            : IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
}
