// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using ISettingsLoadingService = EvolveOS_Optimizer.Core.Interfaces.ISettingsLoadingService;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class StartMenuCustomizationsViewModel : BaseSettingsFeatureViewModel, ICustomizationFeatureViewModel
{
    public StartMenuCustomizationsViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
    {
    }

    public override string ModuleId => FeatureIds.StartMenu;

    protected override string GetDisplayNameKey() => "Feature_StartMenu_Name";
}
