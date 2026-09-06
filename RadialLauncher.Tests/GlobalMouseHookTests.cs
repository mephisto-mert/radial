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

        [Fact]
        public void IsMouseTriggerMatched_KeyboardHotkeys_ReturnFalse()
        {
            Assert.False(GlobalMouseHook.IsMouseTriggerMatched("AltSpace", 0x0207, 0));
            Assert.False(GlobalMouseHook.IsMouseTriggerMatched("CtrlSpace", 0x0207, 0));
            Assert.False(GlobalMouseHook.IsMouseTriggerMatched("F4", 0x0207, 0));
            Assert.False(GlobalMouseHook.IsMouseTriggerMatched(string.Empty, 0x0207, 0));
        }

        [Fact]
        public void IsMouseTriggerMatched_XButtons_MatchCorrectHighWord()
        {
            // If modifiers are not held during test run:
            // 0x00010000 is high-order word 1 (XBUTTON1)
            // 0x00020000 is high-order word 2 (XBUTTON2)
            const int WM_XBUTTONDOWN = 0x020B;
            bool x1Match = GlobalMouseHook.IsMouseTriggerMatched("XButton1", WM_XBUTTONDOWN, 0x00010000);
            bool x2Match = GlobalMouseHook.IsMouseTriggerMatched("XButton2", WM_XBUTTONDOWN, 0x00020000);
            
            // Should match when no modifiers are required and none pressed
            Assert.True(x1Match);
            Assert.True(x2Match);

            // Mismatched button index
            Assert.False(GlobalMouseHook.IsMouseTriggerMatched("XButton1", WM_XBUTTONDOWN, 0x00020000));
            Assert.False(GlobalMouseHook.IsMouseTriggerMatched("XButton2", WM_XBUTTONDOWN, 0x00010000));
        }
    }
}