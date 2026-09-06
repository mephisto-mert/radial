using System;
using System.IO;
using System.Text.Json;
using RadialLauncher.Services.Context;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ContextualActionServiceTests : IDisposable
    {
        private readonly string _tempConfigPath;

        public ContextualActionServiceTests()
        {
            _tempConfigPath = Path.Combine(Path.GetTempPath(), $"context_actions_test_{Guid.NewGuid()}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_tempConfigPath))
            {
                try { File.Delete(_tempConfigPath); } catch { }
            }
        }

        [Fact]
        public void DefaultConfig_IsCreatedAndProvidesDefaults()
        {
            var service = new ContextualActionService(_tempConfigPath);

            Assert.True(File.Exists(_tempConfigPath));

            var codeItems = service.GetContextualItems("code.exe");
            Assert.NotEmpty(codeItems);
            Assert.Contains(codeItems, i => i.Name.Contains("Terminal"));

            var chromeItems = service.GetContextualItems("chrome.exe");
            Assert.NotEmpty(chromeItems);
            Assert.Contains(chromeItems, i => i.Name.Contains("Yeni Sekme"));

            var unknown = service.GetContextualItems("non_existent_process.exe");
            Assert.Empty(unknown);
        }

        [Fact]
        public void ProcessMatching_IsFlexibleCaseAndExtension()
        {
            var service = new ContextualActionService(_tempConfigPath);

            var itemsWithExt = service.GetContextualItems("CODE.EXE");
            var itemsNoExt = service.GetContextualItems("code");
            var itemsFullPath = service.GetContextualItems(@"C:\Users\App\Local\Programs\Microsoft VS Code\code.exe");

            Assert.NotEmpty(itemsWithExt);
            Assert.Equal(itemsWithExt.Count, itemsNoExt.Count);
            Assert.Equal(itemsWithExt.Count, itemsFullPath.Count);
            Assert.Equal(itemsWithExt[0].Name, itemsNoExt[0].Name);
        }

        [Fact]
        public void CustomRules_CanBeLoadedFromDisk()
        {
            var customRules = new[]
            {
                new ContextualRuleConfig
                {
                    ProcessName = "notepad.exe",
                    Items = new System.Collections.Generic.List<ContextualItemConfig>
                    {
                        new ContextualItemConfig { Name = "Yeni Belge", Target = "notepad.exe", Type = "EXE" },
                        new ContextualItemConfig { Name = "Günlük Aç", Target = "diary.txt", Type = "EXE" }
                    }
                }
            };

            File.WriteAllText(_tempConfigPath, JsonSerializer.Serialize(customRules));

            var service = new ContextualActionService(_tempConfigPath);
            var items = service.GetContextualItems("notepad.exe");

            Assert.Equal(2, items.Count);
            Assert.Contains(items, i => i.Name.Contains("Yeni Belge"));
            Assert.Contains(items, i => i.Name.Contains("Günlük Aç"));
        }
    }
}
