// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IProcessExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default);
    Task<ProcessExecutionResult> ExecuteWithStreamingAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken ct = default);
    void KillProcessesByName(string processName);
    Task<int?> ShellExecuteAsync(
        string fileName,
        string? arguments = null,
        bool waitForExit = false,
        CancellationToken ct = default);
}
