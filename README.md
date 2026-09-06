# ⚡ Radial Launcher

<p align="center">
  <img src="docs/images/radial_overlay_main.png" alt="Radial Launcher Hero Screenshot" width="720"/>
</p>

<p align="center">
  <strong>Ultra-Fast, Futuristic Circular Application & Game Launcher for Windows 10 & 11</strong><br>
  <em>Summon your entire workspace, running applications, Steam/Epic games, and web tools in a fraction of a second with smooth radial gestures.</em>
</p>

<p align="center">
  <a href="https://github.com/mephisto-mert/radial/releases/latest"><img src="https://img.shields.io/github/v/release/mephisto-mert/radial?style=for-the-badge&color=6366F1&label=Release" alt="Release Version"></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?style=for-the-badge&logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-7.0%20%7C%20WPF-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 7">
  <img src="https://img.shields.io/badge/Languages-EN%20%7C%20TR%20(100%25%20Symmetric)-10B981?style=for-the-badge" alt="Languages">
  <img src="https://img.shields.io/badge/Privacy-100%25%20Offline%20%26%20Local-8B5CF6?style=for-the-badge" alt="Privacy">
  <img src="https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge" alt="License">
</p>

---

## 🌟 Overview

**Radial Launcher** is a modern, high-performance, keyboard-and-mouse driven circular application launcher built with WPF and .NET 7. Designed for power users, gamers, streamers, and developers, it replaces cluttered desktop shortcuts, crowded taskbars, and slow start menus with an intuitive, aesthetic circular ring HUD.

Summon it instantly anywhere on your screen using your **Mouse Middle Click**, **Mouse 4/5 (Side Buttons)**, or a custom hotkey like `Alt + Space`.

---

## ✨ Key Features

### 🚀 High-Speed Radial Menu HUD
- **60 FPS Hardware-Accelerated Animations**: Smooth spring easing and cinematic clockwise bloom effect.
- **Adaptive Ring Density**: Choose between **Expanded** (15 items per page) and **Compact** (18 items per page).
- **Customizable Opacity & Effects**: Adjust background transparency from 20% to 100% with optional background blur backdrop and motion reduction.
- **Smart Auto-Dismissal**: Menu smoothly closes when the cursor moves beyond the radial boundary.

### 🔍 Deep PC & Task Manager Auto-Discovery
- **Process & Running App Scanner**: Detects running applications and active windows directly from Windows Task Manager.
- **Steam & Epic Games Detection**: Automatically indexes installed game libraries with custom protocol launch support (`steam://rungameid/{id}`).
- **Smart Categorization**: Automatically organizes items into **Applications (📱)**, **Web & Internet (🌐)**, **Games (🎮)**, and **System Tools (⚡)**.
- **Full Icon Extraction**: Extracts high-resolution icons from `.exe`, `.lnk`, Steam manifests, URL favicons, and system actions.

### 🌐 Pure Dual-Language Architecture (English 🇬🇧 & Türkçe 🇹🇷)
- 100% symmetrically localized across all UI windows, radial overlays, dialogs, context menus, tooltips, and status messages.
- Instant, live language switching without requiring an application restart.

### 🎨 Dynamic High-Contrast Theme Engine
- **8 Curated Presets**: Dark, Light, White, Red, Blue, Purple, Forest, and AMOLED Black.
- **Perceived Luminance & Contrast Calculation**: Automatically ensures sharp text contrast across all dropdowns, buttons, and popups.
- **Live Preview HUD**: Preview your theme and colors in real time from the Settings window.

### 🎯 Contextual Quick Actions Micro-HUD
- Hovering any game, app, or tool reveals an instant center action HUD:
  - ▶ **Launch / Play**
  - ⚡ **Run as Administrator**
  - 📁 **Open File Location**
  - 🛒 **Steam Store / Community** (for games)
  - ⭐ **Toggle Favorite**

### 💾 Local Data Security & Portability
- **100% Offline & Private**: All data is stored locally in SQLite (`launcher.db`). Zero telemetry, zero external tracking.
- **Automated Rolling Backups**: Automatically maintains up to 10 rolling backups of your configuration and shortcuts.
- **JSON Import / Export**: Easily export and migrate your shortcuts and settings across machines.
- **Portable Mode**: Place a `data` directory next to `RadialLauncher.exe` to run in isolated portable mode (e.g., USB drive).

---

## 📸 Screenshots & Visual Walkthrough

### 1. Circular Ring Overlay & Radial Navigation
> Smooth circular ring layout with active item labels and rapid page switching.
<p align="center">
  <img src="docs/images/radial_overlay_main.png" alt="Radial Launcher Circular Overlay" width="680"/>
</p>

### 2. Centered Contextual Quick Actions Micro HUD
> Hover over any Steam game, executable, or tool to reveal instant actions.
<p align="center">
  <img src="docs/images/radial_overlay_hud.png" alt="Contextual Quick Actions Micro HUD" width="680"/>
</p>

### 3. Application & Shortcut Management (Tab 1)
> Search, filter by category, sort by usage or position, and trigger automated PC scanning.
<p align="center">
  <img src="docs/images/settings_tab1_applications.png" alt="Applications & Shortcuts Tab" width="680"/>
</p>

### 4. Curated Themes & Appearance (Tab 2)
> 8 curated color themes with real-time contrast checking and ring density controls.
<p align="center">
  <img src="docs/images/settings_tab2_themes.png" alt="Themes & Appearance Tab" width="680"/>
</p>

### 5. Activation Hotkeys & Windows Startup (Tab 3)
> Interactive shortcut picker for mouse and keyboard triggers with Windows tray auto-startup.
<p align="center">
  <img src="docs/images/settings_tab3_shortcuts.png" alt="Shortcuts & Startup Tab" width="680"/>
</p>

### 6. Local Backups & Data Portability (Tab 4)
> Automated rolling backups and JSON Import/Export for seamless data safety.
<p align="center">
  <img src="docs/images/settings_tab4_backups.png" alt="Backups & Data Tab" width="680"/>
</p>

### 7. Multi-Language Support & Diagnostics (Tab 5)
> Native English and Turkish language selection with system diagnostics and log viewers.
<p align="center">
  <img src="docs/images/settings_tab5_diagnostics.png" alt="Multi-Language & Diagnostics Tab" width="680"/>
</p>

---

## 🚀 Download & Installation

### Option 1: Standalone Windows Installer (Recommended)
1. Download **`RadialLauncher-Setup-v1.0.0.exe`** from [GitHub Releases](https://github.com/mephisto-mert/radial/releases/latest).
2. Run the installer, choose your language (English / Türkçe), and click **Install**.
3. Radial Launcher will automatically create Desktop and Start Menu shortcuts and start in the system tray.

### Option 2: Portable ZIP
1. Download **`RadialLauncher-1.0.0-win-x64.zip`** from [GitHub Releases](https://github.com/mephisto-mert/radial/releases/latest).
2. Extract the archive to any folder on your computer or USB drive.
3. Launch `RadialLauncher.exe`.

---

## 🎮 Default Controls & Shortcuts

| Action | Default Shortcut | Description |
| :--- | :--- | :--- |
| **Summon / Dismiss Menu** | `Middle Click` / `XButton1` / `Alt + Space` | Opens the radial menu at the current cursor position. |
| **Launch Item** | `Left Click` on Item | Launches the selected application, game, file, or website. |
| **Context Actions** | Hover over Item | Reveals centered quick actions (Play, Run as Admin, Open Folder). |
| **Switch Category / Page** | `Scroll Wheel` or `Category Tabs` | Cycles through categories or circular pages. |
| **Open Management Settings** | Right-Click Tray Icon ➔ `Settings` | Opens the comprehensive 5-tab configuration center. |
| **Exit Radial Launcher** | Right-Click Tray Icon ➔ `Exit` | Closes the application completely. |

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11 x64
- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) or higher
- Visual Studio 2022 (v17.4+) with *.NET Desktop Development* workload (or VS Code / CLI)

### Build Commands

```powershell
# 1. Clone the repository
git clone https://github.com/mephisto-mert/radial.git
cd radial

# 2. Restore dependencies
dotnet restore

# 3. Run all unit & integration tests
dotnet test -c Release

# 4. Build and run locally
dotnet run --project RadialLauncher.csproj -c Release

# 5. Build full release package and standalone installer
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

---

## 🛡️ Privacy & Security

- **Zero Cloud Dependency**: Radial Launcher operates 100% offline. No telemetry, tracking, or remote data collection.
- **Local Storage**: User data is stored locally in `%LOCALAPPDATA%\RadialLauncher\` (or `./data/` in portable mode).
- **Secure Process Execution**: Applications and games are executed through standard Windows Win32 APIs without elevated escalation unless explicitly requested via "Run as Administrator".

---

## 📄 License

Radial Launcher is open-source software licensed under the **[MIT License](LICENSE)**.
Feel free to use, modify, and distribute it in accordance with the license terms.
