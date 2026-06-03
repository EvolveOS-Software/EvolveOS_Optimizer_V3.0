// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    internal class SystemViewModel : ViewModelPageBase<SystemModel, SystemTweaks>
    {
        public Visibility RealtekSupport => HardwareData.VendorDetection.Realtek ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BluetoothSupport => BluetoothManager.IsAvailable ? Visibility.Visible : Visibility.Collapsed;

        protected override Dictionary<string, object> GetControlStates()
        {
            var invertedStates = new Dictionary<string, object>();

            foreach (var kvp in SystemTweaks.ControlStates)
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

        protected override void Analyze(SystemTweaks tweaks)
        {
            tweaks?.AnalyzeAndUpdate();

            OnPropertyChanged("Item[]");
        }

        public void ApplyRecommendations()
        {
            foreach (var kvp in SystemTweaks.RecommendedStates)
            {
                var model = this[kvp.Key];

                if (model != null)
                {
                    model.RecommendedState = kvp.Value;
                    model.OnPropertyChanged(nameof(model.IsRecommendedVisible));
                }
            }
        }

        private int _totalTweaksCount;
        public int TotalTweaksCount
        {
            get => _totalTweaksCount;
            set => SetProperty(ref _totalTweaksCount, value);
        }

        private int _customTweaksCount;
        public int CustomTweaksCount
        {
            get => _customTweaksCount;
            set => SetProperty(ref _customTweaksCount, value);
        }

        private int _defaultTweaksCount;
        public int DefaultTweaksCount
        {
            get => _defaultTweaksCount;
            set => SetProperty(ref _defaultTweaksCount, value);
        }

        public void UpdateCounters()
        {
            if (Toggles == null) return;

            TotalTweaksCount = Toggles.Count;
            CustomTweaksCount = Toggles.Count(t => t.State);
            DefaultTweaksCount = Toggles.Count(t => !t.State);
        }
    }
}