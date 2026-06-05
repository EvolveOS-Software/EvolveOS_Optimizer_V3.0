// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record SettingGroup
{
    public required string Name { get; init; }
    public required string FeatureId { get; init; }
    public required IReadOnlyList<SettingDefinition> Settings { get; init; }
}
