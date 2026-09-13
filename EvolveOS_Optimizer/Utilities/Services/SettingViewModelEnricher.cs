// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Utilities.Services;

public class SettingViewModelEnricher : ISettingViewModelEnricher
{
    private readonly IHardwareDetectionService _hardwareDetectionService;
    private readonly ISettingLocalizationService _settingLocalizationService;

    public SettingViewModelEnricher(
        IHardwareDetectionService hardwareDetectionService,
        ISettingLocalizationService settingLocalizationService)
    {
        _hardwareDetectionService = hardwareDetectionService;
        _settingLocalizationService = settingLocalizationService;
    }

    public async Task DetectBatteryAsync(SettingItemViewModel viewModel)
    {
        viewModel.HasBattery = await _hardwareDetectionService.HasBatteryAsync();
    }

    public void SetCrossGroupInfoMessage(SettingItemViewModel viewModel, SettingDefinition setting)
    {
        viewModel.CrossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);
    }
}
