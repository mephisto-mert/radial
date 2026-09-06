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
            ApplyLocalization();

            _onLanguageChangedHandler = () => Dispatcher.Invoke(ApplyLocalization);
            LocalizationService.Instance.OnLanguageChanged += _onLanguageChangedHandler;
            Closed += (s, e) => LocalizationService.Instance.OnLanguageChanged -= _onLanguageChangedHandler;
        }

        private readonly Action _onLanguageChangedHandler;

        public void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;
            Title = loc.GetString("EditItem_Title", "Edit Item");
            if (TxtNameLabel != null) TxtNameLabel.Text = loc.GetString("Item_Name", "Name:");
            if (TxtTypeLabel != null) TxtTypeLabel.Text = loc.GetString("Item_Type", "Type:");
            if (TxtTargetLabel != null) TxtTargetLabel.Text = loc.GetString("Item_Target", "Target / Link:");
            if (BrowseTargetButton != null) BrowseTargetButton.Content = loc.GetString("Browse", "Browse...");
            if (TxtArgsLabel != null) TxtArgsLabel.Text = loc.GetString("Item_Args", "Arguments:");
            if (TxtCategoryLabel != null) TxtCategoryLabel.Text = loc.GetString("Item_Category", "Category:");
            if (TxtIconLabel != null) TxtIconLabel.Text = loc.GetString("Item_Icon", "Custom Icon:");
            if (IconTextBox != null) IconTextBox.ToolTip = loc.GetString("Item_Icon_Tooltip", "Optional custom .ico, .exe, or .png file path");
            if (BrowseIconButton != null) BrowseIconButton.Content = loc.GetString("Browse_Icon", "Select Icon");
            if (FavoriteCheckBox != null) FavoriteCheckBox.Content = loc.GetString("Item_Favorite_Edit", "Mark as Favorite (⭐)");
            if (SaveButton != null) SaveButton.Content = loc.GetString("Save", "Save");
            if (CancelButton != null) CancelButton.Content = loc.GetString("Cancel", "Cancel");

            if (CategoryComboBox != null)
            {
                int currentCatId = (CategoryComboBox.SelectedValue is int id) ? id : Item.CategoryId;
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
            if (TargetTextBox == null || BrowseTargetButton == null || ActionSelectComboBox == null || EditMacroBtn == null) return;
            if (TypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string type = cbi.Content.ToString()!;
                if (type == "ACTION")
                {
                    TargetTextBox.Visibility = Visibility.Collapsed;
                    BrowseTargetButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Visible;
                    EditMacroBtn.Visibility = Visibility.Collapsed;
                }
                else if (type == "MACRO")
                {
                    TargetTextBox.Visibility = Visibility.Collapsed;
                    BrowseTargetButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                    EditMacroBtn.Visibility = Visibility.Visible;
                }
                else if (type == "SUBMENU")
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    TargetTextBox.Text = "SUBMENU";
                    BrowseTargetButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                    EditMacroBtn.Visibility = Visibility.Collapsed;
                }
                else if (type == "URL")
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    BrowseTargetButton.Visibility = Visibility.Collapsed;
                    ActionSelectComboBox.Visibility = Visibility.Collapsed;
                    EditMacroBtn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TargetTextBox.Visibility = Visibility.Visible;
                    BrowseTargetButton.Visibility = Visibility.Visible;
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

            if (string.Equals(Item.Type, "MACRO", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Item.Target))
            {
                try
                {
                    var steps = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<MacroStep>>(Item.Target);
                    int cnt = steps?.Count ?? 0;
                    string fmt = LocalizationService.Instance.GetString("Macro_Defined_Steps", "⚡ Macro ({0} Steps Defined)");
                    EditMacroBtn.Content = string.Format(fmt, cnt);
                }
                catch (System.Exception) { }
            }

            CategoryComboBox.SelectedValue = Item.CategoryId;
        }

        private void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            var dialog = new OpenFileDialog
            {
                Title = loc.GetString("Browse_App_Title", "Select Application or File"),
                Filter = "All Supported|*.exe;*.bat;*.cmd;*.lnk;*.*|Applications (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                TargetTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseIconButton_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            var dialog = new OpenFileDialog
            {
                Title = loc.GetString("Browse_Icon_Title", "Select Icon File"),
                Filter = "Icons & Images (*.ico;*.exe;*.png;*.jpg)|*.ico;*.exe;*.png;*.jpg|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                IconTextBox.Text = dialog.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || string.IsNullOrWhiteSpace(TargetTextBox.Text))
            {
                MessageBox.Show(
                    loc.GetString("Validation_No_Empty", "Name and Target fields cannot be empty."),
                    loc.GetString("Warning", "Warning"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
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

            Item.Name = name;
            Item.Target = target;
            Item.Arguments = ArgsTextBox.Text.Trim();
            Item.IconPath = IconTextBox.Text.Trim();
            Item.IsFavorite = FavoriteCheckBox.IsChecked == true;
            Item.Type = itemType;
            Item.IsUserAdded = true;

            if (CategoryComboBox.SelectedValue is int catId)
            {
                Item.CategoryId = catId;
            }

            _dbManager.UpdateItem(Item);

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
