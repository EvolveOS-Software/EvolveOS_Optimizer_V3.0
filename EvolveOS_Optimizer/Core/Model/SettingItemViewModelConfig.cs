// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Model;

public record SettingItemViewModelConfig
{
    #region Core References
    public required SettingDefinition SettingDefinition { get; init; }
    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; init; }
    public required string SettingId { get; init; }
    #endregion

    #region UI & Display Metadata
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string IconPack { get; init; } = "Material";
    #endregion

    #region State & Input Configuration
    public required InputType InputType { get; init; }
    public bool IsSelected { get; init; }
    public string OnText { get; init; } = "On";
    public string OffText { get; init; } = "Off";
    #endregion
}