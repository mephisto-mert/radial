using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RadialLauncher.Services.Localization;

namespace RadialLauncher.UI.Windows
{
    public partial class CategoryRenameDialog : Window
    {
        public string NewCategoryName { get; private set; } = string.Empty;
        private readonly string _initialName;
        private readonly System.Collections.Generic.HashSet<string> _existingNames;

        public CategoryRenameDialog(string currentName, System.Collections.Generic.IEnumerable<string>? existingNames = null)
        {
            InitializeComponent();
            _initialName = currentName ?? string.Empty;
            _existingNames = new System.Collections.Generic.HashSet<string>(
                (existingNames ?? System.Array.Empty<string>())
                    .Where(n => !string.Equals(n, _initialName, StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);

            CategoryNameTextBox.Text = _initialName;
            ApplyLocalization();

            Loaded += (s, e) =>
            {
                CategoryNameTextBox.Focus();
                CategoryNameTextBox.SelectAll();
            };
        }

        public void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;
            Title = loc.GetString("Cat_Rename_Dialog_Title", "Rename Category — Radial Launcher");
            if (TxtHeaderTitle != null) TxtHeaderTitle.Text = loc.GetString("Cat_Rename_Header", "🏷️ Rename Category");
            if (TxtHeaderSub != null) TxtHeaderSub.Text = loc.GetString("Cat_Rename_Sub", "Enter a new display name for the selected category.");
            if (TxtCategoryNameLabel != null) TxtCategoryNameLabel.Text = loc.GetString("Cat_Name_Label", "Category Name:");
            if (BtnCancel != null) BtnCancel.Content = loc.GetString("Cancel", "Cancel");
            if (BtnSave != null) BtnSave.Content = loc.GetString("Save", "Save");
        }

        private void CategoryNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (ValidationWarningText != null) ValidationWarningText.Text = string.Empty;
        }

        private void CategoryNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                BtnSave_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            string trimmed = CategoryNameTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (ValidationWarningText != null)
                    ValidationWarningText.Text = loc.GetString("Cat_Err_Empty", "Category name cannot be empty.");
                return;
            }

            if (trimmed.Length > 50)
            {
                if (ValidationWarningText != null)
                    ValidationWarningText.Text = loc.GetString("Cat_Err_TooLong", "Category name cannot exceed 50 characters.");
                return;
            }

            if (_existingNames != null && _existingNames.Contains(trimmed))
            {
                if (ValidationWarningText != null)
                    ValidationWarningText.Text = loc.GetString("Cat_Err_Duplicate", "A category with this name already exists.");
                return;
            }

            NewCategoryName = trimmed;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
