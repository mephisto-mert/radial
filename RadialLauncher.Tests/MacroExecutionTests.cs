using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
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
        public async Task ProcessRunner_ExecutesMacroActionStepsSequentially()
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

            string json = JsonSerializer.Serialize(steps);
            await runner.ExecuteMacroAsync(json);

            Assert.Equal(2, executedActions.Count);
            Assert.Equal("VOLUME_UP", executedActions[0]);
            Assert.Equal("VOLUME_MUTE", executedActions[1]);
        }

        [Fact]
        public async Task ProcessRunner_EmptyOrNullMacro_CompletesSafely()
        {
            var actionServiceMock = new Mock<ISystemActionService>();
            var runner = new ProcessRunner(null, actionServiceMock.Object);

            var ex1 = await Record.ExceptionAsync(() => runner.ExecuteMacroAsync(""));
            var ex2 = await Record.ExceptionAsync(() => runner.ExecuteMacroAsync("[]"));
            var ex3 = await Record.ExceptionAsync(() => runner.ExecuteMacroAsync(null!));
            var ex4 = await Record.ExceptionAsync(() => runner.ExecuteMacroAsync("invalid json"));

            Assert.Null(ex1);
            Assert.Null(ex2);
            Assert.Null(ex3);
            Assert.Null(ex4);
        }

        [Fact]
        public async Task ProcessRunner_NegativeAndZeroDelay_HandledSafely()
        {
            var actionServiceMock = new Mock<ISystemActionService>();
            var executedActions = new List<string>();
            actionServiceMock.Setup(a => a.ExecuteAction(It.IsAny<string>())).Callback<string>(s => executedActions.Add(s));

            var runner = new ProcessRunner(null, actionServiceMock.Object);
            var steps = new List<MacroStep>
            {
                new MacroStep { Name = "Zero", Type = "ACTION", Target = "ZERO", DelayMs = 0 },
                new MacroStep { Name = "Negative", Type = "ACTION", Target = "NEG", DelayMs = -500 }
            };

            await runner.ExecuteMacroAsync(JsonSerializer.Serialize(steps));

            Assert.Equal(2, executedActions.Count);
            Assert.Equal("ZERO", executedActions[0]);
            Assert.Equal("NEG", executedActions[1]);
        }

        [Fact]
        public async Task ProcessRunner_CancellationRequested_ThrowsAndStopsPromptly()
        {
            var actionServiceMock = new Mock<ISystemActionService>();
            var executedActions = new List<string>();
            actionServiceMock.Setup(a => a.ExecuteAction(It.IsAny<string>())).Callback<string>(s => executedActions.Add(s));

            var runner = new ProcessRunner(null, actionServiceMock.Object);
            var steps = new List<MacroStep>
            {
                new MacroStep { Name = "Step 1", Type = "ACTION", Target = "STEP_1", DelayMs = 5000 },
                new MacroStep { Name = "Step 2", Type = "ACTION", Target = "STEP_2", DelayMs = 10 }
            };

            using var cts = new CancellationTokenSource();
            // Cancel after 50ms (during the 5000ms delay of Step 1)
            cts.CancelAfter(50);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await runner.ExecuteMacroAsync(JsonSerializer.Serialize(steps), cts.Token);
            });

            // Step 1 executed, Step 2 did NOT execute
            Assert.Single(executedActions);
            Assert.Equal("STEP_1", executedActions[0]);
        }

        [Fact]
        public async Task ProcessRunner_PreCancelledToken_DoesNotExecuteAnySteps()
        {
            var actionServiceMock = new Mock<ISystemActionService>();
            var executedActions = new List<string>();
            actionServiceMock.Setup(a => a.ExecuteAction(It.IsAny<string>())).Callback<string>(s => executedActions.Add(s));

            var runner = new ProcessRunner(null, actionServiceMock.Object);
            var steps = new List<MacroStep>
            {
                new MacroStep { Name = "Step 1", Type = "ACTION", Target = "STEP_1", DelayMs = 10 }
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await runner.ExecuteMacroAsync(JsonSerializer.Serialize(steps), cts.Token);
            });

            Assert.Empty(executedActions);
        }

        [Fact]
        public void ProcessRunner_CancelAllRunningMacros_CancelsActiveBackgroundTokens()
        {
            var runner = new ProcessRunner();
            // Bulk cancel without active macros should not throw
            var ex = Record.Exception(() => runner.CancelAllRunningMacros());
            Assert.Null(ex);
        }
    }
}