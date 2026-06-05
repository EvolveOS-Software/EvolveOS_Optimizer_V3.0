// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface INewBadgeService
{
    void Initialize(IEnumerable<string?> allAddedInVersions);

    bool IsSettingNew(string? addedInVersion, string settingId);
    bool ShowNewBadges { get; set; }
}
