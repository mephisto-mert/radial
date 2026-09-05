using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RadialLauncher.Hooks
{
    public class GlobalMouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;

        public event EventHandler<Point> OnMiddleMouseDown;

        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public bool IsPassThroughEnabled { get; set; } = false;

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
            IntPtr handle = GetModuleHandle(null);
            IntPtr hook = SetWindowsHookEx(WH_MOUSE_LL, proc, handle, 0);
            System.IO.File.WriteAllText("hook_setup.log", $"Handle: {handle}, Hook: {hook}, LastError: {Marshal.GetLastWin32Error()}");
            return hook;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // DEBUG LOGGING
                System.IO.File.AppendAllText("hook_all_events.log", $"Hook called: {wParam}\n");
                
                if (wParam == (IntPtr)WM_MBUTTONDOWN)
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    OnMiddleMouseDown?.Invoke(this, new Point(hookStruct.pt.x, hookStruct.pt.y));

                    if (!IsPassThroughEnabled)
                    {
                        // Block the event
                        return (IntPtr)1;
                    }
                }
                else if (wParam == (IntPtr)WM_MBUTTONUP && !IsPassThroughEnabled)
                {
                    // Block the up event too if pass-through is disabled
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

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
