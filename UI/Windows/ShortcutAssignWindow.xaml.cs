using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RadialLauncher.UI.Windows
{
    public partial class ShortcutAssignWindow : Window
    {
        public string SelectedShortcut { get; private set; } = string.Empty;
        public string FriendlyName { get; private set; } = string.Empty;

        public ShortcutAssignWindow(string currentShortcut = "MiddleClick")
        {
            InitializeComponent();
            SetShortcut(currentShortcut);
        }

        private void SetShortcut(string internalCode)
        {
            if (string.IsNullOrWhiteSpace(internalCode)) return;

            string clean = internalCode.Trim();
            string lower = clean.ToLowerInvariant();

            // Validate against reserved system hotkeys
            if (lower == "alt+f4" || lower == "altf4" || lower == "ctrl+alt+del" || lower == "win+l")
            {
                ValidationWarningText.Text = "⚠️ Bu kısayol Windows sistem işlevleri için ayrılmıştır.";
                SaveButton.IsEnabled = false;
                return;
            }

            ValidationWarningText.Text = string.Empty;
            SelectedShortcut = clean;
            FriendlyName = ToFriendlyName(clean);

            ShortcutDisplayText.Text = FriendlyName;
            ShortcutRawText.Text = $"Internal Kod: {SelectedShortcut}";
            SaveButton.IsEnabled = true;
        }

        public static string ToFriendlyName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "Kısayol Yok";

            return code switch
            {
                "MiddleClick" => "🖱️ Orta Tuş (Fare Tekerleği)",
                "XButton1" => "🖱️ Fare 4 (Geri Tuşu)",
                "XButton2" => "🖱️ Fare 5 (İleri Tuşu)",
                "Ctrl+XButton1" => "🖱️ Ctrl + Fare 4",
                "Ctrl+XButton2" => "🖱️ Ctrl + Fare 5",
                "AltRightClick" => "🖱️ Alt + Sağ Tık",
                "ShiftRightClick" => "🖱️ Shift + Sağ Tık",
                "CtrlRightClick" => "🖱️ Ctrl + Sağ Tık",
                "AltSpace" => "⌨️ Alt + Boşluk (Alt+Space)",
                "CtrlSpace" => "⌨️ Ctrl + Boşluk (Ctrl+Space)",
                "F4" => "⌨️ F4 Tuşu",
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
                sb.Append("Fare 4");
            else if (rest.Equals("XButton2", StringComparison.OrdinalIgnoreCase) || rest.Equals("Mouse5", StringComparison.OrdinalIgnoreCase))
                sb.Append("Fare 5");
            else if (rest.Equals("MiddleClick", StringComparison.OrdinalIgnoreCase) || rest.Equals("Middle", StringComparison.OrdinalIgnoreCase))
                sb.Append("Orta Tuş");
            else if (rest.Equals("RightClick", StringComparison.OrdinalIgnoreCase) || rest.Equals("Right", StringComparison.OrdinalIgnoreCase))
                sb.Append("Sağ Tık");
            else if (rest.Equals("LeftClick", StringComparison.OrdinalIgnoreCase) || rest.Equals("Left", StringComparison.OrdinalIgnoreCase))
                sb.Append("Sol Tık");
            else if (rest.Equals("Space", StringComparison.OrdinalIgnoreCase))
                sb.Append("Boşluk (Space)");
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

            // Check if Alt+Space or Ctrl+Space (normalize without plus for legacy compatibility if needed)
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
            // Only capture if clicking with middle / XButton / right with modifier
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
