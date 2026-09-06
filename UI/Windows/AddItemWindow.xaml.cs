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
using RadialLauncher.Services.Localization;
using Serilog;

namespace RadialLauncher.UI.Windows
{
    public partial class AddItemWindow : Window
    {
        public LauncherItem? CreatedItem { get; private set; }
        private readonly IDatabaseManager _dbManager;
        private readonly IIconExtractor _iconExtractor;
        private readonly ISystemActionService _actionService;

        public AddItemWindow() : this(
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

        public AddItemWindow(
            IDatabaseManager dbManager,
            IIconExtractor iconExtractor,
            ISystemActionService actionService)
        {
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
                Log.Debug(ex, "Could not set AddItemWindow icon");
            }

            LoadCategories();
            ActionSelectComboBox.ItemsSource = _actionService.GetAvailableActions();
            ApplyLocalization();

            _onLanguageChangedHandler = () => Dispatcher.Invoke(ApplyLocalization);
            LocalizationService.Instance.OnLanguageChanged += _onLanguageChangedHandler;
            Closed += (s, e) => LocalizationService.Instance.OnLanguageChanged -= _onLanguageChangedHandler;
        }

        private readonly Action _onLanguageChangedHandler;

        public void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;
            Title = loc.GetString("AddItem_Title", "Add New Item");
            if (TxtNameLabel != null) TxtNameLabel.Text = loc.GetString("Item_Name", "Name:");
            if (TxtTypeLabel != null) TxtTypeLabel.Text = loc.GetString("Item_Type", "Type:");
            if (TxtTargetLabel != null) TxtTargetLabel.Text = loc.GetString("Item_Target", "Target:");
            if (TargetTextBox != null) TargetTextBox.ToolTip = loc.GetString("Item_Target_Tooltip", "Executable path, website URL, file or folder path");
            if (EditMacroBtn != null) EditMacroBtn.Content = loc.GetString("Edit_Macro", "⚡ Edit Macro Steps...");
            if (BrowseButton != null) BrowseButton.Content = loc.GetString("Browse", "Browse...");
            if (TxtArgsLabel != null) TxtArgsLabel.Text = loc.GetString("Item_Args", "Arguments:");
            if (ArgsTextBox != null) ArgsTextBox.ToolTip = loc.GetString("Item_Args_Tooltip", "Optional command line arguments");
            if (TxtCategoryLabel != null) TxtCategoryLabel.Text = loc.GetString("Item_Category", "Category:");
            if (FavoriteCheckBox != null) FavoriteCheckBox.Content = loc.GetString("Item_Favorite_Add", "Add to Favorites (Inner Ring ⭐)");
            if (SaveButton != null) SaveButton.Content = loc.GetString("Save", "Save");
            if (CancelButton != null) CancelButton.Content = loc.GetString("Cancel", "Cancel");

            if (CategoryComboBox != null)
            {
                int currentCatId = (CategoryComboBox.SelectedValue is int id) ? id : 1;
                LoadCategories();
                CategoryComboBox.SelectedValue = currentCatId;
            }

            if (ActionSelectComboBox != null)
            {
                string? selectedAction = (ActionSelectComboBox.SelectedItem as SystemActionInfo)?.ActionKey;
                ActionSelectComboBox.ItemsSource = null;
                var actions = _actionService.GetAvailableActions();
                ActionSelectComboBox.ItemsSource = actions;
                if (!string.IsNullOrEmpty(selectedAction))
                {
                    ActionSelectComboBox.SelectedItem = actions.Find(a => a.ActionKey == selectedAction);
                }
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetTextBox == null || BrowseButton == null || ActionSelectComboBox == null || EditMacroBtn == null) return;
            if (TypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string type = cbi.Content.ToString()!;
                if (type == "ACTION")
                {
                    TargetTextBox.Visibility = Visibility.Collapsed;
                    BrowseButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Visible;
                    EditMacroBtn.Visibility = Visibility.Collapsed;
                }
                else if (type == "MACRO")
                {
                    TargetTextBox.Visibility = Visibility.Collapsed;
                    BrowseButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                    EditMacroBtn.Visibility = Visibility.Visible;
                    if (string.IsNullOrWhiteSpace(TargetTextBox.Text))
                    {
                        TargetTextBox.Text = "[]";
                    }
                }
                else if (type == "SUBMENU")
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    TargetTextBox.Text = "SUBMENU";
                    BrowseButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                    EditMacroBtn.Visibility = Visibility.Collapsed;
                }
                else if (type == "URL")
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    BrowseButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                    EditMacroBtn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    BrowseButton.Visibility = Visibility.Visible;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                    EditMacroBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void EditMacroBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MacroEditorDialog(TargetTextBox.Text, _actionService)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true)
            {
                TargetTextBox.Text = dlg.GetSerializedSteps();
                string fmt = LocalizationService.Instance.GetString("Macro_Defined_Steps", "⚡ Macro ({0} Steps Defined)");
                EditMacroBtn.Content = string.Format(fmt, dlg.Steps.Count);
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
            if (categories.Count > 0)
            {
                CategoryComboBox.SelectedIndex = 0;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            var openFileDialog = new OpenFileDialog
            {
                Title = loc.GetString("Browse_App_Title", "Select Application or File"),
                Filter = "All Supported|*.exe;*.bat;*.cmd;*.lnk;*.*|Applications (*.exe)|*.exe|All Files (*.*)|*.*"
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
            var loc = LocalizationService.Instance;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || string.IsNullOrWhiteSpace(TargetTextBox.Text))
            {
                MessageBox.Show(
                    loc.GetString("Validation_Fill_Required", "Please fill in both Name and Target fields."),
                    loc.GetString("Warning", "Warning"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int categoryId = 1;
            if (CategoryComboBox.SelectedValue is int id)
            {
                categoryId = id;
            }

            string name = NameTextBox.Text.Trim();
            string target = TargetTextBox.Text.Trim();

            string itemType = "EXE";
            if (TypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                itemType = cbi.Content.ToString()!;
            }

            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
                itemType == "URL")
            {
                itemType = "URL";
                if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    target = "https://" + target;
                }
            }

            CreatedItem = new LauncherItem
            {
                Name = name,
                Target = target,
                Arguments = ArgsTextBox.Text.Trim(),
                Type = itemType,
                CategoryId = categoryId,
                IsFavorite = FavoriteCheckBox.IsChecked == true,
                IsUserAdded = true
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
