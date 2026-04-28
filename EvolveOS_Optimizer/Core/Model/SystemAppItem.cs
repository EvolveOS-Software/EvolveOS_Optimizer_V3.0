// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class SystemAppItem : ObservableObject
    {
        // Properties that typically don't change after the initial load
        public string DisplayName { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public bool IsWin32 { get; set; }
        public string InstallLocation { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime InstallDate { get; set; }
        public string UninstallString { get; set; } = string.Empty;

        private bool _runsAtStartup;
        public bool RunsAtStartup
        {
            get => _runsAtStartup;
            set => SetProperty(ref _runsAtStartup, value);
        }

        private double _sizeMB;
        public double SizeMB
        {
            get => _sizeMB;
            set
            {
                if (SetProperty(ref _sizeMB, value))
                {
                    OnPropertyChanged(nameof(FormattedSize));
                }
            }
        }

        public string FormattedSize => SizeMB > 0 ? $"{SizeMB:0.##} MB" : ResourceString.GetString("SystemAppsPage_UnknownSize") ?? "Unknown Size";
        public string FormattedDate => InstallDate != DateTime.MinValue ? InstallDate.ToShortDateString() : "";
        public string FormattedVersion => !string.IsNullOrEmpty(Version) ? $"v{Version}" : "";
    }
}