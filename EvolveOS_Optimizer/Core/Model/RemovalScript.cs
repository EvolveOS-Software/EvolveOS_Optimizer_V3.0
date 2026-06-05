// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public record RemovalScript
{
    public string Name { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string TargetScheduledTaskName { get; init; } = string.Empty;
    public bool RunOnStartup { get; init; }
    public string? ActualScriptPath { get; init; }
}
