// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class GamingOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
    {
        public override string ModuleId => FeatureIds.GamingPerformance;

        protected override string GetDisplayNameKey() => "Feature_GamingPerformance_Name";

        public GamingOptimizationsViewModel(
            ISettingsLoadingService settingsLoadingService,
            ILogService logService,
            ILocalizationService localizationService,
            IDispatcherService dispatcherService,
            IEventBus eventBus)
            : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
        {
        }
    }
}