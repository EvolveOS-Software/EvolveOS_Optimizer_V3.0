// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

public class SettingPreparationPipeline : ISettingPreparationPipeline
{
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISettingLocalizationService _settingLocalizationService;

    public SettingPreparationPipeline(
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISettingLocalizationService settingLocalizationService)
    {
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingLocalizationService = settingLocalizationService;
    }

    public IReadOnlyList<SettingDefinition> PrepareSettings(string featureModuleId)
    {
        var settingDefinitions = _compatibleSettingsRegistry.GetFilteredSettings(featureModuleId);
        return settingDefinitions
            .Select(s => _settingLocalizationService.LocalizeSetting(s))
            .ToList();
    }
}