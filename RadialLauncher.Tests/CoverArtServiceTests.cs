using RadialLauncher.Services.Games;
using Xunit;

namespace RadialLauncher.Tests
{
    public class CoverArtServiceTests
    {
        [Theory]
        [InlineData("Cyberpunk 2077", "Cyberpunk 2077")]
        [InlineData("Baldur's Gate 3", "Baldur's Gate 3")]
        [InlineData("Cyberpunk 2077™", "Cyberpunk 2077")]
        [InlineData("Hades®", "Hades")]
        [InlineData("Rocket League - Game", "Rocket League")]
        [InlineData("Elden Ring (PC)", "Elden Ring")]
        [InlineData("Portal 2 (Demo)", "Portal 2")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        public void NormalizeGameName_RemovesNoise(string input, string expected)
        {
            Assert.Equal(expected, CoverArtService.NormalizeGameName(input));
        }
    }
}
