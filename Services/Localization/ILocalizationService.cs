using System;
using System.Collections.Generic;

namespace RadialLauncher.Services.Localization
{
    public record LanguageOption(string Code, string DisplayName, string NativeName, string Flag);

    public interface ILocalizationService
    {
        string CurrentLanguage { get; }
        IReadOnlyList<LanguageOption> SupportedLanguages { get; }
        event Action? OnLanguageChanged;
        void SetLanguage(string languageCode);
        string GetString(string key, string? fallback = null);
        string this[string key] { get; }
    }
}
