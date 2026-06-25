// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ComboBoxMetadata
{
    public required IReadOnlyList<ComboBoxOption> Options { get; init; }
    public string? CustomStateDisplayName { get; init; }
}
