using System;
using System.Collections.Generic;
using RadialLauncher.Models;
using RadialLauncher.Services.Themes;

namespace RadialLauncher.Services
{
    public static class ThemeManager
    {
        private static IThemeService Service => ThemeService.Instance;

        public static List<RadialLauncher.Models.Theme> GetAllThemes() => Service.GetAllThemes();
        public static RadialLauncher.Models.Theme GetTheme(string name) => Service.GetTheme(name);
        public static RadialLauncher.Models.Theme GetCurrentTheme() => Service.GetCurrentTheme();
        public static void SetCurrentTheme(string name) => Service.SetCurrentTheme(name);
        public static string GetActivationShortcut() => Service.GetActivationShortcut();
        public static void SetActivationShortcut(string shortcut) => Service.SetActivationShortcut(shortcut);

        public static event Action<RadialLauncher.Models.Theme>? OnThemeChanged
        {
            add => Service.OnThemeChanged += value;
            remove => Service.OnThemeChanged -= value;
        }

        public static event Action<string>? OnShortcutChanged
        {
            add => Service.OnShortcutChanged += value;
            remove => Service.OnShortcutChanged -= value;
        }
    }
}
