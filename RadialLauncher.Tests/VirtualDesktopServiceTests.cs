using System;
using System.Collections.Generic;
using System.Linq;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.VirtualDesktop;
using Xunit;

namespace RadialLauncher.Tests
{
    public class VirtualDesktopServiceTests
    {
        [Fact]
        public void GetDesktops_ReturnsValidDesktops_OrEmpty_WithoutSyntheticGuids()
        {
            var service = new VirtualDesktopService();
            var desktops = service.GetDesktops();

            Assert.NotNull(desktops);
            // Must contain only non-empty GUIDs
            Assert.All(desktops, d =>
            {
                Assert.False(string.IsNullOrEmpty(d.Name));
                Assert.NotEqual(Guid.Empty, d.Id);
            });
        }

        [Fact]
        public void MoveWindowToDesktop_ZeroHandle_ReturnsFalseSafely()
        {
            var service = new VirtualDesktopService();
            bool result = service.MoveWindowToDesktop(IntPtr.Zero, 0);

            Assert.False(result);
        }

        [Fact]
        public void MoveWindowToDesktop_InvalidIndex_ReturnsFalseSafely()
        {
            var service = new VirtualDesktopService();
            bool resultNeg = service.MoveWindowToDesktop((IntPtr)12345, -1);
            bool resultLarge = service.MoveWindowToDesktop((IntPtr)12345, 999);

            Assert.False(resultNeg);
            Assert.False(resultLarge);
        }

        [Fact]
        public void MoveWindowToDesktop_EmptyGuid_ReturnsFalseSafely()
        {
            var service = new VirtualDesktopService();
            bool result = service.MoveWindowToDesktop((IntPtr)12345, Guid.Empty);

            Assert.False(result);
        }

        [Fact]
        public void SwitchToDesktop_InvalidIndex_DoesNotThrow()
        {
            var service = new VirtualDesktopService();
            var ex1 = Record.Exception(() => service.SwitchToDesktop(-1));
            var ex2 = Record.Exception(() => service.SwitchToDesktop(999));

            Assert.Null(ex1);
            Assert.Null(ex2);
        }

        [Fact]
        public void SystemActionService_ExposesDesktopSwitchActions()
        {
            var service = SystemActionService.Instance;
            var actions = service.GetAvailableActions();

            Assert.Contains(actions, a => a.ActionKey == "NEXT_DESKTOP");
            Assert.Contains(actions, a => a.ActionKey == "PREV_DESKTOP");
        }
    }
}