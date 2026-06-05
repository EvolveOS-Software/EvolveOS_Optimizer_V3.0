// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record NumericRangeMetadata
{
    public required int MinValue { get; init; }
    public required int MaxValue { get; init; }
    public int Increment { get; init; } = 1;
    public string? Units { get; init; }
    public bool UseSlider { get; init; }
}
