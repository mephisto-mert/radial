using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RadialLauncher.Models;
using RadialLauncher.Services.Data;
using Serilog;

namespace RadialLauncher.Services.Localization
{
    public class LocalizationService : ILocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event Action? OnLanguageChanged;

        private string _currentLanguage = "en";
        public string CurrentLanguage => _currentLanguage;

        public string this[string key] => GetString(key);

        private static string SettingsPath => UserDataPathProvider.Instance.GetSettingsPath();

        public IReadOnlyList<LanguageOption> SupportedLanguages { get; } = new List<LanguageOption>
        {
            new LanguageOption("en", "English", "English", "🇬🇧"),
            new LanguageOption("tr", "Türkçe", "Türkçe", "🇹🇷")
        };

        private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

        public LocalizationService()
        {
            InitializeTranslations();
            LoadLanguagePreference();
        }

        public string GetString(string key, string? fallback = null)
        {
            if (_translations.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var val))
            {
                return val;
            }

            if (_translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
            {
                return enVal;
            }

            return fallback ?? key;
        }

        public bool HasKeyDirectly(string langCode, string key)
        {
            return _translations.TryGetValue(langCode, out var dict) && dict.ContainsKey(key);
        }

        public IReadOnlyDictionary<string, string>? GetDictionaryForLanguage(string langCode)
        {
            return _translations.TryGetValue(langCode, out var dict) ? dict : null;
        }

        public string GetCategoryDisplayName(Category? category)
        {
            if (category == null) return string.Empty;
            return GetCategoryDisplayName(category.Name, category.SystemKey);
        }

        public string GetCategoryDisplayName(string rawName, string? systemKey = null)
        {
            if (!string.IsNullOrEmpty(systemKey))
            {
                return GetString(systemKey, rawName);
            }
            return rawName;
        }

        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return;
            var match = SupportedLanguages.FirstOrDefault(l => l.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
            if (match == null) match = SupportedLanguages.FirstOrDefault(l => l.Code == "en") ?? SupportedLanguages.First();

            _currentLanguage = match.Code;
            SaveLanguagePreference(_currentLanguage);
            Log.Information("Language set to: {Lang}", _currentLanguage);
            if (OnLanguageChanged != null)
            {
                foreach (Action handler in OnLanguageChanged.GetInvocationList())
                {
                    try
                    {
                        handler();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error notifying language changed listener");
                    }
                }
            }
        }

        private void LoadLanguagePreference()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var langProp))
                    {
                        string? lang = langProp.GetString();
                        if (!string.IsNullOrWhiteSpace(lang))
                        {
                            var match = SupportedLanguages.FirstOrDefault(l => l.Code.Equals(lang, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                _currentLanguage = match.Code;
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed loading language preference from settings.json, defaulting to en");
            }
            _currentLanguage = "en";
        }

        private void SaveLanguagePreference(string langCode)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                Dictionary<string, object> dict = new();
                if (File.Exists(SettingsPath))
                {
                    try
                    {
                        string existingJson = File.ReadAllText(SettingsPath);
                        dict = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? new();
                    }
                    catch { }
                }
                dict["Language"] = langCode;
                string newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, newJson);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed saving language preference to settings.json");
            }
        }

        private void InitializeTranslations()
        {
            var dict_en = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Access_Sub"] = "Reduce motion and simplify animations for low-spec systems.",
                ["Access_Title"] = "Accessibility & Performance",
                ["Action_Delete"] = "Delete",
                ["Action_Edit"] = "Edit",
                ["Action_Favorite"] = "Favorite",
                ["Active_Shortcut_Label"] = "Active Shortcut:",
                ["AddItem_Title"] = "Add New Item",
                ["Add_Item"] = "➕ Add New Item",
                ["All_Categories"] = "All Categories",
                ["App_Title"] = "Radial Launcher — Settings & Management",
                ["Assign_Shortcut"] = "🎯 Assign Custom Shortcut",
                ["AutoCheck_Disabled"] = "Automatic update check disabled.",
                ["AutoCheck_Enabled"] = "Automatic update check enabled.",
                ["AutoCheck_Updates"] = "Automatically check for updates on startup",
                ["Background"] = "Background",
                ["Backup_Now"] = "💾 Create Local Backup",
                ["Backup_Status_Count"] = "Total {0} local backups available. Latest: {1} ({2})",
                ["Backup_Status_None"] = "No local backups created yet.",
                ["Behavior_Body"] = "• Auto-Close: Menu closes automatically when cursor moves 330px away.\\n• Global Navigation: Drag with middle-mouse or use scroll wheel to switch pages/categories.\\n• Quick Actions: Hovering items reveals instant actions at the center.",
                ["Behavior_Title"] = "🎯 Menu Behavior & Navigation Guidelines",
                ["Browse"] = "Browse...",
                ["Browse_App_Title"] = "Select Application or File",
                ["Browse_Icon"] = "Select Icon",
                ["Browse_Icon_Title"] = "Select Icon File",
                ["Cancel"] = "Cancel",
                ["Cat_ClipboardHistory"] = "📋 Clipboard History",
                ["Cat_Err_Duplicate"] = "A category with this name already exists.",
                ["Cat_Err_Empty"] = "Category name cannot be empty.",
                ["Cat_Err_TooLong"] = "Category name cannot exceed 50 characters.",
                ["Cat_Games"] = "🎮 Games",
                ["Cat_MostUsed"] = "⭐ Most Used",
                ["Cat_Name_Label"] = "Category Name:",
                ["Cat_OpenWindows"] = "🪟 Open Windows",
                ["Cat_Rename_Dialog_Title"] = "Rename Category — Radial Launcher",
                ["Cat_Rename_Failed"] = "Failed to rename category.",
                ["Cat_Rename_Header"] = "🏷️ Rename Category",
                ["Cat_Rename_Sub"] = "Enter a new display name for the selected category.",
                ["Cat_Renamed_Status"] = "Category renamed: {0}",
                ["Cat_Select_To_Rename"] = "Please select a specific category from the dropdown to rename.",
                ["Cat_System"] = "⚡ System",
                ["Category"] = "Category:",
                ["Check_Updates"] = "🔄 Check for Updates Now",
                ["Checking_Release"] = "Checking GitHub Releases...",
                ["Checking_Updates"] = "Checking for updates...",
                ["Cmd_Logs"] = "📂 Open Logs (/logs)",
                ["Cmd_Restart"] = "🔄 Restart App (/restart)",
                ["Cmd_Settings"] = "⚙️ Open Settings (/settings)",
                ["Col_Actions"] = "Actions",
                ["Col_Category"] = "Category",
                ["Col_Icon"] = "Icon",
                ["Col_Launches"] = "Launches",
                ["Col_Name"] = "Name",
                ["Col_Order"] = "Order",
                ["Col_Star"] = "⭐",
                ["Col_Target"] = "Target",
                ["Col_Type"] = "Type",
                ["Community"] = "Community",
                ["Copy"] = "Copy",
                ["Copy_Diag"] = "📋 Copy Diagnostics",
                ["Delete_Confirm"] = "Delete '{0}'?",
                ["Delete_Confirm_Title"] = "Delete Confirmation",
                ["Density_Compact"] = "Compact (18 Items)",
                ["Density_Desc"] = "Number of items displayed per circular ring page.",
                ["Density_Expanded"] = "Expanded (15 Items)",
                ["Density_Title"] = "Ring Density Mode",
                ["Desktop_N"] = "Desktop {0}",
                ["Desktop_Name_Format"] = "Desktop {0}",
                ["Desktops_Unavailable"] = "⚠️ Virtual Desktops Unavailable",
                ["Detected_Shortcut"] = "Detected Shortcut:",
                ["Diag_Copied_Msg"] = "System diagnostic information copied to clipboard.",
                ["Diag_Copied_Status"] = "Diagnostic information copied to clipboard!",
                ["Diag_Sub"] = "Application logs and system diagnostic summary.",
                ["Diag_Title"] = "📊 System Diagnostics & Logs",
                ["Diag_Title_Short"] = "Diagnostics",
                ["EditItem_Title"] = "Edit Item",
                ["Edit_Macro"] = "⚡ Edit Macro Steps...",
                ["Err_Launch_Failed"] = "Could not launch '{0}'. Please check the target path.",
                ["Error"] = "Error",
                ["Export"] = "📤 Export (JSON)",
                ["FileFilter_AllSupported"] = "All Supported (*.exe, *.lnk, *.*)|*.exe;*.bat;*.cmd;*.lnk;*.*|Applications (*.exe)|*.exe|All Files (*.*)|*.*",
                ["FileFilter_Icons"] = "Icons & Images (*.ico;*.exe;*.png;*.jpg)|*.ico;*.exe;*.png;*.jpg|All Files (*.*)|*.*",
                ["FileFilter_Json"] = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                ["Icon_Bubble"] = "Icon Bubble",
                ["Import"] = "📥 Import (JSON)",
                ["Installed_Version"] = "Installed Version: v1.0.0 (Final Release)",
                ["Installer_Btn_Browse"] = "Browse...",
                ["Installer_Btn_Cancel"] = "Cancel",
                ["Installer_Btn_Finish"] = "Finish",
                ["Installer_Btn_Install"] = "Install Now",
                ["Installer_Chk_Desktop"] = "Create Desktop Shortcut",
                ["Installer_Chk_Launch"] = "Launch Radial Launcher when setup completes",
                ["Installer_Chk_StartMenu"] = "Create Start Menu Shortcut",
                ["Installer_Chk_Startup"] = "Run automatically on Windows startup (Tray Mode)",
                ["Installer_Clean_Guarantee"] = "Clean Standalone Installation: Your settings and shortcuts will be safely stored in %LOCALAPPDATA%\\RadialLauncher",
                ["Installer_Header_Sub"] = "Professional Radial Application & Game Launcher for Windows",
                ["Installer_Install_Dir"] = "Installation Directory:",
                ["Installer_Options"] = "Installation Options:",
                ["Installer_Requirements"] = "Requirements: Windows 10/11 x64",
                ["Installer_Status_Complete"] = "Installation completed successfully!",
                ["Installer_Status_Configuring"] = "Configuring system shortcuts and registry...",
                ["Installer_Status_Extracting"] = "Extracting files: {0}",
                ["Installer_Status_Starting"] = "Starting installation...",
                ["Installer_Title"] = "Radial Launcher v1.0.0 — Setup Wizard",
                ["Installer_Uninstall_Confirm"] = "Are you sure you want to completely uninstall Radial Launcher from your system?",
                ["Installer_Uninstall_Done"] = "Radial Launcher has been successfully uninstalled from your computer.",
                ["Installer_Uninstall_RemoveData"] = "Would you also like to delete your personal settings, categories, and database?",
                ["Internal_Code"] = "Internal Code:",
                ["Item_Args"] = "Arguments:",
                ["Item_Args_Tooltip"] = "Optional command line arguments",
                ["Item_Category"] = "Category:",
                ["Item_Favorite_Add"] = "Add to Favorites (Inner Ring ⭐)",
                ["Item_Favorite_Edit"] = "Mark as Favorite (⭐)",
                ["Item_Icon"] = "Custom Icon:",
                ["Item_Icon_Tooltip"] = "Optional custom .ico, .exe, or .png file path",
                ["Item_Name"] = "Name:",
                ["Item_Target"] = "Target:",
                ["Item_Target_Tooltip"] = "Executable path, website URL, file or folder path",
                ["Item_Type"] = "Type:",
                ["JsonBackup_Sub"] = "Export or import your complete configuration and shortcuts as a JSON file.",
                ["JsonBackup_Title"] = "📤 JSON Export / Import",
                ["Language"] = "🌐 Display Language",
                ["Language_Changed_Status"] = "Language changed: {0}",
                ["Language_Desc"] = "Select language for application UI and radial menu (Default: English). Sorted alphabetically.",
                ["Launch"] = "Launch",
                ["Library"] = "Library",
                ["LocalBackup_Sub"] = "Automatically or manually backup shortcuts, categories, and settings to local storage. Last 10 backups are preserved.",
                ["LocalBackup_Title"] = "💾 Local Disk Backup & Restore",
                ["Location"] = "Location",
                ["Logs_Open_Failed"] = "Failed to open logs folder.",
                ["Macro_Add"] = "Add",
                ["Macro_Browse_Title"] = "Select Application/File for Macro Step",
                ["Macro_Defined_Steps"] = "⚡ Macro ({0} Steps Defined)",
                ["Macro_Delete"] = "🗑️ Delete",
                ["Macro_Header"] = "⚡ Macro Sequential Action List",
                ["Macro_MoveDown"] = "⬇️ Move Down",
                ["Macro_MoveUp"] = "⬆️ Move Up",
                ["Macro_NewStep"] = "➕ Add New Step",
                ["Macro_StepArgs"] = "Arguments:",
                ["Macro_StepDelay"] = "Delay (ms):",
                ["Macro_StepName"] = "Step Name:",
                ["Macro_StepTarget"] = "Target:",
                ["Macro_StepType"] = "Type:",
                ["Macro_Title"] = "Macro Steps Editor",
                ["Macro_Validation_Error"] = "Step Name and Target are required.",
                ["Mouse_Alt_Right"] = "🖱️ Alt + Right Click",
                ["Mouse_Ctrl_Right"] = "🖱️ Ctrl + Right Click",
                ["Mouse_Ctrl_XButton1"] = "🖱️ Ctrl + Mouse 4",
                ["Mouse_Ctrl_XButton2"] = "🖱️ Ctrl + Mouse 5",
                ["Mouse_Middle"] = "🖱️ Middle Click",
                ["Mouse_Shift_Right"] = "🖱️ Shift + Right Click",
                ["Mouse_XButton1"] = "🖱️ Mouse 4 (XButton1)",
                ["Mouse_XButton2"] = "🖱️ Mouse 5 (XButton2)",
                ["Move_To_Desktop"] = "🪟 Move to {0}",
                ["MsgAppsAdded"] = "new apps added.",
                ["MsgBackupDone"] = "Local backup completed.",
                ["MsgBackupDoneDetails"] = "Backup completed successfully:\\n{0}",
                ["MsgBackupExportFail"] = "Export failed.",
                ["MsgBackupExportSuccess"] = "Backup exported successfully.",
                ["MsgBackupFailed"] = "Failed to create backup.",
                ["MsgBackupFailedDetails"] = "An error occurred while creating local backup.",
                ["MsgBackupImportFail"] = "Import failed.",
                ["MsgBackupImportSuccess"] = "Backup imported successfully.",
                ["MsgBackupTakenTitle"] = "Backup Created",
                ["MsgCreatingBackup"] = "Creating local backup...",
                ["MsgItemDeleted"] = "deleted.",
                ["MsgScanCompleted"] = "Scan completed:",
                ["MsgScanningPc"] = "Scanning computer...",
                ["MsgThemeApplied"] = "Theme applied:",
                ["Nav_Apps"] = "📋  Applications & Shortcuts",
                ["Nav_Backups"] = "💾  Backup & Data",
                ["Nav_Shortcuts"] = "⚙️  Shortcuts & Startup",
                ["Nav_System"] = "ℹ️  System & Diagnostics",
                ["Nav_Themes"] = "🎨  Themes & Appearance",
                ["Opacity_Desc"] = "Adjust background transparency level of the circular overlay.",
                ["Opacity_Title"] = "Radial Menu Opacity",
                ["Open"] = "Open",
                ["Open_Logs"] = "📁 Open Logs Folder",
                ["Page_Format"] = "Page {0} / {1}",
                ["Page_Name"] = "Page {0}",
                ["Palette_Title"] = "Active Theme Color Palette",
                ["Play"] = "Play",
                ["Press_Key_Or_Mouse"] = "Press a Key or Mouse Button...",
                ["Preview_Header"] = "Live Radial Preview",
                ["Primary_Accent"] = "Primary Accent",
                ["Quick_Desktop"] = "Desktop",
                ["Quick_Mouse_Select"] = "Quick Mouse Button Selection:",
                ["Quick_Mute"] = "Mute",
                ["Quick_Search"] = "Search",
                ["Quick_Settings"] = "Settings",
                ["Quick_Snip"] = "Snipping Tool",
                ["Reduce_Motion"] = "Reduce Motion / Simplified Animations",
                ["Rename_Category"] = "Rename Category",
                ["Reset_Confirm"] = "Reset all theme, shortcut, and appearance settings to defaults?\\n(Your items and usage counts will be preserved)",
                ["Reset_Confirm_Title"] = "Reset Settings",
                ["Reset_Error_Msg"] = "An error occurred while resetting settings.",
                ["Reset_Factory"] = "Reset to Factory Defaults",
                ["Reset_Sub"] = "Restores all theme, shortcut, and visual settings to defaults. (User database is preserved)",
                ["Reset_Success_Msg"] = "Settings reset to default values.",
                ["Reset_Success_Status"] = "Settings successfully reset to defaults.",
                ["Reset_Title"] = "⚠️ Reset to Factory Defaults",
                ["Restore_Backup"] = "📂 Restore from Backup",
                ["Restore_Confirm"] = "Restore backup '{0}'?\\nThis will overwrite current items and settings.",
                ["Restore_Confirm_Title"] = "Restore Confirmation",
                ["Restore_Error"] = "Backup file could not be read or format is invalid.",
                ["Restore_Success"] = "Backup successfully restored and applied.",
                ["Restoring"] = "Restoring backup...",
                ["Run_Admin"] = "Run as Administrator",
                ["Save"] = "Save",
                ["Scan_PC"] = "🔍 Scan PC",
                ["Search"] = "Search:",
                ["Search_Placeholder"] = "Search apps, games, actions...",
                ["Secondary_Accent"] = "Secondary Accent",
                ["Settings_Open_Error"] = "An error occurred while opening settings. Check application logs for details.",
                ["ShortcutAssign_Desc"] = "Press your desired keyboard combination or click one of the quick mouse buttons below.",
                ["ShortcutAssign_Header"] = "🎯 Assign New Hotkey or Mouse Button",
                ["ShortcutAssign_Title"] = "Assign Custom Shortcut — Radial Launcher",
                ["Shortcut_AltSpace"] = "Alt + Space",
                ["Shortcut_Alt_Space"] = "⌨️ Alt + Space",
                ["Shortcut_Assigned_Status"] = "New shortcut assigned: {0}",
                ["Shortcut_CtrlSpace"] = "Ctrl + Space",
                ["Shortcut_Ctrl_Space"] = "⌨️ Ctrl + Space",
                ["Shortcut_F4"] = "F4 Key",
                ["Shortcut_None"] = "No Shortcut",
                ["Shortcut_Saved_Msg"] = "Shortcut saved successfully:\\n\\n{0}\\n({1})",
                ["Shortcut_System_Reserved"] = "⚠️ This shortcut is reserved for Windows system functions.",
                ["Shortcut_Tilde"] = "~ (Tilde Key)",
                ["Shortcut_Updated_Title"] = "Shortcut Updated",
                ["Startup_Check"] = "Automatically start Radial Launcher in tray on Windows startup",
                ["Startup_Title"] = "Windows Startup",
                ["Status_No_Items"] = "No items to display.",
                ["Status_Ready"] = "Ready.",
                ["Status_Saved"] = "Settings saved successfully.",
                ["Status_Total_Items"] = "Total {0} items listed.",
                ["Store"] = "Store",
                ["Success"] = "Success",
                ["SysAction_EMPTY_RECYCLE_BIN"] = "Empty Recycle Bin",
                ["SysAction_FOCUS_25"] = "🍅 Focus Timer (25m)",
                ["SysAction_LOCK_PC"] = "Lock PC",
                ["SysAction_MEDIA_NEXT"] = "Next Track",
                ["SysAction_MEDIA_PLAY_PAUSE"] = "Play / Pause",
                ["SysAction_MEDIA_PREV"] = "Previous Track",
                ["SysAction_NEXT_DESKTOP"] = "Next Desktop (Win+Ctrl+→)",
                ["SysAction_PREV_DESKTOP"] = "Previous Desktop (Win+Ctrl+←)",
                ["SysAction_SHOW_DESKTOP"] = "Show Desktop (Win+D)",
                ["SysAction_SNIP_TOOL"] = "Snipping Tool (Win+Shift+S)",
                ["SysAction_TASK_MANAGER"] = "Task Manager",
                ["SysAction_VOLUME_DOWN"] = "Volume Down (-2%)",
                ["SysAction_VOLUME_MUTE"] = "Mute / Unmute",
                ["SysAction_VOLUME_UP"] = "Volume Up (+2%)",
                ["SysCat_Media"] = "Media",
                ["SysCat_System"] = "System",
                ["SysCat_Windows"] = "Windows",
                ["SysDesc_EMPTY_RECYCLE_BIN"] = "Permanently purge all deleted files from Recycle Bin",
                ["SysDesc_FOCUS_25"] = "Start 25-minute Pomodoro focus timer session",
                ["SysDesc_LOCK_PC"] = "Instantly lock workstation session",
                ["SysDesc_MEDIA_NEXT"] = "Skip to next media track",
                ["SysDesc_MEDIA_PLAY_PAUSE"] = "Play or pause current media playback",
                ["SysDesc_MEDIA_PREV"] = "Skip to previous media track",
                ["SysDesc_NEXT_DESKTOP"] = "Switch to next virtual desktop",
                ["SysDesc_PREV_DESKTOP"] = "Switch to previous virtual desktop",
                ["SysDesc_SHOW_DESKTOP"] = "Minimize or restore all windows to view desktop",
                ["SysDesc_SNIP_TOOL"] = "Capture screen regions with Windows Snipping Tool",
                ["SysDesc_TASK_MANAGER"] = "Open Windows Task Manager",
                ["SysDesc_VOLUME_DOWN"] = "Decrease master system volume by 2%",
                ["SysDesc_VOLUME_MUTE"] = "Toggle master audio mute state",
                ["SysDesc_VOLUME_UP"] = "Increase master system volume by 2%",
                ["Tab1_Sub"] = "Manage all apps, games, websites, and folders listed in the radial menu.",
                ["Tab1_Title"] = "Application & Shortcut Management",
                ["Tab2_Sub"] = "Select from 8 curated themes, customize radial opacity and density.",
                ["Tab2_Title"] = "Themes & Visual Customization",
                ["Tab3_Sub"] = "Set the mouse button or keyboard shortcut to open the radial menu.",
                ["Tab3_Title"] = "Trigger Shortcut & Startup",
                ["Tab4_Sub"] = "Safely backup and restore your shortcuts, stats, and theme settings.",
                ["Tab4_Title"] = "Backup & Data Management",
                ["Tab5_Sub"] = "System diagnostic logs, error logs, and application updates.",
                ["Tab5_Title"] = "Updates & Diagnostics",
                ["Terminal"] = "Terminal",
                ["Theme_AmoledBlack"] = "OLED Black",
                ["Theme_Blue"] = "Deep Navy",
                ["Theme_Dark"] = "Midnight Dark",
                ["Theme_Forest"] = "Emerald Forest",
                ["Theme_HighContrast"] = "Nordic Frost",
                ["Theme_Purple"] = "Cyberpunk Neon",
                ["Theme_Red"] = "Sunset Amber",
                ["Theme_White"] = "Clean Light",
                ["Themes_Header"] = "Curated Themes (8 Themes)",
                ["TrayExit"] = "Exit",
                ["TrayFocusCompletedBody"] = "Your 25-minute focus session ended successfully. Great job!",
                ["TrayFocusCompletedTitle"] = "🍅 Focus Session Complete!",
                ["TrayOpenMenu"] = "Open Menu",
                ["TraySettings"] = "Settings & Management",
                ["TrayUpdateBody"] = "New version v{0} is available.\\nYou can download it from GitHub.",
                ["TrayUpdateTitle"] = "🚀 Update Available!",
                ["Trigger_Desc"] = "Select a mouse button or keyboard hotkey to summon Radial Launcher.",
                ["Trigger_Title"] = "Menu Activation Shortcut",
                ["TutorialBody"] = "• Middle Click: Open / close radial menu\\n• Click any icon: Launch application or shortcut\\n• Mouse wheel / Drag: Navigate pages and categories\\n• Type anytime: Fast search across all apps",
                ["TutorialDismiss"] = "Got it! Start using",
                ["TutorialHeader"] = "🚀 Welcome to Radial Launcher!",
                ["Update_App_UpToDate"] = "Application is up to date.",
                ["Update_Available_Label"] = "🎉 A new version is available: v{0}\\n{1}",
                ["Update_Available_Status"] = "New version v{0} available!",
                ["Update_Check_Failed"] = "Update check failed.",
                ["Update_Dialog_Body"] = "A new version has been released (v{0}).\\n\\nWould you like to open the download page?",
                ["Update_Dialog_Title"] = "Update Available",
                ["Update_Error_Label"] = "An error occurred during update check.",
                ["Update_Error_Status"] = "Update error.",
                ["Update_Latest_Label"] = "✅ You are using the latest version (v{0}).",
                ["Update_Server_Unreachable"] = "Could not reach update server. Please check internet connection.",
                ["Update_Service_NotFound"] = "Update service not found.",
                ["Updates_Title"] = "🚀 Application Updates",
                ["Validation_Fill_Required"] = "Please fill in both Name and Target fields.",
                ["Validation_No_Empty"] = "Name and Target fields cannot be empty.",
                ["Warning"] = "Warning",
            };
            _translations["en"] = dict_en;

            var dict_tr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Access_Sub"] = "Düşük donanımlarda veya sade animasyon tercihinde hareketi azaltın.",
                ["Access_Title"] = "Erişilebilirlik & Performans",
                ["Action_Delete"] = "Sil",
                ["Action_Edit"] = "Düzenle",
                ["Action_Favorite"] = "Favori",
                ["Active_Shortcut_Label"] = "Aktif Kısayol:",
                ["AddItem_Title"] = "Yeni Öğe Ekle",
                ["Add_Item"] = "➕ Yeni Öğe Ekle",
                ["All_Categories"] = "Tüm Kategoriler",
                ["App_Title"] = "Radial Launcher — Yönetim & Ayarlar",
                ["Assign_Shortcut"] = "🎯 Yeni Kısayol Ata",
                ["AutoCheck_Disabled"] = "Otomatik güncelleme kontrolü devre dışı bırakıldı.",
                ["AutoCheck_Enabled"] = "Otomatik güncelleme kontrolü etkinleştirildi.",
                ["AutoCheck_Updates"] = "Açılışta güncellemeleri otomatik kontrol et",
                ["Background"] = "Arkaplan",
                ["Backup_Now"] = "💾 Şimdi Yerel Yedekle",
                ["Backup_Status_Count"] = "Toplam {0} yerel yedek mevcut. En son: {1} ({2})",
                ["Backup_Status_None"] = "Henüz oluşturulmuş yerel yedek bulunmuyor.",
                ["Behavior_Body"] = "• Otomatik Kapanma: Fare radyal menü alanından 330px uzaklaştığında menü otomatik kapanır.\\n• Global Gezinme: Farenin orta tuşuyla basıp sağa/sola sürükleyerek veya fare tekerleğiyle sayfalar ve kategoriler arası geçebilirsiniz.\\n• Hızlı Eylemler: Öğelerin üzerine fare ile gelindiğinde hızlı aksiyon kartı merkezde görüntülenir.",
                ["Behavior_Title"] = "🎯 Menü Davranışı & Gezinme Kuralları",
                ["Browse"] = "Gözat...",
                ["Browse_App_Title"] = "Uygulama veya Dosya Seçin",
                ["Browse_Icon"] = "Simge Seç",
                ["Browse_Icon_Title"] = "İkon Dosyası Seç",
                ["Cancel"] = "İptal",
                ["Cat_ClipboardHistory"] = "📋 Pano Geçmişi",
                ["Cat_Err_Duplicate"] = "Bu ada sahip bir kategori zaten var.",
                ["Cat_Err_Empty"] = "Kategori adı boş bırakılamaz.",
                ["Cat_Err_TooLong"] = "Kategori adı 50 karakteri geçemez.",
                ["Cat_Games"] = "🎮 Oyunlar",
                ["Cat_MostUsed"] = "⭐ Sık Kullanılanlar",
                ["Cat_Name_Label"] = "Kategori Adı:",
                ["Cat_OpenWindows"] = "🪟 Açık Pencereler",
                ["Cat_Rename_Dialog_Title"] = "Kategoriyi Yeniden Adlandır — Radial Launcher",
                ["Cat_Rename_Failed"] = "Kategori yeniden adlandırılamadı.",
                ["Cat_Rename_Header"] = "🏷️ Kategoriyi Yeniden Adlandır",
                ["Cat_Rename_Sub"] = "Seçilen kategori için yeni bir ad girin.",
                ["Cat_Renamed_Status"] = "Kategori yeniden adlandırıldı: {0}",
                ["Cat_Select_To_Rename"] = "Lütfen yeniden adlandırmak için açılır listeden bir kategori seçin.",
                ["Cat_System"] = "⚡ Sistem",
                ["Category"] = "Kategori:",
                ["Check_Updates"] = "🔄 Güncellemeleri Şimdi Kontrol Et",
                ["Checking_Release"] = "GitHub Release kontrol ediliyor...",
                ["Checking_Updates"] = "Güncellemeler kontrol ediliyor...",
                ["Cmd_Logs"] = "📂 Log Klasörünü Aç (/logs)",
                ["Cmd_Restart"] = "🔄 Uygulamayı Yeniden Başlat (/restart)",
                ["Cmd_Settings"] = "⚙️ Ayarları Aç (/settings)",
                ["Col_Actions"] = "İşlemler",
                ["Col_Category"] = "Kategori",
                ["Col_Icon"] = "İkon",
                ["Col_Launches"] = "Çalıştırma",
                ["Col_Name"] = "İsim",
                ["Col_Order"] = "Sıra",
                ["Col_Star"] = "⭐",
                ["Col_Target"] = "Hedef",
                ["Col_Type"] = "Tür",
                ["Community"] = "Topluluk",
                ["Copy"] = "Kopyala",
                ["Copy_Diag"] = "📋 Tanılamayı Kopyala",
                ["Delete_Confirm"] = "'{0}' silinsin mi?",
                ["Delete_Confirm_Title"] = "Silme Onayı",
                ["Density_Compact"] = "Kompakt (18 Öğeli)",
                ["Density_Desc"] = "Tek bir halka sayfasında kaç öğe yerleştirileceğini belirler.",
                ["Density_Expanded"] = "Geniş (15 Öğeli)",
                ["Density_Title"] = "Halka Yoğunluk Modu",
                ["Desktop_N"] = "Masaüstü {0}",
                ["Desktop_Name_Format"] = "Masaüstü {0}",
                ["Desktops_Unavailable"] = "⚠️ Sanal Masaüstleri Kullanılamıyor",
                ["Detected_Shortcut"] = "Algılanan Kısayol:",
                ["Diag_Copied_Msg"] = "Sistem tanılama bilgileri panoya kopyalandı.",
                ["Diag_Copied_Status"] = "Tanılama bilgileri panoya kopyalandı!",
                ["Diag_Sub"] = "Uygulama log dosyaları ve sistem tanılama bilgileri.",
                ["Diag_Title"] = "📊 Tanılama & Sistem Kayıtları",
                ["Diag_Title_Short"] = "Tanılama",
                ["EditItem_Title"] = "Öğeyi Düzenle",
                ["Edit_Macro"] = "⚡ Makro Adımlarını Düzenle...",
                ["Err_Launch_Failed"] = "'{0}' başlatılamadı. Lütfen hedef dosya yolunu kontrol edin.",
                ["Error"] = "Hata",
                ["Export"] = "📤 Dışa Aktar (JSON)",
                ["FileFilter_AllSupported"] = "Tüm Desteklenenler (*.exe, *.lnk, *.*)|*.exe;*.bat;*.cmd;*.lnk;*.*|Uygulamalar (*.exe)|*.exe|Tüm Dosyalar (*.*)|*.*",
                ["FileFilter_Icons"] = "Simgeler ve Resimler (*.ico;*.exe;*.png;*.jpg)|*.ico;*.exe;*.png;*.jpg|Tüm Dosyalar (*.*)|*.*",
                ["FileFilter_Json"] = "JSON Dosyaları (*.json)|*.json|Tüm Dosyalar (*.*)|*.*",
                ["Icon_Bubble"] = "İkon Baloncuğu",
                ["Import"] = "📥 İçe Aktar (JSON)",
                ["Installed_Version"] = "Yüklü Sürüm: v1.0.0 (Nihai Sürüm)",
                ["Installer_Btn_Browse"] = "Gözat...",
                ["Installer_Btn_Cancel"] = "İptal",
                ["Installer_Btn_Finish"] = "Bitir",
                ["Installer_Btn_Install"] = "Şimdi Kur",
                ["Installer_Chk_Desktop"] = "Masaüstü Kısayolu Oluştur",
                ["Installer_Chk_Launch"] = "Kurulum bittiğinde Radial Launcher'ı başlat",
                ["Installer_Chk_StartMenu"] = "Başlat Menüsü Kısayolu Oluştur",
                ["Installer_Chk_Startup"] = "Windows başlangıcında otomatik çalıştır (Sistem Tepsisi)",
                ["Installer_Clean_Guarantee"] = "Temiz Kurulum: Ayarlarınız ve kısayollarınız %LOCALAPPDATA%\\RadialLauncher içinde güvenle saklanır",
                ["Installer_Header_Sub"] = "Windows için Profesyonel Dairesel Uygulama ve Oyun Başlatıcı",
                ["Installer_Install_Dir"] = "Kurulum Klasörü:",
                ["Installer_Options"] = "Kurulum Seçenekleri:",
                ["Installer_Requirements"] = "Gereksinimler: Windows 10/11 x64",
                ["Installer_Status_Complete"] = "Kurulum başarıyla tamamlandı!",
                ["Installer_Status_Configuring"] = "Sistem kısayolları ve kayıt defteri yapılandırılıyor...",
                ["Installer_Status_Extracting"] = "Dosyalar çıkartılıyor: {0}",
                ["Installer_Status_Starting"] = "Kuruluma başlanıyor...",
                ["Installer_Title"] = "Radial Launcher v1.0.0 — Kurulum Sihirbazı",
                ["Installer_Uninstall_Confirm"] = "Radial Launcher'ı sisteminizden tamamen kaldırmak istediğinizden emin misiniz?",
                ["Installer_Uninstall_Done"] = "Radial Launcher bilgisayarınızdan başarıyla kaldırıldı.",
                ["Installer_Uninstall_RemoveData"] = "Kişisel ayarlarınızı, kategorilerinizi ve veritabanınızı da silmek ister misiniz?",
                ["Internal_Code"] = "Dahili Kod:",
                ["Item_Args"] = "Argümanlar:",
                ["Item_Args_Tooltip"] = "Opsiyonel komut satırı argümanları",
                ["Item_Category"] = "Kategori:",
                ["Item_Favorite_Add"] = "Favorilere Ekle (İç Halka ⭐)",
                ["Item_Favorite_Edit"] = "Favori Olarak İşaretle (⭐)",
                ["Item_Icon"] = "Özel İkon:",
                ["Item_Icon_Tooltip"] = "İsteğe bağlı özel .ico, .exe veya .png dosya yolu",
                ["Item_Name"] = "İsim:",
                ["Item_Target"] = "Hedef:",
                ["Item_Target_Tooltip"] = "EXE dosya yolu, site URL'si (ör. youtube.com), dosya veya klasör yolu",
                ["Item_Type"] = "Tür:",
                ["JsonBackup_Sub"] = "Ayarlarınızı ve kısayollarınızı JSON dosyası olarak bilgisayarınıza kaydedin veya farklı bir cihazdan içe aktarın.",
                ["JsonBackup_Title"] = "📤 JSON Dışa / İçe Aktar",
                ["Language"] = "🌐 Görüntüleme Dili",
                ["Language_Changed_Status"] = "Dil değiştirildi: {0}",
                ["Language_Desc"] = "Uygulama arayüzü ve radyal menü için dil seçin (Varsayılan: İngilizce). Alfabetik sıralıdır.",
                ["Launch"] = "Başlat",
                ["Library"] = "Kütüphane",
                ["LocalBackup_Sub"] = "Tüm kısayollarınızı, kategorilerinizi ve istatistiklerinizi yerel diske otomatik veya manuel yedekleyin. Son 10 yedek güvenle korunur.",
                ["LocalBackup_Title"] = "💾 Yerel Disk Yedekleme & Geri Yükleme",
                ["Location"] = "Konum",
                ["Logs_Open_Failed"] = "Log klasörü açılamadı.",
                ["Macro_Add"] = "Ekle",
                ["Macro_Browse_Title"] = "Makro Adımı İçin Uygulama/Dosya Seçin",
                ["Macro_Defined_Steps"] = "⚡ Makro ({0} Adım Tanımlı)",
                ["Macro_Delete"] = "🗑️ Sil",
                ["Macro_Header"] = "⚡ Makro Sıralı Eylem Listesi",
                ["Macro_MoveDown"] = "⬇️ Aşağı",
                ["Macro_MoveUp"] = "⬆️ Yukarı",
                ["Macro_NewStep"] = "➕ Yeni Adım Ekle",
                ["Macro_StepArgs"] = "Argümanlar:",
                ["Macro_StepDelay"] = "Gecikme (ms):",
                ["Macro_StepName"] = "Adım Adı:",
                ["Macro_StepTarget"] = "Hedef:",
                ["Macro_StepType"] = "Tür:",
                ["Macro_Title"] = "Makro Adımları Düzenleyici",
                ["Macro_Validation_Error"] = "Adım Adı ve Hedef alanları zorunludur.",
                ["Mouse_Alt_Right"] = "🖱️ Alt + Sağ Tık",
                ["Mouse_Ctrl_Right"] = "🖱️ Ctrl + Sağ Tık",
                ["Mouse_Ctrl_XButton1"] = "🖱️ Ctrl + Fare 4",
                ["Mouse_Ctrl_XButton2"] = "🖱️ Ctrl + Fare 5",
                ["Mouse_Middle"] = "🖱️ Orta Tuş",
                ["Mouse_Shift_Right"] = "🖱️ Shift + Sağ Tık",
                ["Mouse_XButton1"] = "🖱️ Fare 4 (Geri Tuşu)",
                ["Mouse_XButton2"] = "🖱️ Fare 5 (İleri Tuşu)",
                ["Move_To_Desktop"] = "🪟 {0} Konumuna Taşı",
                ["MsgAppsAdded"] = "yeni uygulama eklendi.",
                ["MsgBackupDone"] = "Yerel yedekleme tamamlandı.",
                ["MsgBackupDoneDetails"] = "Yedekleme başarıyla tamamlandı:\\n{0}",
                ["MsgBackupExportFail"] = "Dışa aktarma başarısız.",
                ["MsgBackupExportSuccess"] = "Yedek başarıyla dışa aktarıldı.",
                ["MsgBackupFailed"] = "Yedekleme oluşturulamadı.",
                ["MsgBackupFailedDetails"] = "Yerel yedek oluşturulurken bir hata meydana geldi.",
                ["MsgBackupImportFail"] = "İçe aktarma başarısız.",
                ["MsgBackupImportSuccess"] = "Yedek başarıyla içe aktarıldı.",
                ["MsgBackupTakenTitle"] = "Yedek Alındı",
                ["MsgCreatingBackup"] = "Yerel yedek oluşturuluyor...",
                ["MsgItemDeleted"] = "silindi.",
                ["MsgScanCompleted"] = "Tarama tamamlandı:",
                ["MsgScanningPc"] = "Bilgisayar taranıyor...",
                ["MsgThemeApplied"] = "Tema uygulandı:",
                ["Nav_Apps"] = "📋  Uygulamalar & Kısayollar",
                ["Nav_Backups"] = "💾  Yedekleme & Veri",
                ["Nav_Shortcuts"] = "⚙️  Kısayol & Başlangıç",
                ["Nav_System"] = "ℹ️  Güncelleme & Tanılama",
                ["Nav_Themes"] = "🎨  Temalar & Görsellik",
                ["Opacity_Desc"] = "Dairesel menünün arkaplan şeffaflık derecesini ayarlar.",
                ["Opacity_Title"] = "Radial Şeffaflığı (Opacity)",
                ["Open"] = "Aç",
                ["Open_Logs"] = "📁 Log Klasörünü Aç",
                ["Page_Format"] = "Sayfa {0} / {1}",
                ["Page_Name"] = "Sayfa {0}",
                ["Palette_Title"] = "Seçili Tema Renk Paleti",
                ["Play"] = "Oyna",
                ["Press_Key_Or_Mouse"] = "Tuşa veya Fare Butonuna Basın...",
                ["Preview_Header"] = "Canlı Dairesel Önizleme",
                ["Primary_Accent"] = "Birincil Vurgu",
                ["Quick_Desktop"] = "Masaüstü",
                ["Quick_Mouse_Select"] = "Hızlı Fare Düğmesi Seçimi:",
                ["Quick_Mute"] = "Sesi Kapat",
                ["Quick_Search"] = "Arama",
                ["Quick_Settings"] = "Ayarlar",
                ["Quick_Snip"] = "Ekran Alıntısı",
                ["Reduce_Motion"] = "Hareketi Azalt ve Animasyonları Sadeleştir",
                ["Rename_Category"] = "Kategoriyi Yeniden Adlandır",
                ["Reset_Confirm"] = "Tüm tema, kısayol ve görsel ayarlarınız varsayılan fabrika değerlerine sıfırlanacaktır.\\n(Kullanıcı öğeleri, kısayollar ve kullanım sayaçları KORUNUR)\\n\\nDevam etmek istiyor musunuz?",
                ["Reset_Confirm_Title"] = "Ayarları Sıfırla",
                ["Reset_Error_Msg"] = "Ayarlar sıfırlanırken bir sorun oluştu.",
                ["Reset_Factory"] = "Fabrika Ayarlarına Sıfırla",
                ["Reset_Sub"] = "Tüm tema, kısayol ve görsel ayarları varsayılan fabrika değerlerine geri döndürür. (Kullanıcı veritabanı korunur)",
                ["Reset_Success_Msg"] = "Ayarlar varsayılan değerlere sıfırlandı.",
                ["Reset_Success_Status"] = "Ayarlar başarıyla varsayılanlara sıfırlandı.",
                ["Reset_Title"] = "⚠️ Fabrika Ayarlarına Sıfırla",
                ["Restore_Backup"] = "📂 Yerel Yedekten Geri Yükle",
                ["Restore_Confirm"] = "'{0}' yedeği geri yüklenecek.\\nMevcut verilerinizin üzerine yazılacaktır. Onaylıyor musunuz?",
                ["Restore_Confirm_Title"] = "Yedekten Geri Yükleme Onayı",
                ["Restore_Error"] = "Yedek dosyası okunamadı veya biçimi geçersiz.",
                ["Restore_Success"] = "Yedek başarıyla geri yüklendi ve uygulandı.",
                ["Restoring"] = "Yedek geri yükleniyor...",
                ["Run_Admin"] = "Yönetici Olarak Çalıştır",
                ["Save"] = "Kaydet",
                ["Scan_PC"] = "🔍 Bilgisayarı Tara",
                ["Search"] = "Ara:",
                ["Search_Placeholder"] = "Uygulama, oyun veya eylem ara...",
                ["Secondary_Accent"] = "İkincil Vurgu",
                ["Settings_Open_Error"] = "Ayarlar penceresi açılırken bir sorun oluştu. Detaylar için uygulama loglarını kontrol edebilirsiniz.",
                ["ShortcutAssign_Desc"] = "İstediğiniz klavye kombinasyonuna basın veya aşağıdaki hızlı fare düğmelerinden birine tıklayın.",
                ["ShortcutAssign_Header"] = "🎯 Yeni Kısayol Tuşu veya Fare Düğmesi Ata",
                ["ShortcutAssign_Title"] = "Özel Kısayol Ata — Radial Launcher",
                ["Shortcut_AltSpace"] = "Alt + Boşluk",
                ["Shortcut_Alt_Space"] = "⌨️ Alt + Boşluk",
                ["Shortcut_Assigned_Status"] = "Yeni kısayol atandı: {0}",
                ["Shortcut_CtrlSpace"] = "Ctrl + Boşluk",
                ["Shortcut_Ctrl_Space"] = "⌨️ Ctrl + Boşluk",
                ["Shortcut_F4"] = "F4 Tuşu",
                ["Shortcut_None"] = "Kısayol Yok",
                ["Shortcut_Saved_Msg"] = "Kısayol başarıyla kaydedildi:\\n\\n{0}\\n({1})",
                ["Shortcut_System_Reserved"] = "⚠️ Bu kısayol Windows sistem işlevleri için ayrılmıştır.",
                ["Shortcut_Tilde"] = "~ (Tilde Tuşu)",
                ["Shortcut_Updated_Title"] = "Kısayol Güncellendi",
                ["Startup_Check"] = "Windows açıldığında Radial Launcher'ı otomatik başlat",
                ["Startup_Title"] = "Windows Başlangıcı",
                ["Status_No_Items"] = "Henüz listelenecek öğe bulunmuyor.",
                ["Status_Ready"] = "Hazır.",
                ["Status_Saved"] = "Ayarlar başarıyla kaydedildi.",
                ["Status_Total_Items"] = "Toplam {0} öğe listelendi.",
                ["Store"] = "Mağaza",
                ["Success"] = "Başarılı",
                ["SysAction_EMPTY_RECYCLE_BIN"] = "Geri Dönüşüm Kutusunu Boşalt",
                ["SysAction_FOCUS_25"] = "🍅 Odak Zamanlayıcı (25dk)",
                ["SysAction_LOCK_PC"] = "Bilgisayarı Kilitle",
                ["SysAction_MEDIA_NEXT"] = "Sonraki Parça",
                ["SysAction_MEDIA_PLAY_PAUSE"] = "Oynat / Duraklat",
                ["SysAction_MEDIA_PREV"] = "Önceki Parça",
                ["SysAction_NEXT_DESKTOP"] = "Sonraki Masaüstü (Win+Ctrl+→)",
                ["SysAction_PREV_DESKTOP"] = "Önceki Masaüstü (Win+Ctrl+←)",
                ["SysAction_SHOW_DESKTOP"] = "Masaüstünü Göster (Win+D)",
                ["SysAction_SNIP_TOOL"] = "Ekran Alıntısı Aracı (Win+Shift+S)",
                ["SysAction_TASK_MANAGER"] = "Görev Yöneticisi",
                ["SysAction_VOLUME_DOWN"] = "Sesi Kıs (-%2)",
                ["SysAction_VOLUME_MUTE"] = "Sesi Kapat / Aç",
                ["SysAction_VOLUME_UP"] = "Sesi Aç (+%2)",
                ["SysCat_Media"] = "Medya",
                ["SysCat_System"] = "Sistem",
                ["SysCat_Windows"] = "Pencereler",
                ["SysDesc_EMPTY_RECYCLE_BIN"] = "Geri Dönüşüm Kutusundaki tüm silinmiş dosyaları kalıcı olarak temizle",
                ["SysDesc_FOCUS_25"] = "25 dakikalık Pomodoro odak zamanlayıcı oturumu başlat",
                ["SysDesc_LOCK_PC"] = "Bilgisayar oturumunu anında kilitle",
                ["SysDesc_MEDIA_NEXT"] = "Sonraki medya parçasına geç",
                ["SysDesc_MEDIA_PLAY_PAUSE"] = "Mevcut medyayı oynat veya duraklat",
                ["SysDesc_MEDIA_PREV"] = "Önceki medya parçasına dön",
                ["SysDesc_NEXT_DESKTOP"] = "Sonraki sanal masaüstüne geç",
                ["SysDesc_PREV_DESKTOP"] = "Önceki sanal masaüstüne dön",
                ["SysDesc_SHOW_DESKTOP"] = "Masaüstünü görmek için tüm pencereleri küçült veya geri yükle",
                ["SysDesc_SNIP_TOOL"] = "Windows Ekran Alıntısı Aracı ile ekran görüntüsü al",
                ["SysDesc_TASK_MANAGER"] = "Windows Görev Yöneticisini aç",
                ["SysDesc_VOLUME_DOWN"] = "Ana sistem sesini %2 azalt",
                ["SysDesc_VOLUME_MUTE"] = "Ana sistem sesini aç/kapat",
                ["SysDesc_VOLUME_UP"] = "Ana sistem sesini %2 artır",
                ["Tab1_Sub"] = "Radyal menüde listelenen tüm program, web sitesi ve klasörleri yönetin.",
                ["Tab1_Title"] = "Uygulama & Kısayol Yönetimi",
                ["Tab2_Sub"] = "8 seçkin renk paletinden birini seçin, radyal şeffaflığı ve yoğunluğu ayarlayın.",
                ["Tab2_Title"] = "Görsel Temalar & Şeffaflık",
                ["Tab3_Sub"] = "Menüyü açmak için kullanacağınız fare ve klavye kısayollarını belirleyin.",
                ["Tab3_Title"] = "Tetikleme Kısayolu & Başlangıç",
                ["Tab4_Sub"] = "Kısayollarınızı, istatistiklerinizi ve tema ayarlarınızı güvenle yedekleyin.",
                ["Tab4_Title"] = "Yedekleme & Veri Yönetimi",
                ["Tab5_Sub"] = "Sistem tanılama bilgileri, hata kayıtları ve yazılım güncellemeleri.",
                ["Tab5_Title"] = "Güncellemeler & Tanılama",
                ["Terminal"] = "Terminal",
                ["Theme_AmoledBlack"] = "OLED Siyah",
                ["Theme_Blue"] = "Derin Lacivert",
                ["Theme_Dark"] = "Gece Karanlığı",
                ["Theme_Forest"] = "Zümrüt Ormanı",
                ["Theme_HighContrast"] = "Kutup Ayazı",
                ["Theme_Purple"] = "Siberpunk Neon",
                ["Theme_Red"] = "Gün Batımı Kehribarı",
                ["Theme_White"] = "Açık / Temiz",
                ["Themes_Header"] = "Hazır Temalar (8 Seçkin Tema)",
                ["TrayExit"] = "Çıkış",
                ["TrayFocusCompletedBody"] = "25 dakikalık odaklanma süreniz başarıyla bitti. Harika iş!",
                ["TrayFocusCompletedTitle"] = "🍅 Odaklanma Tamamlandı!",
                ["TrayOpenMenu"] = "Menüyü Aç",
                ["TraySettings"] = "Ayarlar & Yönetim",
                ["TrayUpdateBody"] = "Yeni sürüm v{0} yayınlandı.\\nGitHub üzerinden indirebilirsiniz.",
                ["TrayUpdateTitle"] = "🚀 Güncelleme Mevcut!",
                ["Trigger_Desc"] = "Radial Launcher'ı ekranda açmak için farenizden veya klavyenizden bir kısayol seçin.",
                ["Trigger_Title"] = "Menüyü Tetikleme Kısayolu",
                ["TutorialBody"] = "• Orta Fare Tuşu: Menüyü açar / kapatır\\n• Çemberdeki bir öğeye tıklayın: Başlatır\\n• Fare sürükleme / Tekerlek: Sayfalar arası geçiş yapar\\n• Klavye: Herhangi bir şey yazarak anında arayın",
                ["TutorialDismiss"] = "Anladım, Başla!",
                ["TutorialHeader"] = "🚀 Radial Launcher'a Hoş Geldiniz!",
                ["Update_App_UpToDate"] = "Uygulama güncel.",
                ["Update_Available_Label"] = "🎉 Yeni bir sürüm mevcut: v{0}\\n{1}",
                ["Update_Available_Status"] = "Yeni sürüm v{0} mevcut!",
                ["Update_Check_Failed"] = "Güncelleme kontrolü başarısız.",
                ["Update_Dialog_Body"] = "Yeni bir sürüm yayınlandı (v{0}).\\n\\nİndirme sayfasına gitmek ister misiniz?",
                ["Update_Dialog_Title"] = "Güncelleme Mevcut",
                ["Update_Error_Label"] = "Güncelleme kontrolü sırasında bir sorun oluştu.",
                ["Update_Error_Status"] = "Güncelleme hatası.",
                ["Update_Latest_Label"] = "✅ En güncel sürümü kullanıyorsunuz (v{0}).",
                ["Update_Server_Unreachable"] = "Güncelleme sunucusuna ulaşılamadı. Lütfen internet bağlantınızı kontrol edin.",
                ["Update_Service_NotFound"] = "Güncelleme servisi bulunamadı.",
                ["Updates_Title"] = "🚀 Yazılım Güncellemeleri",
                ["Validation_Fill_Required"] = "Lütfen hem Ad hem de Hedef alanlarını doldurun.",
                ["Validation_No_Empty"] = "İsim ve Hedef alanları boş bırakılamaz.",
                ["Warning"] = "Uyarı",
            };
            _translations["tr"] = dict_tr;
        }
    }
}
