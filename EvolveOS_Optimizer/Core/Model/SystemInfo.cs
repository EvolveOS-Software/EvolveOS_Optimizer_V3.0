// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record SystemInfo
{
    public string AppVersion { get; init; } = "Unknown";
    public string OperatingSystem { get; init; } = "Unknown";
    public string Architecture { get; init; } = "Unknown";
    public string DeviceType { get; init; } = "Unknown";
    public string Cpu { get; init; } = "Unknown";
    public string Ram { get; init; } = "Unknown";
    public string Gpu { get; init; } = "Unknown";
    public string DotNetRuntime { get; init; } = "Unknown";
    public string Elevation { get; init; } = "Unknown";
    public string FirmwareType { get; init; } = "Unknown";
    public string SecureBoot { get; init; } = "Unknown";
    public string Tpm { get; init; } = "Unknown";
    public string DomainJoined { get; init; } = "Unknown";

    public int BuildNumber { get; set; }
    public bool IsWindows10 { get; set; }
    public bool IsWindows11 { get; set; }
}
