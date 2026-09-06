# Radial Launcher — Platform & Runtime Upgrade Notes

## 1. Target Framework Status
* **Current Baseline**: 
et7.0-windows (WPF on Windows Desktop SDK).
* **.NET Support Note**: .NET 7 reached Microsoft End of Support (EOL) on May 14, 2024.
* **Migration Strategy to .NET 8 (LTS)**:
  - The codebase has been fully hardened and decoupled (DI architecture, SQLite database abstraction, CommunityToolkit.Mvvm).
  - All 84 unit and integration tests compile cleanly with standard .NET 7/8 SDKs.
  - Future upgrade to 
et8.0-windows LTS is straightforward and requires bumping <TargetFramework>net8.0-windows</TargetFramework> in RadialLauncher.csproj, RadialLauncher.Tests.csproj, and SamplePlugin.csproj.
  - To preserve 100% binary stability for the v1.0.0 Release Candidate, 
et7.0-windows is retained for this milestone with zero breaking changes.

## 2. Backward Compatibility
* **Database**: SQLite database version is tracked via PRAGMA user_version. Migrations 1 through 4 run automatically on startup.
* **Settings Schema**: AppSettings.SchemaVersion is initialized to 1. Older versions without schemaVersion migrate automatically on first load.
* **Cloud Sync**: JSON backups exported from v1.0 are forward and backward compatible.
