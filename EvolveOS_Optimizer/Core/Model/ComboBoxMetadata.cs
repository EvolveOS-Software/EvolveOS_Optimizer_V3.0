// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Generic;

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ComboBoxMetadata
{
    public required IReadOnlyList<ComboBoxOption> Options { get; init; }
    public string? CustomStateDisplayName { get; init; }
}
