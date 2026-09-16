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
            PreviewButtonsToggle.IsOn = SettingsEngine.Shell_TaskbarPreviewButtons;
            ClockSecondsToggle.IsOn = SettingsEngine.Shell_TaskbarClockSeconds;
            ShowUnpinnedToggle.IsOn = SettingsEngine.Shell_TaskbarShowUnpinned;

            SelectComboBoxItemByTag(StartMenuStyleCombo, SettingsEngine.Shell_StartMenuStyle);
            SelectComboBoxItemByTag(TaskbarStyleCombo, SettingsEngine.Shell_TaskbarStyle);
            SelectComboBoxItemByTag(TaskbarAlignmentCombo, SettingsEngine.Shell_TaskbarAlignment ?? "Split");
            SelectComboBoxItemByTag(UnpinnedModeCombo, SettingsEngine.Shell_TaskbarUnpinnedMode);

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
            TaskbarToggle.IsEnabled = isMasterEnabled;

            StartMenuStyleCombo.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;

            TaskbarStyleCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarAlignmentCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            PreviewButtonsToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            ClockSecondsToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            ShowUnpinnedToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            UnpinnedModeCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn && ShowUnpinnedToggle.IsOn;
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
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Alignment:{SettingsEngine.Shell_TaskbarAlignment}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewButtons:{SettingsEngine.Shell_TaskbarPreviewButtons}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_ClockSeconds:{SettingsEngine.Shell_TaskbarClockSeconds}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_ShowUnpinned:{SettingsEngine.Shell_TaskbarShowUnpinned}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_UnpinnedMode:{SettingsEngine.Shell_TaskbarUnpinnedMode}");
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
            else if (commandTag == "Taskbar_PreviewButtons")
                SettingsEngine.Shell_TaskbarPreviewButtons = toggle.IsOn;
            else if (commandTag == "Taskbar_ClockSeconds")
                SettingsEngine.Shell_TaskbarClockSeconds = toggle.IsOn;
            else if (commandTag == "Taskbar_ShowUnpinned")
            {
                SettingsEngine.Shell_TaskbarShowUnpinned = toggle.IsOn;
                UnpinnedModeCombo.IsEnabled = MasterToggle.IsOn && TaskbarToggle.IsOn && toggle.IsOn;
            }

            UpdateChildControlStates(MasterToggle.IsOn);

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

            else if (commandTag == "Taskbar_Alignment")
                SettingsEngine.Shell_TaskbarAlignment = style;

            else if (commandTag == "Taskbar_UnpinnedMode")
                SettingsEngine.Shell_TaskbarUnpinnedMode = style;

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"{commandTag}:{style}");
            }
        }
        #endregion
    }
}