using RadialLauncher.Models;

namespace RadialLauncher.Services.Processes
{
    public interface IProcessRunner
    {
        void Launch(LauncherItem item);
    }
}
