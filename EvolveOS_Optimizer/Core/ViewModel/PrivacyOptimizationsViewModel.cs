// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class PrivacyOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.Privacy;

    protected override string GetDisplayNameKey() => "Feature_Privacy_Name";

    public PrivacyOptimizationsViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
    {
    }
}
