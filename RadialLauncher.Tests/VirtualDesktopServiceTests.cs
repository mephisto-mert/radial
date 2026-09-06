using System;
using System.Linq;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.VirtualDesktop;
using Xunit;

namespace RadialLauncher.Tests
{
    public class VirtualDesktopServiceTests
    {
        [Fact]
        public void GetDesktops_ReturnsValidDesktops()
        {
            var service = new VirtualDesktopService();
            var desktops = service.GetDesktops();

            Assert.NotNull(desktops);
            Assert.True(desktops.Count >= 1);
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
        public void SystemActionService_ExposesDesktopSwitchActions()
        {
            var service = SystemActionService.Instance;
            var actions = service.GetAvailableActions();

            Assert.Contains(actions, a => a.ActionKey == "NEXT_DESKTOP");
            Assert.Contains(actions, a => a.ActionKey == "PREV_DESKTOP");
        }
    }
}
