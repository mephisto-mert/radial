using System;
using System.Runtime.InteropServices;
using Serilog;

namespace RadialLauncher.UI.Helpers
{
    public static class DwmThumbnailHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, out IntPtr phThumbnailId);

        [DllImport("dwmapi.dll")]
        private static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

        [DllImport("dwmapi.dll")]
        private static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            public bool fVisible;
            public bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public const int DWM_TNP_RECTDESTINATION = 0x00000001;
        public const int DWM_TNP_OPACITY = 0x00000004;
        public const int DWM_TNP_VISIBLE = 0x00000008;

        public static IntPtr RegisterThumbnail(IntPtr destHwnd, IntPtr srcHwnd, int left, int top, int width, int height)
        {
            try
            {
                int hr = DwmRegisterThumbnail(destHwnd, srcHwnd, out IntPtr thumb);
                if (hr == 0 && thumb != IntPtr.Zero)
                {
                    var props = new DWM_THUMBNAIL_PROPERTIES
                    {
                        dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE,
                        rcDestination = new RECT { Left = left, Top = top, Right = left + width, Bottom = top + height },
                        opacity = 255,
                        fVisible = true,
                        fSourceClientAreaOnly = false
                    };
                    DwmUpdateThumbnailProperties(thumb, ref props);
                    return thumb;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("DWM thumbnail registration failed: {Message}", ex.Message);
            }
            return IntPtr.Zero;
        }

        public static void UnregisterThumbnail(IntPtr thumbHandle)
        {
            if (thumbHandle != IntPtr.Zero)
            {
                try
                {
                    DwmUnregisterThumbnail(thumbHandle);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to unregister DWM thumbnail handle {ThumbHandle}", thumbHandle);
                }
            }
        }
    }
}
