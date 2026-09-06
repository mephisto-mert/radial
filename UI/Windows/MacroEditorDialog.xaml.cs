using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RadialLauncher.Models;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Localization;
using Serilog;

namespace RadialLauncher.UI.Windows
{
    public partial class MacroEditorDialog : Window
    {
        public ObservableCollection<MacroStep> Steps { get; } = new();
        private readonly ISystemActionService _actionService;

        public MacroEditorDialog(string? initialJson = null, ISystemActionService? actionService = null)
        {
            _actionService = actionService ?? SystemActionService.Instance;
            InitializeComponent();

            StepsListBox.ItemsSource = Steps;

            if (!string.IsNullOrWhiteSpace(initialJson))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<List<MacroStep>>(initialJson);
                    if (loaded != null)
                    {
                        foreach (var s in loaded) Steps.Add(s);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load initial macro steps JSON");
                }
            }

            NewStepActionComboBox.ItemsSource = _actionService.GetAvailableActions();
            ApplyLocalization();

            LocalizationService.Instance.OnLanguageChanged += () => Dispatcher.Invoke(ApplyLocalization);
        }

        public void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;
            Title = loc.GetString("Macro_Title", "Macro Steps Editor");
            if (TxtHeader != null) TxtHeader.Text = loc.GetString("Macro_Header", "⚡ Macro Sequential Action List");
            if (MoveUpBtn != null) MoveUpBtn.Content = loc.GetString("Macro_MoveUp", "⬆️ Move Up");
            if (MoveDownBtn != null) MoveDownBtn.Content = loc.GetString("Macro_MoveDown", "⬇️ Move Down");
            if (DeleteStepBtn != null) DeleteStepBtn.Content = loc.GetString("Macro_Delete", "🗑️ Delete");
            if (TxtNewStepHeader != null) TxtNewStepHeader.Text = loc.GetString("Macro_NewStep", "➕ Add New Step");
            if (TxtStepNameLabel != null) TxtStepNameLabel.Text = loc.GetString("Macro_StepName", "Step Name:");
            if (TxtStepTypeLabel != null) TxtStepTypeLabel.Text = loc.GetString("Macro_StepType", "Type:");
            if (TxtStepTargetLabel != null) TxtStepTargetLabel.Text = loc.GetString("Macro_StepTarget", "Target:");
            if (NewStepBrowseBtn != null) NewStepBrowseBtn.Content = loc.GetString("Browse", "Browse...");
            if (TxtStepArgsLabel != null) TxtStepArgsLabel.Text = loc.GetString("Macro_StepArgs", "Arguments:");
            if (TxtStepDelayLabel != null) TxtStepDelayLabel.Text = loc.GetString("Macro_StepDelay", "Delay (ms):");
            if (AddStepBtn != null) AddStepBtn.Content = loc.GetString("Macro_Add", "Add");
            if (SaveBtn != null) SaveBtn.Content = loc.GetString("Save", "Save");
            if (CancelBtn != null) CancelBtn.Content = loc.GetString("Cancel", "Cancel");
        }

        public string GetSerializedSteps()
        {
            return JsonSerializer.Serialize(new List<MacroStep>(Steps), new JsonSerializerOptions { WriteIndented = false });
        }

        private void NewStepTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NewStepTargetTextBox == null || NewStepActionComboBox == null || NewStepBrowseBtn == null) return;

            if (NewStepTypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string t = cbi.Content.ToString()!;
                if (t == "ACTION")
                {
                    NewStepTargetTextBox.Visibility = Visibility.Collapsed;
                    NewStepBrowseBtn.Visibility = Visibility.Collapsed;
                    NewStepActionComboBox.Visibility = Visibility.Visible;
                }
                else if (t == "URL")
                {
                    NewStepTargetTextBox.Visibility = Visibility.Visible;
                    NewStepBrowseBtn.Visibility = Visibility.Collapsed;
                    NewStepActionComboBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NewStepTargetTextBox.Visibility = Visibility.Visible;
                    NewStepBrowseBtn.Visibility = Visibility.Visible;
                    NewStepActionComboBox.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void NewStepActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NewStepActionComboBox.SelectedItem is SystemActionInfo act)
            {
                NewStepTargetTextBox.Text = act.ActionKey;
                if (string.IsNullOrWhiteSpace(NewStepNameTextBox.Text))
                {
                    NewStepNameTextBox.Text = act.DisplayName;
                }
            }
        }

        private void NewStepBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            var dlg = new OpenFileDialog
            {
                Title = loc.GetString("Macro_Browse_Title", "Select Application/File for Macro Step"),
                Filter = "Applications (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                NewStepTargetTextBox.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(NewStepNameTextBox.Text))
                {
                    NewStepNameTextBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
        }

        private void AddStepBtn_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationService.Instance;
            string name = NewStepNameTextBox.Text.Trim();
            string target = NewStepTargetTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show(
                    loc.GetString("Macro_Validation_Error", "Step Name and Target are required."),
                    loc.GetString("Warning", "Warning"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string type = "EXE";
            if (NewStepTypeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                type = cbi.Content.ToString()!;
            }

            int delay = 200;
            int.TryParse(NewStepDelayTextBox.Text.Trim(), out delay);
            if (delay < 0) delay = 0;

            var step = new MacroStep
            {
                Name = name,
                Target = target,
                Arguments = NewStepArgsTextBox.Text.Trim(),
                Type = type,
                DelayMs = delay
            };

            Steps.Add(step);

            // Reset inputs
            NewStepNameTextBox.Text = string.Empty;
            NewStepTargetTextBox.Text = string.Empty;
            NewStepArgsTextBox.Text = string.Empty;
            NewStepDelayTextBox.Text = "200";
        }

        private void MoveUpBtn_Click(object sender, RoutedEventArgs e)
        {
            int idx = StepsListBox.SelectedIndex;
            if (idx > 0)
            {
                var item = Steps[idx];
                Steps.RemoveAt(idx);
                Steps.Insert(idx - 1, item);
                StepsListBox.SelectedIndex = idx - 1;
            }
        }

        private void MoveDownBtn_Click(object sender, RoutedEventArgs e)
        {
            int idx = StepsListBox.SelectedIndex;
            if (idx >= 0 && idx < Steps.Count - 1)
            {
                var item = Steps[idx];
                Steps.RemoveAt(idx);
                Steps.Insert(idx + 1, item);
                StepsListBox.SelectedIndex = idx + 1;
            }
        }

        private void DeleteStepBtn_Click(object sender, RoutedEventArgs e)
        {
            int idx = StepsListBox.SelectedIndex;
            if (idx >= 0 && idx < Steps.Count)
            {
                Steps.RemoveAt(idx);
            }
        }

        private void StepsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
