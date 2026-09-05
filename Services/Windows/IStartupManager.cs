namespace RadialLauncher.Services.Windows
{
    public interface IStartupManager
    {
        bool IsRunOnStartup();
        void SetRunOnStartup(bool enable);
    }
}
