// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class FindDialog : ContentDialog
    {
        private RegistrySearchOptions _options;

        #region Constructor
        public FindDialog(RegistrySearchOptions savedOptions)
        {
            this.InitializeComponent();
            _options = savedOptions;

            SearchInput.Text = _options.Query;
            ChkKeys.IsChecked = _options.SearchKeys;
            ChkValues.IsChecked = _options.SearchValues;
            ChkData.IsChecked = _options.SearchData;
            ChkWholeString.IsChecked = _options.MatchWholeString;

            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(SearchInput.Text);
            InitializeCurrentShortcutUI();
        }
        #endregion

        #region Initialization
        private void InitializeCurrentShortcutUI()
        {
            if (LocalMachineSettingsEngine.IsFindHotkeyEnabled)
            {
                var mod = (VirtualKeyModifiers)LocalMachineSettingsEngine.FindHotkeyModifier;
                var key = (VirtualKey)LocalMachineSettingsEngine.FindHotkeyKey;
                TxtFindHotkey.Text = mod != VirtualKeyModifiers.None ? $"{mod} + {key}" : key.ToString();
                TxtFindHotkey.Tag = new Tuple<uint, uint>((uint)mod, (uint)key);
            }
        }
        #endregion

        #region Event Handlers
        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = true;
            var targetKey = e.Key;

            if (targetKey == VirtualKey.Control || targetKey == VirtualKey.Menu || targetKey == VirtualKey.Shift || targetKey == VirtualKey.LeftWindows || targetKey == VirtualKey.RightWindows)
            {
                return;
            }

            var activeModifiers = VirtualKeyModifiers.None;

            if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                activeModifiers |= VirtualKeyModifiers.Control;
            if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                activeModifiers |= VirtualKeyModifiers.Menu;
            if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                activeModifiers |= VirtualKeyModifiers.Shift;

            if (sender is TextBox baseBox)
            {
                string rawKeyDisplay = targetKey.ToString();
                string rawModifierDisplay = activeModifiers != VirtualKeyModifiers.None ? $"{activeModifiers} + " : "";
                baseBox.Text = $"{rawModifierDisplay}{rawKeyDisplay}";
                baseBox.Tag = new Tuple<uint, uint>((uint)activeModifiers, (uint)targetKey);
            }
        }

        private async void OnSaveShortcutsClick(object sender, RoutedEventArgs e)
        {
            if (TxtFindHotkey.Tag is Tuple<uint, uint> findResult)
            {
                LocalMachineSettingsEngine.IsFindHotkeyEnabled = true;
                LocalMachineSettingsEngine.FindHotkeyModifier = (int)findResult.Item1;
                LocalMachineSettingsEngine.FindHotkeyKey = (int)findResult.Item2;
            }

            bool bindingMatrixValid = await App.NotifyHotkeySettingsChanged();

            if (bindingMatrixValid)
            {
                App.ShowNotification("Shortcuts Re-Bound", "Registry search parameters updating successfully.", InfoBarSeverity.Success, 3500);
            }
            else
            {
                App.ShowNotification("Binding Notice", "One or more hotkeys encountered a system collision.", InfoBarSeverity.Informational, 5000);
            }
        }
        #endregion

        #region State Management
        public void SaveState()
        {
            _options.Query = SearchInput.Text;
            _options.SearchKeys = ChkKeys.IsChecked ?? false;
            _options.SearchValues = ChkValues.IsChecked ?? false;
            _options.SearchData = ChkData.IsChecked ?? false;
            _options.MatchWholeString = ChkWholeString.IsChecked ?? false;
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(SearchInput.Text);
        }
        #endregion
    }
}