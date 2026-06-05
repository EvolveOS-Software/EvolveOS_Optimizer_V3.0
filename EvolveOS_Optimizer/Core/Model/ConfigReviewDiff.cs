// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ConfigReviewDiff
{
    #region Identification
    public string SettingId { get; init; } = string.Empty;
    public string SettingName { get; init; } = string.Empty;
    public string FeatureModuleId { get; init; } = string.Empty;
    #endregion

    #region Value Comparison
    public string CurrentValueDisplay { get; init; } = string.Empty;
    public string ConfigValueDisplay { get; init; } = string.Empty;
    public object? ConfigValue { get; init; }
    public string? CurrentDisplayKey { get; init; }
    public string? ConfigDisplayKey { get; init; }
    public ConfigurationItem? ConfigItem { get; init; }
    #endregion

    #region Review Status
    public bool IsReviewed { get; init; } = false;
    public bool IsApproved { get; init; } = false;
    #endregion

    #region Action Configuration
    public InputType InputType { get; init; }
    public bool IsActionSetting { get; init; }
    public string? ActionConfirmationMessage { get; init; }
    public bool IsActionReviewed { get; init; }
    public bool IsActionApproved { get; init; }
    #endregion
}
