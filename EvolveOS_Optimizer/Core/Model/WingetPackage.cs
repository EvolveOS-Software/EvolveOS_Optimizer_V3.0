// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Core.Model
{
    public class WingetPackage : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isInstalled;
        private bool _hasUpdate;
        private string _latestVersion = string.Empty;

        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;

        public string DisplaySource
        {
            get
            {
                string safeId = Id ?? string.Empty;
                string safeName = Name ?? string.Empty;

                if (safeId.Contains("PowerShell", StringComparison.OrdinalIgnoreCase) ||
                    safeName.Contains("PowerShell", StringComparison.OrdinalIgnoreCase))
                {
                    return "PowerShell";
                }

                if (safeId.StartsWith("Microsoft.DotNet", StringComparison.OrdinalIgnoreCase) ||
                    safeName.Contains(".NET", StringComparison.OrdinalIgnoreCase))
                {
                    return "DotNet";
                }

                if (!string.IsNullOrWhiteSpace(Source))
                {
                    if (Source.Equals("msstore", StringComparison.OrdinalIgnoreCase)) return "Microsoft Store";
                    if (Source.Equals("winget", StringComparison.OrdinalIgnoreCase)) return "WinGet";

                    return string.Concat(Source[0].ToString().ToUpper(), Source.AsSpan(1));
                }

                if (safeId.Length == 12 && System.Text.RegularExpressions.Regex.IsMatch(safeId, "^[a-zA-Z0-9]+$"))
                {
                    return "Microsoft Store";
                }

                if (safeId.Contains("."))
                {
                    return "WinGet";
                }

                return "Local App";
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;

        private string _version = string.Empty;
        public string Version
        {
            get => _version;
            set
            {
                if (_version != value)
                {
                    _version = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LatestVersion
        {
            get => _latestVersion;
            set
            {
                if (_latestVersion != value)
                {
                    _latestVersion = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InstalledVisibility));
                    OnPropertyChanged(nameof(NotInstalledVisibility));
                    OnPropertyChanged(nameof(InstalledNoUpdateVisibility));
                    OnPropertyChanged(nameof(ItemOpacity));
                    OnPropertyChanged(nameof(ItemIsEnabled));
                }
            }
        }

        public bool HasUpdate
        {
            get => _hasUpdate;
            set
            {
                if (_hasUpdate != value)
                {
                    _hasUpdate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UpdateVisibility));
                    OnPropertyChanged(nameof(InstalledNoUpdateVisibility));
                    OnPropertyChanged(nameof(ItemOpacity));
                    OnPropertyChanged(nameof(ItemIsEnabled));
                }
            }
        }

        public Visibility InstalledNoUpdateVisibility =>
            IsInstalled && !HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InstalledVisibility =>
            IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NotInstalledVisibility =>
            !IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UpdateVisibility =>
            HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        public double ItemOpacity => IsInstalled && !HasUpdate ? 0.5 : 1.0;
        public bool ItemIsEnabled => !IsInstalled || HasUpdate;


        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}