using System.Collections.Generic;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Plugins
{
    public interface IRadialItemProvider
    {
        string ProviderName { get; }
        string CategoryName { get; }
        string CategoryColor { get; }
        IEnumerable<LauncherItem> GetItems();
    }

    public interface IPluginService
    {
        void RegisterProvider(IRadialItemProvider provider);
        IReadOnlyList<IRadialItemProvider> GetProviders();
        void LoadPlugins(string? pluginsDir = null);
        void LoadPluginAssembly(string dllPath);
    }
}
