// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Converters;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Native;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Executes the low-level system operations required to apply a <see cref="SettingDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service acts as the final executor in the settings pipeline. It maps high-level setting 
/// intent (Toggle, Selection, Numeric) to concrete system changes across multiple domains:
/// <list type="bullet">
/// <item><b>Registry:</b> Manipulating keys and values.</item>
/// <item><b>Power Configuration:</b> Managing power plans via PowerCfg and NT Power APIs.</item>
/// <item><b>OS Automation:</b> Running PowerShell scripts and managing scheduled tasks.</item>
/// <item><b>Context Awareness:</b> Handling OTS (Over-The-Shoulder) elevation to ensure changes 
/// apply to the interactive user session, not just the elevated process.</item>
/// </list>
/// </para>
/// </remarks>
public class SettingOperationExecutor(
    IWindowsRegistryService registryService,
    IComboBoxResolver comboBoxResolver,
    IProcessRestartManager processRestartManager,
    IPowerCfgApplier powerCfgApplier,
    IScheduledTaskService scheduledTaskService,
    IInteractiveUserService interactiveUserService,
    IProcessExecutor processExecutor,
    IPowerShellRunner powerShellRunner,
    IFileSystemService fileSystemService,
    ILogService logService) : ISettingOperationExecutor
{
    #region Public Execution API

    public async Task<OperationResult> ApplySettingOperationsAsync(SettingDefinition setting, bool enable, object? value, bool resetToDefault = false)
    {
        logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Processing operations for '{setting.Id}' - Type: {setting.InputType}{(resetToDefault ? " (resetting to default values)" : "")}");

        bool allSucceeded = true;
        var failedOperations = new List<string>();

        #region Registry Operations
        if (setting.RegistrySettings?.Count > 0 && setting.RegContents?.Count == 0)
        {
            if (setting.InputType == InputType.Selection && value is Dictionary<string, object> customValues)
            {
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with custom state values");

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    var valueName = registrySetting.ValueName ?? "KeyExists";

                    if (customValues.TryGetValue(valueName, out var specificValue))
                    {
                        bool ok = specificValue == null
                            ? registryService.ApplySetting(registrySetting, false)
                            : registryService.ApplySetting(registrySetting, true, specificValue);

                        if (!ok)
                        {
                            allSucceeded = false;
                            failedOperations.Add($"Registry: {registrySetting.KeyPath}\\{valueName}");
                            logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to apply registry setting: {registrySetting.KeyPath}\\{valueName}");
                        }
                    }
                }
            }
            else if (setting.InputType == InputType.Selection && (value is int || (value is string stringValue && !string.IsNullOrEmpty(stringValue))))
            {
                int index = value switch
                {
                    int intValue => intValue,
                    string strValue => comboBoxResolver.GetIndexFromDisplayName(setting, strValue),
                    _ => 0
                };
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with unified mapping for index: {index}");

                var specificValues = comboBoxResolver.ResolveIndexToRawValues(setting, index);

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    var valueName = registrySetting.ValueName ?? "KeyExists";
                    bool ok;

                    if (specificValues.TryGetValue(valueName, out var specificValue))
                    {
                        ok = specificValue == null
                            ? registryService.ApplySetting(registrySetting, false)
                            : registryService.ApplySetting(registrySetting, true, specificValue);
                    }
                    else
                    {
                        bool applyValue = comboBoxResolver.GetValueFromIndex(setting, index) != 0;
                        ok = registryService.ApplySetting(registrySetting, applyValue);
                    }

                    if (!ok)
                    {
                        allSucceeded = false;
                        failedOperations.Add($"Registry: {registrySetting.KeyPath}\\{valueName}");
                        logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to apply registry setting: {registrySetting.KeyPath}\\{valueName}");
                    }
                }
            }
            else
            {
                bool applyValue = setting.InputType switch
                {
                    InputType.Toggle => enable,
                    InputType.NumericRange when value != null => NumericConversionHelper.ConvertNumericValue(value) != 0,
                    InputType.Selection => enable,
                    _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for registry operations")
                };

                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with value: {applyValue}");

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    bool ok;
                    if (resetToDefault && !applyValue)
                    {
                        var parentDisableValue = registrySetting.DisabledValue?.Length > 1 ? registrySetting.DisabledValue[1] : null;
                        logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Parent cascade disable for '{registrySetting.KeyPath}\\{registrySetting.ValueName}' - using DisabledValue[1]: {parentDisableValue?.ToString() ?? "null (delete)"}");
                        ok = registryService.ApplySetting(registrySetting, false, useDefaultValue: true);
                    }
                    else
                    {
                        ok = registryService.ApplySetting(registrySetting, applyValue);
                    }

                    if (!ok)
                    {
                        allSucceeded = false;
                        var valueName = registrySetting.ValueName ?? "KeyExists";
                        failedOperations.Add($"Registry: {registrySetting.KeyPath}\\{valueName}");
                        logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to apply registry setting: {registrySetting.KeyPath}\\{valueName}");
                    }
                }
            }
        }
        #endregion

        #region Scheduled Tasks
        if (setting.ScheduledTaskSettings?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.ScheduledTaskSettings.Count} scheduled task settings for '{setting.Id}'");

            foreach (var taskSetting in setting.ScheduledTaskSettings)
            {
                var taskResult = enable
                    ? await scheduledTaskService.EnableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false)
                    : await scheduledTaskService.DisableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false);

                if (!taskResult.Success)
                {
                    allSucceeded = false;
                    failedOperations.Add($"ScheduledTask: {taskSetting.TaskPath}");
                    logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to {(enable ? "enable" : "disable")} scheduled task: {taskSetting.TaskPath}");
                }
            }
        }
        #endregion

        #region PowerShell Scripts
        if (setting.PowerShellScripts?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Executing {setting.PowerShellScripts.Count} PowerShell scripts for '{setting.Id}'");

            foreach (var scriptSetting in setting.PowerShellScripts)
            {
                var useEnabled = enable;
                if (setting.InputType == InputType.Selection
                    && setting.ComboBox?.Options is { } scriptOptions
                    && value is int scriptIndex
                    && scriptIndex >= 0 && scriptIndex < scriptOptions.Count
                    && scriptOptions[scriptIndex].Script is { } scriptOption)
                {
                    if (scriptOption == ScriptOption.None)
                    {
                        continue;
                    }

                    useEnabled = scriptOption == ScriptOption.Enabled;
                }

                var script = useEnabled ? scriptSetting.EnabledScript : scriptSetting.DisabledScript;

                if (!string.IsNullOrEmpty(script)
                    && setting.ComboBox?.Options is { } varOptions
                    && value is int varIndex
                    && varIndex >= 0 && varIndex < varOptions.Count
                    && varOptions[varIndex].ScriptVariables is { } variables)
                {
                    foreach (var kvp in variables)
                    {
                        script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
                    }
                }

                if (!string.IsNullOrEmpty(script))
                {
                    await powerShellRunner.RunScriptInMemoryAsync(script).ConfigureAwait(false);
                }
            }
        }
        #endregion

        #region Registry Content Imports (.reg files)
        if (setting.RegContents?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Importing {setting.RegContents.Count} registry contents for '{setting.Id}'");

            foreach (var regContentSetting in setting.RegContents)
            {
                var regContent = enable ? regContentSetting.EnabledContent : regContentSetting.DisabledContent;

                if (!string.IsNullOrEmpty(regContent))
                {
                    string tempDir;
                    if (interactiveUserService.IsOtsElevation)
                    {
                        var userLocalAppData = interactiveUserService.GetInteractiveUserFolderPath(
                            Environment.SpecialFolder.LocalApplicationData);
                        tempDir = fileSystemService.CombinePath(userLocalAppData, "Temp");
                        fileSystemService.CreateDirectory(tempDir);
                    }
                    else
                    {
                        tempDir = fileSystemService.GetTempPath();
                    }

                    var tempFile = fileSystemService.CombinePath(tempDir, $"evolveos_{Guid.NewGuid()}.reg");
                    try
                    {
                        await fileSystemService.WriteAllTextAsync(tempFile, regContent).ConfigureAwait(false);
                        logService.Log(LogLevel.Debug, $"[SettingOperationExecutor] Wrote registry content to temp file: {tempFile}");

                        if (interactiveUserService.IsOtsElevation
                            && interactiveUserService.HasInteractiveUserToken)
                        {
                            logService.Log(LogLevel.Debug, "[SettingOperationExecutor] OTS mode — running reg import as interactive user");
                            var result = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                                "reg.exe", $"import \"{tempFile}\"").ConfigureAwait(false);

                            if (result.ExitCode != 0)
                            {
                                logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] reg import as interactive user failed (exit {result.ExitCode}): {result.StandardError}");
                            }
                        }
                        else
                        {
                            await RunCommandAsync($"reg import \"{tempFile}\"").ConfigureAwait(false);
                        }

                        logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Registry import completed for '{setting.Id}'");
                    }
                    catch (Exception ex)
                    {
                        logService.Log(LogLevel.Error, $"[SettingOperationExecutor] Failed to import registry content for '{setting.Id}': {ex.Message}");
                        throw;
                    }
                    finally
                    {
                        if (fileSystemService.FileExists(tempFile))
                        {
                            fileSystemService.DeleteFile(tempFile);
                        }
                    }
                }
            }
        }
        #endregion

        #region Power Management
        if (setting.PowerCfgSettings?.Count > 0)
        {
            await powerCfgApplier.ApplyPowerCfgSettingsAsync(setting, enable, value).ConfigureAwait(false);
        }

        if (setting.NativePowerApiSettings?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.NativePowerApiSettings.Count} native power API settings for '{setting.Id}'");

            foreach (var apiSetting in setting.NativePowerApiSettings)
            {
                byte inputValue = enable ? apiSetting.EnabledValue : apiSetting.DisabledValue;
                var result = PowerProf.CallNtPowerInformation(
                    apiSetting.InformationLevel,
                    ref inputValue,
                    1,
                    IntPtr.Zero,
                    0);

                if (result == 0)
                {
                    logService.Log(LogLevel.Info, $"[SettingOperationExecutor] CallNtPowerInformation(level={apiSetting.InformationLevel}) succeeded for '{setting.Id}'");
                }
                else
                {
                    allSucceeded = false;
                    failedOperations.Add($"NativePowerApi: level={apiSetting.InformationLevel}");
                    logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] CallNtPowerInformation(level={apiSetting.InformationLevel}) failed with status {result} for '{setting.Id}'");
                }
            }
        }
        #endregion

        #region Restarts & Finalization
        await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

        if (!allSucceeded)
        {
            var message = $"One or more operations failed for '{setting.Id}': {string.Join(", ", failedOperations)}";
            logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] {message}");
            return OperationResult.Failed(message);
        }

        return OperationResult.Succeeded();
        #endregion
    }

    #endregion

    #region Private Helpers

    private async Task RunCommandAsync(string command)
    {
        try
        {
            var result = await processExecutor.ExecuteAsync("cmd.exe", $"/c {command}").ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Command failed: {command} - {result.StandardError}");
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[SettingOperationExecutor] Command execution failed: {command} - {ex.Message}");
        }
    }

    #endregion
}