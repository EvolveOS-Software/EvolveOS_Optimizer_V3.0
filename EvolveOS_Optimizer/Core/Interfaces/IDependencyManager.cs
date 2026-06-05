// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IDependencyManager
{
    Task<bool> HandleSettingEnabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ISystemSettingsDiscoveryService discoveryService);
    Task HandleSettingDisabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ISystemSettingsDiscoveryService discoveryService);
    Task HandleSettingValueChangedAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ISystemSettingsDiscoveryService discoveryService);
}
