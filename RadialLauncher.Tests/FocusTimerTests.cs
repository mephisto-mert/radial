using System;
using System.Threading;
using RadialLauncher.Services.Actions;
using Xunit;

namespace RadialLauncher.Tests
{
    public class FocusTimerTests
    {
        [Fact]
        public void FocusTimer_StartAndStop_UpdatesRunningStateAndRemaining()
        {
            var service = new SystemActionService();

            Assert.False(service.IsFocusTimerRunning);
            Assert.Equal(TimeSpan.Zero, service.FocusTimerRemaining);

            service.StartFocusTimer(25);

            Assert.True(service.IsFocusTimerRunning);
            Assert.True(service.FocusTimerRemaining > TimeSpan.FromMinutes(24));
            Assert.True(service.FocusTimerRemaining <= TimeSpan.FromMinutes(25));

            service.StopFocusTimer();

            Assert.False(service.IsFocusTimerRunning);
            Assert.Equal(TimeSpan.Zero, service.FocusTimerRemaining);
        }

        [Fact]
        public void ExecuteAction_FOCUS_25_TogglesState()
        {
            var service = new SystemActionService();

            service.ExecuteAction("FOCUS_25");
            Assert.True(service.IsFocusTimerRunning);

            service.ExecuteAction("FOCUS_25");
            Assert.False(service.IsFocusTimerRunning);
        }

        [Fact]
        public void AvailableActions_ContainsFocus25()
        {
            var service = new SystemActionService();
            var actions = service.GetAvailableActions();

            var focusAction = actions.Find(a => a.ActionKey == "FOCUS_25");
            Assert.NotNull(focusAction);
            Assert.Contains("Focus", focusAction.DisplayName);
            Assert.Equal("🍅", focusAction.IconSymbol);
        }
    }
}