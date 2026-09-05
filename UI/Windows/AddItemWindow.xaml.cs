using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RadialLauncher.Data;
using RadialLauncher.Models;

namespace RadialLauncher.UI.Windows
{
    public partial class AddItemWindow : Window
    {
        public LauncherItem? CreatedItem { get; private set; }
        private readonly DatabaseManager _dbManager = new();

        public AddItemWindow()
        {
            InitializeComponent();
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath)) this.Icon = Services.IconExtractor.GetIconForFile(iconPath);
            }
            catch { }
            LoadCategories();
            ActionSelectComboBox.ItemsSource = Services.SystemActionService.AvailableActions;
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetTextBox == null || BrowseButton == null || ActionSelectComboBox == null) return;
            if (TypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string type = cbi.Content.ToString()!;
                if (type == "ACTION")
                {
                    TargetTextBox.Visibility = Visibility.Collapsed;
                    BrowseButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Visible;
                }
                else if (type == "SUBMENU")
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    TargetTextBox.Text = "SUBMENU";
                    BrowseButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    BrowseButton.Visibility = Visibility.Visible;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ActionSelectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionSelectComboBox.SelectedItem is Services.SystemActionInfo action)
            {
                TargetTextBox.Text = action.ActionKey;
                if (string.IsNullOrWhiteSpace(NameTextBox.Text))
                {
                    NameTextBox.Text = action.DisplayName;
                }
            }
        }

        private void LoadCategories()
        {
            var categories = _dbManager.GetAllCategories();
            CategoryComboBox.ItemsSource = categories;
            if (categories.Count > 0)
            {
                CategoryComboBox.SelectedIndex = 0;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Uygulama veya Dosya Seç",
                Filter = "Tüm Desteklenenler|*.exe;*.bat;*.cmd;*.lnk;*.*|Uygulamalar (*.exe)|*.exe|Tüm Dosyalar (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TargetTextBox.Text = openFileDialog.FileName;
                if (string.IsNullOrWhiteSpace(NameTextBox.Text))
                {
                    NameTextBox.Text = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                }
                TypeComboBox.SelectedIndex = 0; // EXE
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || string.IsNullOrWhiteSpace(TargetTextBox.Text))
            {
                MessageBox.Show("Lütfen İsim ve Hedef alanlarını doldurunuz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int categoryId = 1;
            if (CategoryComboBox.SelectedValue is int id)
            {
                categoryId = id;
            }

            string itemType = "EXE";
            if (TypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                itemType = cbi.Content.ToString()!;
            }

            CreatedItem = new LauncherItem
            {
                Name = NameTextBox.Text.Trim(),
                Target = TargetTextBox.Text.Trim(),
                Arguments = ArgsTextBox.Text.Trim(),
                Type = itemType,
                CategoryId = categoryId,
                IsFavorite = FavoriteCheckBox.IsChecked == true
            };

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
