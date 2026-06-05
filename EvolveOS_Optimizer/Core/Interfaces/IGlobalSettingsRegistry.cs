// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IGlobalSettingsRegistry
{
    void RegisterSettings(string moduleName, IEnumerable<ISettingItem> settings);

    ISettingItem? GetSetting(string settingId, string? moduleName = null);

    IEnumerable<ISettingItem> GetAllSettings();

    void RegisterSetting(string moduleName, ISettingItem setting);

}
