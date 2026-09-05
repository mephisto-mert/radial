using System;
using System.Runtime.InteropServices;
using Serilog;

namespace RadialLauncher.Services.VirtualDesktop
{
    public class VirtualDesktopService : IVirtualDesktopService
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_LWIN = 0x5B;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_LEFT = 0x25;
        private const byte VK_RIGHT = 0x27;
        private const byte VK_D = 0x44;

        public void SwitchToNextDesktop()
        {
            try
            {
                // Win + Ctrl + Right
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_RIGHT, 0, 0, UIntPtr.Zero);
                keybd_event(VK_RIGHT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to switch to next virtual desktop");
            }
        }

        public void SwitchToPreviousDesktop()
        {
            try
            {
                // Win + Ctrl + Left
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_LEFT, 0, 0, UIntPtr.Zero);
                keybd_event(VK_LEFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to switch to previous virtual desktop");
            }
        }

        public void CreateNewDesktop()
        {
            try
            {
                // Win + Ctrl + D
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_D, 0, 0, UIntPtr.Zero);
                keybd_event(VK_D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create new virtual desktop");
            }
        }
    }
}
