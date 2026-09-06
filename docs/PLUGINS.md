# Radial Launcher — Plugin Development Guide

Radial Launcher supports modular extensibility via dynamic .NET Assembly plugins. Plugins can introduce custom categories, dynamically calculated menu items, system integrations, and quick actions directly into the radial interface.

---

## 1. Overview & Architecture

Plugins are standard .NET class libraries targeting 
et7.0-windows (or compatible .NET 7/8 runtimes) that implement the IRadialItemProvider interface.

Radial Launcher discovers and loads plugins at runtime from:
`
%LocalAppData%\RadialLauncher\Plugins\
`

Every .dll located in the plugins folder (or subfolders) is inspected using an isolated AssemblyLoadContext. Any non-abstract class implementing IRadialItemProvider is automatically instantiated and registered.

---

## 2. The Plugin Interface

Implement IRadialItemProvider from RadialLauncher.Services.Plugins:

`csharp
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
}
`

### Property & Method Specifications:
* **ProviderName**: Unique display name identifying your plugin.
* **CategoryName**: The category title displayed on the radial menu pill when this provider is active (e.g. \"?? Eklentiler\" or \"??? DevOps Tools\").
* **CategoryColor**: Hex color code for category accent (e.g. \"#9B59B6\").
* **GetItems()**: Returns the collection of LauncherItem objects to render on the radial dial.

---

## 3. Example Implementation

Here is a complete sample provider:

`csharp
using System.Collections.Generic;
using RadialLauncher.Models;
using RadialLauncher.Services.Plugins;

namespace MyCustomPlugin
{
    public class QuickToolsProvider : IRadialItemProvider
    {
        public string ProviderName => \"Quick Tools Provider\";
        public string CategoryName => \"??? Quick Tools\";
        public string CategoryColor => \"#00D2D3\";

        public IEnumerable<LauncherItem> GetItems()
        {
            return new List<LauncherItem>
            {
                new LauncherItem
                {
                    Name = \"Notepad\",
                    Type = \"EXE\",
                    Target = \"notepad.exe\",
                    Position = 0
                },
                new LauncherItem
                {
                    Name = \"GitHub\",
                    Type = \"URL\",
                    Target = \"https://github.com\",
                    Position = 1
                }
            };
        }
    }
}
`

---

## 4. Item Types Supported

* **EXE**: Starts an executable binary. Target is file path or command name. Arguments can optionally be passed.
* **URL**: Opens a web URL in the default browser.
* **ACTION**: Triggers a built-in system action (e.g. LOCK, SLEEP, MUTE, SCREENSHOT, DESKTOP_1, FOCUS_25).
* **MACRO**: Executes a JSON-encoded series of sequential steps with delays.

---

## 5. Security & Isolation Model

> [!WARNING]
> **No Process Sandbox:** Plugins run inside the main RadialLauncher.exe host process with standard user permissions. Only install and run plugins from sources you trust.

* **Runtime Fault Isolation:** Radial Launcher executes plugin item fetching inside an isolated GetSafeItems() try/catch boundary. If a plugin throws an unhandled exception or returns corrupted entries during GetItems(), Radial Launcher safely isolates the error, logs diagnostics to Serilog, and prevents any UI or application crash.
* **Malformed Data Filtering:** Null items, empty targets, and invalid types are automatically sanitized.

---

## 6. Deployment & Testing

1. Compile your plugin project (dotnet build -c Release).
2. Copy YourPlugin.dll (and any private non-framework dependencies) to %LocalAppData%\RadialLauncher\Plugins\.
3. Restart Radial Launcher or open **Ayarlar & Yönetim**.
4. The new category and radial items will appear automatically on your radial dial!
