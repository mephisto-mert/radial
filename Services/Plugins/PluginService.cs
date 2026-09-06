using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using RadialLauncher.Models;
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

        public IReadOnlyList<LauncherItem> GetSafeItems(int providerIndex)
        {
            if (providerIndex < 0 || providerIndex >= _providers.Count)
            {
                return Array.Empty<LauncherItem>();
            }
            return GetSafeItems(_providers[providerIndex]);
        }

        public IReadOnlyList<LauncherItem> GetSafeItems(IRadialItemProvider provider)
        {
            if (provider == null) return Array.Empty<LauncherItem>();

            try
            {
                var rawItems = provider.GetItems();
                if (rawItems == null) return Array.Empty<LauncherItem>();

                var safeList = new List<LauncherItem>();
                int pos = 0;
                foreach (var item in rawItems)
                {
                    if (item == null) continue;
                    if (string.IsNullOrWhiteSpace(item.Name) && string.IsNullOrWhiteSpace(item.Target)) continue;

                    // Ensure safe non-null properties and assigned position
                    safeList.Add(new LauncherItem
                    {
                        Id = item.Id != 0 ? item.Id : (-900 - pos),
                        Name = string.IsNullOrWhiteSpace(item.Name) ? (item.Target ?? "Plugin Item") : item.Name,
                        Type = string.IsNullOrWhiteSpace(item.Type) ? "EXE" : item.Type,
                        Target = item.Target ?? string.Empty,
                        Arguments = item.Arguments ?? string.Empty,
                        IconPath = item.IconPath ?? string.Empty,
                        CategoryId = item.CategoryId,
                        Position = pos++
                    });
                }
                return safeList;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Plugin provider '{ProviderName}' failed during GetItems() execution", provider.ProviderName);
                return Array.Empty<LauncherItem>();
            }
        }

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
