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

        [Fact]
        public void SystemActionInfo_DisplayName_TranslatesDynamicallyAcrossLanguages()
        {
            var service = new SystemActionService();
            var actions = service.GetAvailableActions();
            var loc = RadialLauncher.Services.Localization.LocalizationService.Instance;

            var volUp = actions.Find(a => a.ActionKey == "VOLUME_UP");
            var focus25 = actions.Find(a => a.ActionKey == "FOCUS_25");
            var lockPc = actions.Find(a => a.ActionKey == "LOCK_PC");
            var recycle = actions.Find(a => a.ActionKey == "EMPTY_RECYCLE_BIN");

            Assert.NotNull(volUp);
            Assert.NotNull(focus25);
            Assert.NotNull(lockPc);
            Assert.NotNull(recycle);

            // English
            loc.SetLanguage("en");
            Assert.Equal("Volume Up (+2%)", volUp.DisplayName);
            Assert.Equal("🍅 Focus Timer (25m)", focus25.DisplayName);
            Assert.Equal("Lock PC", lockPc.DisplayName);
            Assert.Equal("Empty Recycle Bin", recycle.DisplayName);

            // Turkish
            loc.SetLanguage("tr");
            Assert.Equal("Sesi Aç (+%2)", volUp.DisplayName);
            Assert.Equal("🍅 Odak Zamanlayıcı (25dk)", focus25.DisplayName);
            Assert.Equal("Bilgisayarı Kilitle", lockPc.DisplayName);
            Assert.Equal("Geri Dönüşüm Kutusunu Boşalt", recycle.DisplayName);

            // German
            loc.SetLanguage("de");
            Assert.Equal("Lautstärke erhöhen (+2%)", volUp.DisplayName);
            Assert.Equal("🍅 Fokus-Timer (25 Min.)", focus25.DisplayName);
            Assert.Equal("PC sperren", lockPc.DisplayName);
            Assert.Equal("Papierkorb leeren", recycle.DisplayName);

            // Restore English
            loc.SetLanguage("en");
        }

        [Fact]
        public void SystemActionInfo_All14Actions_HaveNonEmptyDisplayNamesInAll11Languages()
        {
            var service = new SystemActionService();
            var actions = service.GetAvailableActions();
            var loc = RadialLauncher.Services.Localization.LocalizationService.Instance;

            Assert.Equal(14, actions.Count);

            foreach (var lang in loc.SupportedLanguages)
            {
                loc.SetLanguage(lang.Code);
                foreach (var action in actions)
                {
                    Assert.False(string.IsNullOrWhiteSpace(action.DisplayName),
                        $"Action {action.ActionKey} has empty DisplayName in {lang.Code}");
                }
            }

            loc.SetLanguage("en");
        }
    }
}