// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class TweakProfileBackup
    {
        public string ProfileName { get; set; } = "EvolveOS_Custom_Profile";
        public string ExportDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public Dictionary<string, bool> ServicesTweaks { get; set; } = new();
        public Dictionary<string, bool> PrivacyTweaks { get; set; } = new();
        public Dictionary<string, bool> SystemTweaks { get; set; } = new();
        public Dictionary<string, bool> InterfaceTweaks { get; set; } = new();
        public Dictionary<string, uint>? SystemSliders { get; set; }
        public Dictionary<string, string>? DNSCryptSettings { get; set; }

        public string? ActivePowerPlanGuid { get; set; }
        public string? WindowsUpdatesMode { get; set; }
        public uint? UacLevel { get; set; }
        public uint? SmartAppControlState { get; set; }
        public string? PowerShellExecutionPolicy { get; set; }
        public bool? IsRemoteDesktopEnabled { get; set; }
        public bool? IsRemoteAssistanceEnabled { get; set; }
        public bool? IsDeveloperModeEnabled { get; set; }
        public string? DnsIpv4Primary { get; set; }
        public string? DnsIpv4Secondary { get; set; }
        public string? DnsIpv6Primary { get; set; }
        public string? DnsIpv6Secondary { get; set; }
        public bool? IsDNSCryptRunning { get; set; }
    }
}
