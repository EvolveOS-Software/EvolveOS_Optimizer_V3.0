// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Native;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services;

public class PowerService(
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    ICompatibleSettingsRegistry compatibleSettingsRegistry,
    IEventBus eventBus,
    IPowerPlanComboBoxService powerPlanComboBoxService,
    IProcessExecutor processExecutor,
    IFileSystemService fileSystemService,
    IPowerSchemeOperations powerSchemeOperations) : IPowerService, ISpecialSettingHandler
{
    private volatile IEnumerable<SettingDefinition>? _cachedSettings;
    private readonly object _cacheLock = new object();

    public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
        {
            logService.Log(LogLevel.Info, "[PowerService] Applying power-plan-selection");

            if (value is Dictionary<string, object> planDict)
            {
                var guid = planDict["Guid"].ToString()!;
                var name = planDict["Name"].ToString()!;

                logService.Log(LogLevel.Info, $"[PowerService] Config import: applying power plan {name} ({guid})");
                return await ApplyPowerPlanByGuidAsync(setting, guid, name, settingApplicationService).ConfigureAwait(false);
            }

            if (value is int index)
            {
                logService.Log(LogLevel.Info, $"[PowerService] UI selection: applying power plan at index {index}");

                var resolution = await powerPlanComboBoxService.ResolvePowerPlanByIndexAsync(index).ConfigureAwait(false);
                if (!resolution.Success)
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to resolve power plan index: {resolution.ErrorMessage}");
                    return false;
                }

                return await ApplyPowerPlanSelectionAsync(setting, resolution.Guid, index, resolution.DisplayName, settingApplicationService).ConfigureAwait(false);
            }

            logService.Log(LogLevel.Error, $"[PowerService] Invalid power plan value type: {value?.GetType().Name}");
            return false;
        }

        return false;
    }

    public async Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(IEnumerable<SettingDefinition> settings)
    {
        var results = new Dictionary<string, Dictionary<string, object?>>();

        var powerPlanSetting = settings.FirstOrDefault(s => s.Id == SettingIds.PowerPlanSelection);
        if (powerPlanSetting != null)
        {
            await CleanupCorruptEvolveOSPlanAsync().ConfigureAwait(false);

            var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            var rawValues = new Dictionary<string, object?>
            {
                ["ActivePowerPlan"] = activePlan?.Name,
                ["ActivePowerPlanGuid"] = activePlan?.Guid
            };
            results[SettingIds.PowerPlanSelection] = rawValues;
        }

        return results;
    }

    private async Task CleanupCorruptEvolveOSPlanAsync()
    {
        try
        {
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var evolveosGuid = "57696e68-616e-6365-506f-776572000000";

            var matchingPlan = systemPlans.FirstOrDefault(p =>
                string.Equals(p.Guid, evolveosGuid, StringComparison.OrdinalIgnoreCase));

            if (matchingPlan != null &&
                !string.Equals(matchingPlan.Name?.Trim(), "EvolveOS Optimizer Power Plan", StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, $"[PowerService] Detected corrupt EvolveOS plan (name: '{matchingPlan.Name}'), cleaning up");

                if (matchingPlan.IsActive)
                {
                    var balancedGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
                    var activateResult = powerSchemeOperations.SetActiveScheme(balancedGuid);
                    if (activateResult == PowerProf.ERROR_SUCCESS)
                    {
                        logService.Log(LogLevel.Info, "[PowerService] Switched to Balanced before deleting corrupt EvolveOS plan");
                    }
                }

                var deleteResult = powerSchemeOperations.DeleteScheme(Guid.Parse(evolveosGuid));
                if (deleteResult == PowerProf.ERROR_SUCCESS)
                {
                    logService.Log(LogLevel.Info, "[PowerService] Successfully deleted corrupt EvolveOS plan");
                    powerSettingsQueryService.InvalidateCache();
                }
                else
                {
                    logService.Log(LogLevel.Warning, $"[PowerService] Failed to delete corrupt EvolveOS plan: error 0x{deleteResult:X8}");
                }
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Error during EvolveOS plan cleanup: {ex.Message}");
        }
    }

    private Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return Task.FromResult(_cachedSettings);

        lock (_cacheLock)
        {
            if (_cachedSettings != null)
                return Task.FromResult(_cachedSettings);

            try
            {
                logService.Log(LogLevel.Info, "Loading Power settings");
                _cachedSettings = compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.Power);
                return Task.FromResult(_cachedSettings);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Power settings: {ex.Message}");
                return Task.FromResult(Enumerable.Empty<SettingDefinition>());
            }
        }
    }

    private void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedSettings = null;
        }
    }

    public async Task<PowerPlan?> GetActivePowerPlanAsync()
    {
        try
        {
            return await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
            return null;
        }
    }

    public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
    {
        try
        {
            var powerPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            return powerPlans.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting available power plans: {ex.Message}");
            return Enumerable.Empty<object>();
        }
    }


    private async Task<bool> SetActivePowerPlanAsync(string powerPlanGuid)
    {
        try
        {
            var currentActivePlan = await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
            if (currentActivePlan != null && string.Equals(currentActivePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Info, $"Power plan {powerPlanGuid} is already active, skipping application");
                return true;
            }

            var schemeGuid = Guid.Parse(powerPlanGuid);
            var result = powerSchemeOperations.SetActiveScheme(schemeGuid);

            if (result == PowerProf.ERROR_SUCCESS)
            {
                powerSettingsQueryService.InvalidateCache();
                return true;
            }

            logService.Log(LogLevel.Warning, $"PowerSetActiveScheme failed with code {result}");
            return false;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error setting active power plan: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
    {
        try
        {
            logService.Log(LogLevel.Info, $"Attempting to delete power plan: {powerPlanGuid}");

            var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            if (activePlan != null && string.Equals(activePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, "Cannot delete active power plan");
                return false;
            }

            var schemeGuid = Guid.Parse(powerPlanGuid);
            var result = powerSchemeOperations.DeleteScheme(schemeGuid);

            if (result == PowerProf.ERROR_SUCCESS)
            {
                powerSettingsQueryService.InvalidateCache();
                logService.Log(LogLevel.Info, $"Successfully deleted power plan: {powerPlanGuid}");
                return true;
            }
            else
            {
                logService.Log(LogLevel.Error, $"Failed to delete power plan: {powerPlanGuid}. Error code: {result}");
                return false;
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ApplyPowerPlanSelectionAsync(SettingDefinition setting, string powerPlanGuid, int planIndex, string planName, ISettingApplicationService? settingApplicationService)
    {
        logService.Log(LogLevel.Info, $"[PowerService] Applying power plan: {planName} ({powerPlanGuid})");

        if (string.IsNullOrEmpty(powerPlanGuid))
        {
            throw new ArgumentException("Power plan GUID cannot be null or empty");
        }

        var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);

        var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var existingSystemPlan = systemPlans.FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        var planExists = existingSystemPlan != null;

        if (planExists && IsEvolveOSPowerPlan(powerPlanGuid) &&
            !string.Equals(existingSystemPlan!.Name?.Trim(), "EvolveOS Optimizer Power Plan", StringComparison.OrdinalIgnoreCase))
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Found corrupt EvolveOS Optimizer plan (name: '{existingSystemPlan.Name}'), deleting and recreating");
            var corruptGuid = Guid.Parse(powerPlanGuid);
            powerSchemeOperations.DeleteScheme(corruptGuid);
            powerSettingsQueryService.InvalidateCache();
            planExists = false;
        }

        bool success = false;

        if (!planExists)
        {
            var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            if (predefinedPlan != null)
            {
                logService.Log(LogLevel.Info, $"[PowerService] Plan '{predefinedPlan.Name}' not found, attempting import");
                var importResult = await ImportPowerPlanAsync(predefinedPlan).ConfigureAwait(false);

                if (importResult.Success)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Successfully imported '{predefinedPlan.Name}', activating");
                    await Task.Delay(200).ConfigureAwait(false);

                    var importedSchemeGuid = Guid.Parse(importResult.ImportedGuid);
                    var activateResult = powerSchemeOperations.SetActiveScheme(importedSchemeGuid);
                    success = activateResult == PowerProf.ERROR_SUCCESS;

                    if (success)
                    {
                        powerSettingsQueryService.InvalidateCache();
                        InvalidateCache();
                        logService.Log(LogLevel.Info, $"[PowerService] Successfully activated imported plan");
                    }
                    else
                    {
                        logService.Log(LogLevel.Warning, $"[PowerService] First activation failed, retrying...");
                        await Task.Delay(500).ConfigureAwait(false);
                        activateResult = powerSchemeOperations.SetActiveScheme(importedSchemeGuid);
                        success = activateResult == PowerProf.ERROR_SUCCESS;

                        if (success)
                        {
                            powerSettingsQueryService.InvalidateCache();
                            InvalidateCache();
                            logService.Log(LogLevel.Info, $"[PowerService] Successfully activated on retry");
                        }
                        else
                        {
                            logService.Log(LogLevel.Error, $"[PowerService] Failed to activate after import. Error code: {activateResult}");
                        }
                    }

                    powerPlanGuid = importResult.ImportedGuid;
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                    return false;
                }
            }
            else
            {
                logService.Log(LogLevel.Error, $"[PowerService] Unknown power plan GUID: {powerPlanGuid}");
                return false;
            }
        }
        else
        {
            success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
        }

        if (success)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Publishing PowerPlanChangedEvent");

            eventBus.Publish(new PowerPlanChangedEvent
            {
                PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                NewPlanGuid = powerPlanGuid,
                NewPlanName = planName,
                NewPlanIndex = planIndex
            });

            if (IsEvolveOSPowerPlan(powerPlanGuid))
            {
                await ApplyEvolveOSRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
            }

            logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan");
        }

        return success;
    }

    private async Task<bool> ApplyPowerPlanByGuidAsync(SettingDefinition setting, string powerPlanGuid, string planName, ISettingApplicationService? settingApplicationService)
    {
        logService.Log(LogLevel.Info, $"[PowerService] Applying power plan by GUID: {planName} ({powerPlanGuid})");

        if (string.IsNullOrEmpty(powerPlanGuid))
        {
            throw new ArgumentException("Power plan GUID cannot be null or empty");
        }

        var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
        var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var planExists = systemPlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

        bool success = false;

        if (!planExists)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Plan '{planName}' ({powerPlanGuid}) not found on system");

            var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            if (predefinedPlan != null)
            {
                logService.Log(LogLevel.Info, $"[PowerService] Importing predefined plan '{predefinedPlan.Name}'");
                var importResult = await ImportPowerPlanAsync(predefinedPlan).ConfigureAwait(false);

                if (importResult.Success)
                {
                    logService.Log(LogLevel.Info, "[PowerService] Successfully imported, now activating");
                    await Task.Delay(200).ConfigureAwait(false);

                    success = await SetActivePowerPlanAsync(importResult.ImportedGuid).ConfigureAwait(false);
                    powerPlanGuid = importResult.ImportedGuid;
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                    return false;
                }
            }
            else
            {
                logService.Log(LogLevel.Info, $"[PowerService] Custom power plan '{planName}' - creating by duplicating Balanced");

                var targetGuid = Guid.Parse(powerPlanGuid);
                var cleanupResult = powerSchemeOperations.DeleteScheme(targetGuid);
                if (cleanupResult == PowerProf.ERROR_SUCCESS)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Cleaned up ghost plan entry with GUID {powerPlanGuid}");
                }

                var (dupSuccess, dupOutput) = await RunPowercfgAsync($"/duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e {powerPlanGuid}").ConfigureAwait(false);

                if (dupSuccess)
                {
                    var actualGuid = ParseGuidFromPowercfgOutput(dupOutput) ?? powerPlanGuid;
                    if (!string.Equals(actualGuid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        logService.Log(LogLevel.Warning, $"[PowerService] powercfg assigned GUID {actualGuid} instead of requested {powerPlanGuid}");
                    }

                    SetPowerPlanName(Guid.Parse(actualGuid), planName);

                    powerSettingsQueryService.InvalidateCache();
                    logService.Log(LogLevel.Info, $"[PowerService] Successfully created custom plan '{planName}' with GUID {actualGuid}");

                    powerPlanGuid = actualGuid;
                    success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to create custom plan '{planName}' with GUID {powerPlanGuid}");
                    return false;
                }
            }
        }
        else
        {
            success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
        }

        if (success)
        {
            var options = await powerPlanComboBoxService.GetPowerPlanOptionsAsync().ConfigureAwait(false);
            var planIndex = options.FindIndex(o =>
                string.Equals(o.SystemPlan?.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            eventBus.Publish(new PowerPlanChangedEvent
            {
                PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                NewPlanGuid = powerPlanGuid,
                NewPlanName = planName,
                NewPlanIndex = planIndex >= 0 ? planIndex : 0
            });

            if (IsEvolveOSPowerPlan(powerPlanGuid))
            {
                await ApplyEvolveOSRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
            }

            logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan '{planName}'");
        }

        return success;
    }

    public async Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan)
    {
        try
        {
            if (predefinedPlan.Name == "EvolveOS Optimizer Power Plan")
            {
                return await CreateEvolveOSPowerPlanAsync(predefinedPlan).ConfigureAwait(false);
            }

            if (predefinedPlan.Name == "Ultimate Performance")
            {
                var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                var existingPlan = systemPlans.FirstOrDefault(p => PowerPlanHelper.IsUltimatePerformancePlan(p.Name));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Ultimate Performance plan already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                var sourceGuid = Guid.Parse(predefinedPlan.Guid);
                var dupResult = powerSchemeOperations.DuplicateScheme(sourceGuid, out var newGuid);

                if (dupResult == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();

                    var actualGuid = newGuid.ToString("D");

                    if (!string.IsNullOrEmpty(actualGuid))
                    {
                        SetPowerPlanNameAndDescription(newGuid, predefinedPlan.Name, predefinedPlan.Description);
                        return new PowerPlanImportResult(true, actualGuid);
                    }
                }

                return new PowerPlanImportResult(false, "", "Ultimate Performance creation failed");
            }
            else
            {
                var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                var existingPlan = systemPlans.FirstOrDefault(p =>
                    string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Power plan '{predefinedPlan.Name}' already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                logService.Log(LogLevel.Info, $"Attempting to duplicate power plan '{predefinedPlan.Name}' using GUID {predefinedPlan.Guid}");
                var srcGuid = Guid.Parse(predefinedPlan.Guid);
                var duplicateResult = powerSchemeOperations.DuplicateScheme(srcGuid, out var dupNewGuid);

                if (duplicateResult == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();

                    var actualGuid = dupNewGuid.ToString("D");

                    if (!string.IsNullOrEmpty(actualGuid))
                    {
                        logService.Log(LogLevel.Info, $"Successfully duplicated power plan '{predefinedPlan.Name}' with GUID: {actualGuid}");
                        return new PowerPlanImportResult(true, actualGuid);
                    }
                }

                logService.Log(LogLevel.Warning, $"Duplicate scheme failed for '{predefinedPlan.Name}', falling back to backup/restore method");
                return await SimpleBackupRestore(predefinedPlan).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }

    private async Task<PowerPlanImportResult> SimpleBackupRestore(PredefinedPowerPlan targetPlan)
    {
        var backupDir = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\EvolveOS\Backup\PowerPlans");

        try
        {
            await BackupCustomPlansAsync(backupDir).ConfigureAwait(false);

            var restoreResult = PowerProf.PowerRestoreDefaultPowerSchemes();
            if (restoreResult != PowerProf.ERROR_SUCCESS)
                return new PowerPlanImportResult(false, "", "Failed to restore default schemes");

            await Task.Delay(1000).ConfigureAwait(false);
            await RestoreCustomPlansAsync(backupDir).ConfigureAwait(false);

            powerSettingsQueryService.InvalidateCache();

            if (fileSystemService.DirectoryExists(backupDir))
            {
                fileSystemService.DeleteDirectory(backupDir, true);
            }

            var plans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var targetGuid = plans.FirstOrDefault(p =>
                string.Equals(PowerPlanHelper.CleanPlanName(p.Name), targetPlan.Name, StringComparison.OrdinalIgnoreCase))?.Guid;

            return !string.IsNullOrEmpty(targetGuid)
                ? new PowerPlanImportResult(true, targetGuid)
                : new PowerPlanImportResult(false, "", "Target plan not found after restore");
        }
        catch (Exception ex)
        {
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }



    private async Task BackupCustomPlansAsync(string backupFolder)
    {
        fileSystemService.CreateDirectory(backupFolder);

        var allPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var customPlans = IdentifyCustomPlans(allPlans);

        foreach (var plan in customPlans)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"{SanitizeFilename(plan.Name)}_{timestamp}.pow";
            var filepath = fileSystemService.CombinePath(backupFolder, filename);

            await RunPowercfgAsync($"/export \"{filepath}\" {plan.Guid}").ConfigureAwait(false);
        }
    }

    private async Task RestoreCustomPlansAsync(string backupFolder)
    {
        if (!fileSystemService.DirectoryExists(backupFolder)) return;

        var backupFiles = fileSystemService.GetFiles(backupFolder, "*.pow");
        foreach (var file in backupFiles)
        {
            var importResult = PowerProf.PowerImportPowerScheme(IntPtr.Zero, file, out var importedPtr);
            if (importResult == PowerProf.ERROR_SUCCESS)
            {
                PowerProf.LocalFree(importedPtr);
            }
            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    private List<PowerPlan> IdentifyCustomPlans(List<PowerPlan> allPlans)
    {
        var builtInGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a1841308-3541-4fab-bc81-f71556f20b4a",
            "381b4222-f694-41f0-9685-ff5bb260df2e",
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
        };

        var builtInNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Power Saver", "Balanced", "High Performance"
        };

        return allPlans.Where(plan =>
            !builtInGuids.Contains(plan.Guid) ||
            !builtInNames.Contains(PowerPlanHelper.CleanPlanName(plan.Name))
        ).ToList();
    }

    private string SanitizeFilename(string filename)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task<PowerPlanImportResult> CreateEvolveOSPowerPlanAsync(PredefinedPowerPlan predefinedPlan)
    {
        var ultimatePerformancePlan = PowerPlanDefinitions.BuiltInPowerPlans
            .FirstOrDefault(p => p.Name == "Ultimate Performance");

        if (ultimatePerformancePlan == null)
        {
            return new PowerPlanImportResult(false, "", "Ultimate Performance plan not found");
        }

        try
        {
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var existingPlan = systemPlans.FirstOrDefault(p =>
                string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

            if (existingPlan != null &&
                string.Equals(existingPlan.Name?.Trim(), "EvolveOS Optimizer Power Plan", StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Info, $"EvolveOS Optimizer Power Plan already exists with GUID: {existingPlan.Guid}");
                return new PowerPlanImportResult(true, existingPlan.Guid);
            }

            var evolveosGuid = Guid.Parse(predefinedPlan.Guid);
            var cleanupResult = powerSchemeOperations.DeleteScheme(evolveosGuid);
            if (cleanupResult == PowerProf.ERROR_SUCCESS)
            {
                logService.Log(LogLevel.Info, existingPlan != null
                    ? $"[PowerService] Deleted corrupt EvolveOS Optimizer Plan (name was: '{existingPlan.Name}')"
                    : "[PowerService] Cleaned up ghost EvolveOS Optimizer Power Plan entry");
                powerSettingsQueryService.InvalidateCache();
            }

            logService.Log(LogLevel.Info, "Creating EvolveOS Optimizer Power Plan from Ultimate Performance");

            var (dupSuccess, dupOutput) = await RunPowercfgAsync($"/duplicatescheme {ultimatePerformancePlan.Guid} {predefinedPlan.Guid}").ConfigureAwait(false);

            if (!dupSuccess)
            {
                logService.Log(LogLevel.Error, "Failed to duplicate plan for EvolveOS Optimizer Power Plan");
                return new PowerPlanImportResult(false, "", "Failed to create plan");
            }

            var actualGuid = ParseGuidFromPowercfgOutput(dupOutput) ?? predefinedPlan.Guid;
            if (!string.Equals(actualGuid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, $"[PowerService] powercfg assigned GUID {actualGuid} instead of requested {predefinedPlan.Guid}");
            }

            SetPowerPlanNameAndDescription(Guid.Parse(actualGuid), predefinedPlan.Name, predefinedPlan.Description);

            await ApplyRecommendedSettingsToPlanAsync(actualGuid).ConfigureAwait(false);

            powerSettingsQueryService.InvalidateCache();

            logService.Log(LogLevel.Info, $"Successfully created EvolveOS Optimizer Power Plan: {actualGuid}");
            return new PowerPlanImportResult(true, actualGuid);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error creating EvolveOS Optimizer Power Plan: {ex.Message}");
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }

    private async Task ApplyRecommendedSettingsToPlanAsync(string planGuid)
    {
        logService.Log(LogLevel.Info, $"Applying recommended settings to plan: {planGuid}");

        try
        {
            var allSettings = await GetSettingsAsync().ConfigureAwait(false);
            int appliedCount = 0;

            foreach (var setting in allSettings)
            {
                try
                {
                    var powerCfgWithRecommended = setting.PowerCfgSettings?.FirstOrDefault(ps =>
                        ps.RecommendedValueAC.HasValue || ps.RecommendedValueDC.HasValue);

                    if (powerCfgWithRecommended != null)
                    {
                        var acValue = powerCfgWithRecommended.RecommendedValueAC ?? powerCfgWithRecommended.RecommendedValueDC ?? 0;
                        var dcValue = powerCfgWithRecommended.RecommendedValueDC ?? powerCfgWithRecommended.RecommendedValueAC ?? 0;

                        logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {acValue}, DC: {dcValue}");

                        var planSchemeGuid = Guid.Parse(planGuid);
                        var subgroupGuid = Guid.Parse(powerCfgWithRecommended.SubgroupGuid);
                        var settGuid = Guid.Parse(powerCfgWithRecommended.SettingGuid);

                        PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)acValue);
                        PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)dcValue);

                        appliedCount++;
                        continue;
                    }

                    if (setting.InputType == InputType.Selection &&
                        setting.Recommendation?.RecommendedOptionAC != null &&
                        setting.PowerCfgSettings?.Any() == true)
                    {
                        var recommendedOptionAC = setting.Recommendation.RecommendedOptionAC;
                        var recommendedOptionDC = setting.Recommendation.RecommendedOptionDC ?? recommendedOptionAC;

                        var options = setting.ComboBox?.Options;

                        if (options != null)
                        {
                            var indexAC = -1;
                            var indexDC = -1;
                            for (int oi = 0; oi < options.Count; oi++)
                            {
                                if (indexAC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionAC, StringComparison.Ordinal))
                                    indexAC = oi;
                                if (indexDC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionDC, StringComparison.Ordinal))
                                    indexDC = oi;
                            }

                            if (options.Any(o => o.ValueMappings != null))
                            {
                                int? acValue = null, dcValue = null;

                                if (indexAC >= 0 && options[indexAC].ValueMappings is { } valueDictAC &&
                                    valueDictAC.TryGetValue("PowerCfgValue", out var powerCfgValueAC) && powerCfgValueAC != null)
                                    acValue = Convert.ToInt32(powerCfgValueAC);

                                if (indexDC >= 0 && options[indexDC].ValueMappings is { } valueDictDC &&
                                    valueDictDC.TryGetValue("PowerCfgValue", out var powerCfgValueDC) && powerCfgValueDC != null)
                                    dcValue = Convert.ToInt32(powerCfgValueDC);

                                if (acValue.HasValue && dcValue.HasValue)
                                {
                                    var powerCfgSetting = setting.PowerCfgSettings[0];

                                    logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {recommendedOptionAC} ({acValue}), DC: {recommendedOptionDC} ({dcValue})");

                                    var recPlanGuid = Guid.Parse(planGuid);
                                    var recSubGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                                    var recSettGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                                    PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)acValue.Value);
                                    PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)dcValue.Value);

                                    appliedCount++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to apply recommended setting '{setting.Id}': {ex.Message}");
                }
            }

            logService.Log(LogLevel.Info, $"Applied {appliedCount} PowerCfg settings to EvolveOS Optimizer Power Plan");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error applying recommended settings: {ex.Message}");
        }
    }

    private static bool IsEvolveOSPowerPlan(string guid) =>
        IsEvolveOSPowerPlan(guid, null);

    private static bool IsEvolveOSPowerPlan(string guid, string? name) =>
        string.Equals(guid, "57696e68-616e-6365-506f-776572000000", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name?.Trim(), "EvolveOS Optimizer Power Plan", StringComparison.OrdinalIgnoreCase);

    private async Task ApplyEvolveOSRecommendedSettingsAsync(ISettingApplicationService? settingApplicationService)
    {
        try
        {
            if (settingApplicationService == null)
                throw new InvalidOperationException("settingApplicationService is required for applying recommended settings");
            logService.Log(LogLevel.Info, "[PowerService] Applying recommended settings for EvolveOS Optimizer Power Plan");
            await settingApplicationService.ApplyRecommendedSettingsForFeatureAsync(SettingIds.PowerPlanSelection).ConfigureAwait(false);
            logService.Log(LogLevel.Info, "[PowerService] Successfully applied recommended settings for EvolveOS Optimizer Power Plan");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Failed to apply recommended settings: {ex.Message}");
        }
    }

    private void SetPowerPlanName(Guid schemeGuid, string name)
    {
        powerSchemeOperations.WriteFriendlyName(schemeGuid, name);
    }

    private void SetPowerPlanNameAndDescription(Guid schemeGuid, string name, string description)
    {
        powerSchemeOperations.WriteFriendlyName(schemeGuid, name);

        if (!string.IsNullOrEmpty(description))
        {
            powerSchemeOperations.WriteDescription(schemeGuid, description);
        }
    }

    private static string? ParseGuidFromPowercfgOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        var match = Regex.Match(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task<(bool Success, string Output)> RunPowercfgAsync(string arguments, bool useCmd = false)
    {
        try
        {
            string fileName;
            string args;

            if (useCmd)
            {
                fileName = "cmd.exe";
                args = $"/c {arguments}";
            }
            else
            {
                fileName = "powercfg";
                args = arguments;
            }

            var result = await processExecutor.ExecuteAsync(fileName, args).ConfigureAwait(false);
            return (result.Succeeded, result.StandardOutput.TrimEnd());
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"powercfg {arguments} failed: {ex.Message}");
            return (false, string.Empty);
        }
    }

}
