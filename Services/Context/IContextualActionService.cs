using System.Collections.Generic;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Context
{
    public interface IContextualActionService
    {
        List<LauncherItem> GetContextualItems(string processName);
        void Reload();
    }
}
