// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Utilities.Services;

public sealed class SpecialSettingHandlerRegistry(IReadOnlyDictionary<string, ISpecialSettingHandler> handlers)
    : ISpecialSettingHandlerRegistry
{
    public ISpecialSettingHandler? TryGet(string settingId)
        => handlers.TryGetValue(settingId, out var h) ? h : null;
}
