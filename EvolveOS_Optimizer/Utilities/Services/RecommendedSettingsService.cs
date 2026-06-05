// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Provides logic for identifying and resolving recommended configuration settings based on system compatibility.
/// </summary>
/// <remarks>
/// <para>
/// <b>Compatibility Filtering:</b>
/// This service evaluates <see cref="SettingDefinition"/> objects against the current OS build number 
/// and Windows version (10 vs 11). It ensures that only settings explicitly compatible with the 
/// current environment are surfaced as "recommended."
/// </para>
/// <para>
/// <b>Resolution:</b>
/// It acts as the source of truth for determining whether a setting should be considered "Recommended" 
/// based on registry definitions or ComboBox metadata flags.
/// </para>
/// </remarks>

public class RecommendedSettingsService(
    ICompatibleSettingsRegistry compatibleSettingsRegistry,
    IWindowsVersionService versionService,
    ILogService logService) : IRecommendedSettingsService
{

    #region Public API

    public Task<IEnumerable<SettingDefinition>> GetRecommendedSettingsAsync(string settingId)
    {
        try
        {
            var featureId = compatibleSettingsRegistry.GetFeatureIdForSetting(settingId)
                ?? throw new InvalidOperationException($"Setting '{settingId}' has no feature mapping");
            logService.Log(LogLevel.Debug, $"[RecommendedSettings] Getting recommended settings for feature '{featureId}'");

            var allSettings = compatibleSettingsRegistry.GetFilteredSettings(featureId);

            var osInfo = new SystemInfo
            {
                BuildNumber = versionService.GetWindowsBuildNumber(),
                IsWindows10 = !versionService.IsWindows11(),
                IsWindows11 = versionService.IsWindows11()
            };

            var recommendedSettings = allSettings.Where(setting =>
                HasRecommendedValue(setting) && IsCompatibleWithCurrentOS(setting, osInfo)
            );

            var settingsList = recommendedSettings.ToList();
            logService.Log(LogLevel.Debug, $"[RecommendedSettings] Found {settingsList.Count} recommended settings for feature '{featureId}'");

            return Task.FromResult<IEnumerable<SettingDefinition>>(settingsList);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[RecommendedSettings] Error getting recommended settings: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Internal Recommendation Logic

    internal static int? GetRecommendedSelectionIndex(SettingDefinition setting)
    {
        var options = setting.ComboBox?.Options;
        if (options == null) return null;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].IsRecommended) return i;
        }
        return null;
    }

    internal static object? GetRecommendedValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
        return registrySetting?.RecommendedValue;
    }

    #endregion

    #region Private Helpers

    private static bool HasRecommendedValue(SettingDefinition setting)
    {
        return setting.RegistrySettings?.Any(rs => rs.RecommendedValue != null) == true;
    }

    private static bool IsCompatibleWithCurrentOS(SettingDefinition setting, SystemInfo osInfo)
    {
        if (setting.IsWindows10Only && !osInfo.IsWindows10) return false;
        if (setting.IsWindows11Only && !osInfo.IsWindows11) return false;

        if (setting.SupportedBuildRanges?.Count > 0)
        {
            bool inSupportedRange = setting.SupportedBuildRanges.Any(range =>
                osInfo.BuildNumber >= range.MinBuild && osInfo.BuildNumber <= range.MaxBuild);
            if (!inSupportedRange) return false;
        }
        else
        {
            if (setting.MinimumBuildNumber.HasValue && osInfo.BuildNumber < setting.MinimumBuildNumber.Value) return false;
            if (setting.MaximumBuildNumber.HasValue && osInfo.BuildNumber > setting.MaximumBuildNumber.Value) return false;
        }

        return true;
    }

    #endregion
}

