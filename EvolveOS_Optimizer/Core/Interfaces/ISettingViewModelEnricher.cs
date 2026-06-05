// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingViewModelEnricher
{
    Task DetectBatteryAsync(SettingItemViewModel viewModel);

    void SetCrossGroupInfoMessage(SettingItemViewModel viewModel, SettingDefinition setting);

    void ApplyReviewDiff(SettingItemViewModel viewModel, SettingStateResult currentState);
}
