using System.Collections.Generic;

namespace RadialLauncher.Services.Games
{
    public class GameDetector : IGameDetector
    {
        public List<DetectedGame> DetectAll() => RadialLauncher.Services.GameDetector.DetectAllGames();
        public List<DetectedGame> DetectSteam() => RadialLauncher.Services.GameDetector.DetectSteamGames();
        public List<DetectedGame> DetectEpic() => RadialLauncher.Services.GameDetector.DetectEpicGames();
    }
}
