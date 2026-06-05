// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record SettingStateResult
{
    #region State & Value
    public bool IsEnabled { get; init; }
    public object? CurrentValue { get; init; }
    #endregion

    #region Result Status
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    #endregion

    #region Extended Metadata
    public IReadOnlyDictionary<string, object?>? RawValues { get; init; }
    public SettingTooltipData? TooltipData { get; init; }
    #endregion
}