// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IConfigReviewModeService
{
    bool IsInReviewMode { get; }
    bool IsWindowsDefaults { get; }
    UnifiedConfigurationFile? ActiveConfig { get; }
    Task EnterReviewModeAsync(UnifiedConfigurationFile config, bool isWindowsDefaults = false);
    void ExitReviewMode();
    event EventHandler? ReviewModeChanged;
}
