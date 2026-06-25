// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Utilities.Services;

public class DispatcherService : IDispatcherService
{
    #region Fields & Properties

    private DispatcherQueue? _dispatcherQueue;

    public bool HasThreadAccess => _dispatcherQueue?.HasThreadAccess ?? false;

    #endregion

    #region Initialization

    public void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    #endregion

    #region Synchronous Execution

    public void RunOnUIThread(Action action)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }

    public void RunOnUIThread(DispatcherQueuePriority priority, Action action)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(priority, () => action());
        }
    }

    #endregion

    #region Asynchronous Execution

    public async Task RunOnUIThreadAsync(Func<Task> asyncAction)
    {
        await RunOnUIThreadAsync(DispatcherQueuePriority.Normal, asyncAction);
    }

    public async Task RunOnUIThreadAsync(DispatcherQueuePriority priority, Func<Task> asyncAction)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            await asyncAction();
            return;
        }

        var tcs = new TaskCompletionSource();

        var enqueued = _dispatcherQueue.TryEnqueue(priority, async () =>
        {
            try
            {
                await asyncAction();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            throw new InvalidOperationException("Failed to enqueue action to dispatcher queue.");
        }

        await tcs.Task;
    }

    public Task RunOnUIThreadWithContextAsync(Func<Task> asyncAction)
    {
        EnsureInitialized();

        var tcs = new TaskCompletionSource();

        void Start()
        {
            if (SynchronizationContext.Current is null)
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(_dispatcherQueue!));
            }

            _ = InvokeAsync();
        }

        async Task InvokeAsync()
        {
            try
            {
                await asyncAction();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        if (_dispatcherQueue!.HasThreadAccess)
        {
            Start();
        }
        else if (!_dispatcherQueue.TryEnqueue(Start))
        {
            throw new InvalidOperationException("Failed to enqueue action to dispatcher queue.");
        }

        return tcs.Task;
    }

    #endregion

    #region Private Helpers

    private void EnsureInitialized()
    {
        if (_dispatcherQueue == null)
        {
            throw new InvalidOperationException(
                "DispatcherService not initialized. Call Initialize() from MainWindow constructor after window creation.");
        }
    }

    #endregion
}