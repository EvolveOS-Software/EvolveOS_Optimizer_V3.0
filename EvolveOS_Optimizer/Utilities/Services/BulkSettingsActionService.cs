// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Orchestrates batch operations on application settings, such as applying recommended states or resetting to defaults.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service handles bulk processing for groups of settings. It manages the lifecycle of batch updates 
/// by suppressing individual setting restarts, coalescing them into a single flush operation via 
/// <see cref="IProcessRestartManager"/>, and providing granular progress reporting.
/// </para>
/// <para>
/// <b>Capability Resolution:</b>
/// It interprets complex setting definitions—including <see cref="InputType.Toggle"/>, <see cref="InputType.Selection"/>, 
/// and power-cfg-backed values—to ensure the correct data is dispatched to the <see cref="ISettingApplicationService"/>.
/// </para>
/// </remarks>
public class BulkSettingsActionService(
    ICompatibleSettingsRegistry settingsRegistry,
    IWindowsVersionService versionService,
    ISettingApplicationService settingApplicationService,
    IProcessRestartManager processRestartManager,
    ILogService logService) : IBulkSettingsActionService
{
    #region Public API

    public async Task<int> ApplyRecommendedAsync(
        IEnumerable<string> settingIds,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        int applied = 0;
        int total = settings.Count;
        var appliedForRestart = new List<SettingDefinition>(total);

        using (processRestartManager.SuppressRestarts())
        {
            for (int i = 0; i < total; i++)
            {
                var setting = settings[i];
                try
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = (double)i / total * 100,
                        StatusText = $"Applying recommended: {setting.Name}",
                        QueueCurrent = i + 1,
                        QueueTotal = total,
                        IsActive = true
                    });

                    if (setting.InputType == InputType.Toggle)
                    {
                        var toggleState = SettingDefinitionToggleState.GetRecommendedToggleState(setting);
                        if (toggleState is not bool enableValue)
                        {
                            logService.Log(LogLevel.Debug, $"[BulkSettings] Skipping '{setting.Id}' - no recommended toggle state");
                            continue;
                        }

                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = enableValue,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                    else if (setting.InputType == InputType.Selection)
                    {
                        var powerCfgValue = BuildPowerCfgApplyValue(setting, useRecommended: true);
                        if (powerCfgValue != null)
                        {
                            await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id,
                                Enable = true,
                                Value = powerCfgValue,
                                SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                        else
                        {
                            var recommendedIndex = GetRecommendedIndex(setting);
                            if (recommendedIndex is int idx)
                            {
                                await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                                {
                                    SettingId = setting.Id,
                                    Enable = true,
                                    Value = idx,
                                    SkipValuePrerequisites = true
                                }).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        var valueToApply = GetRecommendedValueForSetting(setting)
                            ?? BuildPowerCfgApplyValue(setting, useRecommended: true);
                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = true,
                            Value = valueToApply,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }

                    applied++;
                    appliedForRestart.Add(setting);
                    logService.Log(LogLevel.Debug, $"[BulkSettings] Applied recommended for '{setting.Id}'");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to apply recommended for '{setting.Id}': {ex.Message}");
                }
            }
        }

        await processRestartManager.FlushCoalescedRestartsAsync(appliedForRestart).ConfigureAwait(false);

        progress?.Report(new TaskProgressDetail
        {
            Progress = 100,
            StatusText = $"Applied {applied} of {total} settings",
            IsCompletion = true,
            IsActive = false
        });

        return applied;
    }

    public async Task<int> ResetToDefaultsAsync(
        IEnumerable<string> settingIds,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        int applied = 0;
        int total = settings.Count;
        var appliedForRestart = new List<SettingDefinition>(total);

        using (processRestartManager.SuppressRestarts())
        {
            for (int i = 0; i < total; i++)
            {
                var setting = settings[i];
                try
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = (double)i / total * 100,
                        StatusText = $"Resetting to default: {setting.Name}",
                        QueueCurrent = i + 1,
                        QueueTotal = total,
                        IsActive = true
                    });

                    if (setting.InputType == InputType.Toggle)
                    {
                        var toggleState = SettingDefinitionToggleState.GetDefaultToggleState(setting);
                        if (toggleState is not bool enableValue)
                        {
                            logService.Log(LogLevel.Debug, $"[BulkSettings] Skipping '{setting.Id}' - no default toggle state");
                            continue;
                        }

                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = enableValue,
                            ResetToDefault = true,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                    else if (setting.InputType == InputType.Selection)
                    {
                        var powerCfgValue = BuildPowerCfgApplyValue(setting, useRecommended: false);
                        if (powerCfgValue != null)
                        {
                            await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id,
                                Enable = true,
                                Value = powerCfgValue,
                                ResetToDefault = true,
                                SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                        else
                        {
                            var defaultIndex = GetDefaultIndex(setting);
                            if (defaultIndex is int idx)
                            {
                                await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                                {
                                    SettingId = setting.Id,
                                    Enable = true,
                                    Value = idx,
                                    ResetToDefault = true,
                                    SkipValuePrerequisites = true
                                }).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        var valueToApply = GetDefaultValueForSetting(setting)
                            ?? BuildPowerCfgApplyValue(setting, useRecommended: false);
                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = valueToApply != null,
                            Value = valueToApply,
                            ResetToDefault = true,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }

                    applied++;
                    appliedForRestart.Add(setting);
                    logService.Log(LogLevel.Debug, $"[BulkSettings] Reset to default for '{setting.Id}'");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to reset default for '{setting.Id}': {ex.Message}");
                }
            }
        }

        await processRestartManager.FlushCoalescedRestartsAsync(appliedForRestart).ConfigureAwait(false);

        progress?.Report(new TaskProgressDetail
        {
            Progress = 100,
            StatusText = $"Reset {applied} of {total} settings",
            IsCompletion = true,
            IsActive = false
        });

        return applied;
    }

    public async Task<int> GetAffectedCountAsync(
        IEnumerable<string> settingIds,
        BulkActionType actionType)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        int count = 0;

        foreach (var setting in settings)
        {
            try
            {
                bool wouldChange = actionType switch
                {
                    BulkActionType.ApplyRecommended => HasRecommendedValue(setting),
                    BulkActionType.ResetToDefaults => HasDefaultValue(setting),
                    _ => false
                };

                if (wouldChange)
                {
                    count++;
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"[BulkSettings] Error checking affected state for '{setting.Id}': {ex.Message}");
            }
        }

        return count;
    }

    #endregion

    #region Private Helpers

    private Task<List<SettingDefinition>> ResolveSettingsAsync(IEnumerable<string> settingIds)
    {
        var osInfo = new SystemInfo
        {
            BuildNumber = versionService.GetWindowsBuildNumber(),
            IsWindows10 = !versionService.IsWindows11(),
            IsWindows11 = versionService.IsWindows11()
        };

        var result = new List<SettingDefinition>();
        var idSet = settingIds.ToHashSet();

        foreach (var settingId in idSet)
        {
            try
            {
                var setting = settingsRegistry.GetById(settingId);
                if (setting == null)
                {
                    logService.Log(LogLevel.Warning, $"[BulkSettings] Setting '{settingId}' not found in registry");
                    continue;
                }

                if (IsCompatibleWithCurrentOS(setting, osInfo))
                {
                    result.Add(setting);
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to resolve setting '{settingId}': {ex.Message}");
            }
        }

        return Task.FromResult(result);
    }

    private static bool IsCompatibleWithCurrentOS(SettingDefinition setting, SystemInfo osInfo)
    {
        if (setting.IsWindows10Only && !osInfo.IsWindows10) return false;
        if (setting.IsWindows11Only && !osInfo.IsWindows11) return false;

        if (setting.SupportedBuildRanges?.Count > 0)
        {
            bool inSupportedRange = setting.SupportedBuildRanges.Any(range =>
                osInfo.BuildNumber >= range.MinBuild && osInfo.BuildNumber <= range.MaxBuild);
            if (!inSupportedRange) return false;
        }
        else
        {
            if (setting.MinimumBuildNumber.HasValue && osInfo.BuildNumber < setting.MinimumBuildNumber.Value) return false;
            if (setting.MaximumBuildNumber.HasValue && osInfo.BuildNumber > setting.MaximumBuildNumber.Value) return false;
        }

        return true;
    }

    private static object? GetRecommendedValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
        return registrySetting?.RecommendedValue;
    }

    private static object? GetDefaultValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.DefaultValue != null);
        return registrySetting?.DefaultValue;
    }

    private static bool HasRecommendedValue(SettingDefinition setting)
    {
        if (SettingDefinitionToggleState.GetRecommendedToggleState(setting).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsRecommended) == true) return true;
        return false;
    }

    private static bool HasDefaultValue(SettingDefinition setting)
    {
        if (SettingDefinitionToggleState.GetDefaultToggleState(setting).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsDefault) == true) return true;
        return false;
    }

    private static int? GetRecommendedIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsRecommended) return i;
        return null;
    }

    private static int? GetDefaultIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsDefault) return i;
        return null;
    }

    private static object? BuildPowerCfgApplyValue(SettingDefinition setting, bool useRecommended)
    {
        var pcfg = setting.PowerCfgSettings?.FirstOrDefault();
        if (pcfg == null) return null;

        int? acRaw = useRecommended ? pcfg.RecommendedValueAC : pcfg.DefaultValueAC;
        int? dcRaw = useRecommended ? pcfg.RecommendedValueDC : pcfg.DefaultValueDC;
        if (!acRaw.HasValue && !dcRaw.HasValue) return null;

        bool isSeparate = pcfg.PowerModeSupport == PowerModeSupport.Separate;

        if (setting.InputType == InputType.Selection)
        {
            int? acIdx = FindOptionIndexForPowerCfgValue(setting, acRaw);
            int? dcIdx = FindOptionIndexForPowerCfgValue(setting, dcRaw);

            if (isSeparate)
            {
                if (!acIdx.HasValue && !dcIdx.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acIdx ?? 0,
                    ["DCValue"] = dcIdx ?? 0
                };
            }
            return (object?)(acIdx ?? dcIdx);
        }

        if (setting.InputType == InputType.NumericRange)
        {
            string displayUnits = GetPowerCfgDisplayUnits(setting);
            int? acDisplay = acRaw.HasValue ? ConvertSystemToDisplayUnits(acRaw.Value, displayUnits) : null;
            int? dcDisplay = dcRaw.HasValue ? ConvertSystemToDisplayUnits(dcRaw.Value, displayUnits) : null;

            if (isSeparate)
            {
                if (!acDisplay.HasValue && !dcDisplay.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acDisplay ?? 0,
                    ["DCValue"] = dcDisplay ?? 0
                };
            }
            return (object?)(acDisplay ?? dcDisplay);
        }

        return null;
    }

    private static int? FindOptionIndexForPowerCfgValue(SettingDefinition setting, int? targetValue)
    {
        if (!targetValue.HasValue) return null;
        var opts = setting.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
        {
            if (opts[i].ValueMappings is { } m && m.TryGetValue("PowerCfgValue", out var v) && v != null)
            {
                try { if (Convert.ToInt32(v) == targetValue.Value) return i; }
                catch { }
            }
        }
        return null;
    }

    private static string GetPowerCfgDisplayUnits(SettingDefinition setting)
    {
        if (setting.NumericRange?.Units is { } unitsStr) return unitsStr;
        return setting.PowerCfgSettings?[0]?.Units ?? string.Empty;
    }

    private static int ConvertSystemToDisplayUnits(int systemValue, string? units)
    {
        return units?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }

    #endregion
}