using System;
using RadialLauncher.Services.Actions;
using Xunit;

namespace RadialLauncher.Tests
{
    public class SystemActionServiceTests
    {
        [Fact]
        public void AvailableActions_ContainsExpectedCoreActions()
        {
            var service = new SystemActionService();
            var actions = service.GetAvailableActions();

            Assert.NotEmpty(actions);
            Assert.Contains(actions, a => a.ActionKey == "VOLUME_UP");
            Assert.Contains(actions, a => a.ActionKey == "VOLUME_DOWN");
            Assert.Contains(actions, a => a.ActionKey == "VOLUME_MUTE");
            Assert.Contains(actions, a => a.ActionKey == "MEDIA_PLAY_PAUSE");
            Assert.Contains(actions, a => a.ActionKey == "SHOW_DESKTOP");
            Assert.Contains(actions, a => a.ActionKey == "TASK_MANAGER");
            Assert.Contains(actions, a => a.ActionKey == "FOCUS_25");
        }

        [Fact]
        public void ExecuteAction_InvalidOrUnknownKey_DoesNotThrow()
        {
            var service = new SystemActionService();

            var ex1 = Record.Exception(() => service.ExecuteAction(null!));
            var ex2 = Record.Exception(() => service.ExecuteAction(""));
            var ex3 = Record.Exception(() => service.ExecuteAction("UNKNOWN_ACTION_KEY_XYZ"));

            Assert.Null(ex1);
            Assert.Null(ex2);
            Assert.Null(ex3);
        }

        [Fact]
        public void GetIconForAction_ReturnsExpectedSymbols_AndFallback()
        {
            var service = new SystemActionService();

            Assert.Equal("🔊", service.GetIconForAction("VOLUME_UP"));
            Assert.Equal("🔇", service.GetIconForAction("VOLUME_MUTE"));
            Assert.Equal("🍅", service.GetIconForAction("FOCUS_25"));
            Assert.Equal("⚡", service.GetIconForAction("NON_EXISTENT_KEY"));
        }

        [Theory]
        [InlineData("VOLUME_UP")]
        [InlineData("VOLUME_DOWN")]
        [InlineData("VOLUME_MUTE")]
        [InlineData("MEDIA_PLAY_PAUSE")]
        [InlineData("MEDIA_NEXT")]
        [InlineData("MEDIA_PREV")]
        [InlineData("SNIP_TOOL")]
        [InlineData("TASK_MANAGER")]
        [InlineData("LOCK_PC")]
        [InlineData("EMPTY_RECYCLE_BIN")]
        [InlineData("SHOW_DESKTOP")]
        [InlineData("FOCUS_25")]
        public void VectorIconFactory_GetActionIcon_ReturnsNonNullImageSource(string actionKey)
        {
            var icon = RadialLauncher.Services.Icons.VectorIconFactory.GetActionIcon(actionKey);
            Assert.NotNull(icon);
        }
    }
}