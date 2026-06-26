// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Management;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Services;

public class VssQueryService
{
    public string GetCurrentDiskUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UsedSpace FROM Win32_ShadowStorage");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["UsedSpace"] != null)
                {
                    ulong bytes = (ulong)obj["UsedSpace"];
                    return FormatBytes(bytes);
                }
            }
        }
        catch { /* WMI queries fail if VSS is disabled or needs admin rights */ }

        return "Current usage: 0 bytes";
    }

    public List<SnapshotData> GetCurrentSnapshots()
    {
        var snapshots = new List<SnapshotData>();
        string osBuild = GetCurrentWindowsBuild();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ID, InstallDate FROM Win32_ShadowCopy");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["InstallDate"] != null && obj["ID"] != null)
                {
                    string dateStr = obj["InstallDate"]!.ToString() ?? string.Empty;
                    string idStr = obj["ID"]!.ToString() ?? string.Empty;
                    string displayTemplate = ResourceString.GetString("Setting_PointInTimeRestore_Snapshots_DisplayText") ?? "Created on {0} • OS Build {1}";

                    DateTime date = ManagementDateTimeConverter.ToDateTime(dateStr);

                    snapshots.Add(new SnapshotData
                    {
                        Id = idStr,
                        DisplayText = string.Format(displayTemplate, date.ToString("f"), osBuild)
                    });
                }
            }
        }
        catch { }

        snapshots.Reverse();
        return snapshots;
    }

    private string FormatBytes(ulong bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblSByte = bytes;
        while (dblSByte >= 1024 && i < suffixes.Length - 1)
        {
            dblSByte /= 1024;
            i++;
        }
        return $"Current usage: {dblSByte:0.##} {suffixes[i]}";
    }

    private string GetCurrentWindowsBuild()
    {
        try
        {
            const string registryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            using var key = Registry.LocalMachine.OpenSubKey(registryKey);

            if (key != null)
            {
                var build = key.GetValue("CurrentBuild")?.ToString();
                var ubr = key.GetValue("UBR")?.ToString();

                if (!string.IsNullOrEmpty(build) && !string.IsNullOrEmpty(ubr))
                {
                    return $"10.0.{build}.{ubr}";
                }
            }
        }
        catch { /* Ignore access errors */ }

        return Environment.OSVersion.Version.ToString();
    }

    public void DeleteSnapshot(string shadowId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_ShadowCopy WHERE ID='{shadowId}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.Delete();
            }
        }
        catch { /* Fails gracefully if not running as Admin */ }
    }
}

public class SnapshotData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
}