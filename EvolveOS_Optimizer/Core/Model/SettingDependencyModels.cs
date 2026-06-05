// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

#region Model Definitions
public sealed record SettingDependency
{
    #region Relationship
    public SettingDependencyType DependencyType { get; init; }
    public required string DependentSettingId { get; init; }
    public required string RequiredSettingId { get; init; }
    #endregion

    #region Scope & Values
    public string? RequiredModule { get; init; }
    public string? RequiredValue { get; init; }
    #endregion
}
#endregion

#region Enums
public enum SettingDependencyType
{
    RequiresEnabled,
    RequiresDisabled,
    RequiresSpecificValue,
    RequiresValueBeforeAnyChange
}
#endregion