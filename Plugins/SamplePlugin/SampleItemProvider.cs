using System.Collections.Generic;
using RadialLauncher.Models;
using RadialLauncher.Services.Plugins;

namespace SamplePlugin
{
    public class SampleItemProvider : IRadialItemProvider
    {
        public string ProviderName => "Sample Plugin Provider";
        public string CategoryName => "🧩 Eklentiler";
        public string CategoryColor => "#9b59b6";

        public IEnumerable<LauncherItem> GetItems()
        {
            return new List<LauncherItem>
            {
                new LauncherItem
                {
                    Id = -501,
                    Name = "Google Çeviri",
                    Type = "URL",
                    Target = "https://translate.google.com",
                    Position = 0
                },
                new LauncherItem
                {
                    Id = -502,
                    Name = "Hesap Makinesi",
                    Type = "EXE",
                    Target = "calc.exe",
                    Position = 1
                }
            };
        }
    }
}
