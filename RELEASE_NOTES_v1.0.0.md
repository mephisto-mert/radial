# ⚡ Radial Launcher v1.0.0 — Official Production Release

**Radial Launcher** is a modern, ultra-fast circular application, game, and shortcut launcher for Windows 10 & 11. Designed with hardware-accelerated WPF graphics, fluid 60 FPS animations, and smart background scanners, Radial Launcher transforms how you access your desktop applications.

---

## 🌟 What's New in v1.0.0 (Son Güncellemeler & Yenilikler)

### 🇬🇧 🇹🇷 Clean 2-Language Localization (Sadece 2 Dil Desteği)
- Streamlined localization strictly to **English** and **Türkçe**.
- 100% of all UI screens, management dashboards, dialogs, system tray menus, tooltips, and toasts are dynamically translated.
- Zero leftover untranslated strings across all views.

### 🔗 Smart URL & Website Link Support (Web Siteleri ve Bağlantı Desteği)
- Add websites directly to your radial menu or categories (e.g. `google.com`, `youtube.com`, `github.com`).
- Automatic URL protocol detection and `https://` normalization.
- Opens instantly in your system's default browser with full icon extraction.

### 🎮 Task Manager & Most Used App Auto-Discovery (Görev Yöneticisi ve Uygulama Algılama)
- Automatic scanner detects actively running applications from Task Manager and Windows process tree.
- Comprehensive Steam game library (`steamapps/libraryfolders.vdf`, ACF manifests) & Epic Games auto-detection.
- Windows Desktop shortcuts, Start Menu apps, and Control Panel items detected seamlessly.

### 🎨 High-Contrast Adaptive Theme Engine (Gelişmiş Tema ve Kontrast Sistemi)
- 8 Curated themes: **Dark**, **Light**, **Midnight Blue**, **Cyberpunk**, **Forest**, **Crimson**, **Obsidian**, and **Wallpaper Adaptive**.
- Fixed ComboBox and dropdown contrast across all light and dark themes to guarantee perfect text readability.

### 📦 Standalone Installer & Portable Mode (Kurulum Sihirbazı & Taşınabilir Mod)
- **Standalone Setup Wizard (`RadialLauncher-Setup-v1.0.0.exe`)**: Single-file dark installer with custom install directory, desktop shortcut, startup toggle, and progress bar.
- **Zero-Dependency Portable Mode**: Place `portable.mode` in the application directory to store all databases and settings locally without touching `%LOCALAPPDATA%`.

### ⚡ Performance & Core Architecture (Performans ve Güvenlik)
- Native 60 FPS circular HUD with 3x cinematic bloom cascade animation.
- Instant summon via **Middle Mouse Click** or **`Alt + Space`** (fully customizable hotkeys).
- Contextual quick action micro-HUD (Launch, Run as Admin, Open Location, Steam Store/Community).
- 100% offline & private: Zero telemetry, local SQLite WAL mode database with automated rolling backups.

---

## 📥 Downloads & Assets (İndirme Dosyaları)

| File | Type | Description |
| :--- | :--- | :--- |
| **`RadialLauncher-Setup-v1.0.0.exe`** | Setup Wizard | Standalone single-file Windows installer (Recommended) |
| **`RadialLauncher-1.0.0-win-x64.zip`** | Portable Package | Zero-install zip archive for USB drives & portable setups |
| **`SHA256SUMS.txt`** | Checksums | Cryptographic SHA-256 verification hashes |

---

## 🔒 Verification & SHA-256 Checksums

```
5F3008C893C650E2BA414C8BC235C6D335AABC8C6504F9F5465BBAEAEE7FBEB1  RadialLauncher-1.0.0-win-x64.zip
3ADF753B24F788D99CF38AC0E0BB5B22BD374620B264E34D46FFD4F23E7EAA6B  RadialLauncher-Setup-v1.0.0.exe
```

Verify in PowerShell:
```powershell
Get-FileHash -Algorithm SHA256 .\RadialLauncher-Setup-v1.0.0.exe
```

---

## 💻 System Requirements
- **OS**: Windows 10 (1903+) or Windows 11 (64-bit)
- **Runtime**: [.NET 7.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/7.0) (x64)
- **Display**: 1080p or higher recommended (DirectX 9+ compatible GPU)
