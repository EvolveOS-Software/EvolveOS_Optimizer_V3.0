// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class AdvancedOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.Advanced;

    protected override string GetDisplayNameKey() => "Feature_Advanced_Name";

    public AdvancedOptimizationsViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
    {
    }
}