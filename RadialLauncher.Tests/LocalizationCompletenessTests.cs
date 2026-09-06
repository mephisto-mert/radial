using System;
using System.Collections.Generic;
using RadialLauncher.Services.Localization;
using Xunit;

namespace RadialLauncher.Tests
{
    public class LocalizationCompletenessTests
    {
        private static readonly string[] RequiredKeys = new[]
        {
            "App_Title", "Nav_Apps", "Nav_Themes", "Nav_Shortcuts", "Nav_Backups", "Nav_System",
            "Tab1_Title", "Tab1_Sub", "Scan_PC", "Add_Item", "Category", "All_Categories",
            "Tab2_Title", "Themes_Header", "Preview_Header", "Opacity_Title",
            "Tab3_Title", "Trigger_Title", "Assign_Shortcut", "Startup_Title",
            "Tab4_Title", "LocalBackup_Title", "Backup_Now", "Restore_Backup",
            "Tab5_Title", "Language", "Updates_Title", "Installed_Version",
            "Cat_Rename_Dialog_Title", "Cat_Rename_Header", "Save", "Cancel",
            "AddItem_Title", "EditItem_Title", "Item_Name", "Item_Type", "Item_Target",
            "ShortcutAssign_Title", "ShortcutAssign_Header", "Detected_Shortcut",
            "Macro_Title", "Macro_Header", "TrayOpenMenu", "TraySettings", "TrayExit",
            "TutorialHeader", "TutorialDismiss"
        };

        [Fact]
        public void All11Languages_HaveAllRequiredKeys()
        {
            var service = new LocalizationService();

            foreach (var lang in service.SupportedLanguages)
            {
                service.SetLanguage(lang.Code);
                Assert.Equal(lang.Code, service.CurrentLanguage);

                foreach (var key in RequiredKeys)
                {
                    string value = service.GetString(key);
                    Assert.False(string.IsNullOrWhiteSpace(value), $"Missing or empty key '{key}' for language '{lang.Code}' ({lang.DisplayName})");
                }
            }

            // Restore English
            service.SetLanguage("en");
        }
    }
}
