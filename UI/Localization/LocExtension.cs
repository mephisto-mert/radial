using System;
using System.Windows.Markup;
using RadialLauncher.Services.Localization;

namespace RadialLauncher.UI.Localization
{
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        public string? Fallback { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;
            return LocalizationService.Instance.GetString(Key, Fallback ?? Key);
        }
    }
}
