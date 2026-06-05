// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record FeatureBadgeSummary(
    int TotalWithBadgeData,
    int RecommendedCount,
    int DefaultCount,
    int CustomCount,
    int NewCount);
