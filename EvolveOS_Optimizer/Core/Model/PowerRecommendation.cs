// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record PowerRecommendation
{
    public string? RecommendedOptionAC { get; init; }
    public string? RecommendedOptionDC { get; init; }
    public bool LoadDynamicOptions { get; init; }
}
