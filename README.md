# ⚡ Radial Launcher

<p align="center">
  <img src="docs/images/radial_overlay_main.png" alt="Radial Launcher Hero Screenshot" width="720"/>
</p>

<p align="center">
  <strong>Ultra-Fast, Futuristic Circular Application & Game Launcher for Windows 10 & 11</strong><br>
  <em>Summon your entire workspace, favorite games, system controls, and websites in a fraction of a second with smooth radial gestures.</em>
</p>

<p align="center">
  <a href="https://github.com/mephisto-mert/radial/releases/latest"><img src="https://img.shields.io/github/v/release/mephisto-mert/radial?style=for-the-badge&color=6366F1&label=Release" alt="Release Version"></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?style=for-the-badge&logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-7.0%20%7C%20WPF-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 7">
  <img src="https://img.shields.io/badge/Languages-10%2B%20Supported-10B981?style=for-the-badge" alt="Languages">
  <img src="https://img.shields.io/badge/Privacy-100%25%20Offline%20%26%20Local-8B5CF6?style=for-the-badge" alt="Privacy">
  <img src="https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge" alt="License">
</p>

---

## 🌟 Overview

**Radial Launcher** is a high-performance, keyboard-and-mouse driven radial launcher built with WPF and .NET 7. Designed for power users, gamers, and developers, it replaces cluttered desktop shortcuts and cumbersome taskbars with an intuitive, aesthetic circular ring HUD.

Summon it instantly anywhere on your screen using your **Mouse Middle Click**, **Side Buttons (XButton1/2)**, or a custom hotkey like `Alt + Space`.

---

## 📸 Screenshots & Visual Walkthrough

### 1. Circular Ring Overlay & Cinematic Clockwise Bloom
> Smooth sequential clockwise cascade animation when switching categories or pages.
<p align="center">
  <img src="docs/images/radial_overlay_main.png" alt="Radial Launcher Circular Overlay" width="680"/>
</p>

### 2. Centered Contextual Quick Actions Micro HUD
> Hover over any Steam game, executable, or tool to reveal instant actions (▶ Play, 🛒 Store, 👥 Community, 📁 Location, ⚡ Run as Admin) without overlapping radial bubbles.
<p align="center">
  <img src="docs/images/radial_overlay_hud.png" alt="Contextual Quick Actions Micro HUD" width="680"/>
</p>

### 3. Application & Shortcut Management (Tab 1)
> Search, filter by category, sort by usage or position, and trigger automated full-PC scanning.
<p align="center">
  <img src="docs/images/settings_tab1_applications.png" alt="Applications & Shortcuts Tab" width="680"/>
</p>

### 4. Curated Themes & Appearance (Tab 2)
> 8 curated color themes (Dark, White, Red, Blue, Purple, Forest, AMOLED Black, High Contrast) with real-time contrast checking and ring density controls.
<p align="center">
  <img src="docs/images/settings_tab2_themes.png" alt="Themes & Appearance Tab" width="680"/>
</p>

### 5. Activation Hotkeys & Windows Startup (Tab 3)
> Interactive shortcut picker for mouse and keyboard triggers with auto-closing perimeter boundaries.
<p align="center">
  <img src="docs/images/settings_tab3_shortcuts.png" alt="Shortcuts & Startup Tab" width="680"/>
</p>

### 6. Local Backups & Data Portability (Tab 4)
> Automated rolling backups (keeps last 10 backups) and JSON Import/Export for seamless multi-device migration.
<p align="center">
  <img src="docs/images/settings_tab4_backups.png" alt="Backups & Data Tab" width="680"/>
</p>

### 7. Multi-Language Support & Diagnostics (Tab 5)
> 10+ natively translated languages sorted alphabetically with one-click GitHub update checks.
<p align="center">
  <img src="docs/images/settings_tab5_diagnostics.png" alt="Multi-Language & Diagnostics Tab" width="680"/>
</p>

---

## ✨ Key Features

| Feature | Description |
| :--- | :--- |
| 🏎️ **Ultra-Fast Radial HUD** | 60 FPS hardware-accelerated animations with spring easing and 3x cinematic clockwise bloom. |
| 🎮 **Auto Game Detection** | Automatically detects Steam & Epic Games libraries with custom protocol execution. |
| 🔍 **Deep PC Scanner** | Automatically discovers installed software, desktop shortcuts, and Windows Store apps. |
| ⚡ **Contextual Actions** | Quick action micro HUD for games and apps (Run as Administrator, Open File Location, Steam Store). |
| 🌐 **10+ Languages** | Deutsch, English (Primary), Español, Français, Italiano, Japanese, Korean, Polish, Portuguese (BR), Russian, Turkish. |
| 🎨 **Theme Engine** | 8+ dark/light palettes, wallpaper accent color extraction, and dynamic ring opacity slider. |
| 🛡️ **Zero Telemetry & 100% Local** | All database and configuration files are stored safely in `%LOCALAPPDATA%\RadialLauncher`. No cloud tracking. |
| 📦 **Clean Standalone Installer** | Includes both a modern dark GUI setup wizard (`RadialLauncher-Setup-v1.0.0.exe`) and portable zip. |

---

## 🚀 Download & Installation

### Option 1: Standalone Windows Installer (Recommended)
1. Download **`RadialLauncher-Setup-v1.0.0.exe`** from [GitHub Releases](https://github.com/mephisto-mert/radial/releases/latest).
2. Run the installer, select your installation folder (Default: `%LOCALAPPDATA%\Programs\RadialLauncher`), and click **Install**.
3. Radial Launcher will automatically create desktop & start menu shortcuts and launch in the system tray.

### Option 2: Portable ZIP
1. Download **`RadialLauncher-1.0.0-win-x64.zip`**.
2. Extract to any directory and run `RadialLauncher.exe`.

---

## 🎮 Default Controls & Navigation

- **Summon / Dismiss Overlay**: `Mouse Middle Click` or `Alt + Space`
- **Switch Categories / Pages**: Middle Click & Drag Left/Right or use `Mouse Scroll Wheel`
- **Select / Launch Item**: `Left Click` on any radial icon
- **Contextual Quick Actions**: Hover mouse over any item to reveal centered quick actions
- **Auto-Dismiss**: Moving the mouse 330px away from the menu automatically hides the radial overlay
- **Open Settings**: Click the center gear icon or right-click the system tray icon

---

## 🛠️ Technology Stack & Architecture

- **Framework**: .NET 7.0 (Windows WPF x64)
- **Database**: SQLite with Dapper ORM & WAL Mode (`Microsoft.Data.Sqlite`, `Dapper`)
- **MVVM Pattern**: CommunityToolkit.Mvvm
- **Tray & Notifications**: Hardcodet.NotifyIcon.Wpf
- **Logging**: Serilog file sink with rolling audit logs
- **Packaging**: Standalone WPF Setup Wizard + Inno Setup Script

---

## 🏷️ Keywords & Tags

`radial-launcher` `app-launcher` `game-launcher` `radial-menu` `pie-menu` `windows11` `windows10` `wpf` `csharp` `dotnet` `productivity` `hotkey-launcher` `fluent-design` `quick-access` `open-source` `steam-launcher`

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
