using System;
using RadialLauncher.Hooks;
using Xunit;

namespace RadialLauncher.Tests
{
    public class GlobalMouseHookTests
    {
        [Fact]
        public void GlobalMouseHook_InitialState_IsNotInstalled()
        {
            using var hook = new GlobalMouseHook();
            Assert.False(hook.IsInstalled);
            Assert.Equal("MiddleClick", hook.TriggerMode);
            Assert.False(hook.IsPassThroughEnabled);
            Assert.Equal(0.0, hook.LastCursorVelocity);
        }

        [Fact]
        public void GlobalMouseHook_StartAndStop_HandlesRepeatedCallsSafely()
        {
            using var hook = new GlobalMouseHook();

            // Calling stop before start should not throw
            var ex1 = Record.Exception(() => hook.Stop());
            Assert.Null(ex1);

            // Repeated start/stop cycle
            var ex2 = Record.Exception(() =>
            {
                hook.Start();
                hook.Start(); // Duplicate start
                hook.Stop();
                hook.Stop();  // Duplicate stop
            });
            Assert.Null(ex2);
            Assert.False(hook.IsInstalled);
        }

        [Theory]
        [InlineData("MiddleClick")]
        [InlineData("AltRightClick")]
        [InlineData("ShiftRightClick")]
        [InlineData("CtrlRightClick")]
        [InlineData("XButton1")]
        [InlineData("XButton2")]
        public void GlobalMouseHook_SupportsAllTriggerModes(string mode)
        {
            using var hook = new GlobalMouseHook();
            hook.TriggerMode = mode;
            Assert.Equal(mode, hook.TriggerMode);
        }
    }
}