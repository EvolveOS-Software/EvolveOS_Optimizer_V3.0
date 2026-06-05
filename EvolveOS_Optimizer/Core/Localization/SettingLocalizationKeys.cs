// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Localization;

public static class SettingLocalizationKeys
{
    #region Constants

    public const string CommonCustomState = "Common_CustomState";

    #endregion

    #region Base Resolution

    private static string Base(SettingDefinition setting) => setting.LocalizationId ?? setting.Id;

    #endregion

    #region Setting Property Keys

    public static string Name(SettingDefinition setting) => $"Setting_{Base(setting)}_Name";

    public static string Description(SettingDefinition setting) => $"Setting_{Base(setting)}_Description";

    public static string OptionDisplay(SettingDefinition setting, int index) => $"Setting_{Base(setting)}_Option_{index}";

    public static string OptionTooltip(SettingDefinition setting, int index) => $"Setting_{Base(setting)}_OptionTooltip_{index}";

    public static string OptionWarning(SettingDefinition setting, int index) => $"Setting_{Base(setting)}_OptionWarning_{index}";

    public static string OptionCustom(SettingDefinition setting) => $"Setting_{Base(setting)}_Option_Custom";

    #endregion

    #region Group Keys

    public static string GroupCompact(string groupName) =>
        $"SettingGroup_{groupName.Replace(" ", "").Replace("&", "")}";

    public static string GroupSnake(string groupName)
    {
        var snakeCaseName = groupName
            .Replace(" & ", "_")
            .Replace(" ", "_")
            .Replace("&", "_");

        while (snakeCaseName.Contains("__"))
        {
            snakeCaseName = snakeCaseName.Replace("__", "_");
        }

        return $"SettingGroup_{snakeCaseName}";
    }

    #endregion

    #region Validation & Utilities

    public static bool IsLocalizationKey(string value)
    {
        return value.StartsWith("Template_") ||
               value.StartsWith("Setting_") ||
               value.StartsWith("PowerPlan_") ||
               value.StartsWith("ServiceOption_");
    }

    public static IEnumerable<string> ExpectedKeys(SettingDefinition setting)
    {
        yield return Name(setting);
        yield return Description(setting);

        if (setting.GroupName != null)
        {
            yield return GroupCompact(setting.GroupName);
            yield return GroupSnake(setting.GroupName);
        }

        if (setting.ComboBox != null)
        {
            yield return OptionCustom(setting);
            yield return CommonCustomState;

            var options = setting.ComboBox.Options;
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    var option = options[i];

                    if (!IsLocalizationKey(option.DisplayName))
                    {
                        yield return OptionDisplay(setting, i);
                    }

                    if (!string.IsNullOrEmpty(option.Tooltip))
                    {
                        yield return OptionTooltip(setting, i);
                    }

                    if (!string.IsNullOrEmpty(option.Warning))
                    {
                        yield return OptionWarning(setting, i);
                    }
                }
            }
        }
    }

    #endregion
}