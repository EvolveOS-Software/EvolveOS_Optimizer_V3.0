using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    internal class InterfaceViewModel : ViewModelPageBase<InterfaceModel, InterfaceTweaks>
    {
        public Visibility Win11FeatureOnly => HardwareData.OS.IsWin11 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Win11FeatureAvailable => HardwareData.OS.IsWin11 && HardwareData.OS.Build.CompareTo(22621.2361m) >= 0 ? Visibility.Visible : Visibility.Collapsed;
        public bool IsBlockWithoutLicense => WindowsLicense.IsWindowsActivated;

        protected override Dictionary<string, object> GetControlStates() => InterfaceTweaks.ControlStates;

        protected override void Analyze(InterfaceTweaks tweaks) => tweaks?.AnalyzeAndUpdate();

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
