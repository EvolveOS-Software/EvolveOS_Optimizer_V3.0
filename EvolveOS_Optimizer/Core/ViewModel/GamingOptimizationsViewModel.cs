// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;

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