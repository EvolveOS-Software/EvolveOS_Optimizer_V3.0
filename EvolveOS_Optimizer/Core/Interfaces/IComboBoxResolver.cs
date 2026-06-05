// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IComboBoxResolver
{
    Task<object?> ResolveCurrentValueAsync(SettingDefinition setting, Dictionary<string, object?>? existingRawValues = null);
    int ResolveRawValuesToIndex(SettingDefinition setting, Dictionary<string, object?> rawValues);
    Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index);
    int GetValueFromIndex(SettingDefinition setting, int index);
    int GetIndexFromDisplayName(SettingDefinition setting, string displayName);
}
