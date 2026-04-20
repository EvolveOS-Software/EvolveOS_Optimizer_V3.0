// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using EvolveOS_Optimizer.Utilities.Helpers;
using static EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model
{
    public class StartupApp : INotifyPropertyChanged
    {
        #region Core Data Properties

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Publisher { get; set; } = "Unverified Developer";
        public bool IsVerified { get; set; } = false;
        public string SourceLocation { get; set; } = string.Empty;
        public string RegistryPath { get; set; } = string.Empty;
        public StartupSourceType SourceType { get; set; }

        #endregion

        #region State Properties

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        private int _delaySeconds = 0;
        public int DelaySeconds
        {
            get => _delaySeconds;
            set
            {
                if (_delaySeconds != value)
                {
                    _delaySeconds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DelayTooltipText));
                }
            }
        }

        #endregion

        #region UI Display Helpers

        public string StatusText => IsEnabled ? "Enabled" : "Disabled";
        public SolidColorBrush StatusColor => IsEnabled
            ? new SolidColorBrush(Colors.LimeGreen)
            : new SolidColorBrush(Colors.Gray);

        public SolidColorBrush TrustColor => IsVerified
            ? new SolidColorBrush(Colors.DodgerBlue)
            : new SolidColorBrush(Colors.Orange);
        public string TrustIcon => IsVerified ? "\xE104" : "\xE7BA";

        public string DelayTooltipText
        {
            get
            {
                return DelaySeconds > 0
                    ? string.Format(ResourceString.GetString("startup_manager_page_delay_tooltip_active"), DelaySeconds)
                    : ResourceString.GetString("startup_manager_page_delay_tooltip_off");
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}