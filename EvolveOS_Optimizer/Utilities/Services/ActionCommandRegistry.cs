// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Utilities.Services;

public sealed class ActionCommandRegistry(IReadOnlyDictionary<string, IActionCommandProvider> providers)
    : IActionCommandRegistry
{
    public IActionCommandProvider? TryGet(string settingId)
        => providers.TryGetValue(settingId, out var p) ? p : null;
}
