// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ITooltipDataService
{
    Task<IReadOnlyDictionary<string, SettingTooltipData>> GetTooltipDataAsync(IEnumerable<SettingDefinition> settings);
    Task<SettingTooltipData?> RefreshTooltipDataAsync(string settingId, SettingDefinition setting);
    Task<IReadOnlyDictionary<string, SettingTooltipData>> RefreshMultipleTooltipDataAsync(IEnumerable<SettingDefinition> settings);
}
