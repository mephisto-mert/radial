using System.Threading;
using System.Threading.Tasks;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Processes
{
    public interface IProcessRunner
    {
        void Launch(LauncherItem item);
        Task ExecuteMacroAsync(string jsonSteps, CancellationToken cancellationToken = default);
        void CancelAllRunningMacros();
    }
}
