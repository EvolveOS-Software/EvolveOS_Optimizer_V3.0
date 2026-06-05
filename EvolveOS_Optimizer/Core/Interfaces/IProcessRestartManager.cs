// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IProcessRestartManager
{
    Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting);
    Task FlushCoalescedRestartsAsync(IEnumerable<SettingDefinition> appliedSettings);
    IDisposable SuppressRestarts();
}
