// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Model.Profiles; // The namespace from Step 1

namespace EvolveOS_Optimizer.Utilities.Services;

public class ProfileExportService : IProfileExportService
{
    private readonly ILogService _logService;
    private readonly ICompatibleSettingsRegistry _settingsRegistry;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly IFileSystemService _fileSystemService;

    // We use strict serialization options to keep the JSON clean and small
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfileExportService(
        ILogService logService,
        ICompatibleSettingsRegistry settingsRegistry,
        ISystemSettingsDiscoveryService discoveryService,
        IFileSystemService fileSystemService)
    {
        _logService = logService;
        _settingsRegistry = settingsRegistry;
        _discoveryService = discoveryService;
        _fileSystemService = fileSystemService;
    }

    public async Task ExportCurrentSystemStateAsync(string filePath)
    {
        _logService.Log(LogLevel.Info, $"[ProfileExportService] Starting system state export to: {filePath}");

        try
        {
            var profile = new EvolveOSProfile
            {
                ProfileVersion = "1.0",
                ExportDateUtc = DateTime.UtcNow,
                AppVersion = "2.0.0" // Update dynamically if you have an AppVersion service
            };

            // 1. Fetch all available settings from the registry
            var allSettings = _settingsRegistry.GetAllFilteredSettings();

            // 2. Fetch the actual LIVE state of the PC in one batch
            var flatSettingList = allSettings.SelectMany(kvp => kvp.Value).ToList();
            var systemStates = await _discoveryService.GetSettingStatesAsync(flatSettingList);

            // 3. Map to our clean JSON structure
            foreach (var kvp in allSettings)
            {
                var featureId = kvp.Key;
                var settingsForFeature = kvp.Value.ToList();

                if (settingsForFeature.Count == 0) continue;

                var isOptimize = FeatureDefinitions.OptimizeFeatures.Contains(featureId);
                var isCustomize = FeatureDefinitions.CustomizeFeatures.Contains(featureId);

                if (!isOptimize && !isCustomize) continue;

                var profileFeature = new ProfileFeature();

                foreach (var settingDef in settingsForFeature)
                {
                    // If we couldn't read the state, default to an empty result
                    var state = systemStates.TryGetValue(settingDef.Id, out var foundState)
                        ? foundState
                        : new SettingStateResult();

                    var profileItem = MapStateToProfileItem(settingDef, state);
                    profileFeature.Settings.Add(profileItem);
                }

                // Add to the correct category
                if (isOptimize)
                {
                    profile.Optimize.Features[featureId] = profileFeature;
                }
                else
                {
                    profile.Customize.Features[featureId] = profileFeature;
                }
            }

            // 4. Serialize and Write to Disk
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            await _fileSystemService.WriteAllTextAsync(filePath, json);

            _logService.Log(LogLevel.Info, "[ProfileExportService] Export completed successfully.");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"[ProfileExportService] Export failed: {ex.Message}");
            throw; // Re-throw so the UI can catch it and show an error dialog
        }
    }

    /// <summary>
    /// Translates the complex, OS-specific SettingStateResult into our clean JSON model.
    /// </summary>
    private ProfileSettingItem MapStateToProfileItem(SettingDefinition definition, SettingStateResult state)
    {
        var item = new ProfileSettingItem
        {
            Id = definition.Id
        };

        switch (definition.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
                item.IsEnabled = state.IsEnabled;
                break;

            case InputType.Selection:
                // Handle Power Plans specially by exporting their GUID
                if (definition.Id == SettingIds.PowerPlanSelection && state.RawValues != null)
                {
                    if (state.RawValues.TryGetValue("ActivePowerPlanGuid", out var guidObj) && guidObj != null)
                    {
                        item.CustomValue = guidObj.ToString();
                    }
                }
                // Handle standard ComboBoxes
                else if (state.CurrentValue is int selectedIndex)
                {
                    item.SelectedIndex = selectedIndex;

                    // If they selected a "Custom" state, we might need to store raw values
                    if (selectedIndex == ComboBoxConstants.CustomStateIndex && state.RawValues != null)
                    {
                        item.CustomValue = state.RawValues;
                    }
                }
                break;

            case InputType.NumericRange:
                // Just dump the raw numeric value
                item.CustomValue = state.CurrentValue;
                break;
        }

        return item;
    }
}