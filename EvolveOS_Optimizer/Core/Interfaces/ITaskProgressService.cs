// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ITaskProgressService
{
    bool IsTaskRunning { get; }
    int CurrentProgress { get; }
    string CurrentStatusText { get; }
    bool IsIndeterminate { get; }
    CancellationTokenSource? CurrentTaskCancellationSource { get; }
    CancellationTokenSource StartTask(string taskName, bool isIndeterminate = false);
    void UpdateProgress(int progressPercentage, string? statusText = null);
    void UpdateDetailedProgress(TaskProgressDetail detail);
    void CompleteTask();
    void CancelCurrentTask();
    IProgress<TaskProgressDetail> CreateDetailedProgress();
    IProgress<TaskProgressDetail> CreatePowerShellProgress();
    event EventHandler<TaskProgressDetail>? ProgressUpdated;
    bool ConsumeSkipNextRequest();
    IReadOnlyList<string> GetTerminalOutputLines();

}
