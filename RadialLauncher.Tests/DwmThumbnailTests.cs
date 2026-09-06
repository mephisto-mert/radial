using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RadialLauncher.UI.Helpers;
using Xunit;

namespace RadialLauncher.Tests
{
    public class DwmThumbnailTests
    {
        [Fact]
        public void UnregisterThumbnail_SafeWithZeroOrInvalidHandle()
        {
            // Should not throw on IntPtr.Zero or invalid handle
            DwmThumbnailHelper.UnregisterThumbnail(IntPtr.Zero);
            DwmThumbnailHelper.UnregisterThumbnail(new IntPtr(-1));
        }

        [Fact]
        public void DwmThumbnailProperties_Layout_IsExpectedSize()
        {
            int rectSize = Marshal.SizeOf<DwmThumbnailHelper.RECT>();
            Assert.Equal(16, rectSize); // 4 ints * 4 bytes = 16 bytes

            int propSize = Marshal.SizeOf<DwmThumbnailHelper.DWM_THUMBNAIL_PROPERTIES>();
            Assert.True(propSize >= 40); // Native DWM_THUMBNAIL_PROPERTIES struct size
        }

        [Fact]
        public void RegisterThumbnail_WithInvalidHandles_ReturnsZeroSafely()
        {
            // Registering with zero/invalid HWNDs must return IntPtr.Zero without throwing
            IntPtr thumb1 = DwmThumbnailHelper.RegisterThumbnail(IntPtr.Zero, IntPtr.Zero, 0, 0, 100, 100);
            IntPtr thumb2 = DwmThumbnailHelper.RegisterThumbnail(new IntPtr(100), IntPtr.Zero, 0, 0, 100, 100);
            IntPtr thumb3 = DwmThumbnailHelper.RegisterThumbnail(IntPtr.Zero, new IntPtr(200), 0, 0, 100, 100);

            Assert.Equal(IntPtr.Zero, thumb1);
            Assert.Equal(IntPtr.Zero, thumb2);
            Assert.Equal(IntPtr.Zero, thumb3);
        }

        [Fact]
        public void StressTest_50Cycles_NoHandleLeak()
        {
            var process = Process.GetCurrentProcess();
            process.Refresh();
            int initialHandles = process.HandleCount;

            for (int i = 0; i < 50; i++)
            {
                // Simulate thumbnail register/unregister cycle on dummy HWND
                IntPtr thumb = DwmThumbnailHelper.RegisterThumbnail(new IntPtr(100), new IntPtr(200), 0, 0, 160, 100);
                DwmThumbnailHelper.UnregisterThumbnail(thumb);
            }

            process.Refresh();
            int finalHandles = process.HandleCount;

            // Handle count should not leak over 50 iterations
            int diff = Math.Abs(finalHandles - initialHandles);
            Assert.True(diff < 20, $"Handle count grew by {diff} (from {initialHandles} to {finalHandles})");
        }
    }
}