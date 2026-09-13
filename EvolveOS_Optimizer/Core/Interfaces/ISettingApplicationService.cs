// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingApplicationService
{
    Task<OperationResult> ApplySettingAsync(ApplySettingRequest request);
    Task ApplyRecommendedSettingsForFeatureAsync(string settingId);
}
