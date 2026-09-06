using System;
using Moq;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Commands;
using RadialLauncher.Services.Themes;
using Xunit;

namespace RadialLauncher.Tests
{
    public class CommandPaletteServiceTests
    {
        private readonly Mock<ISystemActionService> _systemActionMock;
        private readonly Mock<IThemeService> _themeMock;
        private readonly CommandPaletteService _service;

        public CommandPaletteServiceTests()
        {
            _systemActionMock = new Mock<ISystemActionService>();
            _themeMock = new Mock<IThemeService>();
            _service = new CommandPaletteService(_systemActionMock.Object, _themeMock.Object);
        }

        [Theory]
        [InlineData("= 2+2", "4")]
        [InlineData("= 10 + 5 * 2", "20")]
        [InlineData("= (10 + 5) * 2", "30")]
        [InlineData("= 100 / 4", "25")]
        [InlineData("= 2^3", "8")]
        [InlineData("= 15 - 5", "10")]
        public void TryHandle_MathCalculations_EvaluatesCorrectly(string expr, string expectedVal)
        {
            bool handled = _service.TryHandle(expr, out string msg);
            Assert.True(handled);
            Assert.Contains(expectedVal, msg);
        }

        [Fact]
        public void TryHandle_MathIncomplete_ReturnsErrorMessage()
        {
            bool handled = _service.TryHandle("= 2+", out string msg);
            Assert.True(handled);
            Assert.Contains("Could not evaluate", msg);
        }

        [Fact]
        public void TryHandle_LockCommand_ExecutesSystemAction()
        {
            bool handled = _service.TryHandle(">lock", out string msg);
            Assert.True(handled);
            _systemActionMock.Verify(s => s.ExecuteAction("LOCK_PC"), Times.Once);
        }

        [Fact]
        public void TryHandle_RecycleCommand_ExecutesSystemAction()
        {
            bool handled = _service.TryHandle(">recycle", out string msg);
            Assert.True(handled);
            _systemActionMock.Verify(s => s.ExecuteAction("EMPTY_RECYCLE_BIN"), Times.Once);
        }

        [Fact]
        public void TryHandle_DarkThemeCommand_SetsTheme()
        {
            bool handled = _service.TryHandle(">dark", out string msg);
            Assert.True(handled);
            _themeMock.Verify(t => t.SetCurrentTheme("Dark"), Times.Once);
        }

        [Fact]
        public void TryHandle_EmptyOrNonCommand_ReturnsFalse()
        {
            Assert.False(_service.TryHandle("", out _));
            Assert.False(_service.TryHandle("   ", out _));
            Assert.False(_service.TryHandle("notepad", out _));
        }
    }
}
