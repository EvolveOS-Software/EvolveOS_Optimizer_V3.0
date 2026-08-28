// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public record SecurityHealthResult(
        string StatusText,
        int PenaltyScore,
        int IssuesCount,
        List<string> Issues,
        string FirewallStatus,
        string DefenderStatus,
        string UacStatus,
        bool IsCoreProtected,
        bool IsWarningState
    );

    public static class SecurityHealthHelper
    {
        public static async Task<SecurityHealthResult> EvaluateSecurityAsync(CancellationToken cancellationToken = default)
        {
            int penaltyScore = 0;
            var issues = new List<string>();
            var ignoredIssues = LocalMachineSettingsEngine.IgnoredSecurityIssues;

            void ReportIssue(string issueText, int penalty)
            {
                if (!ignoredIssues.Contains(issueText))
                {
                    penaltyScore += penalty;
                    issues.Add(issueText);
                }
            }

            var avTask = SecurityDiagnostics.GetAntivirusInfoAsync(cancellationToken);
            var fwTask = SecurityDiagnostics.IsFirewallEnabledAsync(cancellationToken);
            var wuTask = SecurityDiagnostics.IsWindowsUpdateEnabledAsync(cancellationToken);
            var ssTask = SecurityDiagnostics.IsSmartScreenEnabledAsync(cancellationToken);
            var rtpTask = SecurityDiagnostics.IsRealTimeProtectionEnabledAsync(cancellationToken);
            var uacTask = SecurityDiagnostics.IsUACEnabledAsync(cancellationToken);
            var tamperTask = SecurityDiagnostics.IsTamperProtectionEnabledAsync(cancellationToken);
            var cfaTask = SecurityDiagnostics.IsControlledFolderAccessEnabledAsync(cancellationToken);
            var bitLockerTask = SecurityDiagnostics.IsBitLockerEnabledAsync(cancellationToken);
            var coreIsoTask = SecurityDiagnostics.IsCoreIsolationEnabledAsync(cancellationToken);
            var defSvcTask = SecurityDiagnostics.IsDefenderServiceEnabledAsync(cancellationToken);
            var accTask = SecurityDiagnostics.IsAccountProtectionEnabledAsync(cancellationToken);
            var sacTask = SecurityDiagnostics.GetSmartAppControlStateAsync(cancellationToken);
            var psTask = SecurityDiagnostics.GetPowerShellExecutionPolicyAsync(cancellationToken);
            var lsaTask = SecurityDiagnostics.IsLsaProtectionEnabledAsync(cancellationToken);
            var rdpTask = SecurityDiagnostics.IsRdpEnabledAsync(cancellationToken);
            var raTask = SecurityDiagnostics.IsRemoteAssistanceEnabledAsync(cancellationToken);
            var devTask = SecurityDiagnostics.IsDeveloperModeEnabledAsync(cancellationToken);

            await Task.WhenAll(
                avTask, fwTask, wuTask, ssTask, rtpTask, uacTask, tamperTask, cfaTask,
                bitLockerTask, coreIsoTask, defSvcTask, accTask, sacTask, psTask,
                lsaTask, rdpTask, raTask, devTask
            ).ConfigureAwait(false);

            var antivirusInfo = avTask.Result;
            bool isFirewallEnabled = fwTask.Result;
            bool isWindowsUpdateEnabled = wuTask.Result;
            bool isSmartScreenEnabled = ssTask.Result;
            bool isRealTimeProtectionEnabled = rtpTask.Result;
            bool isUacEnabled = uacTask.Result;
            bool isTamperProtectionEnabled = tamperTask.Result;
            bool isControlledFolderAccessEnabled = cfaTask.Result;
            bool isBitLockerEnabled = bitLockerTask.Result;
            bool isCoreIsolationEnabled = coreIsoTask.Result;
            bool isDefenderServiceEnabled = defSvcTask.Result;
            bool isAccountProtectionEnabled = accTask.Result;
            int smartAppControlState = sacTask.Result;
            string psPolicy = psTask.Result;
            bool isLsaProtectionEnabled = lsaTask.Result;
            bool isRdpEnabled = rdpTask.Result;
            bool isRaEnabled = raTask.Result;
            bool isDevModeEnabled = devTask.Result;

            int uacPromptBehavior = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 5);
            int smb1Enabled = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", "SMB1", 0);
            int limitBlankPass = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa", "LimitBlankPasswordUse", 1);
            int rdpNla = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp", "UserAuthentication", 1);
            int secureBoot = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled", 1);

            int wDigest = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\WDigest", "UseLogonCredential", 0);
            int llmnr = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", 1);
            int autoRun = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 0);
            int lmCompatibility = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa", "lmcompatibilitylevel", 3);

            string firewallStatus = isFirewallEnabled ? "Active" : "Disabled";
            string defenderStatus = isRealTimeProtectionEnabled ? "Active" : "Disabled";
            string uacStatus;

            #region CRITICAL VULNERABILITIES (+10 Penalty)

            if (!isFirewallEnabled)
            {
                ReportIssue("Windows Firewall is disabled.", 10);
            }

            if (!isRealTimeProtectionEnabled)
            {
                ReportIssue("Real-time virus protection is disabled.", 10);
            }

            if (!isDefenderServiceEnabled && antivirusInfo.ProductName == "Windows Defender")
            {
                ReportIssue("Windows Defender background service is stopped.", 10);
            }

            if (!isUacEnabled)
            {
                uacStatus = "Disabled";
                ReportIssue("User Account Control (UAC) is completely disabled.", 10);
            }
            else if (uacPromptBehavior == 0)
            {
                uacStatus = "Never Notify";
                ReportIssue("UAC is set to 'Never Notify' (Reduced Security).", 3);
            }
            else
            {
                uacStatus = "Active";
            }

            #endregion

            #region HIGH RISK CONFIGURATIONS (+5 Penalty)

            if (!isWindowsUpdateEnabled)
            {
                ReportIssue("Windows Automatic Updates are disabled.", 5);
            }

            if (!antivirusInfo.IsEnabled)
            {
                ReportIssue($"Antivirus ({antivirusInfo.ProductName}) reports as disabled.", 5);
            }

            if (smb1Enabled == 1)
            {
                ReportIssue("Insecure SMBv1 protocol is enabled (High Vulnerability).", 5);
            }

            if (wDigest == 1)
            {
                ReportIssue("WDigest authentication is enabled (Plaintext credentials in memory).", 5);
            }

            #endregion

            #region MODERATE RISKS (+2 to +3 Penalty)

            if (!isAccountProtectionEnabled || limitBlankPass == 0)
            {
                ReportIssue("Account protection is weakened (Blank passwords may be allowed).", 3);
            }

            if (isRdpEnabled)
            {
                ReportIssue("Remote Desktop (RDP) connections are allowed.", 2);

                if (rdpNla == 0)
                {
                    ReportIssue("RDP Network Level Authentication (NLA) is disabled (High Risk).", 3);
                }
            }

            if (isRaEnabled)
            {
                ReportIssue("Remote Assistance connections are allowed.", 2);
            }

            if (isDevModeEnabled)
            {
                ReportIssue("Developer Mode is enabled (Allows sideloading unsigned apps).", 2);
            }

            if (secureBoot == 0)
            {
                ReportIssue("Secure Boot is disabled in BIOS/UEFI.", 2);
            }

            if (!isCoreIsolationEnabled)
            {
                ReportIssue("Core Isolation (Memory Integrity) is disabled.", 2);
            }

            if (!isTamperProtectionEnabled)
            {
                ReportIssue("Antivirus Tamper Protection is disabled.", 2);
            }

            if (!isSmartScreenEnabled)
            {
                ReportIssue("Windows SmartScreen is disabled.", 2);
            }

            if (smartAppControlState == 0)
            {
                ReportIssue("Smart App Control is disabled.", 2);
            }

            if (psPolicy.Equals("Unrestricted", StringComparison.OrdinalIgnoreCase) ||
                psPolicy.Equals("Bypass", StringComparison.OrdinalIgnoreCase))
            {
                ReportIssue($"PowerShell execution policy is insecure ({psPolicy}).", 2);
            }

            if (llmnr == 1)
            {
                ReportIssue("LLMNR is enabled (Vulnerable to local network spoofing).", 3);
            }

            if (autoRun != 255)
            {
                ReportIssue("AutoRun is enabled for removable drives (USB malware risk).", 2);
            }

            #endregion

            #region MINOR HARDENING OPPORTUNITIES (+1 Penalty)

            if (!isLsaProtectionEnabled)
            {
                ReportIssue("Local Security Authority (LSA) protection is not enforced.", 1);
            }

            if (!isControlledFolderAccessEnabled)
            {
                ReportIssue("Controlled Folder Access (Ransomware protection) is disabled.", 1);
            }

            if (!isBitLockerEnabled)
            {
                ReportIssue("OS Drive is not encrypted with BitLocker.", 1);
            }

            if (lmCompatibility < 3)
            {
                ReportIssue("Insecure NTLMv1 authentication protocol is allowed.", 1);
            }

            #endregion

            bool isCoreProtected = penaltyScore < 10;
            bool isWarningState = penaltyScore >= 5 && penaltyScore < 10;

            string statusText;
            if (!isCoreProtected)
                statusText = ResourceString.GetString("text_security_critical") ?? "Critical Issues";
            else if (isWarningState)
                statusText = ResourceString.GetString("text_security_warning") ?? "Warnings Found";
            else
                statusText = ResourceString.GetString("text_security_good") ?? "System is Secure";

            return new SecurityHealthResult(statusText, penaltyScore, issues.Count, issues, firewallStatus, defenderStatus, uacStatus, isCoreProtected, isWarningState);
        }
    }
}