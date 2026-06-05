// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Orchestrates complex setting dependencies, including cascading enables, value prerequisites, and preset synchronization.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dependency Resolution:</b>
/// This service acts as the central engine for enforcing logical rules between settings. It manages three primary behaviors:
/// <list type="bullet">
/// <item><b>Cascading Dependencies:</b> Automatically enables or resets dependent settings when a parent's state changes.</item>
/// <item><b>Value Prerequisites:</b> Forces a required setting to a specific state before a dependent setting can be modified.</item>
/// <item><b>Preset Sync:</b> Observes child settings and automatically synchronizes a parent setting to a matching "preset" index if the child values align.</item>
/// </list>
/// </para>
/// </remarks>
public class SettingDependencyResolver(
    IDependencyManager dependencyManager,
    IGlobalSettingsRegistry globalSettingsRegistry,
    ISystemSettingsDiscoveryService discoveryService,
    IWindowsCompatibilityFilter compatibilityFilter,
    IProcessRestartManager processRestartManager,
    ILogService logService) : ISettingDependencyResolver
{

    #region Public API: Dependency & Prerequisite Handling

    public async Task HandleDependenciesAsync(
        string settingId,
        IEnumerable<SettingDefinition> allSettings,
        bool enable,
        object? value,
        ISettingApplicationService settingApplicationService)
    {
        if (enable)
        {
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);
            var directionalDependencies = setting?.Dependencies?
                .Where(d => d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange)
                .ToList();

            if (directionalDependencies?.Any() == true)
            {
                logService.Log(LogLevel.Info, $"[SettingDependencyResolver] Handling dependencies for '{settingId}'");
                var dependencyResult = await dependencyManager.HandleSettingEnabledAsync(settingId, allSettings.Cast<ISettingItem>(), settingApplicationService, discoveryService).ConfigureAwait(false);
                if (!dependencyResult)
                    throw new InvalidOperationException($"Cannot enable '{settingId}' due to unsatisfied dependencies");
            }

            if (setting?.AutoEnableSettingIds?.Count > 0)
            {
                using (processRestartManager.SuppressRestarts())
                {
                    foreach (var autoEnableId in setting.AutoEnableSettingIds)
                    {
                        try
                        {
                            var autoEnableDef = allSettings.FirstOrDefault(s => s.Id == autoEnableId)
                                ?? globalSettingsRegistry.GetSetting(autoEnableId) as SettingDefinition;
                            if (autoEnableDef != null)
                            {
                                logService.Log(LogLevel.Info,
                                    $"[SettingDependencyResolver] Auto-enabling '{autoEnableId}' because '{settingId}' was enabled");
                                await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                                {
                                    SettingId = autoEnableId,
                                    Enable = true,
                                    SkipValuePrerequisites = true
                                }).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            logService.Log(LogLevel.Warning,
                                $"[SettingDependencyResolver] Failed to auto-enable '{autoEnableId}': {ex.Message}");
                        }
                    }
                }
            }
        }
        else
        {
            var allRegisteredSettings = globalSettingsRegistry.GetAllSettings();
            var hasDependentSettings = allRegisteredSettings.Any(s => s.Dependencies?.Any(d =>
                d.RequiredSettingId == settingId &&
                d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange) == true);
            if (hasDependentSettings)
            {
                logService.Log(LogLevel.Info, $"[SettingDependencyResolver] Handling dependent settings for disabled '{settingId}'");
                await dependencyManager.HandleSettingDisabledAsync(settingId, allRegisteredSettings, settingApplicationService, discoveryService).ConfigureAwait(false);
            }
        }

        if (enable && value != null)
        {
            var allRegisteredSettings = globalSettingsRegistry.GetAllSettings();
            await dependencyManager.HandleSettingValueChangedAsync(settingId, allRegisteredSettings, settingApplicationService, discoveryService).ConfigureAwait(false);
        }
    }

    public async Task HandleValuePrerequisitesAsync(
        SettingDefinition setting,
        string settingId,
        IEnumerable<SettingDefinition> allSettings,
        ISettingApplicationService settingApplicationService)
    {
        if (setting.Dependencies?.Any() != true)
        {
            return;
        }

        var valuePrerequisites = setting.Dependencies
            .Where(d => d.DependencyType == SettingDependencyType.RequiresValueBeforeAnyChange)
            .ToList();

        if (!valuePrerequisites.Any())
        {
            return;
        }

        foreach (var dependency in valuePrerequisites)
        {
            logService.Log(LogLevel.Info,
                $"[ValuePrereq] Processing: '{settingId}' requires '{dependency.RequiredSettingId}' = '{dependency.RequiredValue}'");

            var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);

            if (requiredSetting == null)
            {
                requiredSetting = globalSettingsRegistry.GetSetting(dependency.RequiredSettingId) as SettingDefinition;
            }

            if (requiredSetting == null)
            {
                logService.Log(LogLevel.Warning,
                    $"[ValuePrereq] Required setting '{dependency.RequiredSettingId}' not found in current module or global registry");
                continue;
            }

            var states = await discoveryService.GetSettingStatesAsync(new[] { requiredSetting }).ConfigureAwait(false);
            if (!states.TryGetValue(dependency.RequiredSettingId, out var currentState) || !currentState.Success)
            {
                logService.Log(LogLevel.Warning,
                    $"[ValuePrereq] Could not get current state of '{dependency.RequiredSettingId}'");
                continue;
            }

            bool requirementMet = DoesCurrentValueMatchRequirement(
                requiredSetting,
                currentState,
                dependency.RequiredValue);

            if (!requirementMet)
            {
                logService.Log(LogLevel.Info,
                    $"[ValuePrereq] Auto-fixing '{dependency.RequiredSettingId}' to '{dependency.RequiredValue}' before applying '{settingId}'");

                var valueToApply = GetValueToApplyForRequirement(requiredSetting, dependency.RequiredValue);

                await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                {
                    SettingId = dependency.RequiredSettingId,
                    Enable = true,
                    Value = valueToApply,
                    SkipValuePrerequisites = true
                }).ConfigureAwait(false);

                logService.Log(LogLevel.Info,
                    $"[ValuePrereq] Successfully auto-fixed '{dependency.RequiredSettingId}', proceeding with '{settingId}'");
            }
        }
    }

    public async Task SyncParentToMatchingPresetAsync(
        SettingDefinition setting,
        string settingId,
        IEnumerable<SettingDefinition> allSettings,
        ISettingApplicationService settingApplicationService)
    {
        var prerequisite = setting.Dependencies?
            .FirstOrDefault(d => d.DependencyType == SettingDependencyType.RequiresValueBeforeAnyChange);

        if (prerequisite == null)
        {
            return;
        }

        var parentSetting = allSettings.FirstOrDefault(s => s.Id == prerequisite.RequiredSettingId);
        if (parentSetting?.SettingPresets == null || parentSetting.SettingPresets.Count == 0)
        {
            return;
        }

        var presets = parentSetting.SettingPresets;

        if (presets.Count == 0)
        {
            return;
        }

        logService.Log(LogLevel.Info,
            $"[PostChange] Checking if child settings now match a preset for parent '{prerequisite.RequiredSettingId}'");

        foreach (var (presetIndex, presetChildren) in presets)
        {
            var allMatch = await DoAllChildrenMatchPreset(presetChildren, allSettings).ConfigureAwait(false);

            if (allMatch)
            {
                logService.Log(LogLevel.Info,
                    $"[PostChange] All children match preset at index {presetIndex}, syncing parent '{prerequisite.RequiredSettingId}'");

                await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                {
                    SettingId = prerequisite.RequiredSettingId,
                    Enable = true,
                    Value = presetIndex,
                    SkipValuePrerequisites = true
                }).ConfigureAwait(false);

                return;
            }
        }

        logService.Log(LogLevel.Debug,
            $"[PostChange] No preset match found for parent '{prerequisite.RequiredSettingId}', leaving at current value");
    }

    #endregion

    #region Private Helpers

    private async Task<bool> DoAllChildrenMatchPreset(
        Dictionary<string, bool> preset,
        IEnumerable<SettingDefinition> allSettings)
    {
        var compatiblePresetEntries = new Dictionary<string, bool>();

        foreach (var (childId, expectedValue) in preset)
        {
            var childSetting = globalSettingsRegistry.GetSetting(childId);
            if (childSetting == null)
            {
                logService.Log(LogLevel.Debug,
                    $"[PostChange] Skipping preset child '{childId}' from matching - not registered (likely OS-filtered)");
                continue;
            }

            if (childSetting is SettingDefinition childSettingDef)
            {
                var compatibleSettings = compatibilityFilter.FilterSettingsByWindowsVersion(new[] { childSettingDef });
                if (!compatibleSettings.Any())
                {
                    logService.Log(LogLevel.Debug,
                        $"[PostChange] Skipping preset child '{childId}' from matching - not compatible with current OS version");
                    continue;
                }
            }

            compatiblePresetEntries[childId] = expectedValue;
        }

        var childSettingDefinitions = allSettings
            .Where(s => compatiblePresetEntries.ContainsKey(s.Id))
            .ToList();

        if (childSettingDefinitions.Count != compatiblePresetEntries.Count)
        {
            logService.Log(LogLevel.Info,
                $"[PostChange] Child count mismatch - Expected: {compatiblePresetEntries.Count}, Found in allSettings: {childSettingDefinitions.Count}");
            logService.Log(LogLevel.Info,
                $"[PostChange] This is likely because child settings span multiple features. Fetching from global registry instead.");

            childSettingDefinitions.Clear();
            foreach (var childId in compatiblePresetEntries.Keys)
            {
                var childSetting = globalSettingsRegistry.GetSetting(childId) as SettingDefinition;
                if (childSetting != null)
                {
                    childSettingDefinitions.Add(childSetting);
                }
            }

            if (childSettingDefinitions.Count != compatiblePresetEntries.Count)
            {
                logService.Log(LogLevel.Warning,
                    $"[PostChange] Still mismatched after global registry lookup - Expected: {compatiblePresetEntries.Count}, Found: {childSettingDefinitions.Count}");
                return false;
            }
        }

        var states = await discoveryService.GetSettingStatesAsync(childSettingDefinitions).ConfigureAwait(false);

        foreach (var (childId, expectedValue) in compatiblePresetEntries)
        {
            if (!states.TryGetValue(childId, out var state) || !state.Success)
            {
                logService.Log(LogLevel.Debug,
                    $"[PostChange] Could not get state for child '{childId}'");
                return false;
            }

            if (state.IsEnabled != expectedValue)
            {
                logService.Log(LogLevel.Info,
                    $"[PostChange] Child '{childId}' mismatch - Expected: {expectedValue}, Actual: {state.IsEnabled}");
                return false;
            }

            logService.Log(LogLevel.Debug,
                $"[PostChange] Child '{childId}' matches - Value: {state.IsEnabled}");
        }

        return true;
    }

    private bool DoesCurrentValueMatchRequirement(
        SettingDefinition setting,
        SettingStateResult currentState,
        string? requiredValue)
    {
        if (string.IsNullOrEmpty(requiredValue))
        {
            return true;
        }

        if (setting.InputType == InputType.Selection &&
            setting.ComboBox?.Options is { } options)
        {
            int requiredIndex = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].DisplayName.Equals(requiredValue, StringComparison.OrdinalIgnoreCase))
                {
                    requiredIndex = i;
                    break;
                }
            }

            if (requiredIndex >= 0 && currentState.CurrentValue is int currentIndex)
            {
                return currentIndex == requiredIndex;
            }
        }

        if (setting.InputType == InputType.Toggle)
        {
            bool requiredBool = requiredValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                               requiredValue.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            bool currentBool = currentState.IsEnabled;
            return currentBool == requiredBool;
        }

        return false;
    }

    private object? GetValueToApplyForRequirement(SettingDefinition setting, string? requiredValue)
    {
        if (string.IsNullOrEmpty(requiredValue))
        {
            return null;
        }

        if (setting.InputType == InputType.Selection &&
            setting.ComboBox?.Options is { } options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].DisplayName.Equals(requiredValue, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            logService.Log(LogLevel.Warning,
                $"[ValuePrereq] Could not find ComboBox option matching '{requiredValue}'");
            return null;
        }

        if (setting.InputType == InputType.Toggle)
        {
            return requiredValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   requiredValue.Equals("enabled", StringComparison.OrdinalIgnoreCase);
        }

        return null;
    }

    #endregion
}
