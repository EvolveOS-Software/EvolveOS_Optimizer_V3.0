using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    internal class SystemViewModel : ViewModelPageBase<SystemModel, SystemTweaks>
    {
        public Visibility RealtekSupportAvailable => HardwareData.VendorDetection.Realtek ? Visibility.Visible : Visibility.Collapsed;

        protected override Dictionary<string, object> GetControlStates() => SystemTweaks.ControlStates;

        protected override void Analyze(SystemTweaks tweaks) => tweaks?.AnalyzeAndUpdate();

        public bool IsHoverGlowEnabled
        {
            get => SettingsEngine.IsHoverGlowEnabled;
            set
            {
                SettingsEngine.IsHoverGlowEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelectionGlowEnabled
        {
            get => SettingsEngine.IsSelectionGlowEnabled;
            set
            {
                SettingsEngine.IsSelectionGlowEnabled = value;
                OnPropertyChanged();
            }
        }
    }
}
