using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Serilog;

namespace RadialLauncher.UI.Helpers
{
    public static class WindowAcrylicHelper
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        public static void EnableBlur(Window window, uint tintColor = 0x99121216)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.EnsureHandle();

                var policy = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2, // Draw all borders
                    GradientColor = (int)tintColor
                };

                int sizeOfPolicy = Marshal.SizeOf(policy);
                IntPtr policyPtr = Marshal.AllocHGlobal(sizeOfPolicy);
                Marshal.StructureToPtr(policy, policyPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = policyPtr,
                    SizeOfData = sizeOfPolicy
                };

                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(policyPtr);
            }
            catch (Exception ex)
            {
                Log.Debug("Acrylic blur not supported on this Windows version: {Message}", ex.Message);
            }
        }
    }
}
