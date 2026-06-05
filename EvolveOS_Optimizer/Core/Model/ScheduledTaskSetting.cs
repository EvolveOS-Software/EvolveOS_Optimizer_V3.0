// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ScheduledTaskSetting
{
    public string Id { get; init; } = string.Empty;
    public string TaskPath { get; init; } = string.Empty;
    public required bool? RecommendedState { get; init; }
    public required bool? DefaultState { get; init; }
}
