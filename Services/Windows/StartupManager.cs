namespace RadialLauncher.Services.Windows
{
    public class StartupManager : IStartupManager
    {
        public bool IsRunOnStartup() => RadialLauncher.Services.StartupManager.IsRunOnStartup();
        public void SetRunOnStartup(bool enable) => RadialLauncher.Services.StartupManager.SetRunOnStartup(enable);
    }
}
