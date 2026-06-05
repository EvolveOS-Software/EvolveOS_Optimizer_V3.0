// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IPowerPlanComboBoxService
{
    Task<ComboBoxSetupResult> SetupPowerPlanComboBoxAsync(SettingDefinition setting, object? currentValue);
    Task<List<PowerPlanComboBoxOption>> GetPowerPlanOptionsAsync();
    Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues);
    Task<PowerPlanResolutionResult> ResolvePowerPlanByIndexAsync(int index);
    void InvalidateCache();
}
