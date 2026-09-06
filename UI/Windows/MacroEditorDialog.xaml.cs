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
            var dlg = new OpenFileDialog
            {
                Title = "Makro Adımı için Program/Dosya Seç",
                Filter = "Uygulamalar (*.exe)|*.exe|Tüm Dosyalar (*.*)|*.*"
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
            string name = NewStepNameTextBox.Text.Trim();
            string target = NewStepTargetTextBox.Text.Trim();
            string type = (NewStepTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EXE";
            string args = NewStepArgsTextBox.Text.Trim();
            int delay = int.TryParse(NewStepDelayTextBox.Text, out int d) ? d : 200;

            if (type == "ACTION" && NewStepActionComboBox.SelectedItem is SystemActionInfo act)
            {
                target = act.ActionKey;
                if (string.IsNullOrEmpty(name)) name = act.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = !string.IsNullOrEmpty(target) ? target : "Adım " + (Steps.Count + 1);
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show("Lütfen adım hedefini giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Steps.Add(new MacroStep
            {
                Name = name,
                Type = type,
                Target = target,
                Arguments = args,
                DelayMs = delay
            });

            // Clear inputs
            NewStepNameTextBox.Clear();
            NewStepTargetTextBox.Clear();
            NewStepArgsTextBox.Clear();
            NewStepDelayTextBox.Text = "200";
        }

        private void DeleteStepBtn_Click(object sender, RoutedEventArgs e)
        {
            if (StepsListBox.SelectedItem is MacroStep step)
            {
                Steps.Remove(step);
            }
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

        private void StepsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = StepsListBox.SelectedItem != null;
            DeleteStepBtn.IsEnabled = hasSelection;
            MoveUpBtn.IsEnabled = hasSelection && StepsListBox.SelectedIndex > 0;
            MoveDownBtn.IsEnabled = hasSelection && StepsListBox.SelectedIndex < Steps.Count - 1;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Steps.Count == 0)
            {
                MessageBox.Show("Makroda en az bir adım olmalıdır.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
