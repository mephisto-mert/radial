using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RadialLauncher.Services.Localization;

namespace RadialLauncher.UI.Windows
{
    public partial class ShortcutAssignWindow : Window
    {
        public string SelectedShortcut { get; private set; } = string.Empty;
        public string FriendlyName { get; private set; } = string.Empty;

        private readonly Action _onLanguageChangedHandler;

        public ShortcutAssignWindow(string currentShortcut = "MiddleClick")
        {
            InitializeComponent();
            ApplyLocalization();
            SetShortcut(currentShortcut);

            _onLanguageChangedHandler = () => Dispatcher.Invoke(ApplyLocalization);
            LocalizationService.Instance.OnLanguageChanged += _onLanguageChangedHandler;
            Closed += (s, e) => LocalizationService.Instance.OnLanguageChanged -= _onLanguageChangedHandler;
        }

        public void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;
            Title = loc.GetString("ShortcutAssign_Title", "Assign Custom Shortcut — Radial Launcher");
            if (TxtHeaderTitle != null) TxtHeaderTitle.Text = loc.GetString("ShortcutAssign_Header", "🎯 Assign New Hotkey or Mouse Button");
            if (TxtHeaderDesc != null) TxtHeaderDesc.Text = loc.GetString("ShortcutAssign_Desc", "Press your desired keyboard combination or click one of the quick mouse buttons below.");
            if (TxtDetectedLabel != null) TxtDetectedLabel.Text = loc.GetString("Detected_Shortcut", "Detected Shortcut:");
            if (TxtQuickMouseLabel != null) TxtQuickMouseLabel.Text = loc.GetString("Quick_Mouse_Select", "Quick Mouse Button Selection:");
            if (BtnMiddleClick != null) BtnMiddleClick.Content = loc.GetString("Mouse_Middle", "🖱️ Middle Click");
            if (BtnXButton1 != null) BtnXButton1.Content = loc.GetString("Mouse_XButton1", "🖱️ Mouse 4 (XButton1)");
            if (BtnXButton2 != null) BtnXButton2.Content = loc.GetString("Mouse_XButton2", "🖱️ Mouse 5 (XButton2)");
            if (BtnCtrlXButton1 != null) BtnCtrlXButton1.Content = loc.GetString("Mouse_Ctrl_XButton1", "🖱️ Ctrl + Mouse 4");
            if (BtnAltRight != null) BtnAltRight.Content = loc.GetString("Mouse_Alt_Right", "🖱️ Alt + Right Click");
            if (BtnShiftRight != null) BtnShiftRight.Content = loc.GetString("Mouse_Shift_Right", "🖱️ Shift + Right Click");
            if (CancelButton != null) CancelButton.Content = loc.GetString("Cancel", "Cancel");
            if (SaveButton != null) SaveButton.Content = loc.GetString("Save", "Save");

            if (!string.IsNullOrEmpty(SelectedShortcut))
            {
                SetShortcut(SelectedShortcut);
            }
        }

        private void SetShortcut(string internalCode)
        {
            if (string.IsNullOrWhiteSpace(internalCode)) return;

            string clean = internalCode.Trim();
            string lower = clean.ToLowerInvariant();
            var loc = LocalizationService.Instance;

            // Validate against reserved system hotkeys
            if (lower == "alt+f4" || lower == "altf4" || lower == "ctrl+alt+del" || lower == "win+l")
            {
                if (ValidationWarningText != null)
                    ValidationWarningText.Text = loc.GetString("Shortcut_System_Reserved", "⚠️ This shortcut is reserved for Windows system functions.");
                if (SaveButton != null)
                    SaveButton.IsEnabled = false;
                return;
            }

            if (ValidationWarningText != null)
                ValidationWarningText.Text = string.Empty;
            SelectedShortcut = clean;
            FriendlyName = ToFriendlyName(clean);

            if (ShortcutDisplayText != null)
                ShortcutDisplayText.Text = FriendlyName;
            if (ShortcutRawText != null)
                ShortcutRawText.Text = $"{loc.GetString("Internal_Code", "Internal Code:")} {SelectedShortcut}";
            if (SaveButton != null)
                SaveButton.IsEnabled = true;
        }

        public static string ToFriendlyName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return LocalizationService.Instance.GetString("Shortcut_None", "No Shortcut");

            var loc = LocalizationService.Instance;

            return code switch
            {
                "MiddleClick" => loc.GetString("Mouse_Middle", "🖱️ Middle Click"),
                "XButton1" => loc.GetString("Mouse_XButton1", "🖱️ Mouse 4 (XButton1)"),
                "XButton2" => loc.GetString("Mouse_XButton2", "🖱️ Mouse 5 (XButton2)"),
                "Ctrl+XButton1" => loc.GetString("Mouse_Ctrl_XButton1", "🖱️ Ctrl + Mouse 4"),
                "Ctrl+XButton2" => "🖱️ Ctrl + Mouse 5",
                "AltRightClick" => loc.GetString("Mouse_Alt_Right", "🖱️ Alt + Right Click"),
                "ShiftRightClick" => loc.GetString("Mouse_Shift_Right", "🖱️ Shift + Right Click"),
                "CtrlRightClick" => loc.GetString("Mouse_Ctrl_Right", "🖱️ Ctrl + Right Click"),
                "AltSpace" => loc.GetString("Shortcut_Alt_Space", "⌨️ Alt + Space"),
                "CtrlSpace" => loc.GetString("Shortcut_Ctrl_Space", "⌨️ Ctrl + Space"),
                "F4" => "⌨️ F4",
                "Tilde" => "⌨️ ~ (Tilde)",
                _ => FormatArbitraryShortcut(code)
            };
        }

        private static string FormatArbitraryShortcut(string code)
        {
            var sb = new StringBuilder();
            if (code.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)) sb.Append("Ctrl + ");
            if (code.Contains("Shift", StringComparison.OrdinalIgnoreCase)) sb.Append("Shift + ");
            if (code.Contains("Alt", StringComparison.OrdinalIgnoreCase)) sb.Append("Alt + ");
            if (code.Contains("Win", StringComparison.OrdinalIgnoreCase)) sb.Append("Win + ");

            string rest = code.Replace("Ctrl", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("Shift", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("Alt", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("Win", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("+", "").Trim();

            if (rest.Equals("XButton1", StringComparison.OrdinalIgnoreCase) || rest.Equals("Mouse4", StringComparison.OrdinalIgnoreCase))
                sb.Append("Mouse 4");
            else if (rest.Equals("XButton2", StringComparison.OrdinalIgnoreCase) || rest.Equals("Mouse5", StringComparison.OrdinalIgnoreCase))
                sb.Append("Mouse 5");
            else if (rest.Equals("MiddleClick", StringComparison.OrdinalIgnoreCase) || rest.Equals("Middle", StringComparison.OrdinalIgnoreCase))
                sb.Append("Middle Click");
            else if (rest.Equals("RightClick", StringComparison.OrdinalIgnoreCase) || rest.Equals("Right", StringComparison.OrdinalIgnoreCase))
                sb.Append("Right Click");
            else if (rest.Equals("LeftClick", StringComparison.OrdinalIgnoreCase) || rest.Equals("Left", StringComparison.OrdinalIgnoreCase))
                sb.Append("Left Click");
            else if (rest.Equals("Space", StringComparison.OrdinalIgnoreCase))
                sb.Append("Space");
            else if (!string.IsNullOrEmpty(rest))
                sb.Append(rest);

            return sb.ToString().TrimEnd(' ', '+');
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore pure modifier keys while being pressed
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) sb.Append("Win+");

            string keyName = key switch
            {
                Key.Space => "Space",
                Key.OemTilde => "Tilde",
                Key.F1 => "F1",
                Key.F2 => "F2",
                Key.F3 => "F3",
                Key.F4 => "F4",
                Key.F5 => "F5",
                Key.F6 => "F6",
                Key.F7 => "F7",
                Key.F8 => "F8",
                Key.F9 => "F9",
                Key.F10 => "F10",
                Key.F11 => "F11",
                Key.F12 => "F12",
                _ => key.ToString()
            };

            sb.Append(keyName);
            string constructed = sb.ToString();

            if (constructed == "Alt+Space") constructed = "AltSpace";
            else if (constructed == "Ctrl+Space") constructed = "CtrlSpace";

            SetShortcut(constructed);
            e.Handled = true;
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

                string sc = (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + "MiddleClick";
                SetShortcut(sc);
                e.Handled = true;
            }
            else if (e.XButton1 == MouseButtonState.Pressed)
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

                string sc = (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + "XButton1";
                SetShortcut(sc);
                e.Handled = true;
            }
            else if (e.XButton2 == MouseButtonState.Pressed)
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

                string sc = (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + "XButton2";
                SetShortcut(sc);
                e.Handled = true;
            }
            else if (e.RightButton == MouseButtonState.Pressed && Keyboard.Modifiers != ModifierKeys.None)
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

                string sc = (ctrl ? "Ctrl" : "") + (shift ? "Shift" : "") + (alt ? "Alt" : "") + "RightClick";
                SetShortcut(sc);
                e.Handled = true;
            }
        }

        private void QuickMouse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                SetShortcut(tag);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SelectedShortcut))
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
