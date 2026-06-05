// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ImportOptions
{
    public bool ProcessWindowsAppsRemoval { get; init; }
    public bool ProcessWindowsAppsInstallation { get; init; }
    public bool ProcessExternalAppsInstallation { get; init; }
    public bool ProcessExternalAppsRemoval { get; init; }
    public bool ApplyThemeWallpaper { get; init; }
    public bool ApplyCleanTaskbar { get; init; }
    public bool ApplyCleanStartMenu { get; init; }
    public bool ReviewBeforeApplying { get; init; }
    public bool IsWindowsDefaults { get; init; }
    public IReadOnlyCollection<string> ActionOnlySubsections { get; init; } = new HashSet<string>();
}
