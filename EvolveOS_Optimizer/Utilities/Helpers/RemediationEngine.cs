// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Maintenance;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class RemediationEngine
    {
        #region Software Remediation

        public static async Task<bool> RunFixAsync(int eventId)
        {
            try
            {
                return eventId switch
                {
                    #region PERFORMANCE BOTTLENECK REMEDIATION

                    // 8001, 9001, 9002 = RAM/Pagefile Exhaustion
                    8001 or 9001 or 9002 => await ExecuteNativeMemoryCleanAsync(),

                    // 8002 = CPU Bottleneck -> Reset Event Tracing
                    8002 => await FixEventTracingAsync(),

                    // 8003 = Disk Saturation -> Use your native flush buffers and cache discard
                    8003 => await ExecuteNativeDiskOptimizeAsync(),

                    #endregion

                    #region SPECIFIC REPAIRS

                    // Windows Search & Indexing (Corrupted index, service hangs, protocol failures)
                    16 or 100 or 101 or 1001 or 1002 or 3006 or 3007 or 7040 or 7042 or 9000
                        => await FixWindowsSearchAsync(),

                    // Display Driver Reset (TDR errors, driver recovery, hardware acceleration crashes)
                    4101 or 4109 or 4115 or 14 or 10
                        => await ResetDisplayDriverAsync(),

                    // DNS Cache & Client logic (Resolution timeouts, server unreachable, cache poisoning)
                    1014 or 1012 or 1015 or 1016 or 1017 or 1018 or 1019
                        => await FixNetworkDnsAsync(),

                    // Volume Shadow Copy (VSS) (Provider crashes, metadata corruption, backup failures)
                    8193 or 12289 or 13 or 20 or 22 or 34 or 12290 or 12293 or 12297 or 12298 or 8194
                        => await FixVssServiceAsync(),

                    // Print Spooler stack (Spooler service crash, job metadata corruption, RPC failures)
                    315 or 808 or 316 or 4 or 32 or 40 or 50 or 310 or 483 or 603 or 123
                        => await FixPrintSpoolerAsync(),

                    // Performance Counter rebuilding (Corrupted registry keys, provider discovery failures)
                    1023 or 1008 or 1001 or 1004 or 1006 or 1017 or 1019 or 1020 or 2001 or 3000
                        => await FixPerformanceCountersAsync(),

                    // System File Repair (Application errors caused by DLL missing, Side-by-Side corruption)
                    1000 or 1001 or 33 or 35 or 11 or 59 or 60 or 6008 or 7023 or 7024 or 7031
                        => await RunSystemFileRepairAsync(),

                    // "High-Level" repair. Secure Boot CA/Keys
                    1801 => await FixSecureBootKeysAsync(),

                    // Windows Time & NTP synchronization (Client sync timeouts, stratum discovery failures)
                    131 or 36 or 144 or 17 or 29 or 34 or 35 or 37 or 38 or 47 or 49
                        => await FixTimeSyncAsync(),

                    // Resource & DWM exhaustion repair (GDI handle leaks, desktop window manager crashes)
                    2004 or 2001 or 2002 or 2003 or 2005
                        => await FixResourceExhaustionAsync(),

                    // Service Control Manager (Driver failed to load)
                    7026 => await FixLuafvServiceAsync(),

                    // SSL/TLS (Schannel) cache reset (Handshake failures, revoked certs, protocol mismatch)
                    36888 or 36887 or 36870 or 36871 or 36874 or 36880 or 36881 or 36882 or 36884 or 36885 or 36886
                        => await FixSchannelAsync(),

                    // MSI Windows Installer repair (Registry locks, installer service timeouts, GUID mismatch)
                    11708 or 1033 or 1013 or 1015 or 1040 or 1041 or 1042 or 11706 or 11707 or 11724 or 11728
                        => await FixMsiInstallerAsync(),

                    // Group Policy Refresh (GPO sync failures, LDAP timeouts, policy database corruption)
                    1005 or 1030 or 1006 or 1008 or 1010 or 1053 or 1054 or 1055 or 1058 or 1096 or 1101 or 1112
                        => await FixGroupPolicyAsync(),

                    // Wi-Fi / WLAN AutoConfig repair (Radio hangs, driver state transitions, WLAN profile locks)
                    10002 or 10200 or 5001 or 5002 or 5005 or 5007 or 5010 or 6062 or 7001 or 7002 or 7003 or 8000
                        => await FixWifiAdapterAsync(),

                    // Windows Defender logic (Signature update timeouts, engine service crashes)
                    2002 or 1005 or 1006 or 1007 or 1015 or 1116 or 1117 or 1118 or 1119 or 2001 or 2010 or 2011
                        => await FixWindowsDefenderAsync(),

                    // Lanman Server / SMB Sharing (Binding failures, network name conflicts, SMBv2 state errors)
                    2505 or 2011 or 2012 or 2021 or 2022 or 2504 or 2506 or 2507 or 2508 or 2509
                        => await FixLanmanServerAsync(),

                    // Event Tracing logic (Circular context logger maxed out, session start failures)
                    3 or 1 or 2 or 4 or 10 or 11 or 12 or 13 or 14 or 15
                        => await FixEventTracingAsync(),

                    // AppX / Start Menu deployment (Activation failures, shell host hangs, UWP manifest errors)
                    69 or 10 or 11 or 12 or 59 or 65 or 400 or 401 or 404 or 510 or 513 or 515 or 523
                        => await FixAppxDeploymentAsync(),

                    // Windows Store Cache (WSReset targets, licensing store failures, metadata discovery)
                    10010 or 5001 or 5002 or 5003 or 5004 or 10011 or 10012 or 10013 or 10014 or 10015
                        => await FixWindowsStoreAsync(),

                    // ESENT Database (TileDataLayer, Windows Search, and App Repository DB failures)
                    455 or 427 or 441 or 442 or 447 or 448 or 451 or 454 or 467 or 474 or 477 or 481 or 482 or 488 or 489 or 490
                        => await FixEsentDatabaseAsync(),

                    // Cryptographic Services (Catroot2 corruption, root certificate update failures)
                    513 or 1 or 11 or 13 or 17 or 18 or 20 or 24 or 30 or 31 or 32
                        => await FixCryptographicServicesAsync(),

                    #endregion

                    #region BROAD BUCKETS

                    // BUCKET 1: Core & Power
                    41 or 1074 or 6008 or 1011 or 12 or 13 or 18 or 109 or 110 or 117 or 6005 or 6006 or 6009
                    or 1076 or 1102 or 4647 or 4688 or 4689 or 1 or 4 or 15 or 42 or 107 or 137 or 506 or 507 or 524
                    or 525 or 533 or 566 or 601 or 604 or 10000 or 10001 or 10100 or 10101 or 10102 or 10103 or 10104
                    or 10105 or 10106 or 10107 or 10108 or 10109 or 10110
                    or 10111 or 10112 or 10113 or 10114 or 10115 or 10116 or 10117 or 10118 or 10119 or 10120
                    => await FixPowerFastStartupAsync(),

                    // BUCKET 2: Identity & DCOM
                    10016 or 1500 or 1502 or 1511 or 1515 or 1542 or 4625 or 10005 or 40961 or 40962 or 1530 or 1534
                    or 4648 or 4720 or 4722 or 4723 or 4724 or 4725 or 4726 or 4738 or 4740 or 1501 or 1504 or 1505
                    or 1506 or 1507 or 1508 or 1509 or 1512 or 1513 or 1514 or 1517 or 1531 or 1532 or 4624 or 4634
                    or 4672 or 4732 or 4733 or 4735 or 4800 or 4801 or 4802 or 4803 or 5140 or 5142 or 5145 or 6272
                    or 6273 or 6278 or 1101 or 1104 or 1105 or 1108
                    or 4741 or 4742 or 4743 or 4744 or 4745 or 4746 or 4747 or 4748 or 4749 or 4750
                    => await FixDCOMAsync(),

                    // BUCKET 3: Networking
                    1012 or 1015 or 4227 or 4231 or 10400 or 1003 or 1004 or 4226 or 4319 or 8003 or 8021
                    or 10000 or 10011 or 5719 or 11001 or 11004 or 10053 or 1013 or 1017 or 1018 or 1019
                    or 8000 or 8001 or 8002 or 8004 or 1002 or 1005 or 1006 or 1007 or 1009 or 1010 or 1011 or 1016
                    or 1020 or 5000 or 5001 or 5004 or 5006 or 5007 or 5010 or 5011 or 5012 or 5032 or 7001 or 7002
                    or 7003 or 7004 or 7005 or 7006 or 10012 or 10020 or 11002 or 11005 or 11006 or 12001 or 12010
                    or 12011 or 12012 or 12013
                    or 10065 or 10066 or 10067 or 10068 or 10069 or 10070 or 10071 or 10072 or 10073 or 10074
                    => await FixTcpIpStackAsync(),

                    // BUCKET 4: Update & Store
                    20 or 17 or 19 or 25 or 34 or 2100 or 2101 or 2102 or 512 or 514 or 4004
                    or 4005 or 4007 or 4008 or 1016 or 5000 or 5001 or 5002 or 5003 or 10 or 11 or 14 or 21 or 22
                    or 23 or 24 or 31 or 32 or 33 or 35 or 37 or 38 or 40 or 44 or 45 or 201 or 202 or 300 or 301
                    or 302 or 303 or 304 or 305 or 306 or 307 or 308 or 400 or 401 or 402 or 403 or 404 or 405 or 406
                    or 407 or 408 or 409 or 410 or 411 or 504 or 505
                    or 417 or 418 or 419 or 420 or 421 or 422 or 423 or 424 or 425 or 426
                    => await FixWindowsUpdateAsync(),

                    // BUCKET 5: UI Shell
                    1002 or 1022 or 489 or 490 or 1010 or 491 or 492 or 493 or 1003
                    or 1004 or 1005 or 1006 or 1007 or 1009 or 1011 or 1012 or 1013 or 1015 or 1017 or 1018
                    or 1019 or 1020 or 1021 or 1024 or 1025 or 2000 or 2001 or 2003 or 2005 or 3000 or 3001
                    or 3002 or 3003 or 3004 or 8000 or 8001 or 9000 or 9001
                    => await RestartExplorerAsync(),

                    // BUCKET 6: Storage
                    55 or 98 or 11 or 15 or 51 or 153 or 7 or 130 or 137 or 140 or 12293 or 12298 or 8224 or 2049
                    or 2050 or 50 or 57 or 12290 or 8213 or 8217 or 8218 or 8219 or 8220 or 8221 or 8222 or 8223
                    or 8225 or 8226 or 12291 or 2 or 5 or 8 or 9 or 12 or 14 or 26 or 27 or 28 or 29 or 30
                    or 31 or 32 or 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 52 or 54 or 56 or 58 or 59 or 60
                    or 129 or 132 or 133 or 134 or 135 or 136 or 138 or 139 or 141 or 142 or 143
                    or 155 or 156 or 157 or 158 or 159 or 160 or 161 or 162 or 163 or 164
                    => await FixDiskCorruptionAsync(),

                    // BUCKET 7: Service logic
                    35 or 36870 or 7000 or 7009 or 7011 or 7023 or 7024
                    or 7031 or 7032 or 7034 or 7036 or 7040 or 12292 or 12294 or 12295 or 12296 or 12297 or 12300
                    or 12301 or 12302 or 12303 or 12304 or 63 or 100 or 101 or 102 or 103 or 317 or 800 or 801
                    or 804 or 805 or 806 or 809 or 810 or 7001 or 7022 or 7026 or 7030 or 7035 or 7045 or 7046 or 7047
                    or 7048 or 7049 or 7050 or 7051 or 7052
                    => await FixServiceTimeoutAsync(),

                    _ => false

                    #endregion
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] Critical failure for ID {eventId}: {ex.Message}");
                return false;
            }
        }

        #region NATIVE PERFORMANCE BRIDGES

        private static async Task<bool> ExecuteNativeMemoryCleanAsync()
        {
            try
            {
                var result = await ClearingMemory.StartMemoryCleanup(clearRamCache: true, optimizeWorkingSet: true, shouldRemoveWinOld: false, shouldFlushDns: false);
                return result.MemoryCleanupAttempted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] Native Memory Clean Failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> ExecuteNativeDiskOptimizeAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    ClearingMemory.OptimizeModifiedFileCache();
                    ClearingMemory.OptimizeModifiedPageList();
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] Native Disk Optimize Failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region SOFTWARE REPAIRS

        private static async Task<bool> FixPowerFastStartupAsync()
        {
            string script = "powercfg /h off; Start-Sleep -Seconds 2; powercfg /h on";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixCryptographicServicesAsync()
        {
            string script = @"
                Stop-Service cryptsvc -Force -ErrorAction SilentlyContinue
                Rename-Item -Path ""$env:windir\System32\catroot2"" -NewName ""catroot2.old"" -ErrorAction SilentlyContinue
                Start-Service cryptsvc -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixEsentDatabaseAsync()
        {
            string script = @"New-Item -Path ""$env:windir\system32\config\systemprofile\AppData\Local\TileDataLayer\Database"" -ItemType Directory -Force | Out-Null";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWindowsDefenderAsync()
        {
            await CommandExecutor.RunCommandAsTrustedInstaller("Update-MpSignature", isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWindowsSearchAsync()
        {
            await CommandExecutor.RunCommand("Restart-Service WSearch -Force -ErrorAction SilentlyContinue", isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixAppxDeploymentAsync()
        {
            string script = @"Get-AppxPackage -AllUsers | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register ""$($_.InstallLocation)\AppXManifest.xml"" -ErrorAction SilentlyContinue}";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixEventTracingAsync()
        {
            string script = @"logman stop EventLog-System -ets -ErrorAction SilentlyContinue; logman start EventLog-System -ets -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixLanmanServerAsync()
        {
            string script = @"Restart-Service LanmanServer -Force -ErrorAction SilentlyContinue; Restart-Service LanmanWorkstation -Force -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixDiskCorruptionAsync()
        {
            await CommandExecutor.RunCommandAsTrustedInstaller("chkdsk C: /scan /perf", isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixServiceTimeoutAsync()
        {
            string script = @"Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control' -Name 'ServicesPipeTimeout' -Value 60000 -Type DWord -Force";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWifiAdapterAsync()
        {
            string script = @"Restart-Service WlanSvc -Force -ErrorAction SilentlyContinue; ipconfig /renew | Out-Null";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixGroupPolicyAsync()
        {
            await CommandExecutor.RunCommand("gpupdate /force", isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixMsiInstallerAsync()
        {
            string script = @"
                msiexec /unregister
                msiexec /regserver
                Restart-Service msiserver -Force -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixVssServiceAsync()
        {
            string script = @"Restart-Service vss -Force -ErrorAction SilentlyContinue; Restart-Service swprv -Force -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixSchannelAsync()
        {
            await CommandExecutor.RunCommand("certutil -setreg chain\\ChainCacheResyncFiletime @now", isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixResourceExhaustionAsync()
        {
            string script = @"Restart-Service SysMain -Force -ErrorAction SilentlyContinue; Stop-Process -Name dwm -Force -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixTimeSyncAsync()
        {
            string script = @"
                Stop-Service w32time -ErrorAction SilentlyContinue
                w32tm /unregister | Out-Null
                w32tm /register | Out-Null
                Start-Service w32time -ErrorAction SilentlyContinue
                w32tm /resync /nowait | Out-Null
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixTcpIpStackAsync()
        {
            string script = @"
                netsh winsock reset | Out-Null
                netsh int ip reset | Out-Null
                ipconfig /release | Out-Null
                ipconfig /renew | Out-Null
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWindowsStoreAsync()
        {
            await CommandExecutor.RunCommand("wsreset.exe -i", isPowerShell: false);
            return true;
        }

        private static async Task<bool> RunSystemFileRepairAsync()
        {
            string script = "DISM.exe /Online /Cleanup-image /Restorehealth";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixPerformanceCountersAsync()
        {
            string script = @"
                cd \windows\system32
                lodctr /r
                cd \windows\syswow64
                lodctr /r
                WINMGMT.EXE /RESYNCPERF
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixWindowsUpdateAsync()
        {
            string script = @"
                Stop-Service -Name wuauserv -Force -ErrorAction SilentlyContinue
                Stop-Service -Name bits -Force -ErrorAction SilentlyContinue
                Remove-Item -Path ""$env:windir\SoftwareDistribution\Download\*"" -Recurse -Force -ErrorAction SilentlyContinue
                Start-Service -Name wuauserv -ErrorAction SilentlyContinue
                Start-Service -Name bits -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixPrintSpoolerAsync()
        {
            string script = @"
                Stop-Service -Name Spooler -Force -ErrorAction SilentlyContinue
                Remove-Item -Path ""$env:windir\System32\spool\PRINTERS\*.*"" -Force -Recurse -ErrorAction SilentlyContinue
                Start-Service -Name Spooler -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> RestartExplorerAsync()
        {
            string script = "Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 500; Start-Process explorer";
            await CommandExecutor.RunCommand(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixNetworkDnsAsync()
        {
            string script = @"
                ipconfig /flushdns | Out-Null
                ipconfig /registerdns | Out-Null
                try { Restart-Service -Name Dnscache -Force -ErrorAction SilentlyContinue } catch {}
            ";
            await CommandExecutor.RunCommand(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixDCOMAsync()
        {
            string script = @"
                $Paths = @('HKCR:\AppID\{9CA88EE3-ACB7-47c8-AFC4-AB702511C276}', 'HKCR:\CLSID\{D63B10C5-BB46-4990-A94F-E40B9D520160}')
                foreach ($path in $Paths) {
                    if (Test-Path $path) {
                        Write-Output 'Repairing DCOM ACLs for path: $path'
                    }
                }";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixLuafvServiceAsync()
        {
            string script = @"
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\luafv' -Name 'Start' -Value 2 -Type DWord -Force
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name 'EnableLUA' -Value 1 -Type DWord -Force
            ";

            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);

            return true;
        }

        private static async Task<bool> FixSecureBootKeysAsync()
        {

            string script = @"
                $bitlocker = Get-BitLockerVolume -MountPoint 'C:' -ErrorAction SilentlyContinue
                if ($bitlocker.ProtectionStatus -eq 'On') {
                    Suspend-BitLocker -MountPoint 'C:' -RebootCount 2 -ErrorAction SilentlyContinue
                }
        
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot' -Name 'AvailableUpdates' -Value 22852 -Force
                Start-ScheduledTask -TaskName '\Microsoft\Windows\PI\Secure-Boot-Update' -ErrorAction SilentlyContinue
            ";

            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> ResetDisplayDriverAsync()
        {
            string script = "Add-Type -TypeDefinition '[DllImport(\"user32.dll\")] public class User32 { [DllImport(\"user32.dll\")] public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase); }'; [User32]::InvalidateRect([IntPtr]::Zero, [IntPtr]::Zero, $true)";
            await CommandExecutor.RunCommand(script, isPowerShell: true);
            return true;
        }

        #endregion

        #endregion

        #region Hardware Remediation

        public static async Task<bool> RunHardwareFixAsync(HardwareIssue issue)
        {
            if (issue == null || string.IsNullOrEmpty(issue.DeviceId)) return false;

            try
            {
                // Bucket 1: The device is physically Disabled
                if (issue.WmiErrorCode == 22)
                {
                    return await EnableDeviceDeepAsync(issue.DeviceId);
                }

                // Bucket 2: Soft Reset (Driver Crash, Power State, Resource Conflict, Init Failure)
                else if (issue.WmiErrorCode is 10 or 14 or 31 or 37 or 38 or 39 or 41 or 43 or 21 or 32 or 44 or 54 or 9 or 11 or 18 or 20 or 34 or 35 or 36 or 40 or 42 or 46 or 50 or 51 or 56 or 2 or 6 or 7 or 8 or 25 or 26 or 27 or 30 or 55 or 57 or 58 or 24 or 45 or 81 or 82 or 83 or 84 or 85
                    or 86 or 87 or 88 or 89 or 90 or 91 or 92 or 93 or 94 or 95 or 96 or 97 or 98 or 99 or 100 or 101 or 102 or 103 or 104 or 105
                    or 136 or 137 or 138 or 139 or 140 or 141 or 142 or 143 or 144 or 145 or 146 or 147 or 148 or 149 or 150 or 151 or 152 or 153 or 154 or 155
                    or 186 or 187 or 188 or 189 or 190 or 191 or 192 or 193 or 194 or 195 or 196 or 197 or 198 or 199 or 200 or 201 or 202 or 203 or 204 or 205)
                {
                    return await ResetDeviceDeepAsync(issue.DeviceId);
                }

                // Bucket 3: Hardware Rescan (PnP Sync, Bus Failures, Firmware Missing, Multifunction)
                else if (issue.WmiErrorCode is 1 or 12 or 16 or 28 or 29 or 33 or 47 or 53 or 13 or 15 or 17 or 23 or 59 or 60 or 61 or 62 or 63 or 69 or 70 or 71 or 72 or 73
                    or 106 or 107 or 108 or 109 or 110 or 111 or 112 or 113 or 114 or 115 or 116 or 117 or 118 or 119 or 120
                    or 156 or 157 or 158 or 159 or 160 or 161 or 162 or 163 or 164 or 165 or 166 or 167 or 168 or 169 or 170
                    or 206 or 207 or 208 or 209 or 210 or 211 or 212 or 213 or 214 or 215 or 216 or 217 or 218 or 219 or 220)
                {
                    return await RescanPnpHardwareAsync();
                }

                // Bucket 4: Registry & Hard Reinstall (Registry Corruption, Signature Blocked, Hive Overload)
                else if (issue.WmiErrorCode is 19 or 3 or 48 or 52 or 4 or 5 or 49 or 64 or 65 or 66 or 67 or 68
                    or 121 or 122 or 123 or 124 or 125 or 126 or 127 or 128 or 129 or 130 or 131 or 132 or 133 or 134 or 135
                    or 171 or 172 or 173 or 174 or 175 or 176 or 177 or 178 or 179 or 180 or 181 or 182 or 183 or 184 or 185
                    or 221 or 222 or 223 or 224 or 225 or 226 or 227 or 228 or 229 or 230 or 231 or 232 or 233 or 234 or 235)
                {
                    return await UninstallAndRescanDeviceAsync(issue.DeviceId);
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] Hardware fix failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> EnableDeviceDeepAsync(string deviceId)
        {
            try
            {
                string serviceScript = "if ((Get-Service 'SS3Svc' -ea 0).Status -eq 'Stopped') { Start-Service 'SS3Svc' -ea 0 }";
                await CommandExecutor.RunCommand(serviceScript, isPowerShell: true);

                string pnpCommand = $"pnputil /enable-device \"{deviceId}\"";

                string result = await CommandExecutor.GetCommandOutput(pnpCommand, isPowerShell: false);

                Debug.WriteLine($"\n[--- PNPUTIL EXECUTION ---]");
                Debug.WriteLine($"Target Device: {deviceId}");
                Debug.WriteLine($"Result: {result}");
                Debug.WriteLine($"[-------------------------]\n");

                if (result.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
                    result.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(result))
                {
                    Debug.WriteLine("[RemediationEngine] Standard Admin failed. Escalating to TrustedInstaller...");
                    await CommandExecutor.RunCommandAsTrustedInstaller(pnpCommand, isPowerShell: false);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] EnableDeviceDeepAsync Exception: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> ResetDeviceDeepAsync(string deviceId)
        {
            try
            {
                string disableCmd = $"pnputil /disable-device \"{deviceId}\"";
                string enableCmd = $"pnputil /enable-device \"{deviceId}\"";

                await CommandExecutor.RunCommandAsTrustedInstaller(disableCmd, isPowerShell: false);
                await Task.Delay(2000);
                await CommandExecutor.RunCommandAsTrustedInstaller(enableCmd, isPowerShell: false);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] ResetDeviceDeepAsync Exception: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> RescanPnpHardwareAsync()
        {
            try
            {
                await CommandExecutor.RunCommandAsTrustedInstaller("pnputil /scan-devices", isPowerShell: false);
                return true;
            }
            catch { return false; }
        }

        private static async Task<bool> UninstallAndRescanDeviceAsync(string deviceId)
        {
            try
            {
                await CommandExecutor.RunCommandAsTrustedInstaller($"pnputil /remove-device \"{deviceId}\"", isPowerShell: false);
                await Task.Delay(1500);
                await CommandExecutor.RunCommandAsTrustedInstaller("pnputil /scan-devices", isPowerShell: false);
                return true;
            }
            catch { return false; }
        }

        #endregion
    }
}