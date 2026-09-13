// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingViewModelEnricher
{
    Task DetectBatteryAsync(SettingItemViewModel viewModel);

    void SetCrossGroupInfoMessage(SettingItemViewModel viewModel, SettingDefinition setting);
}
