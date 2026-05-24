// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Utilities.Controls;
using Windows.System;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class AdvancedUtilsViewModel : ObservableObject
    {
        #region Properties

        public bool UseHotkey
        {
            get => SettingsEngine.IsPasswordGenHotkeyEnabled;
            set
            {
                if (SettingsEngine.IsPasswordGenHotkeyEnabled != value)
                {
                    SettingsEngine.IsPasswordGenHotkeyEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsComboEnabled));

                    _ = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public VirtualKeyModifiers PasswordGenModifier
        {
            get => (VirtualKeyModifiers)SettingsEngine.PasswordGenHotkeyModifier;
            set
            {
                if (SettingsEngine.PasswordGenHotkeyModifier != (int)value)
                {
                    SettingsEngine.PasswordGenHotkeyModifier = (int)value;
                    OnPropertyChanged();
                    _ = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public VirtualKey PasswordGenKey
        {
            get => (VirtualKey)SettingsEngine.PasswordGenHotkeyKey;
            set
            {
                if (SettingsEngine.PasswordGenHotkeyKey != (int)value)
                {
                    SettingsEngine.PasswordGenHotkeyKey = (int)value;
                    OnPropertyChanged();
                    _ = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public bool IsComboEnabled => UseHotkey;

        #endregion

        #region Data Collections (Using Native Enums)

        public List<VirtualKeyModifiers> KeyboardModifiers { get; } = new()
        {
            VirtualKeyModifiers.Control,
            VirtualKeyModifiers.Menu,
            VirtualKeyModifiers.Shift,
            VirtualKeyModifiers.Windows
        };

        public List<VirtualKey> KeyboardKeys { get; } = new()
        {
            VirtualKey.G, VirtualKey.K, VirtualKey.P, VirtualKey.S, VirtualKey.W,
            VirtualKey.F1, VirtualKey.F2, VirtualKey.F3, VirtualKey.F4
        };

        #endregion
    }
}