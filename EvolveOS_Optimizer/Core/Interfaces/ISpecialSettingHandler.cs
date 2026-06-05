// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISpecialSettingHandler
{
    Task<bool> TryApplySpecialSettingAsync(
        SettingDefinition setting,
        object value,
        bool additionalContext = false,
        ISettingApplicationService? settingApplicationService = null);

    Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(
        IEnumerable<SettingDefinition> settings)
    {
        return Task.FromResult(new Dictionary<string, Dictionary<string, object?>>());
    }
}
