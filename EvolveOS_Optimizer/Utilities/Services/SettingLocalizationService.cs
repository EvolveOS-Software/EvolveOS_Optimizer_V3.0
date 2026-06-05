// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Localization;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Provides translation and localization services for application setting definitions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service acts as a decorator for <see cref="SettingDefinition"/> objects. It dynamically 
/// replaces hard-coded metadata strings (names, descriptions, units, etc.) with localized resources 
/// fetched via <see cref="ILocalizationService"/>.
/// </para>
/// <para>
/// <b>Cross-Group Handling:</b>
/// It also handles complex scenarios such as cross-group dependency warnings, where settings 
/// belonging to different modules must be identified, grouped, and localized for user notifications.
/// </para>
/// </remarks>
public class SettingLocalizationService : ISettingLocalizationService
{

    #region Fields & Constructor

    private readonly ILocalizationService _localization;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;

    public SettingLocalizationService(
        ILocalizationService localization,
        ICompatibleSettingsRegistry compatibleSettingsRegistry)
    {
        _localization = localization;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
    }

    #endregion

    #region Public API

    public SettingDefinition LocalizeSetting(SettingDefinition setting)
    {
        var localized = setting with
        {
            Name = GetLocalizedName(setting),
            Description = GetLocalizedDescription(setting),
            GroupName = setting.GroupName != null ? GetLocalizedGroupName(setting.GroupName) : null
        };

        if (setting.ComboBox != null)
        {
            var comboBox = setting.ComboBox;

            var localizedComboBox = comboBox with
            {
                Options = LocalizeComboBoxOptions(setting),
                CustomStateDisplayName = GetLocalizedCustomState(setting)
            };

            localized = localized with { ComboBox = localizedComboBox };
        }

        if (setting.NumericRange?.Units != null)
        {
            localized = localized with
            {
                NumericRange = setting.NumericRange with
                {
                    Units = LocalizeUnits(setting.NumericRange.Units)
                }
            };
        }

        if (setting.VersionCompatibilityMessage is { } compatKey && compatKey.StartsWith("Compatibility_"))
        {
            var parts = compatKey.Split('|');
            var key = parts[0];

            if (parts.Length > 1)
            {
                var args = parts.Skip(1).ToArray();
                try
                {
                    var format = _localization.GetString(key);
                    localized = localized with { VersionCompatibilityMessage = string.Format(format, args) };
                }
                catch
                {
                    localized = localized with { VersionCompatibilityMessage = _localization.GetString(key) };
                }
            }
            else
            {
                localized = localized with { VersionCompatibilityMessage = _localization.GetString(key) };
            }
        }

        return localized;
    }

    public string? BuildCrossGroupInfoMessage(SettingDefinition setting)
    {
        var crossGroupSettings = setting.CrossGroupChildSettings;
        if (crossGroupSettings == null || crossGroupSettings.Count == 0)
        {
            return null;
        }

        var groupedSettings = new Dictionary<string, List<string>>();

        foreach (var (childSettingId, localizationKey) in crossGroupSettings)
        {
            try
            {
                var featureId = _compatibleSettingsRegistry.GetFeatureIdForSetting(childSettingId);
                if (featureId == null) continue;

                var filteredSettings = _compatibleSettingsRegistry.GetFilteredSettings(featureId);
                var childSetting = filteredSettings.FirstOrDefault(s => s.Id == childSettingId);

                if (childSetting == null) continue;

                var featureName = GetFeatureName(childSettingId);
                var groupNameKey = $"SettingGroup_{childSetting.GroupName?.Replace(" ", "_")}";
                var localizedGroupName = _localization.GetString(groupNameKey);
                var groupKey = $"{featureName} ({localizedGroupName})";

                if (!groupedSettings.ContainsKey(groupKey))
                {
                    groupedSettings[groupKey] = new List<string>();
                }

                var localizedChildName = _localization.GetString(localizationKey);
                if (!string.IsNullOrEmpty(localizedChildName))
                {
                    groupedSettings[groupKey].Add(localizedChildName);
                }
            }
            catch
            {
                // Skip settings that can't be looked up
            }
        }

        if (groupedSettings.Count == 0) return null;

        var header = _localization.GetString("Setting_CrossGroupWarning_Header");
        var lines = groupedSettings.Select(kvp => $"• {kvp.Key}: {string.Join(", ", kvp.Value)}");
        return $"{header}\n{string.Join("\n", lines)}";
    }


    #endregion

    #region Private Localization Helpers

    private string GetLocalizedName(SettingDefinition setting)
    {
        var key = SettingLocalizationKeys.Name(setting);
        return GetStringOrFallback(key, setting.Name);
    }

    private string GetLocalizedDescription(SettingDefinition setting)
    {
        var key = SettingLocalizationKeys.Description(setting);
        return GetStringOrFallback(key, setting.Description);
    }

    private string GetLocalizedGroupName(string groupName)
    {
        var key = SettingLocalizationKeys.GroupCompact(groupName);
        var localized = _localization.GetString(key);

        if (!localized.StartsWith("[") || !localized.EndsWith("]"))
        {
            return localized;
        }

        var keySnake = SettingLocalizationKeys.GroupSnake(groupName);
        return GetStringOrFallback(keySnake, groupName);
    }

    private string GetLocalizedCustomState(SettingDefinition setting)
    {
        var perSettingKey = SettingLocalizationKeys.OptionCustom(setting);
        var perSetting = _localization.GetString(perSettingKey);
        if (!perSetting.StartsWith("[") || !perSetting.EndsWith("]"))
        {
            return perSetting;
        }
        return GetStringOrFallback(SettingLocalizationKeys.CommonCustomState, setting.ComboBox?.CustomStateDisplayName ?? "Custom");
    }

    private IReadOnlyList<ComboBoxOption> LocalizeComboBoxOptions(SettingDefinition setting)
    {
        var originalOptions = setting.ComboBox?.Options;
        if (originalOptions == null || originalOptions.Count == 0)
            return Array.Empty<ComboBoxOption>();

        var localized = new List<ComboBoxOption>(originalOptions.Count);
        for (int i = 0; i < originalOptions.Count; i++)
        {
            var original = originalOptions[i];

            var displayKey = SettingLocalizationKeys.IsLocalizationKey(original.DisplayName)
                ? original.DisplayName
                : SettingLocalizationKeys.OptionDisplay(setting, i);
            var localizedDisplay = GetStringOrFallback(displayKey, original.DisplayName);

            string? localizedTooltip = original.Tooltip;
            if (!string.IsNullOrEmpty(original.Tooltip))
            {
                var tooltipKey = SettingLocalizationKeys.OptionTooltip(setting, i);
                localizedTooltip = GetStringOrFallback(tooltipKey, original.Tooltip);
            }

            string? localizedWarning = original.Warning;
            if (!string.IsNullOrEmpty(original.Warning))
            {
                var warningKey = SettingLocalizationKeys.OptionWarning(setting, i);
                localizedWarning = GetStringOrFallback(warningKey, original.Warning);
            }

            (string Title, string Message)? localizedConfirmation = original.Confirmation;
            if (original.Confirmation is { } confirmation)
            {
                var title = GetStringOrFallback(confirmation.Title, confirmation.Title);
                var message = GetStringOrFallback(confirmation.Message, confirmation.Message);
                localizedConfirmation = (title, message);
            }

            localized.Add(original with
            {
                DisplayName = localizedDisplay,
                Tooltip = localizedTooltip,
                Warning = localizedWarning,
                Confirmation = localizedConfirmation,
            });
        }

        return localized;
    }

    private string LocalizeUnits(string units)
    {
        var key = units switch
        {
            "Minutes" => "Common_Unit_Minutes",
            "Milliseconds" => "Common_Unit_Milliseconds",
            "%" => "%",
            _ => null
        };

        return key != null ? GetStringOrFallback(key, units) : units;
    }

    private string GetStringOrFallback(string key, string fallback)
    {
        var localized = _localization.GetString(key);
        return localized.StartsWith("[") && localized.EndsWith("]") ? fallback : localized;
    }

    private bool IsLocalizationKey(string value)
    {
        return value.StartsWith("Template_") ||
               value.StartsWith("Setting_") ||
               value.StartsWith("PowerPlan_") ||
               value.StartsWith("ServiceOption_");
    }

    private string GetFeatureName(string settingId)
    {
        if (settingId.StartsWith("privacy-"))
            return _localization.GetString("Feature_Privacy_Name") ?? "Privacy & Security";
        if (settingId.StartsWith("notifications-"))
            return _localization.GetString("Feature_Notifications_Name") ?? "Notifications";
        if (settingId.StartsWith("start-"))
            return _localization.GetString("Feature_StartMenu_Name") ?? "Start Menu";
        if (settingId.StartsWith("customize-"))
            return _localization.GetString("Feature_Explorer_Name") ?? "Explorer";
        if (settingId.StartsWith("gaming-"))
            return _localization.GetString("Feature_GamingPerformance_Name") ?? "Gaming & Performance";
        if (settingId.StartsWith("power-"))
            return _localization.GetString("Feature_Power_Name") ?? "Power";

        return _localization.GetString("Nav_Settings") ?? "Settings";
    }

    #endregion
}
