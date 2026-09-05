using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;

namespace RadialLauncher.Services
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }
    }

    public class WindowSwitcherService
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int SW_RESTORE = 9;
        private const uint WM_CLOSE = 0x0010;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public List<WindowInfo> GetOpenWindows()
        {
            var windows = new List<WindowInfo>();
            var shellWindow = GetShellWindow();
            int currentPid = Process.GetCurrentProcess().Id;

            EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                var builder = new StringBuilder(length + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                string title = builder.ToString().Trim();

                if (string.IsNullOrEmpty(title)) return true;
                if (title == "Program Manager" || title == "Windows Input Experience" || title == "Settings") return true;

                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0)
                    return true;

                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId == currentPid) return true;

                string procName = "";
                string exePath = "";
                IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProc != IntPtr.Zero)
                {
                    var exeBuilder = new StringBuilder(1024);
                    uint size = (uint)exeBuilder.Capacity;
                    if (QueryFullProcessImageName(hProc, 0, exeBuilder, ref size))
                    {
                        exePath = exeBuilder.ToString();
                        procName = Path.GetFileNameWithoutExtension(exePath);
                    }
                    CloseHandle(hProc);
                }

                if (string.IsNullOrEmpty(procName))
                {
                    try
                    {
                        var proc = Process.GetProcessById((int)processId);
                        procName = proc.ProcessName;
                    }
                    catch { }
                }

                ImageSource? icon = null;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    icon = IconExtractor.GetIconForFile(exePath);
                }

                windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = procName,
                    Icon = icon
                });

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public void SwitchToWindow(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
            {
                ShowWindowAsync(hWnd, SW_RESTORE);
            }
            SetForegroundWindow(hWnd);
        }

        public void CloseWindow(IntPtr hWnd)
        {
            PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
