using System.Linq;
using RadialLauncher.Services.Actions;
using Xunit;

namespace RadialLauncher.Tests
{
    public class SystemActionServiceTests
    {
        [Fact]
        public void GetAvailableActions_ReturnsComprehensiveList()
        {
            var service = SystemActionService.Instance;
            var actions = service.GetAvailableActions();

            Assert.NotNull(actions);
            Assert.True(actions.Count >= 10);

            var keys = actions.Select(a => a.ActionKey).ToList();
            Assert.Contains("VOLUME_UP", keys);
            Assert.Contains("VOLUME_DOWN", keys);
            Assert.Contains("LOCK_PC", keys);
            Assert.Contains("TASK_MANAGER", keys);
            Assert.Contains("EMPTY_RECYCLE_BIN", keys);
            Assert.Contains("SHOW_DESKTOP", keys);
            Assert.Contains("SNIP_TOOL", keys);
        }

        [Theory]
        [InlineData("VOLUME_UP", "Ses")]
        [InlineData("LOCK_PC", "Kilitle")]
        public void GetAvailableActions_ContainsExpectedDisplayNames(string key, string expectedSubstring)
        {
            var service = SystemActionService.Instance;
            var action = service.GetAvailableActions().FirstOrDefault(a => a.ActionKey == key);
            Assert.NotNull(action);
            Assert.Contains(expectedSubstring, action!.DisplayName);
        }
    }
}
