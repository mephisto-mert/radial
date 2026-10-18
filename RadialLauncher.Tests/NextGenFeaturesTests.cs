using Xunit;
using RadialLauncher.Services.Intelligence;
using RadialLauncher.Services.SystemTools;
using RadialLauncher.Services.Audio;
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
    }
}
