// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingReviewDiffApplier
{
    void ApplyReviewDiffToViewModel(SettingItemViewModel viewModel, SettingStateResult currentState);
}
