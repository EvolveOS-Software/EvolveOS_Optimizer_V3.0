// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;

namespace EvolveOS_Optimizer.Core.Model;

public static class SettingDefinitionToggleState
{
    #region Public State Resolution API
    public static RegistrySetting? GetPrimaryRegistrySetting(SettingDefinition setting) =>
        setting.RegistrySettings?.FirstOrDefault(r => r.IsPrimary)
        ?? setting.RegistrySettings?.FirstOrDefault();

    public static bool? GetRecommendedToggleState(SettingDefinition setting)
    {
        if (setting.RecommendedToggleState is bool explicitState) return explicitState;

        var reg = GetPrimaryRegistrySetting(setting);
        if (reg != null)
        {
            var fromReg = ResolveToggleStateInternal(reg, reg.RecommendedValue, deriveFromKeyAbsent: false);
            if (fromReg is bool b) return b;
        }

        var taskSetting = setting.ScheduledTaskSettings?.FirstOrDefault(ts => ts.RecommendedState.HasValue);
        return taskSetting?.RecommendedState;
    }

    public static bool? GetDefaultToggleState(SettingDefinition setting)
    {
        if (setting.DefaultToggleState is bool explicitState) return explicitState;

        var reg = GetPrimaryRegistrySetting(setting);
        if (reg != null)
        {
            var fromReg = ResolveToggleStateInternal(reg, reg.DefaultValue, deriveFromKeyAbsent: true);
            if (fromReg is bool b) return b;
        }

        var taskSetting = setting.ScheduledTaskSettings?.FirstOrDefault(ts => ts.DefaultState.HasValue);
        return taskSetting?.DefaultState;
    }
    #endregion

    #region Public Evaluation Utilities
    public static bool IsKeyExistenceToggle(RegistrySetting r) =>
        r.ValueName == null
        && r.EnabledValue == null
        && r.DisabledValue == null
        && r.ValueType == RegistryValueKind.None;

    public static bool? ToggleTargetState(object? targetValue, object?[]? enabledValue, object?[]? disabledValue)
    {
        if (targetValue == null)
        {
            if (ArrayContainsNull(enabledValue)) return true;
            if (ArrayContainsNull(disabledValue)) return false;
            return null;
        }
        if (IsValueInArray(targetValue, enabledValue)) return true;
        if (IsValueInArray(targetValue, disabledValue)) return false;
        return null;
    }
    #endregion

    #region Internal Logic & Equality Helpers
    private static bool? ResolveToggleStateInternal(RegistrySetting reg, object? targetValue, bool deriveFromKeyAbsent)
    {
        if (targetValue == null && !deriveFromKeyAbsent) return null;
        if (targetValue == null && deriveFromKeyAbsent && IsKeyExistenceToggle(reg)) return true;
        return ToggleTargetState(targetValue, reg.EnabledValue, reg.DisabledValue);
    }

    private static bool ArrayContainsNull(object?[]? array) => array?.Any(v => v == null) == true;

    private static bool IsValueInArray(object value, object?[]? array)
    {
        if (array == null) return false;
        return array.Any(v => ValuesEqual(value, v));
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;
        try
        {
            var aVal = Convert.ToInt64(a);
            var bVal = Convert.ToInt64(b);
            return aVal == bVal;
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
    #endregion
}