// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ApplySettingRequest
{
    public required string SettingId { get; init; }
    public required bool Enable { get; init; }
    public object? Value { get; init; }
    public bool CheckboxResult { get; init; }
    public string? CommandString { get; init; }
    public bool ApplyRecommended { get; init; }
    public bool SkipValuePrerequisites { get; init; }
    public bool ResetToDefault { get; init; }
}
