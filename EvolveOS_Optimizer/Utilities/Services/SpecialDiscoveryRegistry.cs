// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Utilities.Services;

public sealed class SpecialDiscoveryRegistry(IReadOnlyList<ISpecialSettingHandler> handlers)
    : ISpecialDiscoveryRegistry
{
    public IEnumerable<ISpecialSettingHandler> All => handlers;
}
