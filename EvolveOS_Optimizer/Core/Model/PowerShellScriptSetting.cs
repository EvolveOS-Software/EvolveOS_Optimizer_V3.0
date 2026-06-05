// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model;

public sealed record PowerShellScriptSetting
{
    public string? Id { get; init; }
    public string? Script { get; init; }
    public string? EnabledScript { get; init; }
    public string? DisabledScript { get; init; }
    public string? Purpose { get; init; }
    public bool RequiresElevation { get; init; } = true;
    public RunContext RunContext { get; init; } = RunContext.System;
}
