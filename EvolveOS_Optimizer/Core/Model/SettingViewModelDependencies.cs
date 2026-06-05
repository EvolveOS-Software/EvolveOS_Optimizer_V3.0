// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Model;

public record SettingViewModelDependencies(
    ISettingApplicationService SettingApplicationService,
    ILogService LogService,
    IDispatcherService DispatcherService,
    IDialogService DialogService,
    IEventBus EventBus,
    IRegeditLauncher RegeditLauncher
);
