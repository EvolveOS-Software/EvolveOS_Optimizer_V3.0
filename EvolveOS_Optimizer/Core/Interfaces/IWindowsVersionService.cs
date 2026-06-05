// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IWindowsVersionService
{
    int GetWindowsBuildNumber();
    int GetWindowsBuildRevision();
    bool IsWindows11();
    bool IsWindowsServer();
}
