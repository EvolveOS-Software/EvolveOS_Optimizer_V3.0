// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingLocalizationService
{
    SettingDefinition LocalizeSetting(SettingDefinition setting);
    string? BuildCrossGroupInfoMessage(SettingDefinition setting);
}
