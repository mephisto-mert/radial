using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Serilog;

namespace RadialLauncher.Services.Plugins
{
    public class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "RadialLauncher")
            {
                return null;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

    public class PluginService : IPluginService
    {
        private readonly List<IRadialItemProvider> _providers = new();
        private readonly List<PluginAssemblyLoadContext> _contexts = new();

        public void RegisterProvider(IRadialItemProvider provider)
        {
            if (provider == null) return;
            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
                Log.Information("Registered item provider plugin: {Name} (Category: {Category})", 
                    provider.ProviderName, provider.CategoryName);
            }
        }

        public IReadOnlyList<IRadialItemProvider> GetProviders() => _providers;

        public void LoadPlugins(string? pluginsDir = null)
        {
            try
            {
                string targetDir = pluginsDir ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RadialLauncher", "Plugins");

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    Log.Information("Created plugins directory at {Path}", targetDir);
                    return;
                }

                var dllFiles = Directory.GetFiles(targetDir, "*.dll", SearchOption.AllDirectories);
                Log.Information("Scanning {Count} DLL files for plugins in {Dir}", dllFiles.Length, targetDir);

                foreach (var dll in dllFiles)
                {
                    LoadPluginAssembly(dll);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during plugin scanning");
            }
        }

        public void LoadPluginAssembly(string dllPath)
        {
            try
            {
                var alc = new PluginAssemblyLoadContext(dllPath);
                Assembly asm = alc.LoadFromAssemblyPath(dllPath);

                var providerTypes = asm.GetTypes()
                    .Where(t => typeof(IRadialItemProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                bool foundAny = false;
                foreach (var type in providerTypes)
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IRadialItemProvider instance)
                        {
                            RegisterProvider(instance);
                            foundAny = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to instantiate plugin provider {Type} from {Path}", type.FullName, dllPath);
                    }
                }

                if (foundAny)
                {
                    _contexts.Add(alc);
                }
                else
                {
                    alc.Unload();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load plugin DLL {Path}", dllPath);
            }
        }
    }
}
