// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingDependencyResolver
{
    Task HandleDependenciesAsync(string settingId, IEnumerable<SettingDefinition> allSettings, bool enable, object? value, ISettingApplicationService settingApplicationService);
    Task HandleValuePrerequisitesAsync(SettingDefinition setting, string settingId, IEnumerable<SettingDefinition> allSettings, ISettingApplicationService settingApplicationService);
    Task SyncParentToMatchingPresetAsync(SettingDefinition setting, string settingId, IEnumerable<SettingDefinition> allSettings, ISettingApplicationService settingApplicationService);
}
