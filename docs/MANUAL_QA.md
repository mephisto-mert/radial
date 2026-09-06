# Radial Launcher — Manual Windows QA Checklist

This document contains manual QA verification procedures for hardware, OS-level hooks, multi-monitor setups, and Windows desktop integration scenarios that cannot be fully simulated via unit tests.

---

## 1. Installation & Deployment

- [ ] **Fresh Installation**: Run RadialLauncher-1.0.0-Setup.exe on a clean Windows system.
- [ ] **Desktop Shortcut**: Verify desktop icon launches the app.
- [ ] **Start Menu Shortcut**: Verify start menu entry exists and has high-res icon.
- [ ] **Portable Archive**: Extract RadialLauncher-1.0.0-win-x64.zip to a standalone folder and run RadialLauncher.exe.
- [ ] **Upgrade Installation**: Install over an existing installation; verify user settings and SQLite database in %LocalAppData%\RadialLauncher are preserved.
- [ ] **Uninstallation**: Uninstall via Windows Settings; verify binaries in Program Files are removed while leaving user config intact.

---

## 2. Startup & Background Service Lifecycle

- [ ] **Cold Startup**: App launches, creates tray icon, and registers global hotkey within ~300ms without blocking.
- [ ] **Run on Startup**: Toggle 'Windows açýldýðýnda baþlat' checkbox; reboot or check HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
- [ ] **Tray Menu**: Right-click tray icon; verify 'Menüyü Aç', 'Ayarlar & Yönetim', and 'Çýkýþ'.
- [ ] **Clean Exit**: Choose 'Çýkýþ'; verify hotkeys, hooks, and clipboard listeners unregister cleanly and process exits (code 0).

---

## 3. Radial Menu Display & Interaction

- [ ] **Middle Mouse Button Trigger**: Click mouse wheel anywhere on desktop; radial menu opens smoothly centered at cursor.
- [ ] **Screen Boundary Clamping**: Click near top-left, bottom-right, or screen edge; verify circle is clamped fully on-screen.
- [ ] **Multi-Monitor Display**: Move mouse to secondary/tertiary monitor and trigger; menu appears on correct monitor without DPI distortion.
- [ ] **DPI Scaling**: Test at 100%, 125%, 150%, and 200% Windows scaling.
- [ ] **Keyboard Navigation**: Use Arrow keys, Tab, Enter, Backspace, and Escape.
- [ ] **Instant Search**: Type any text while radial menu is active; fuzzy search filters items dynamically.
- [ ] **Submenu Morphing**: Click a category item with sub-items; center button smoothly morphs from '?' to '‹'.

---

## 4. Window Switcher & DWM Previews

- [ ] **Open Windows Enumeration**: Switch to 'Açýk Pencereler' category; verify active top-level windows appear.
- [ ] **Live DWM Thumbnail Preview**: Hover over a window item; verify thumbnail preview renders without flickering.
- [ ] **Rapid Hover Stress**: Hover back and forth between 10 window items quickly; verify no GDI/DWM handle leak in Task Manager.
- [ ] **Window Activation**: Click a window item; verify target window is brought to foreground.
- [ ] **Move to Virtual Desktop**: Right-click a window item; select 'Masaüstü 2''ye Taþý'.

---

## 5. System Actions & Focus Timer

- [ ] **Pomodoro Focus Timer**: Launch '?? Odaklan (25dk)'; verify countdown in tray tooltip.
- [ ] **Timer Completion**: After 25 minutes (or manual test tick), verify balloon notification tip fires.
- [ ] **Volume / Media**: Trigger Mute, Volume Up, Volume Down actions.
- [ ] **Screenshot**: Trigger Screenshot action; verify Windows Snip opens.

---

## 6. Clipboard Manager

- [ ] **Clipboard Copying**: Copy text in Notepad/browser; verify it appears in 'Panodakiler' category.
- [ ] **20-Item Cap**: Copy 25 distinct items; verify history maintains rolling cap of 20 items.
- [ ] **Unicode & Turkish Characters**: Copy text with Turkish characters (ðüþiöçÐÜÞÝÖÇ) and emojis (????).
- [ ] **Large Text Guard**: Copy 5MB text block; verify launcher truncates cleanly without freezing.

---

## 7. Cloud Sync & Settings

- [ ] **Theme Customization**: Create a custom theme in Theme Engine; verify live preview and radial dial update immediately.
- [ ] **Windows Theme Follow**: Toggle dark/light mode in Windows Settings; launcher theme reacts instantly.
- [ ] **GitHub Gist Backup**: Enter PAT, click 'Gist\'e Yükle'; verify gist is created on GitHub.
- [ ] **GitHub Gist Restore**: Click 'Gist\'ten Geri Yükle'; verify settings and items are restored.
- [ ] **Corrupted Config Recovery**: Corrupt settings.json with random bytes; launch app; verify .bak backup is created and defaults load cleanly.
