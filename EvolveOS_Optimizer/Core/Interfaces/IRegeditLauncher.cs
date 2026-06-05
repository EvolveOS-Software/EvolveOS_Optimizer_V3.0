// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IRegeditLauncher
{
    bool KeyExists(string registryPath);
    void OpenAtPath(string registryPath);
}
