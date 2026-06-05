// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IRecommendedSettingsApplier
{
    Task ApplyRecommendedSettingsForFeatureAsync(string settingId, ISettingApplicationService settingApplicationService);
}
