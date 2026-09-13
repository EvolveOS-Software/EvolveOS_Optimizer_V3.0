// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IWindowsUIManagementService
{
    bool IsProcessRunning(string processName);
    void KillProcess(string processName);
    Task<OperationResult> RefreshWindowsGUI(bool killExplorer = true);
    void BroadcastRegionalSettingChange();
}
