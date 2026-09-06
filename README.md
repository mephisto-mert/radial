# 🚀 Radial Launcher

<div align="center">
  <img src="Resources/app.ico" width="128" height="128" alt="Radial Launcher Logo" />
  <br />
  <h3>High-Performance Hardware-Accelerated Radial Menu &amp; Windows Command Center</h3>
  <p>Ultra-fast circular application launcher, live window switcher, game library aggregator, macro automation, and system control dashboard for Windows 10 &amp; 11.</p>
</div>

---

## 🌟 Key Features

* **🪟 Live Window Switcher & DWM Previews:** Instant enumeration of all top-level desktop windows with real-time thumbnail previews and virtual desktop migration.
* **⚡ System & Media Actions:** Hardware-level volume control, media keys, Windows screenshot (`Win+Shift+S`), Pomodoro focus timer (25 min), lock screen, and system actions.
* **📂 Hierarchical Submenus & Layers:** Infinite-depth nested submenus with keyboard shortcut navigation (`Esc` / `Backspace`).
* **🎮 Steam & Epic Games Detection:** Automated background scanning of Steam libraries and Epic Games manifests with official game art extraction.
* **📋 Clipboard History Manager:** Low-latency thread-safe clipboard listener with 20-item rolling history, Unicode support, and 500KB text truncation memory guard.
* **🎨 8 Curated Visual Themes:** 8 meticulously tuned contrast-safe themes (`Dark`, `White`, `Red`, `Blue`, `Purple`, `Forest`, `AMOLED Black`, `High Contrast`) with WCAG-compliant automatic luminance contrast calculations.
* **🪞 Radial Transparency / Opacity Slider:** Real-time adjustable radial overlay background opacity (20% – 100%) while preserving 100% crisp foreground text and icon clarity.
* **🔄 Seamless Page & Category Navigation:** Drag middle-mouse horizontally (threshold ~35px) or scroll mouse wheel to smoothly cycle through pages and categories.
* **⚡ Contextual Quick Actions:** Hover over any item to reveal immediate context actions (Steam play/store, Epic launch, web URL open/copy, EXE run-as-admin/open directory, system actions).
* **⭐ Most Used (En Çok Kullanılanlar) Synchronization:** Smart recency and frequency-weighted ranking algorithm synchronized between the live radial menu and management settings.
* **💾 Local AppData Backup & Rotation:** Safe, atomic local JSON backups stored in `%LOCALAPPDATA%\RadialLauncher\Backups\` with automated 10-backup rotation and one-click restore.
* **🎯 Advanced Mouse & Keyboard Shortcuts:** Support for single keys, combinations (`Ctrl`, `Shift`, `Alt`, `Win`), and all mouse buttons (`MiddleClick`, `Mouse 4`/`XButton1`, `Mouse 5`/`XButton2`, `Ctrl+Mouse 4`, `Alt+RightClick`, etc.) with interactive assignment.
* **🧩 Modular Plugin Architecture:** Dynamic assembly plugin loading with isolated runtime exception boundaries.
* **🔍 Instant Search-as-you-type:** Type any character while the radial menu is open to dynamically filter items with fuzzy matching.

---

## 🎮 Controls & Shortcuts

| Action | Input / Shortcut |
| :--- | :--- |
| **Open Menu** | Middle Mouse Button (Wheel Click) / Mouse 4 / Mouse 5 / Ctrl+Space / Alt+Space / Custom |
| **Close Menu** | Move mouse outside radial area (>330px) / Escape / Click outside |
| **Launch Item** | Left Click / Enter |
| **Quick Actions** | Hover mouse on any item to view contextual action card |
| **Page / Category Navigation (Drag)** | Hold & Drag Middle Mouse Left / Right (>35px) |
| **Page / Category Navigation (Wheel)** | Scroll Mouse Wheel Up / Down |
| **Direct Page Select** | Click on Page Dot indicators under Category Pill |
| **Close Window Item** | Middle Click on Window item |
| **Move Window to Virtual Desktop** | Right Click on Window item |
| **Open Submenu** | Left Click on Submenu item |
| **Back to Parent Menu** | Escape / Backspace |
| **Change Category** | Click Category Pill / Number Keys (1-9) |
| **Instant Search** | Type directly on keyboard while overlay is visible |
| **Open Management & Settings** | Tray Icon Menu (or Ctrl+,) |

---

## 💾 Data & Backup Architecture

All user data, settings, and backups are stored in Windows Local AppData:
```text
%LOCALAPPDATA%\RadialLauncher\
├── launcher.db          # SQLite database containing items, categories, statistics
├── settings.json        # Active user configuration (theme, opacity, shortcuts)
├── settings.json.bak    # Atomic recovery mirror for fault-tolerant crash safety
├── Backups\             # Automated & manual local backups (up to 10 rotated copies)
│   ├── backup-20260906-045012.json
│   └── ...
├── Logs\                # Diagnostics and application event logs
└── Plugins\             # Extensibility plugins
```

---

## 📦 Installation & Packaging

### Portable Release
1. Download `RadialLauncher-1.0.0-win-x64.zip` from [Releases](https://github.com/mephisto-mert/radial/releases).
2. Extract to any folder (e.g. `C:\Program Files\RadialLauncher` or Desktop).
3. Run `RadialLauncher.exe`.

---

## 🛠️ Building from Source

### Prerequisites
* Windows 10 (1809+) or Windows 11
* [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) (or [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0))

### Build & Run
```bash
# Clone repository
git clone https://github.com/mephisto-mert/radial.git
cd radial

# Run tests
dotnet test RadialLauncher.Tests/RadialLauncher.Tests.csproj -c Release

# Publish standalone package
dotnet publish -c Release -r win-x64 --no-self-contained -o publish/RadialLauncher-1.0.0-win-x64
```

---

## 📄 License
Copyright © 2026 mephisto-mert. Licensed under the MIT License.
