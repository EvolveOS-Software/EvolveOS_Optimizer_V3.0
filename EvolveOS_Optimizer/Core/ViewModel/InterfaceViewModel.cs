// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    internal class InterfaceViewModel : ViewModelPageBase<InterfaceModel, InterfaceTweaks>
    {
        public Visibility Win11FeatureOnly => HardwareData.OS.IsWin11 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Win11FeatureAvailable => HardwareData.OS.IsWin11 && HardwareData.OS.Build.CompareTo(22621.2361m) >= 0 ? Visibility.Visible : Visibility.Collapsed;
        public bool IsBlockWithoutLicense => WindowsLicense.IsWindowsActivated;

        protected override Dictionary<string, object> GetControlStates()
        {
            var invertedStates = new Dictionary<string, object>();

            foreach (var kvp in InterfaceTweaks.ControlStates)
            {
                if (kvp.Value is bool b)
                {
                    invertedStates[kvp.Key] = !b;
                }
                else
                {
                    invertedStates[kvp.Key] = kvp.Value;
                }
            }

            return invertedStates;
        }

        protected override void Analyze(InterfaceTweaks tweaks) => tweaks?.AnalyzeAndUpdate();

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private int _configuredCount;
        public int ConfiguredCount
        {
            get => _configuredCount;
            set => SetProperty(ref _configuredCount, value);
        }

        private int _defaultCount;
        public int DefaultCount
        {
            get => _defaultCount;
            set => SetProperty(ref _defaultCount, value);
        }

        public void UpdateCounters()
        {
            if (Toggles == null) return;

            TotalCount = Toggles.Count;
            ConfiguredCount = Toggles.Count(t => t.State);
            DefaultCount = Toggles.Count(t => !t.State);
        }
    }
}
