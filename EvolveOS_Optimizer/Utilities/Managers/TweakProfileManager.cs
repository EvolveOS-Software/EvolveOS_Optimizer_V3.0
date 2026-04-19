// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Tweaks;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Configuration;

namespace EvolveOS_Optimizer.Utilities.Managers;

internal static class TweakProfileManager
{
    #region Constants & Fields
    private const string RegistryBaseKey = @"SOFTWARE\EvolveOS_Optimizer\SystemOptimizations";
    #endregion

    #region Profile Export
    public static async Task<TweakProfileBackup> GenerateExportProfileAsync()
    {
        var profile = new TweakProfileBackup();

        // 1. Core Tweaks
        profile.ServicesTweaks = ExtractBools(ServicesTweaks.ControlStates);
        profile.PrivacyTweaks = ExtractBools(PrivacyTweaks.ControlStates);
        profile.SystemTweaks = ExtractBools(SystemTweaks.ControlStates);
        profile.InterfaceTweaks = ExtractBools(InterfaceTweaks.ControlStates);
        profile.SystemSliders = ExtractUints(SystemTweaks.ControlStates);

        // 2. System Custom Settings
        profile.WindowsUpdatesMode = await Task.Run(() =>
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                    ? RegistryView.Registry64
                    : RegistryView.Default).OpenSubKey(RegistryBaseKey);

            return key?.GetValue("WindowsUpdatesMode") as string ?? "Default";
        });

        profile.ActivePowerPlanGuid = await GetActivePowerPlanGuidAsync();

        // 3. Security Custom Settings
        try
        {
            profile.UacLevel = GetUacLevel();
            profile.IsRemoteDesktopEnabled = await SecurityDiagnostics.IsRdpEnabledAsync(CancellationToken.None);
            profile.IsRemoteAssistanceEnabled = await SecurityDiagnostics.IsRemoteAssistanceEnabledAsync(CancellationToken.None);
            profile.IsDeveloperModeEnabled = await SecurityDiagnostics.IsDeveloperModeEnabledAsync(CancellationToken.None);

            var sacState = await SecurityDiagnostics.GetSmartAppControlStateAsync(CancellationToken.None);
            profile.SmartAppControlState = sacState >= 0 ? (uint)sacState : 2u;

            var psPolicy = await SecurityDiagnostics.GetPowerShellExecutionPolicyAsync(CancellationToken.None);
            profile.PowerShellExecutionPolicy = psPolicy != "Error" ? psPolicy : "Restricted";
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug($"[TweakProfileManager] Failed to export some security settings: {ex.Message}");
        }

        return profile;
    }
    #endregion

    #region Data Extraction Helpers
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
    #endregion

    #region Profile Import
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

                // 8. Deploy Security Settings
                if (profile.UacLevel.HasValue) ApplyUacLevel(profile.UacLevel.Value);
                if (profile.SmartAppControlState.HasValue) ApplySmartAppControlState(profile.SmartAppControlState.Value);
                if (!string.IsNullOrEmpty(profile.PowerShellExecutionPolicy)) ApplyPowerShellPolicy(profile.PowerShellExecutionPolicy);
                if (profile.IsRemoteDesktopEnabled.HasValue) await ApplyRemoteDesktopAsync(profile.IsRemoteDesktopEnabled.Value);
                if (profile.IsRemoteAssistanceEnabled.HasValue) await ApplyRemoteAssistanceAsync(profile.IsRemoteAssistanceEnabled.Value);
                if (profile.IsDeveloperModeEnabled.HasValue) ApplyDeveloperMode(profile.IsDeveloperModeEnabled.Value);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"[TweakProfileManager] Import Failed: {ex.Message}");
                throw;
            }
        });
    }
    #endregion

    #region Private Custom Appliers & Fetchers (System)

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

    #region Private Custom Appliers & Fetchers (Security)

    private static uint GetUacLevel()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
        int consentBehavior = (int)(key?.GetValue("ConsentPromptBehaviorAdmin") ?? 5);
        int secureDesktop = (int)(key?.GetValue("PromptOnSecureDesktop") ?? 1);

        if (consentBehavior == 2 && secureDesktop == 1) return 3;
        if (consentBehavior == 5 && secureDesktop == 1) return 2;
        if (consentBehavior == 5 && secureDesktop == 0) return 1;
        return 0;
    }

    private static void ApplyUacLevel(uint level)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
        if (key == null) return;

        if (level == 3) { key.SetValue("ConsentPromptBehaviorAdmin", 2, RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 1, RegistryValueKind.DWord); }
        else if (level == 2) { key.SetValue("ConsentPromptBehaviorAdmin", 5, RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 1, RegistryValueKind.DWord); }
        else if (level == 1) { key.SetValue("ConsentPromptBehaviorAdmin", 5, RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 0, RegistryValueKind.DWord); }
        else if (level == 0) { key.SetValue("ConsentPromptBehaviorAdmin", 0, RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 0, RegistryValueKind.DWord); }
    }

    private static void ApplySmartAppControlState(uint state)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CI\Policy", true);
        key?.SetValue("VerifiedAndReputablePolicyState", (int)state, RegistryValueKind.DWord);
    }

    private static void ApplyPowerShellPolicy(string policy)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", true);
        key?.SetValue("ExecutionPolicy", policy, RegistryValueKind.String);
    }

    private static void ApplyDeveloperMode(bool enable)
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
        if (key != null)
        {
            key.SetValue("AllowAllTrustedApps", enable ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("AllowDevelopmentWithoutDevLicense", enable ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    private static async Task ApplyRemoteDesktopAsync(bool enable)
    {
        int fDenyVal = enable ? 0 : 1;
        string command = $@"
        $ts = Get-WmiObject -Class Win32_TerminalServiceSetting -Namespace root\cimv2\TerminalServices -ComputerName '.' -Authentication 6;
        if ($ts) {{ $ts.SetAllowTSConnections({(enable ? 1 : 0)}, 1); }}
        $tsPath = 'HKLM:\System\CurrentControlSet\Control\Terminal Server';
        Set-ItemProperty -Path $tsPath -Name 'fDenyTSConnections' -Value {fDenyVal};
        Set-ItemProperty -Path ""$tsPath\WinStations\RDP-Tcp"" -Name 'UserAuthentication' -Value {(enable ? 1 : 0)};
        if ({enable.ToString().ToLower()}) {{ Enable-NetFirewallRule -DisplayGroup '@{{Microsoft.Windows.RemoteDesktop.RemoteDesktop.Resources.dll,-28752}}'; }} 
        else {{ Disable-NetFirewallRule -DisplayGroup '@{{Microsoft.Windows.RemoteDesktop.RemoteDesktop.Resources.dll,-28752}}'; }}";

        await CommandExecutor.InvokeRunCommand(command, isPowerShell: true);
    }

    private static async Task ApplyRemoteAssistanceAsync(bool enable)
    {
        int val = enable ? 1 : 0;
        string command = $@"
        Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance' -Name 'fAllowToGetHelp' -Value {val};
        Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -Name 'fAllowToGetHelp' -Value {val};
        if ({enable.ToString().ToLower()}) {{ Enable-NetFirewallRule -DisplayGroup '@{{FirewallAPI.dll,-28502}}' -ErrorAction SilentlyContinue; }} 
        else {{ Disable-NetFirewallRule -DisplayGroup '@{{FirewallAPI.dll,-28502}}' -ErrorAction SilentlyContinue; }}";

        await CommandExecutor.InvokeRunCommand(command, isPowerShell: true);
    }

    #endregion
}