using System;
using System.IO;
using System.Linq;
using RadialLauncher.Models;
using RadialLauncher.Services.Context;
using Xunit;

namespace RadialLauncher.Tests
{
    public class ContextualActionExpandedTests
    {
        private readonly ContextualActionService _service;

        public ContextualActionExpandedTests()
        {
            string tempConfig = Path.Combine(Path.GetTempPath(), $"context_test_{Guid.NewGuid():N}.json");
            _service = new ContextualActionService(tempConfig);
        }

        [Fact]
        public void SteamGameItem_ReturnsPlayAndStoreAndCommunityActions()
        {
            var item = new LauncherItem
            {
                Name = "Counter-Strike 2",
                Target = "steam://rungameid/730",
                Type = "EXE"
            };

            var actions = _service.GetItemQuickActions(item);

            Assert.NotEmpty(actions);
            Assert.Contains(actions, a => a.Id == "STEAM_PLAY");
            Assert.Contains(actions, a => a.Id == "STEAM_STORE" && a.Payload.Contains("730"));
            Assert.Contains(actions, a => a.Id == "STEAM_COMMUNITY");
        }

        [Fact]
        public void SteamClientItem_ReturnsOpenLibraryAndStoreActions()
        {
            var item = new LauncherItem
            {
                Name = "Steam",
                Target = @"C:\Program Files (x86)\Steam\steam.exe",
                Type = "EXE"
            };

            var actions = _service.GetItemQuickActions(item);

            Assert.NotEmpty(actions);
            Assert.Contains(actions, a => a.Id == "STEAM_OPEN");
            Assert.Contains(actions, a => a.Id == "STEAM_LIBRARY");
            Assert.Contains(actions, a => a.Id == "STEAM_STORE_MAIN");
        }

        [Fact]
        public void WebUrlItem_ReturnsBrowserAndCopyUrlActions()
        {
            var item = new LauncherItem
            {
                Name = "GitHub",
                Target = "https://github.com",
                Type = "URL"
            };

            var actions = _service.GetItemQuickActions(item);

            Assert.NotEmpty(actions);
            Assert.Contains(actions, a => a.Id == "URL_OPEN" && a.ActionType == "LAUNCH");
            Assert.Contains(actions, a => a.Id == "URL_COPY" && a.ActionType == "COPY_URL");
        }

        [Fact]
        public void StandardExeItem_ReturnsLaunchAndExploreActions()
        {
            var item = new LauncherItem
            {
                Name = "Notepad",
                Target = "notepad.exe",
                Type = "EXE"
            };

            var actions = _service.GetItemQuickActions(item);

            Assert.NotEmpty(actions);
            Assert.Contains(actions, a => a.Id == "APP_LAUNCH");
        }

        [Fact]
        public void NullOrEmptyItem_HandledGracefullyWithoutException()
        {
            var actionsNull = _service.GetItemQuickActions(null!);
            Assert.Empty(actionsNull);

            var emptyItem = new LauncherItem { Type = "" };
            var actionsEmpty = _service.GetItemQuickActions(emptyItem);
            Assert.NotEmpty(actionsEmpty);
            Assert.Equal("DEFAULT_LAUNCH", actionsEmpty[0].Id);
        }
    }
}
