// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model;

public abstract record BaseDefinition
{
    #region Metadata
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? GroupName { get; init; }
    public string? AddedInVersion { get; init; }
    public string? VersionCompatibilityMessage { get; init; }
    public bool DisableTooltip { get; init; }
    #endregion

    #region System Requirements
    public bool IsWindows11Only { get; init; }
    public bool IsWindows10Only { get; init; }
    public int? MinimumBuildNumber { get; init; }
    public int? MinimumBuildRevision { get; init; }
    public int? MaximumBuildNumber { get; init; }
    public int? MaximumBuildRevision { get; init; }
    #endregion

    #region UI and Icon Configuration
    public string? Icon { get; init; }
    public string? IconPack { get; init; } = "Material";
    public InputType InputType { get; init; } = InputType.Toggle;
    #endregion

    #region Registry and Functional Configuration
    public IReadOnlyList<RegistrySetting> RegistrySettings { get; init; } = Array.Empty<RegistrySetting>();
    public string? RestartProcess { get; init; }
    public string? RestartService { get; init; }
    public bool RequiresRestart { get; init; }
    public DetectionType? DetectionType { get; init; }
    #endregion

    #region Advanced UI Metadata
    public ComboBoxMetadata? ComboBox { get; init; }
    public NumericRangeMetadata? NumericRange { get; init; }
    public PowerRecommendation? Recommendation { get; init; }
    public Dictionary<int, Dictionary<string, bool>>? SettingPresets { get; init; }
    public Dictionary<string, string>? CrossGroupChildSettings { get; init; }
    #endregion
}
