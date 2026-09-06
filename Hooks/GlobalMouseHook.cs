using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RadialLauncher.Hooks
{
    public class GlobalMouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        public event EventHandler<Point>? OnMiddleMouseDown;

        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        private POINT _lastMousePt;
        private long _lastMouseMoveTime = 0;
        public double LastCursorVelocity { get; private set; } = 0.0;

        public bool IsPassThroughEnabled { get; set; } = false;
        public string TriggerMode { get; set; } = "MiddleClick";

        public GlobalMouseHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            _hookID = SetHook(_proc);
        }

        public void Stop()
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            IntPtr handle = GetModuleHandle(null!);
            return SetWindowsHookEx(WH_MOUSE_LL, proc, handle, 0);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (msg == WM_MOUSEMOVE)
                {
                    long now = Environment.TickCount64;
                    long dt = now - _lastMouseMoveTime;
                    if (dt > 0 && dt <= 150)
                    {
                        double dx = hookStruct.pt.x - _lastMousePt.x;
                        double dy = hookStruct.pt.y - _lastMousePt.y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        double instantVelocity = dist / dt; // pixels per ms
                        LastCursorVelocity = (LastCursorVelocity * 0.3) + (instantVelocity * 0.7);
                    }
                    else if (dt > 150)
                    {
                        LastCursorVelocity = 0.0;
                    }
                    _lastMousePt = hookStruct.pt;
                    _lastMouseMoveTime = now;
                }

                bool triggered = false;

                if (TriggerMode == "MiddleClick" && msg == WM_MBUTTONDOWN)
                {
                    triggered = true;
                }
                else if (TriggerMode == "AltRightClick" && msg == WM_RBUTTONDOWN && (GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
                {
                    triggered = true;
                }
                else if (TriggerMode == "ShiftRightClick" && msg == WM_RBUTTONDOWN && (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
                {
                    triggered = true;
                }
                else if (TriggerMode == "CtrlRightClick" && msg == WM_RBUTTONDOWN && (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
                {
                    triggered = true;
                }
                else if (TriggerMode == "XButton1" && msg == WM_XBUTTONDOWN && ((hookStruct.mouseData >> 16) & 0xFFFF) == 1)
                {
                    triggered = true;
                }
                else if (TriggerMode == "XButton2" && msg == WM_XBUTTONDOWN && ((hookStruct.mouseData >> 16) & 0xFFFF) == 2)
                {
                    triggered = true;
                }

                if (triggered)
                {
                    OnMiddleMouseDown?.Invoke(this, new Point(hookStruct.pt.x, hookStruct.pt.y));

                    if (!IsPassThroughEnabled)
                    {
                        return (IntPtr)1;
                    }
                }
                else if (!IsPassThroughEnabled && (msg == WM_MBUTTONUP || msg == WM_RBUTTONUP || msg == WM_XBUTTONUP))
                {
                    // If triggered previously, pass through normally
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public void Dispose()
        {
            Stop();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
    
    public struct Point
    {
        public int X;
        public int Y;
        public Point(int x, int y) { X = x; Y = y; }
    }
}
