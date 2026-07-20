// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class OptimizeViewModel : SectionPageViewModel<OptimizeSectionInfo>
{
    protected override string PageTitleKey => "Category_Optimize_Title";
    protected override string PageDescriptionKey => "Category_Optimize_StatusText";
    protected override string BreadcrumbRootFallback => "Optimizations";
    protected override string LogPrefix => "OptimizeViewModel";
    protected override IReadOnlyList<OptimizeSectionInfo> SectionDefinitions => Sections;

    public static readonly IReadOnlyList<OptimizeSectionInfo> Sections = new List<OptimizeSectionInfo>()
    {
        new("Privacy", "PrivacyIconPath", "Privacy & Security", FeatureIds.Privacy),
        new("Power", "PowerIconPath", "Power", FeatureIds.Power),
        new("Gaming", "GamingIconPath", "Gaming and Performance", FeatureIds.GamingPerformance),
        new("Update", "UpdateIconSymbol", "Updates", FeatureIds.Update),
        new("Notification", "NotificationIconPath", "Notifications", FeatureIds.Notifications),
        new("Sound", "SoundIconSymbol", "Sound", FeatureIds.Sound),
        new("Advanced", "WrenchIconSymbol", "Advanced", FeatureIds.Advanced),
    };

    public ISettingsFeatureViewModel SoundViewModel { get; }
    public ISettingsFeatureViewModel UpdateViewModel { get; }
    public ISettingsFeatureViewModel NotificationViewModel { get; }
    public ISettingsFeatureViewModel PrivacyViewModel { get; }
    public ISettingsFeatureViewModel PowerViewModel { get; }
    public ISettingsFeatureViewModel GamingViewModel { get; }
    public ISettingsFeatureViewModel AdvancedViewModel { get; }

    public OptimizeViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<IOptimizationFeatureViewModel> featureViewModels)
        : base(logService, localizationService, featureViewModels.Cast<ISettingsFeatureViewModel>())
    {
        InitializeSectionMappings();

        SoundViewModel = GetFeatureByModuleId(FeatureIds.Sound);
        UpdateViewModel = GetFeatureByModuleId(FeatureIds.Update);
        NotificationViewModel = GetFeatureByModuleId(FeatureIds.Notifications);
        PrivacyViewModel = GetFeatureByModuleId(FeatureIds.Privacy);
        PowerViewModel = GetFeatureByModuleId(FeatureIds.Power);
        GamingViewModel = GetFeatureByModuleId(FeatureIds.GamingPerformance);
        AdvancedViewModel = GetFeatureByModuleId(FeatureIds.Advanced);
    }
}
