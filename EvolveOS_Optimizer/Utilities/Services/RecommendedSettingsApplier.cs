// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Orchestrates the batch application of recommended configuration settings for feature modules.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service acts as a mediator between the <see cref="IRecommendedSettingsService"/> (which fetches 
/// recommended values) and the <see cref="ISettingApplicationService"/> (which executes the changes).
/// </para>
/// <para>
/// <b>Batch Logic:</b>
/// It iterates through feature-specific dependencies and applies recommended states—including toggles, 
/// selection indices, and raw values—while bypassing standard prerequisites to ensure a clean 
/// batch apply operation for "recommended" configurations.
/// </para>
/// </remarks>
public class RecommendedSettingsApplier(
    ICompatibleSettingsRegistry compatibleSettingsRegistry,
    IRecommendedSettingsService recommendedSettingsService,
    ILogService logService) : IRecommendedSettingsApplier
{
    #region Public API

    public async Task ApplyRecommendedSettingsForFeatureAsync(string settingId, ISettingApplicationService settingApplicationService)
    {
        try
        {
            var featureId = compatibleSettingsRegistry.GetFeatureIdForSetting(settingId)
                ?? throw new InvalidOperationException($"Setting '{settingId}' has no feature mapping");

            logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] Starting to apply recommended settings for feature '{featureId}'");

            var recommendedSettings = await recommendedSettingsService.GetRecommendedSettingsAsync(settingId).ConfigureAwait(false);
            var settingsList = recommendedSettings.Where(s => s.Id != settingId).ToList();

            logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] Found {settingsList.Count} recommended settings for feature '{featureId}'");

            if (settingsList.Count == 0)
            {
                logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] No recommended settings found for feature '{featureId}'");
                return;
            }

            foreach (var setting in settingsList)
            {
                await ApplySingleSettingAsync(setting, settingApplicationService).ConfigureAwait(false);
            }

            logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] Completed applying recommended settings for feature '{featureId}'");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[RecommendedSettingsApplier] Error applying recommended settings: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Private Helpers

    private async Task ApplySingleSettingAsync(SettingDefinition setting, ISettingApplicationService settingApplicationService)
    {
        try
        {
            var recommendedValue = RecommendedSettingsService.GetRecommendedValueForSetting(setting);
            logService.Log(LogLevel.Debug, $"[RecommendedSettingsApplier] Applying recommended setting '{setting.Id}' with value '{recommendedValue}'");

            if (setting.InputType == InputType.Toggle)
            {
                var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
                bool enableValue = false;

                if (registrySetting != null && recommendedValue != null)
                {
                    enableValue = registrySetting.EnabledValue?.Any(ev => ev != null && recommendedValue.Equals(ev)) == true;
                }

                await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                {
                    SettingId = setting.Id,
                    Enable = enableValue,
                    Value = recommendedValue,
                    SkipValuePrerequisites = true
                }).ConfigureAwait(false);
            }
            else if (setting.InputType == InputType.Selection)
            {
                var recommendedIndex = RecommendedSettingsService.GetRecommendedSelectionIndex(setting);

                if (recommendedIndex.HasValue)
                {
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = true,
                        Value = recommendedIndex.Value,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }
                else
                {
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = true,
                        Value = recommendedValue,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }
            }
            else
            {
                await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                {
                    SettingId = setting.Id,
                    Enable = true,
                    Value = recommendedValue,
                    SkipValuePrerequisites = true
                }).ConfigureAwait(false);
            }

            logService.Log(LogLevel.Debug, $"[RecommendedSettingsApplier] Successfully applied recommended setting '{setting.Id}'");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[RecommendedSettingsApplier] Failed to apply recommended setting '{setting.Id}': {ex.Message}");
        }
    }

    #endregion
}