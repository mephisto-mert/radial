using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using RadialLauncher.Data.Repositories;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Processes;
using Xunit;

namespace RadialLauncher.Tests
{
    public class MacroExecutionTests
    {
        [Fact]
        public void MacroStep_SerializationAndDeserialization_PreservesAllFields()
        {
            var originalSteps = new List<MacroStep>
            {
                new MacroStep { Name = "Step 1", Type = "ACTION", Target = "VOLUME_UP", DelayMs = 150 },
                new MacroStep { Name = "Step 2", Type = "URL", Target = "https://example.com", DelayMs = 300 },
                new MacroStep { Name = "Step 3", Type = "EXE", Target = "notepad.exe", Arguments = "test.txt", DelayMs = 0 }
            };

            string json = JsonSerializer.Serialize(originalSteps);
            var deserialized = JsonSerializer.Deserialize<List<MacroStep>>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(3, deserialized.Count);
            Assert.Equal("Step 1", deserialized[0].Name);
            Assert.Equal("ACTION", deserialized[0].Type);
            Assert.Equal(150, deserialized[0].DelayMs);
            Assert.Equal("https://example.com", deserialized[1].Target);
            Assert.Equal("test.txt", deserialized[2].Arguments);
        }

        [Fact]
        public async Task ProcessRunner_ExecutesMacroActionSteps()
        {
            var actionServiceMock = new Mock<ISystemActionService>();
            var itemRepoMock = new Mock<IItemRepository>();
            var executedActions = new List<string>();

            actionServiceMock
                .Setup(a => a.ExecuteAction(It.IsAny<string>()))
                .Callback<string>(key => executedActions.Add(key));

            var runner = new ProcessRunner(itemRepoMock.Object, actionServiceMock.Object);

            var steps = new List<MacroStep>
            {
                new MacroStep { Name = "Volume Up", Type = "ACTION", Target = "VOLUME_UP", DelayMs = 10 },
                new MacroStep { Name = "Mute", Type = "ACTION", Target = "VOLUME_MUTE", DelayMs = 10 }
            };

            var macroItem = new LauncherItem
            {
                Id = 99,
                Name = "Test Macro",
                Type = "MACRO",
                Target = JsonSerializer.Serialize(steps)
            };

            await runner.ExecuteMacroAsync(macroItem.Target);

            Assert.Equal(2, executedActions.Count);
            Assert.Equal("VOLUME_UP", executedActions[0]);
            Assert.Equal("VOLUME_MUTE", executedActions[1]);
        }

        [Fact]
        public async Task ProcessRunner_HandlesEmptyOrMalformedMacroGracefully()
        {
            var actionServiceMock = new Mock<ISystemActionService>();
            var runner = new ProcessRunner(null, actionServiceMock.Object);

            // Empty JSON
            await runner.ExecuteMacroAsync("");
            await runner.ExecuteMacroAsync("[]");
            // Malformed JSON
            await runner.ExecuteMacroAsync("{ invalid json }");

            // Verify no crashes occurred
            actionServiceMock.Verify(a => a.ExecuteAction(It.IsAny<string>()), Times.Never);
        }
    }
}
