using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    internal class PrivacyViewModel : ViewModelPageBase<PrivacyModel, PrivacyTweaks>
    {
        protected override Dictionary<string, object> GetControlStates() => PrivacyTweaks.ControlStates;

        protected override void Analyze(PrivacyTweaks tweaks) => tweaks?.AnalyzeAndUpdate();

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
