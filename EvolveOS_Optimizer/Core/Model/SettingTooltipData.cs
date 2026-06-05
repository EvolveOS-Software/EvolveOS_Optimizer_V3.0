// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record SettingTooltipData
{
    #region Core Identification & Definition
    public string SettingId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public SettingDefinition? SettingDefinition { get; init; }
    #endregion

    #region Current State Information
    public bool? CurrentSettingState { get; init; }
    public IReadOnlyDictionary<RegistrySetting, string?> IndividualRegistryValues { get; init; } = new Dictionary<RegistrySetting, string?>();
    public IReadOnlyDictionary<PowerCfgSetting, (int? AC, int? DC)> CurrentPowerValues { get; init; } = new Dictionary<PowerCfgSetting, (int? AC, int? DC)>();
    #endregion

    #region Execution & Automation Payloads
    public IReadOnlyList<ScheduledTaskSetting> ScheduledTaskSettings { get; init; } = new List<ScheduledTaskSetting>();
    public IReadOnlyList<PowerCfgSetting> PowerCfgSettings { get; init; } = new List<PowerCfgSetting>();
    public IReadOnlyList<PowerShellScriptSetting> PowerShellScripts { get; init; } = new List<PowerShellScriptSetting>();
    public IReadOnlyList<RegContentSetting> RegContents { get; init; } = new List<RegContentSetting>();
    #endregion

    #region Dependencies
    public IReadOnlyList<SettingDependency> Dependencies { get; init; } = new List<SettingDependency>();
    #endregion
}