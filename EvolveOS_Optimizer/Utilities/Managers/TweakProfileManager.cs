// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Tweaks;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Managers;

internal static class TweakProfileManager
{
    private const string RegistryBaseKey = @"SOFTWARE\EvolveOS_Optimizer\SystemOptimizations";

    public static async Task<TweakProfileBackup> GenerateExportProfileAsync()
    {
        var profile = new TweakProfileBackup();

        profile.ServicesTweaks = ExtractBools(ServicesTweaks.ControlStates);
        profile.PrivacyTweaks = ExtractBools(PrivacyTweaks.ControlStates);
        profile.SystemTweaks = ExtractBools(SystemTweaks.ControlStates);
        profile.InterfaceTweaks = ExtractBools(InterfaceTweaks.ControlStates);

        profile.SystemSliders = ExtractUints(SystemTweaks.ControlStates);

        profile.WindowsUpdatesMode = await Task.Run(() =>
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                    ? RegistryView.Registry64
                    : RegistryView.Default).OpenSubKey(RegistryBaseKey);

            return key?.GetValue("WindowsUpdatesMode") as string ?? "Default";
        });

        profile.ActivePowerPlanGuid = await GetActivePowerPlanGuidAsync();

        return profile;
    }

    private static Dictionary<string, bool> ExtractBools(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, bool>();
        if (source == null) return result;

        foreach (var kvp in source)
        {
            if (kvp.Value is bool b)
            {
                result[kvp.Key] = b;
            }
            else if (bool.TryParse(kvp.Value?.ToString(), out bool parsedValue))
            {
                result[kvp.Key] = parsedValue;
            }
        }
        return result;
    }

    private static Dictionary<string, uint> ExtractUints(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, uint>();
        if (source == null) return result;

        foreach (var kvp in source)
        {
            if (kvp.Value is uint u) result[kvp.Key] = u;
            else if (kvp.Value is int i) result[kvp.Key] = (uint)i;
            else if (kvp.Value is double d) result[kvp.Key] = (uint)d;
            else if (uint.TryParse(kvp.Value?.ToString(), out uint parsedValue)) result[kvp.Key] = parsedValue;
        }
        return result;
    }

    public static async Task ApplyImportedProfileAsync(TweakProfileBackup profile)
    {
        await Task.Run(async () =>
        {
            try
            {
                // 1. Deploy Services
                if (profile.ServicesTweaks != null)
                {
                    var svcEngine = new ServicesTweaks();
                    foreach (var kvp in profile.ServicesTweaks)
                    {
                        await svcEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }

                // 2. Deploy Privacy
                if (profile.PrivacyTweaks != null)
                {
                    var privEngine = new PrivacyTweaks();
                    foreach (var kvp in profile.PrivacyTweaks)
                    {
                        await privEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }

                // 3. Deploy System
                if (profile.SystemTweaks != null)
                {
                    var sysEngine = new SystemTweaks();
                    foreach (var kvp in profile.SystemTweaks)
                    {
                        await sysEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }

                // 4. Deploy Interface
                if (profile.InterfaceTweaks != null)
                {
                    var uiEngine = new InterfaceTweaks();
                    foreach (var kvp in profile.InterfaceTweaks)
                    {
                        await uiEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }

                // 5. Deploy System Sliders
                if (profile.SystemSliders != null)
                {
                    var sysEngine = new SystemTweaks();
                    foreach (var kvp in profile.SystemSliders)
                    {
                        sysEngine.ApplyTweaksSlider(kvp.Key, kvp.Value);
                    }
                }

                // 6. Deploy Windows Updates Mode
                if (!string.IsNullOrEmpty(profile.WindowsUpdatesMode))
                {
                    await ApplyWindowsUpdatesModeAsync(profile.WindowsUpdatesMode);
                }

                // 7. Deploy Power Plan
                if (!string.IsNullOrEmpty(profile.ActivePowerPlanGuid))
                {
                    await CommandExecutor.StartTaskAsync($"powercfg /setactive {profile.ActivePowerPlanGuid}");
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"[TweakProfileManager] Import Failed: {ex.Message}");
                throw;
            }
        });
    }

    #region Private Custom Appliers & Fetchers

    private static async Task ApplyWindowsUpdatesModeAsync(string mode)
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
            Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                ? RegistryView.Registry64
                : RegistryView.Default).CreateSubKey(RegistryBaseKey);

        key?.SetValue("WindowsUpdatesMode", mode, RegistryValueKind.String);

        switch (mode)
        {
            case "Default":
                await SystemTweaks.SetWindowsUpdatesDefault();
                break;
            case "Security":
                await SystemTweaks.SetWindowsUpdatesSecurityOnly();
                break;
            case "Manually":
                await SystemTweaks.SetWindowsUpdatesManually();
                break;
            case "Disabled":
                await SystemTweaks.SetWindowsUpdatesDisabled();
                break;
        }
    }

    private static async Task<string?> GetActivePowerPlanGuidAsync()
    {
        try
        {
            var output = await CommandExecutor.StartTaskAsync("powercfg /getactivescheme");
            var match = Regex.Match(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");

            if (match.Success)
            {
                return match.Groups[1].Value.ToLowerInvariant();
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug($"[TweakProfileManager] Failed to get Power Plan: {ex.Message}");
        }
        return null;
    }

    #endregion
}