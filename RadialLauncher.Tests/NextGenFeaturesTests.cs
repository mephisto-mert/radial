using Xunit;
using RadialLauncher.Services.Intelligence;
using RadialLauncher.Services.SystemTools;
using RadialLauncher.Services.Audio;
using RadialLauncher.Services.Clipboard;
using RadialLauncher.Services.Icons;

namespace RadialLauncher.Tests
{
    public class NextGenFeaturesTests
    {
        [Theory]
        [InlineData("245 * 18", "4,410")]
        [InlineData("sqrt(144) + 8", "20")]
        [InlineData("2 ^ 10", "1,024")]
        [InlineData("16 gb to mb", "16,384 MB")]
        [InlineData("100 km to mi", "62.1371 MI")]
        public void InlineCalculator_EvaluatesMathAndUnitConversions(string query, string expectedSubstring)
        {
            var calc = new InlineCalculatorService();
            var result = calc.TryEvaluate(query);
            Assert.True(result.IsValid);
            Assert.Contains(expectedSubstring, result.FormattedResult);
        }

        [Theory]
        [InlineData("discord")]
        [InlineData("42")]
        [InlineData("")]
        public void InlineCalculator_RejectsPlainAppNamesAndSingleNumbers(string query)
        {
            var calc = new InlineCalculatorService();
            var result = calc.TryEvaluate(query);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("chrome", "🌐 Tarayıcı Profili")]
        [InlineData("code", "💻 Geliştirici Profili")]
        [InlineData("spotify", "🎵 Medya Profili")]
        [InlineData("explorer", "⚡ Akıllı Aksiyonlar")]
        public void AppContextProfileService_ReturnsExpectedProfileForProcess(string proc, string expectedTitle)
        {
            var svc = new AppContextProfileService();
            var profile = svc.GetProfileForProcess(proc);
            Assert.Equal(expectedTitle, profile.ProfileTitle);
            Assert.True(profile.QuickItems.Count >= 8);
        }

        [Fact]
        public void PlaytimeTrackerService_RecordsGameLaunchAndFormatsBadge()
        {
            var tracker = new PlaytimeTrackerService();
            tracker.RecordGameLaunch("TestGame_NextGen", @"C:\Games\TestGame.exe");
            string badge = tracker.GetPlaytimeBadge("TestGame_NextGen", @"C:\Games\TestGame.exe");
            Assert.False(string.IsNullOrWhiteSpace(badge));
            Assert.Contains("⏱", badge);
        }

        [Fact]
        public void VectorIconFactory_ResolvesNewNextGenActionIcons()
        {
            Assert.NotNull(VectorIconFactory.GetActionIcon("SNAP_LEFT"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("SNAP_RIGHT"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("ALWAYS_ON_TOP"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("GLASS_OPACITY"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("BOOST_RAM"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("HOTKEY"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("SNIPPET"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("NEXT_MONITOR"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("SAVE_WORKSPACE"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("RESTORE_WORKSPACE"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("SWITCH_AUDIO_DEVICE"));
            Assert.NotNull(VectorIconFactory.GetActionIcon("APP_AUDIO"));
        }

        [Fact]
        public void UiSoundEffectsService_SynthesizesWavsWithoutError()
        {
            var sfx = new UiSoundEffectsService { IsEnabled = false };
            sfx.PlayHoverTick();
            sfx.PlayBloomOpen();
            sfx.PlaySubmenuDive();
            sfx.PlayLaunchConfirm();
            sfx.PlayGamingBoost();
            Assert.False(sfx.IsEnabled);
        }

        [Fact]
        public void RichClipboardPreviewService_DetectsHexColorUrlAndCodeSnippet()
        {
            var svc = new RichClipboardPreviewService();

            var hexMeta = svc.Analyze("#FF5733");
            Assert.Equal(RichClipboardKind.HexColor, hexMeta.Kind);
            Assert.Contains("#FF5733", hexMeta.FormattedTitle);
            Assert.NotNull(svc.CreateRichIcon("#FF5733"));

            var urlMeta = svc.Analyze("https://github.com/google/radial-launcher");
            Assert.Equal(RichClipboardKind.Url, urlMeta.Kind);
            Assert.Contains("github.com", urlMeta.FormattedTitle);
            Assert.NotNull(svc.CreateRichIcon("https://github.com/google/radial-launcher"));

            var codeMeta = svc.Analyze("public void Execute() { return; }");
            Assert.Equal(RichClipboardKind.CodeSnippet, codeMeta.Kind);
            Assert.Contains("💻 Kod:", codeMeta.FormattedTitle);
            Assert.NotNull(svc.CreateRichIcon("public void Execute() { return; }"));
        }

        [Fact]
        public void SystemTelemetryService_ReturnsValidMetrics()
        {
            var telemetry = new SystemTelemetryService();
            var snap = telemetry.GetLatestSnapshot();
            Assert.InRange(snap.CpuPercent, 0, 100);
            Assert.InRange(snap.RamPercent, 1, 100);
            Assert.True(snap.TotalRamMb > 0);
            Assert.InRange(snap.BatteryPercent, 0, 100);
        }

        [Fact]
        public void WorkspaceAndMonitorService_SavesAndRestoresSnapshotSafely()
        {
            var ws = new WorkspaceAndMonitorService();
            string saveMsg = ws.SaveWorkspaceSnapshot("UnitTestLayout");
            Assert.Contains("💾", saveMsg);
        }

        [Fact]
        public void PerAppAudioAndDeviceService_HandlesVolumeAndSessionsGracefully()
        {
            var audio = new PerAppAudioAndDeviceService();
            var sessions = audio.GetActiveAppAudioSessions();
            Assert.NotNull(sessions);
            int adjusted = audio.AdjustAppVolume(999999, -15);
            Assert.Equal(85, adjusted);
        }

        [Fact]
        public void ThemeService_PersistsAndRestoresProFeatureToggles()
        {
            var svc = RadialLauncher.Services.Themes.ThemeService.Instance;
            svc.SetEnableSoundEffects(false);
            svc.SetEnableCursorTrail(false);
            svc.SetEnableTelemetryRings(false);
            svc.SetEnableOrbitalPeek(false);
            svc.SetGameFocusProtection(false);

            var current = svc.GetCurrentTheme();
            Assert.False(current.EnableSoundEffects);
            Assert.False(current.EnableCursorTrail);
            Assert.False(current.EnableTelemetryRings);
            Assert.False(current.EnableOrbitalPeek);
            Assert.False(svc.GetGameFocusProtection());

            svc.SetEnableSoundEffects(true);
            svc.SetEnableCursorTrail(true);
            svc.SetEnableTelemetryRings(true);
            svc.SetEnableOrbitalPeek(true);
            svc.SetGameFocusProtection(true);

            var restored = svc.GetCurrentTheme();
            Assert.True(restored.EnableSoundEffects);
            Assert.True(restored.EnableCursorTrail);
            Assert.True(restored.EnableTelemetryRings);
            Assert.True(restored.EnableOrbitalPeek);
            Assert.True(svc.GetGameFocusProtection());
        }
    }
}
