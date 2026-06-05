// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IDispatcherService
{
    void Initialize(DispatcherQueue dispatcherQueue);

    bool HasThreadAccess { get; }

    void RunOnUIThread(Action action);

    void RunOnUIThread(DispatcherQueuePriority priority, Action action);

    Task RunOnUIThreadAsync(Func<Task> asyncAction);

    Task RunOnUIThreadAsync(DispatcherQueuePriority priority, Func<Task> asyncAction);

    Task RunOnUIThreadWithContextAsync(Func<Task> asyncAction);
}
