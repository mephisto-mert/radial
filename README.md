# ?? Radial Launcher

<div align="center">
  <img src="Resources/app.ico" width="128" height="128" alt="Radial Launcher Logo" />
  <br />
  <h3>High-Performance Hardware-Accelerated Radial Menu &amp; Windows Command Center</h3>
  <p>Ultra-fast circular application launcher, live window switcher, game library aggregator, macro automation, and system control dashboard for Windows 10 &amp; 11.</p>
</div>

---

## ?? Key Features

* **?? Live Window Switcher & DWM Previews:** Instant enumeration of all top-level desktop windows with real-time thumbnail previews and virtual desktop migration.
* **? System & Media Actions:** Hardware-level volume control, media keys, Windows screenshot (Win+Shift+S), Pomodoro focus timer (25 min), lock screen, and system actions.
* **?? Hierarchical Submenus & Layers:** Infinite-depth nested submenus with fluid morphing center back button and keyboard shortcut navigation.
* **?? Steam & Epic Games Detection:** Automated background scanning of Steam libraries and Epic Games manifests with official game art extraction.
* **?? Clipboard History Manager:** Low-latency thread-safe clipboard listener with 20-item rolling history, Unicode support, and 500KB text truncation memory guard.
* **?? Theme Engine & Custom Themes:** 8 built-in themes (Dark, Light, Midnight Blue, Purple Haze, Forest, Cyberpunk, Crimson Red, AMOLED Black), live radial preview, wallpaper accent extraction, and Windows dark mode synchronization.
* **?? Macro Automation Runner:** Multi-step action sequencer with configurable delays, cancellation support, and safe application exit lifecycle.
* **?? GitHub Gist Cloud Sync:** Encrypted DPAPI vault for backing up and synchronizing settings across multiple machines.
* **?? Modular Plugin Architecture:** Dynamic assembly plugin loading with isolated runtime exception boundaries ([Plugin Development Guide](docs/PLUGINS.md)).
* **?? Instant Search-as-you-type:** Type any character while the radial menu is open to dynamically filter items with fuzzy matching.

---

## ?? Controls & Shortcuts

| Action | Input / Shortcut |
| :--- | :--- |
| **Open Menu** | Middle Mouse Button (Wheel Click) / Alt+Space / Ctrl+Space / F4 |
| **Launch Item** | Left Click / Enter |
| **Navigate Items** | Arrow Keys / Tab |
| **Close Window Item** | Middle Click on Window item |
| **Move Window to Virtual Desktop** | Right Click on Window item |
| **Open Submenu** | Left Click on Submenu item |
| **Back to Parent Menu** | Center ‹ Button / Escape / Backspace |
| **Change Category / Page** | Mouse Wheel Scroll / Category Pill Click |
| **Instant Search** | Type directly on keyboard |
| **Open Management & Settings** | Right Click on Center Button or Tray Icon Menu |

---

## ?? Installation & Packaging

### 1. Portable Release
Download RadialLauncher-1.0.0-win-x64.zip from [Releases](https://github.com/mephisto-mert/radial/releases), extract to any folder, and run RadialLauncher.exe.

### 2. Windows Installer
Run RadialLauncher-1.0.0-Setup.exe to install to %ProgramFiles%\RadialLauncher with Start Menu and optional Desktop shortcuts.

---

## ??? Building from Source

### Prerequisites
* Windows 10 (1809+) or Windows 11
* [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) (or [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0))

### Build & Run
``powershell
# Clone repository
git clone https://github.com/mephisto-mert/radial.git
cd radial

# Build Debug
dotnet build

# Run unit and integration tests (84 tests)
dotnet test

# Build & Run application
dotnet run --project RadialLauncher.csproj
``

### Release Packaging
To build the standalone release package and checksums:
``powershell
powershell -ExecutionPolicy Bypass -File scripts\package.ps1
``

---

## ?? Configuration & Data Paths

* **Database (SQLite):** %LocalAppData%\RadialLauncher\radial.db
* **Settings:** %LocalAppData%\RadialLauncher\settings.json
* **Custom Themes:** %LocalAppData%\RadialLauncher\CustomThemes\*.json
* **Plugins:** %LocalAppData%\RadialLauncher\Plugins\*.dll
* **Log Files:** %LocalAppData%\RadialLauncher\Logs\radial_launcher_*.log
* **Favicon Cache:** %LocalAppData%\RadialLauncher\FaviconCache\

---

## ?? Documentation & Guides

* [Plugin Development Guide](docs/PLUGINS.md)
* [Manual Windows QA Matrix](docs/MANUAL_QA.md)
* [Platform & Upgrade Notes](docs/UPGRADE_NOTES.md)
* [Changelog](CHANGELOG.md)

---

## ?? License
This project is open-source under the MIT License.
