// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ILogService
{
    void StartLog();

    void LogInformation(string message);

    void LogWarning(string message);

    void LogError(string message, Exception? exception = null);

    void LogDebug(string message);

    string GetLogPath();

    void Log(LogLevel level, string message, Exception? exception = null);
}
