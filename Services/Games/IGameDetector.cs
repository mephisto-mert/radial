using System.Collections.Generic;

namespace RadialLauncher.Services.Games
{
    public interface IGameDetector
    {
        List<DetectedGame> DetectAll();
        List<DetectedGame> DetectSteam();
        List<DetectedGame> DetectEpic();
    }
}
