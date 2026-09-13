// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingOperationExecutor
{
    Task<OperationResult> ApplySettingOperationsAsync(SettingDefinition setting, bool enable, object? value, bool resetToDefault = false);
}
