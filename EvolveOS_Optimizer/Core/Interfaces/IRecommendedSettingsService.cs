// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IRecommendedSettingsService
{
    Task<IEnumerable<SettingDefinition>> GetRecommendedSettingsAsync(string settingId);
}
