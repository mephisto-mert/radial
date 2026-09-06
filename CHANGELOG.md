# Changelog

All notable changes to Radial Launcher are documented in this file.

## [1.0.0] - 2026-09-06

### Final Release Polish & UX Enhancements
- **Centralized WCAG Luminance Contrast**: Universal contrast engine (`ThemeContrastHelper`) ensuring pristine readability across all 8 themes and custom themes, preventing white-on-white and dark-on-dark UI elements.
- **Sleek Minimal Scrollbar**: Redesigned modern WPF scrollbars with soft rounded thumbs, transparent track, and theme-adaptive hover styling.
- **Empty Radial Center Design**: Removed center close button in favor of an elegant, intentional empty radial core; ESC or outer click dismisses.
- **Intelligent Auto-Close**: Closes overlay smoothly when the mouse leaves interaction boundaries (>330px) with a 320ms grace timer.
- **Universal Page & Category Navigation**: Global middle-mouse drag swipe (>35px threshold) and scroll wheel support to transition fluidly between pages and categories.
- **Contextual Quick Actions Floating Panel**: Instant hover action card offering tailored actions (Steam Play/Store, Epic Launch, Web Open/Copy, Run as Admin, Open Containing Folder, System tools).
- **Premium Soft Motion System**: Upgraded easing engine (`SoftQuartic`, `SoftCubic`, `SubtleSpring`) for velvety page transitions and hover dynamics.
- **Modern Dark Glass Management & Dialogs**: Refreshed ManagementWindow, AddItemWindow, and EditItemWindow with clean tab structure, header contrast, and v1.0.0 branding.

## [1.0.0-rc1] - 2026-09-06

### Added
- **Radial Pie Menu Interface**: Ultra-smooth WPF hardware-accelerated radial menu with dynamic spring animations, magnetic cursor attraction, and DPI-aware multi-monitor clamping.
- **Submenu & Category Navigation**: Multi-tier submenus with fluid morphing center back button and keyboard shortcut navigation.
- **Window Switcher & Live DWM Previews**: Real-time thumbnail preview of active windows with one-click focus switching and virtual desktop migration.
- **Global Hotkey & Mouse Triggers**: Low-level Win32 hook supporting Middle Click, Ctrl/Shift/Alt + Right Click, Alt+Space, Ctrl+Space, and F4.
- **Clipboard History Manager**: Thread-safe background clipboard listener with 20-item rolling history, Unicode support, and large text memory guard.
- **Steam & Epic Game Library Detection**: Automatic registry and ACF/JSON manifest scanning to populate installed games.
- **Theme Engine & Custom Themes**: 8 built-in themes (Dark, Light, Midnight Blue, Purple Haze, Forest, Cyberpunk, Crimson Red, AMOLED Black), live preview, wallpaper accent extraction, and Windows dark mode synchronization.
- **Macro Automation System**: Sequential multi-step action runner with delay clamping, pre-cancellation checks, and graceful application exit hooks.
- **GitHub Gist Cloud Backup & Restore**: Encrypted DPAPI token vault for seamless cross-machine settings synchronization.
- **Update Check Architecture**: Non-blocking GitHub Releases checking with strict HTTPS verification.
- **Plugin Architecture**: Dynamic assembly loading with isolated fault boundaries and sample plugin implementation.
- **Diagnostics & Error Handling**: Serilog rolling file logger, one-click diagnostic report copying, and automated corrupted config backup recovery.
- **Packaging & Inno Setup**: Ready-to-use Windows Installer script and automated portable ZIP package generator with SHA256 checksums.

### Security & Hardening
- Eliminated all synthetic/fake GUID fallbacks in Virtual Desktop discovery.
- Fully isolated plugin runtime exceptions so rogue plugins cannot crash the UI.
- Hardened global mouse hook with thread-safe callbacks and error code logging.
- Sanitized custom theme and file export paths against directory traversal.
- Zero plaintext credential or PAT exposure across all logs and exception handlers.

### Known Limitations & Pending QA
- Hardware-specific multi-monitor and DPI scaling variations require manual QA across distinct graphics configurations (see [MANUAL_QA.md](docs/MANUAL_QA.md)).
