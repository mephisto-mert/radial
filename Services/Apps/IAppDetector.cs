using System.Collections.Generic;

namespace RadialLauncher.Services.Apps
{
    public class DetectedApp
    {
        public string Name { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string CategoryKey { get; set; } = "Cat_Apps";
        public string CategoryName { get; set; } = "📱 Applications";
        public string CategoryColor { get; set; } = "#3498db";
        public bool IsRunning { get; set; }
    }

    public interface IAppDetector
    {
        List<DetectedApp> DetectRunningAndCommonApps();
    }
}
