// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IWindowsRegistryService
{
    bool SetValue(string keyPath, string valueName, object value, RegistryValueKind kind);
    object? GetValue(string keyPath, string valueName);
    bool DeleteKey(string keyPath);
    bool DeleteValue(string keyPath, string valueName);
    bool KeyExists(string keyPath);
    bool ValueExists(string keyPath, string valueName);
    string[] GetSubKeyNames(string keyPath);
    bool IsSettingApplied(RegistrySetting setting);
    bool IsRegistryValueInEnabledState(RegistrySetting setting, object? currentValue, bool valueExists);
    bool ApplySetting(RegistrySetting setting, bool enable, object? specificValue = null, bool useDefaultValue = false);
    Dictionary<string, object?> GetBatchValues(IEnumerable<(string keyPath, string? valueName)> queries);
}
