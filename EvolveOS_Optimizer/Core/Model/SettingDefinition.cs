// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Model;

public sealed record SettingDefinition : BaseDefinition, ISettingItem
{
    #region Action & Automation
    public bool RequiresConfirmation { get; init; } = false;
    public string? ActionCommand { get; init; }
    public string? ActionText { get; set; }
    public IReadOnlyList<ScheduledTaskSetting> ScheduledTaskSettings { get; init; } = Array.Empty<ScheduledTaskSetting>();
    public IReadOnlyList<PowerShellScriptSetting> PowerShellScripts { get; init; } = Array.Empty<PowerShellScriptSetting>();
    public IReadOnlyList<RegContentSetting> RegContents { get; init; } = Array.Empty<RegContentSetting>();
    public IReadOnlyList<PowerCfgSetting>? PowerCfgSettings { get; init; }
    public IReadOnlyList<NativePowerApiSetting> NativePowerApiSettings { get; init; } = Array.Empty<NativePowerApiSetting>();
    #endregion

    #region Dependencies & Relationships
    public IReadOnlyList<SettingDependency> Dependencies { get; init; } = Array.Empty<SettingDependency>();
    public IReadOnlyList<string>? AutoEnableSettingIds { get; init; }
    public string? ParentSettingId { get; init; }
    #endregion

    #region System Requirements & Validation
    public IReadOnlyList<(int MinBuild, int MaxBuild)> SupportedBuildRanges { get; init; } = Array.Empty<(int MinBuild, int MaxBuild)>();
    public bool RequiresBattery { get; init; }
    public bool RequiresLid { get; init; }
    public bool RequiresDesktop { get; init; }
    public bool RequiresBrightnessSupport { get; init; }
    public bool RequiresHybridSleepCapable { get; init; }
    public bool ValidateExistence { get; init; } = true;
    #endregion

    #region State Configuration
    public bool RequiresAdvancedUnlock { get; init; } = false;
    public bool IsSubjectivePreference { get; init; } = false;
    public bool? RecommendedToggleState { get; init; }
    public bool? DefaultToggleState { get; init; }
    public string? LocalizationId { get; init; }
    public bool ResolveUnmatchedToDefault { get; init; } = false;
    #endregion
}
