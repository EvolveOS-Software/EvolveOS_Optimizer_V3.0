// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Utilities.Extensions;

public static class TaskExtensions
{
    public static async void FireAndForget(this Task task, ILogService logService, [CallerMemberName] string? callerName = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"[FireAndForget] Unobserved exception in {callerName}: {ex.Message}");
        }
    }
}
