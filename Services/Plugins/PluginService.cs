using System.Collections.Generic;

namespace RadialLauncher.Services.Plugins
{
    public class PluginService : IPluginService
    {
        private readonly List<IRadialItemProvider> _providers = new();

        public void RegisterProvider(IRadialItemProvider provider)
        {
            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
            }
        }

        public IReadOnlyList<IRadialItemProvider> GetProviders() => _providers;
    }
}
