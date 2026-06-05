// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IScheduledTaskService
{
    Task<OperationResult> RegisterScheduledTaskAsync(RemovalScript script);
    Task<OperationResult> UnregisterScheduledTaskAsync(string taskName);
    Task<bool> IsTaskRegisteredAsync(string taskName);
    Task<OperationResult> RunScheduledTaskAsync(string taskName);
    Task<OperationResult> CreateUserLogonTaskAsync(string taskName, string command, string username, bool deleteAfterRun = true);
    Task<OperationResult> EnableTaskAsync(string taskPath);
    Task<OperationResult> DisableTaskAsync(string taskPath);
    Task<bool?> IsTaskEnabledAsync(string taskPath);
}
