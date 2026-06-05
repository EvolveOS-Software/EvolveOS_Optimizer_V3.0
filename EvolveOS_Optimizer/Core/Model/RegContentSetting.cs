// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record RegContentSetting
{
    public required string EnabledContent { get; init; }
    public required string DisabledContent { get; init; }
    public bool RequiresElevation { get; init; } = true;
}
