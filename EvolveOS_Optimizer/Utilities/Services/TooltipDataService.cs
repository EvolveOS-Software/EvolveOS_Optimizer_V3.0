// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Generates detailed, real-time diagnostic data for setting tooltips.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service builds the <see cref="SettingTooltipData"/> model used by the UI to show users the 
/// technical "under the hood" details of a setting. It aggregates real-time values from the Registry, 
/// Scheduled Tasks, and Power Configuration to show exactly what a setting is modifying.
/// </para>
/// <para>
/// <b>Capabilities:</b>
/// It safely queries complex registry paths (including wildcard resolution for per-network or per-monitor 
/// settings) and fetches live AC/DC power values to provide absolute transparency to the user.
/// </para>
/// </remarks>
public class TooltipDataService(
    IWindowsRegistryService windowsRegistryService,
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService) : ITooltipDataService
{
    #region Fields & Constructor

    private readonly IWindowsRegistryService _registryService = windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
    private readonly ILogService _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    private readonly IPowerSettingsQueryService _powerSettingsQueryService = powerSettingsQueryService ?? throw new ArgumentNullException(nameof(powerSettingsQueryService));

    #endregion

    #region Public API

    public async Task<IReadOnlyDictionary<string, SettingTooltipData>> GetTooltipDataAsync(IEnumerable<SettingDefinition> settings)
    {
        var tooltipData = new Dictionary<string, SettingTooltipData>();

        try
        {
            foreach (var setting in settings)
            {
                var data = await GetTooltipDataForSettingAsync(setting).ConfigureAwait(false);
                if (data != null)
                {
                    tooltipData[setting.Id] = data;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[TooltipDataService] Error fetching bulk tooltip data: {ex.Message}");
        }

        return tooltipData;
    }

    public async Task<SettingTooltipData?> RefreshTooltipDataAsync(string settingId, SettingDefinition setting)
    {
        try
        {
            return await GetTooltipDataForSettingAsync(setting).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[TooltipDataService] Error refreshing tooltip for '{settingId}': {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, SettingTooltipData>> RefreshMultipleTooltipDataAsync(IEnumerable<SettingDefinition> settings)
    {
        var tooltipData = new Dictionary<string, SettingTooltipData>();

        try
        {
            foreach (var setting in settings)
            {
                var data = await GetTooltipDataForSettingAsync(setting).ConfigureAwait(false);
                if (data != null)
                {
                    tooltipData[setting.Id] = data;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[TooltipDataService] Error refreshing multiple tooltips: {ex.Message}");
        }

        return tooltipData;
    }

    #endregion

    #region Core Tooltip Generation

    private async Task<SettingTooltipData?> GetTooltipDataForSettingAsync(SettingDefinition setting)
    {
        if (setting.DisableTooltip)
        {
            return null;
        }

        bool hasRegistrySettings = setting.RegistrySettings?.Any() == true;
        bool hasScheduledTaskSettings = setting.ScheduledTaskSettings?.Any() == true;
        bool hasPowerCfgSettings = setting.PowerCfgSettings?.Any() == true;

        if (!hasRegistrySettings && !hasScheduledTaskSettings && !hasPowerCfgSettings)
            return null;

        try
        {
            string displayValue = string.Empty;
            IReadOnlyDictionary<RegistrySetting, string?> individualRegistryValues = new Dictionary<RegistrySetting, string?>();

            if (hasRegistrySettings)
            {
                var registrySettings = setting.RegistrySettings!.ToList();
                var individualValues = new Dictionary<RegistrySetting, string?>();
                var primaryRegistrySetting = registrySettings.First();
                string? primaryDisplayValue = null;

                foreach (var registrySetting in registrySettings)
                {
                    try
                    {
                        object? currentValue;
                        if (registrySetting.ApplyPerNetworkInterface || registrySetting.ApplyPerMonitor)
                        {
                            var subKeys = _registryService.GetSubKeyNames(registrySetting.KeyPath);
                            if (subKeys.Length > 0)
                            {
                                currentValue = _registryService.GetValue(
                                    $@"{registrySetting.KeyPath}\{subKeys[0]}",
                                    registrySetting.ValueName!);
                            }
                            else
                            {
                                currentValue = null;
                            }
                        }
                        else
                        {
                            currentValue = _registryService.GetValue(registrySetting.KeyPath, registrySetting.ValueName!);
                        }
                        var formattedValue = FormatRegistryValue(currentValue, registrySetting);
                        individualValues[registrySetting] = formattedValue;

                        if (registrySetting == primaryRegistrySetting)
                        {
                            primaryDisplayValue = formattedValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogDebug($"[TooltipDataService] Error reading registry for tooltip '{registrySetting.KeyPath}\\{registrySetting.ValueName}': {ex.Message}");
                        individualValues[registrySetting] = null;
                    }
                }

                displayValue = primaryDisplayValue ?? string.Empty;
                individualRegistryValues = individualValues;
            }

            return new SettingTooltipData
            {
                SettingId = setting.Id,
                DisplayValue = displayValue,
                IndividualRegistryValues = individualRegistryValues,
                ScheduledTaskSettings = setting.ScheduledTaskSettings?.ToList() ?? new List<ScheduledTaskSetting>(),
                PowerCfgSettings = setting.PowerCfgSettings?.ToList() ?? new List<PowerCfgSetting>(),
                PowerShellScripts = setting.PowerShellScripts?.ToList() ?? new List<PowerShellScriptSetting>(),
                RegContents = setting.RegContents?.ToList() ?? new List<RegContentSetting>(),
                Dependencies = setting.Dependencies?.ToList() ?? new List<SettingDependency>(),
                CurrentPowerValues = await BuildCurrentPowerValuesAsync(setting.PowerCfgSettings).ConfigureAwait(false),
                SettingDefinition = setting
            };
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[TooltipDataService] Error building tooltip data for '{setting.Id}': {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Private Helpers

    private static string? FormatRegistryValue(object? value, RegistrySetting? registrySetting)
        => RegistryValueFormatter.Format(value, registrySetting);

    private async Task<IReadOnlyDictionary<PowerCfgSetting, (int? AC, int? DC)>> BuildCurrentPowerValuesAsync(
        IReadOnlyList<PowerCfgSetting>? powerCfgSettings)
    {
        var dict = new Dictionary<PowerCfgSetting, (int? AC, int? DC)>();
        if (powerCfgSettings is null || powerCfgSettings.Count == 0) return dict;
        foreach (var pcs in powerCfgSettings)
        {
            var values = await _powerSettingsQueryService.GetPowerSettingACDCValuesAsync(pcs).ConfigureAwait(false);
            dict[pcs] = (values.acValue, values.dcValue);
        }
        return dict;
    }

    #endregion
}