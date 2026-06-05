// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IInteractiveUserService
{
    bool IsOtsElevation { get; }

    string? InteractiveUserSid { get; }

    string InteractiveUserName { get; }

    string GetInteractiveUserFolderPath(Environment.SpecialFolder folder);

    bool HasInteractiveUserToken { get; }

    Task<InteractiveProcessResult> RunProcessAsInteractiveUserAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default,
        int timeoutMs = 300_000,
        Action<string>? onProgressLine = null);

    void LaunchProcessAsInteractiveUser(string fileName, string arguments = "");
}

public record InteractiveProcessResult(int ExitCode, string StandardOutput, string StandardError);
