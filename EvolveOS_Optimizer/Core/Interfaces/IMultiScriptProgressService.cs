// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IMultiScriptProgressService
{
    CancellationTokenSource StartMultiScriptTask(string[] scriptNames);
    IProgress<TaskProgressDetail> CreateScriptProgress(int slotIndex);
    void CompleteMultiScriptTask();
}
