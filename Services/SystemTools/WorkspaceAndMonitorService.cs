using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RadialLauncher.Services.SystemTools;

public sealed class SavedWindowPlacement
{
    public string ProcessName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class WorkspaceSnapshot
{
    public string Name { get; set; } = "";
    public DateTime SavedAtUtc { get; set; }
    public List<SavedWindowPlacement> Windows { get; set; } = new();
}

public interface IWorkspaceAndMonitorService
{
    string MoveWindowToNextMonitor(IntPtr targetHwnd);
    string SaveWorkspaceSnapshot(string name);
    string RestoreWorkspaceSnapshot(string name);
}

public sealed class WorkspaceAndMonitorService : IWorkspaceAndMonitorService
{
    private readonly string _storagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadialLauncher",
        "workspaces.json");

    private int _singleMonitorToggleStep;

    public string MoveWindowToNextMonitor(IntPtr targetHwnd)
    {
        IntPtr hwnd = ResolveTargetWindow(targetHwnd);
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT winRect))
        {
            return "⚠️ Taşınacak aktif pencere bulunamadı";
        }

        var monitors = GetAllMonitorWorkAreas();
        if (monitors.Count == 0)
        {
            return "⚠️ Monitör bilgisi alınamadı";
        }

        ShowWindow(hwnd, SW_RESTORE);

        if (monitors.Count >= 2)
        {
            int currentIdx = FindCurrentMonitorIndex(winRect, monitors);
            int nextIdx = (currentIdx + 1) % monitors.Count;
            RECT src = monitors[currentIdx];
            RECT dst = monitors[nextIdx];

            int srcW = Math.Max(1, src.Right - src.Left);
            int srcH = Math.Max(1, src.Bottom - src.Top);
            int dstW = Math.Max(1, dst.Right - dst.Left);
            int dstH = Math.Max(1, dst.Bottom - dst.Top);

            double relX = (double)(winRect.Left - src.Left) / srcW;
            double relY = (double)(winRect.Top - src.Top) / srcH;
            double relW = (double)(winRect.Right - winRect.Left) / srcW;
            double relH = (double)(winRect.Bottom - winRect.Top) / srcH;

            int newW = Math.Clamp((int)Math.Round(relW * dstW), 320, dstW);
            int newH = Math.Clamp((int)Math.Round(relH * dstH), 240, dstH);
            int newX = Math.Clamp(dst.Left + (int)Math.Round(relX * dstW), dst.Left, dst.Right - newW);
            int newY = Math.Clamp(dst.Top + (int)Math.Round(relY * dstH), dst.Top, dst.Bottom - newH);

            SetWindowPos(hwnd, IntPtr.Zero, newX, newY, newW, newH, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            return $"🖥️ Pencere Diğer Monitöre Taşındı ({nextIdx + 1}/{monitors.Count})";
        }
        else
        {
            RECT work = monitors[0];
            int workW = Math.Max(640, work.Right - work.Left);
            int workH = Math.Max(480, work.Bottom - work.Top);

            int mode = _singleMonitorToggleStep % 3;
            _singleMonitorToggleStep++;

            int x, y, w, h;
            if (mode == 0)
            {
                x = work.Left;
                y = work.Top;
                w = workW / 2;
                h = workH;
            }
            else if (mode == 1)
            {
                x = work.Left + workW / 2;
                y = work.Top;
                w = workW - workW / 2;
                h = workH;
            }
            else
            {
                w = (int)(workW * 0.8);
                h = (int)(workH * 0.8);
                x = work.Left + (workW - w) / 2;
                y = work.Top + (workH - h) / 2;
            }

            SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            return "🖥️ Tek Monitör: Pencere Konumu Değiştirildi";
        }
    }

    public string SaveWorkspaceSnapshot(string name)
    {
        string snapshotName = string.IsNullOrWhiteSpace(name) ? "Varsayılan" : name.Trim();
        var openWindows = EnumerateTopApplicationWindows(maxCount: 8);
        var placements = new List<SavedWindowPlacement>();

        foreach (var win in openWindows)
        {
            placements.Add(new SavedWindowPlacement
            {
                ProcessName = win.ProcessName,
                ExecutablePath = win.ExecutablePath,
                Left = win.Bounds.Left,
                Top = win.Bounds.Top,
                Width = Math.Max(200, win.Bounds.Right - win.Bounds.Left),
                Height = Math.Max(150, win.Bounds.Bottom - win.Bounds.Top)
            });
        }

        var snapshots = LoadAllSnapshots();
        snapshots.RemoveAll(s => string.Equals(s.Name, snapshotName, StringComparison.OrdinalIgnoreCase));
        snapshots.Add(new WorkspaceSnapshot
        {
            Name = snapshotName,
            SavedAtUtc = DateTime.UtcNow,
            Windows = placements
        });

        SaveAllSnapshots(snapshots);
        return $"💾 Çalışma Alanı Kaydedildi ({placements.Count} pencere)";
    }

    public string RestoreWorkspaceSnapshot(string name)
    {
        var snapshots = LoadAllSnapshots();
        string targetName = string.IsNullOrWhiteSpace(name) ? "Varsayılan" : name.Trim();

        var snapshot = snapshots.FirstOrDefault(s => string.Equals(s.Name, targetName, StringComparison.OrdinalIgnoreCase))
                       ?? snapshots.OrderByDescending(s => s.SavedAtUtc).FirstOrDefault();

        var openWindows = EnumerateTopApplicationWindows(maxCount: 16);

        if (snapshot == null || snapshot.Windows.Count == 0)
        {
            return TileTopTwoWindowsSideBySide(openWindows);
        }

        int restoredCount = 0;
        var usedHandles = new HashSet<IntPtr>();

        foreach (var saved in snapshot.Windows)
        {
            var match = openWindows.FirstOrDefault(w =>
                !usedHandles.Contains(w.Hwnd) &&
                string.Equals(w.ProcessName, saved.ProcessName, StringComparison.OrdinalIgnoreCase));

            if (match.Hwnd != IntPtr.Zero)
            {
                usedHandles.Add(match.Hwnd);
                ShowWindow(match.Hwnd, SW_RESTORE);
                SetWindowPos(
                    match.Hwnd,
                    IntPtr.Zero,
                    saved.Left,
                    saved.Top,
                    Math.Max(250, saved.Width),
                    Math.Max(180, saved.Height),
                    SWP_NOZORDER | SWP_SHOWWINDOW);
                restoredCount++;
            }
        }

        if (restoredCount == 0)
        {
            return TileTopTwoWindowsSideBySide(openWindows);
        }

        return $"🗂️ Çalışma Alanı Geri Yüklendi ({restoredCount} pencere)";
    }

    private string TileTopTwoWindowsSideBySide(List<(IntPtr Hwnd, string ProcessName, string ExecutablePath, RECT Bounds)> openWindows)
    {
        var topTwo = openWindows.Take(2).ToList();
        if (topTwo.Count == 0)
        {
            return "⚠️ Düzenlenecek açık pencere bulunamadı";
        }

        var monitors = GetAllMonitorWorkAreas();
        RECT work = monitors.Count > 0 ? monitors[0] : new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        int workW = Math.Max(640, work.Right - work.Left);
        int workH = Math.Max(480, work.Bottom - work.Top);

        if (topTwo.Count == 1)
        {
            ShowWindow(topTwo[0].Hwnd, SW_RESTORE);
            SetWindowPos(topTwo[0].Hwnd, IntPtr.Zero, work.Left, work.Top, workW, workH, SWP_NOZORDER | SWP_SHOWWINDOW);
            return "🗂️ 1 Pencere Tam Çalışma Alanına Yayıldı";
        }

        int halfW = workW / 2;
        ShowWindow(topTwo[0].Hwnd, SW_RESTORE);
        SetWindowPos(topTwo[0].Hwnd, IntPtr.Zero, work.Left, work.Top, halfW, workH, SWP_NOZORDER | SWP_SHOWWINDOW);

        ShowWindow(topTwo[1].Hwnd, SW_RESTORE);
        SetWindowPos(topTwo[1].Hwnd, IntPtr.Zero, work.Left + halfW, work.Top, workW - halfW, workH, SWP_NOZORDER | SWP_SHOWWINDOW);

        return "🗂️ 2 Pencere Yan Yana Hizalandı (%50/%50)";
    }

    private IntPtr ResolveTargetWindow(IntPtr preferredHwnd)
    {
        if (preferredHwnd != IntPtr.Zero && IsWindowVisible(preferredHwnd))
        {
            return preferredHwnd;
        }

        var open = EnumerateTopApplicationWindows(maxCount: 1);
        return open.Count > 0 ? open[0].Hwnd : GetForegroundWindow();
    }

    private List<(IntPtr Hwnd, string ProcessName, string ExecutablePath, RECT Bounds)> EnumerateTopApplicationWindows(int maxCount)
    {
        var result = new List<(IntPtr, string, string, RECT)>();
        uint currentPid = (uint)Environment.ProcessId;

        EnumWindows((hwnd, _) =>
        {
            if (result.Count >= maxCount) return false;
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;

            int length = GetWindowTextLength(hwnd);
            if (length <= 0) return true;

            var sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString().Trim();
            if (string.IsNullOrEmpty(title) ||
                string.Equals(title, "Program Manager", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("RadialLauncher", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0 || pid == currentPid) return true;

            if (!GetWindowRect(hwnd, out RECT rect)) return true;
            if (rect.Right - rect.Left < 120 || rect.Bottom - rect.Top < 80) return true;

            string procName = "";
            string exePath = "";
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName ?? "";
                if (string.Equals(procName, "RadialLauncher", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(procName, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(procName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                try { exePath = proc.MainModule?.FileName ?? ""; } catch { }
            }
            catch
            {
                return true;
            }

            result.Add((hwnd, procName, exePath, rect));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static List<RECT> GetAllMonitorWorkAreas()
    {
        var list = new List<RECT>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                list.Add(info.rcWork);
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static int FindCurrentMonitorIndex(RECT winRect, List<RECT> monitors)
    {
        int centerX = (winRect.Left + winRect.Right) / 2;
        int centerY = (winRect.Top + winRect.Bottom) / 2;
        for (int i = 0; i < monitors.Count; i++)
        {
            RECT m = monitors[i];
            if (centerX >= m.Left && centerX <= m.Right && centerY >= m.Top && centerY <= m.Bottom)
            {
                return i;
            }
        }
        return 0;
    }

    private List<WorkspaceSnapshot> LoadAllSnapshots()
    {
        try
        {
            if (!File.Exists(_storagePath)) return new List<WorkspaceSnapshot>();
            string json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<List<WorkspaceSnapshot>>(json) ?? new List<WorkspaceSnapshot>();
        }
        catch
        {
            return new List<WorkspaceSnapshot>();
        }
    }

    private void SaveAllSnapshots(List<WorkspaceSnapshot> snapshots)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }
        catch { }
    }

    private const int SW_RESTORE = 9;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
