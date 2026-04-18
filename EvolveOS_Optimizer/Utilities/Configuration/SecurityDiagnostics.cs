// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Management;
using System.Threading;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Win32;
using NetFwTypeLib;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    [Flags]
    public enum ProductState
    {
        Off = 0x0000,
        On = 0x1000,
        Snoozed = 0x2000,
        Expired = 0x3000
    }

    public class AntivirusInfo
    {
        public string? ProductName { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime? SignatureUpdated { get; set; }
    }

    public static class SecurityDiagnostics
    {
        public static async Task<AntivirusInfo> GetAntivirusInfoAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new AntivirusInfo { ProductName = "Windows Defender", IsEnabled = false };
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
                    using var products = searcher.Get();
                    if (products != null)
                    {
                        foreach (ManagementObject obj in products)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            var productName = obj["displayName"]?.ToString();
                            if (obj["productState"] != null && int.TryParse(obj["productState"].ToString(), out var state))
                            {
                                var productState = (ProductState)(state & 0xF000);
                                var isEnabled = productState == ProductState.On;
                                if (isEnabled || result.ProductName == "Windows Defender")
                                {
                                    result.ProductName = productName ?? "Unknown Antivirus";
                                    result.IsEnabled = isEnabled;
                                }
                            }
                            obj.Dispose();
                        }
                    }

                    try
                    {
                        using var defenderSearcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT * FROM MSFT_MpComputerStatus");
                        using var defenderResults = defenderSearcher.Get();
                        foreach (ManagementObject obj in defenderResults)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            if (obj["AntivirusSignatureLastUpdated"] != null)
                            {
                                result.SignatureUpdated = ManagementDateTimeConverter.ToDateTime(obj["AntivirusSignatureLastUpdated"].ToString());
                            }
                            obj.Dispose();
                            break;
                        }
                    }
                    catch { }
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsFirewallEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                if (IsFirewallServiceDisabled()) return false;
                try
                {
                    var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                    if (type != null && Activator.CreateInstance(type) is INetFwPolicy2 firewallPolicy)
                    {
                        return firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] ||
                               firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] ||
                               firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC];
                    }
                }
                catch (System.Runtime.InteropServices.COMException comEx) when ((uint)comEx.HResult == 0x800706D9)
                {
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
                        if (key?.GetValue("EnableFirewall") is int val) return val == 1;
                    }
                    catch { }
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsWindowsUpdateEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var key1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
                    if (key1?.GetValue("NoAutoUpdate") is int noAutoUpdate && noAutoUpdate == 1) return false;

                    using var key2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");
                    if (key2?.GetValue("DisableWindowsUpdateAccess") is int disabled && disabled == 1) return false;

                    using var key3 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\wuauserv");
                    if (key3?.GetValue("Start") is int start && start == 4) return false;

                    return true;
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsSmartScreenEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
                    if (policyKey?.GetValue("EnableSmartScreen") is int policyValue && policyValue == 0) return false;

                    using var explorerKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
                    if (explorerKey?.GetValue("SmartScreenEnabled") as string == "Off") return false;

                    using var userExplorerKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
                    if (userExplorerKey?.GetValue("SmartScreenEnabled") as string == "Off") return false;

                    return true;
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                return true;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsRealTimeProtectionEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
                    var value = key?.GetValue("DisableRealtimeMonitoring");
                    if (value == null) return true;
                    return (int)value == 0;
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsUACEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                    return key?.GetValue("EnableLUA") is int enabled && enabled == 1;
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsTamperProtectionEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Features");
                    var value = key?.GetValue("TamperProtection");
                    if (value == null) return true;
                    return (int)value == 5;
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsControlledFolderAccessEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-NoProfile -NonInteractive -Command \"(Get-MpPreference).EnableControlledFolderAccess\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };

                        using var process = Process.Start(psi);
                        if (process != null)
                        {
                            var readTask = process.StandardOutput.ReadToEndAsync();

                            await Task.WhenAny(readTask, Task.Delay(-1, cancellationToken)).ConfigureAwait(false);

                            if (cancellationToken.IsCancellationRequested)
                            {
                                process.Kill();
                                return false;
                            }

                            var output = await readTask;
                            if (int.TryParse(output.Trim(), out var status))
                            {
                                return status != 0;
                            }
                        }
                    }
                    catch (Exception psEx)
                    {
                        ErrorLogging.LogDebug(psEx);
                    }

                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access");
                    if (key != null)
                    {
                        var value = key.GetValue("EnableControlledFolderAccess");
                        if (value != null)
                        {
                            return (int)value != 0;
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> IsBitLockerEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var drives = DriveInfo.GetDrives();
                    foreach (var drive in drives)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        if (drive.DriveType == DriveType.Fixed)
                        {
                            try
                            {
                                using var searcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption",
                                    $"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = '{drive.Name.TrimEnd('\\', ':')}'");

                                using var volumes = searcher.Get();

                                if (volumes != null)
                                {
                                    foreach (ManagementObject volume in volumes)
                                    {
                                        if (cancellationToken.IsCancellationRequested) break;
                                        var protectionStatus = volume["ProtectionStatus"];
                                        if (protectionStatus != null && (uint)protectionStatus == 1)
                                        {
                                            return true;
                                        }
                                        volume.Dispose();
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static Task<bool> IsCoreIsolationEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
                    if (key != null)
                    {
                        var enabledValue = key.GetValue("Enabled");
                        return enabledValue != null && (int)enabledValue == 1;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);
        }

        public static async Task<bool> IsDefenderServiceEnabledAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WinDefend");
                    if (key?.GetValue("Start") is int startType)
                    {
                        return startType != 4;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
                return false;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static Task<bool> IsAccountProtectionEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\NgcPin\Credentials");

                    if (key != null)
                    {
                        return key.GetSubKeyNames().Length > 0;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);
        }

        public static Task<int> GetSmartAppControlStateAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CI\Policy");
                    if (key != null)
                    {
                        var value = key.GetValue("VerifiedAndReputablePolicyState");
                        if (value != null)
                        {
                            return (int)value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
                return 1;
            }, cancellationToken);
        }

        public static Task<string> GetPowerShellExecutionPolicyAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
                    return key?.GetValue("ExecutionPolicy")?.ToString() ?? "Restricted";
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                    return "Error";
                }
            }, cancellationToken);
        }

        private static bool IsFirewallServiceDisabled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\MpsSvc");
                return (int?)key?.GetValue("Start") == 4;
            }
            catch { return false; }
        }
    }
}

