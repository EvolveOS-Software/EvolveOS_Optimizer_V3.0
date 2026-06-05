// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ICompatibleSettingsRegistry
{
    Task InitializeAsync();
    IEnumerable<SettingDefinition> GetFilteredSettings(string featureId);
    IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllFilteredSettings();
    IEnumerable<SettingDefinition> GetBypassedSettings(string featureId);
    IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllBypassedSettings();
    void SetFilterEnabled(bool enabled);
    bool IsInitialized { get; }
    SettingDefinition? GetById(string settingId);
    string? GetFeatureIdForSetting(string settingId);
}
