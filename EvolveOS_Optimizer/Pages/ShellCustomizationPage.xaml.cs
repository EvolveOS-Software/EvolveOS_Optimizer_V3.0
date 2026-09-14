// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ShellCustomizationPage : Page
    {
        private bool _isInitialized = false;

        public ShellCustomizationPage()
        {
            this.InitializeComponent();
            LoadSavedSettings();
            _isInitialized = true;
        }

        private async void LoadSavedSettings()
        {
            MasterToggle.IsOn = SettingsEngine.Shell_MasterEnabled;
            StartMenuToggle.IsOn = SettingsEngine.Shell_StartMenuEnabled;
            TaskbarToggle.IsOn = SettingsEngine.Shell_TaskbarEnabled;

            SelectComboBoxItemByTag(StartMenuStyleCombo, SettingsEngine.Shell_StartMenuStyle);
            SelectComboBoxItemByTag(TaskbarStyleCombo, SettingsEngine.Shell_TaskbarStyle);

            UpdateChildControlStates(MasterToggle.IsOn);

            if (MasterToggle.IsOn && Process.GetProcessesByName("EvolveOS_ShellEnhancer").Length == 0)
            {
                await ShellEnhancerController.StartEnhancerAsync();
            }
        }

        private void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            var item = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(x => x.Tag?.ToString() == tag);
            if (item != null)
            {
                comboBox.SelectedItem = item;
            }
            else if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void UpdateChildControlStates(bool isMasterEnabled)
        {
            StartMenuToggle.IsEnabled = isMasterEnabled;
            StartMenuStyleCombo.IsEnabled = isMasterEnabled;
            TaskbarToggle.IsEnabled = isMasterEnabled;
            TaskbarStyleCombo.IsEnabled = isMasterEnabled;
        }

        #region Event Handlers
        private async void MasterToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            bool isEnabled = MasterToggle.IsOn;
            SettingsEngine.Shell_MasterEnabled = isEnabled;

            UpdateChildControlStates(isEnabled);

            if (isEnabled)
            {
                await ShellEnhancerController.StartEnhancerAsync();

                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_Enable:{StartMenuToggle.IsOn}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_Style:{SettingsEngine.Shell_StartMenuStyle}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Enable:{TaskbarToggle.IsOn}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Style:{SettingsEngine.Shell_TaskbarStyle}");
            }
            else
            {
                ShellEnhancerController.StopEnhancer();
            }
        }

        private void SettingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || sender is not ToggleSwitch toggle) return;

            string commandTag = toggle.Tag?.ToString() ?? string.Empty;

            if (commandTag == "StartMenu_Enable")
                SettingsEngine.Shell_StartMenuEnabled = toggle.IsOn;
            else if (commandTag == "Taskbar_Enable")
                SettingsEngine.Shell_TaskbarEnabled = toggle.IsOn;

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"{commandTag}:{toggle.IsOn}");
            }
        }

        private void StartMenuStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || StartMenuStyleCombo.SelectedItem is not ComboBoxItem selectedItem) return;

            string style = selectedItem.Tag?.ToString() ?? "Standard";
            SettingsEngine.Shell_StartMenuStyle = style;

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_Style:{style}");
            }
        }

        private void SettingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem selectedItem) return;

            string commandTag = comboBox.Tag?.ToString() ?? string.Empty;
            string style = selectedItem.Tag?.ToString() ?? "Standard";

            if (commandTag == "Taskbar_Style")
                SettingsEngine.Shell_TaskbarStyle = style;

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"{commandTag}:{style}");
            }
        }
        #endregion
    }
}