using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using RadialLauncher.Data;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Icons;
using Serilog;

namespace RadialLauncher.UI.Windows
{
    public partial class EditItemWindow : Window
    {
        public LauncherItem Item { get; private set; }
        private readonly IDatabaseManager _dbManager;
        private readonly IIconExtractor _iconExtractor;
        private readonly ISystemActionService _actionService;

        public EditItemWindow(LauncherItem item) : this(
            item,
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDatabaseManager>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IIconExtractor>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."),
            App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISystemActionService>(App.ServiceProvider) 
                : throw new InvalidOperationException("App.ServiceProvider is not initialized."))
        {
        }

        public EditItemWindow(
            LauncherItem item,
            IDatabaseManager dbManager,
            IIconExtractor iconExtractor,
            ISystemActionService actionService)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            _iconExtractor = iconExtractor ?? throw new ArgumentNullException(nameof(iconExtractor));
            _actionService = actionService ?? throw new ArgumentNullException(nameof(actionService));

            InitializeComponent();

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = _iconExtractor.GetIconForFile(iconPath);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not set EditItemWindow icon");
            }

            LoadCategories();
            ActionSelectComboBox.ItemsSource = _actionService.GetAvailableActions();
            PopulateData();
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string type = cbi.Content.ToString()!;
                if (type == "ACTION")
                {
                    TargetTextBox.Visibility = Visibility.Collapsed;
                    BrowseTargetButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Visible;
                }
                else if (type == "SUBMENU")
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    TargetTextBox.Text = "SUBMENU";
                    BrowseTargetButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    BrowseTargetButton.Visibility = Visibility.Visible;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ActionSelectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionSelectComboBox.SelectedItem is SystemActionInfo action)
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
        }

        private void PopulateData()
        {
            NameTextBox.Text = Item.Name;
            TargetTextBox.Text = Item.Target;
            ArgsTextBox.Text = Item.Arguments;
            IconTextBox.Text = Item.IconPath;
            FavoriteCheckBox.IsChecked = Item.IsFavorite;

            foreach (ComboBoxItem cbi in TypeComboBox.Items)
            {
                if (cbi.Content.ToString()!.Equals(Item.Type, StringComparison.OrdinalIgnoreCase))
                {
                    TypeComboBox.SelectedItem = cbi;
                    break;
                }
            }

            CategoryComboBox.SelectedValue = Item.CategoryId;
        }

        private void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Hedef Dosya veya Uygulama Seç",
                Filter = "Tüm Desteklenenler|*.exe;*.bat;*.cmd;*.lnk;*.*|Uygulamalar (*.exe)|*.exe|Tüm Dosyalar (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                TargetTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseIconButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "İkon Dosyası Seç",
                Filter = "İkonlar ve Resimler (*.ico;*.exe;*.png;*.jpg)|*.ico;*.exe;*.png;*.jpg|Tüm Dosyalar (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                IconTextBox.Text = dialog.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || string.IsNullOrWhiteSpace(TargetTextBox.Text))
            {
                MessageBox.Show("İsim ve Hedef alanları boş bırakılamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Item.Name = NameTextBox.Text.Trim();
            Item.Target = TargetTextBox.Text.Trim();
            Item.Arguments = ArgsTextBox.Text.Trim();
            Item.IconPath = IconTextBox.Text.Trim();
            Item.IsFavorite = FavoriteCheckBox.IsChecked == true;

            if (TypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                Item.Type = cbi.Content.ToString()!;
            }

            if (CategoryComboBox.SelectedValue is int catId)
            {
                Item.CategoryId = catId;
            }

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
