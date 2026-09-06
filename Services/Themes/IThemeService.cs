using System;
using System.Collections.Generic;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Themes
{
    public interface IThemeService
    {
        List<Theme> GetAllThemes();
        Theme GetTheme(string name);
        Theme GetCurrentTheme();
        void SetCurrentTheme(string name);
        void SaveCustomTheme(Theme theme);
        void DeleteCustomTheme(string name);
        string GetActivationShortcut();
        void SetActivationShortcut(string shortcut);
        bool GetFollowWindowsTheme();
        void SetFollowWindowsTheme(bool follow);
        bool GetExtractAccentFromWallpaper();
        void SetExtractAccentFromWallpaper(bool extract);
        double GetRadialOpacity();
        void SetRadialOpacity(double opacity);
        bool GetAutoCheckUpdates();
        void SetAutoCheckUpdates(bool autoCheck);
        void ResetSettingsToDefault();
        ThemeService.AppSettings GetSettings();
        void UpdateSettings(ThemeService.AppSettings settings);

        event Action<Theme>? OnThemeChanged;
        event Action<string>? OnShortcutChanged;
    }
}
