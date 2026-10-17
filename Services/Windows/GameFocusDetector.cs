using System;
using System.Runtime.InteropServices;
using Serilog;

namespace RadialLauncher.Services.Windows
{
    public interface IGameFocusDetector
    {
        bool IsFullscreenGameOrBusy();
    }

    public class GameFocusDetector : IGameFocusDetector
    {
        private enum QUERY_USER_NOTIFICATION_STATE
        {
            QUNS_NOT_PRESENT = 1,
            QUNS_BUSY = 2,
            QUNS_RUNNING_D3D_FULL_SCREEN = 3,
            QUNS_PRESENTATION_MODE = 4,
            QUNS_ACCEPTS_NOTIFICATIONS = 5,
            QUNS_QUIET_TIME = 6,
            QUNS_APP = 7
        }

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public bool IsFullscreenGameOrBusy()
        {
            try
            {
                // 1. Direct Shell D3D Fullscreen check
                int hr = SHQueryUserNotificationState(out var state);
                if (hr == 0) // S_OK
                {
                    if (state == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                        state == QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY)
                    {
                        Log.Debug("GameFocusDetector: Suppressed trigger due to QUNS state {State}", state);
                        return true;
                    }
                }

                // 2. Fallback check: Foreground window matches screen bounds & not shell/desktop
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == GetDesktopWindow() || fg == GetShellWindow())
                    return false;

                var sb = new System.Text.StringBuilder(256);
                GetClassName(fg, sb, 256);
                string className = sb.ToString();
                if (className == "Progman" || className == "WorkerW")
                    return false;

                if (GetWindowRect(fg, out var rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    int screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                    int screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

                    // If foreground window covers the full primary screen or larger
                    if (rect.Left <= 0 && rect.Top <= 0 && width >= screenWidth && height >= screenHeight)
                    {
                        // Some games report QUNS_APP instead of QUNS_RUNNING_D3D_FULL_SCREEN
                        if (hr == 0 && state == QUERY_USER_NOTIFICATION_STATE.QUNS_APP)
                        {
                            Log.Debug("GameFocusDetector: Fullscreen window with QUNS_APP detected");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "GameFocusDetector check encountered exception");
            }

            return false;
        }
    }
}
