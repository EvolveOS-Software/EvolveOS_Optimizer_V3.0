// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public record SystemHealthResult(string StatusText, int PenaltyScore, double Score, double RamUsage, double PagefileUsage, double JunkGb);

    public static class SystemHealthHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        public static async Task<SystemHealthResult> EvaluateHealthAsync()
        {
            double ramPercentage = 0;
            double vRamPercentage = 0;
            double junkGigabytes = 0.0;

            await Task.Run(() =>
            {
                MEMORYSTATUSEX memInfo = new MEMORYSTATUSEX();
                memInfo.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memInfo))
                {
                    ramPercentage = memInfo.dwMemoryLoad;

                    ulong trueTotalPageFile = memInfo.ullTotalPageFile - memInfo.ullTotalPhys;

                    if (trueTotalPageFile > 0)
                    {
                        ulong usedPhysical = memInfo.ullTotalPhys - memInfo.ullAvailPhys;
                        ulong usedCommit = memInfo.ullTotalPageFile - memInfo.ullAvailPageFile;
                        ulong usedPageFile = usedCommit > usedPhysical ? usedCommit - usedPhysical : 0;

                        vRamPercentage = ((double)usedPageFile / trueTotalPageFile) * 100.0;
                    }
                    else
                    {
                        vRamPercentage = 0;
                    }
                }

                if (DiagnosticsPageViewModel.Current != null && !string.IsNullOrEmpty(DiagnosticsPageViewModel.Current.TotalSpaceToFree))
                {
                    junkGigabytes = ParseSizeToGigabytes(DiagnosticsPageViewModel.Current.TotalSpaceToFree);
                }
            });

            int penaltyScore = 0;

            if (ramPercentage >= 90.0) penaltyScore += 2;
            else if (ramPercentage >= 80.0) penaltyScore += 1;

            if (vRamPercentage >= 90.0) penaltyScore += 1;

            if (junkGigabytes >= 20.0) penaltyScore += 2;
            else if (junkGigabytes >= 10.0) penaltyScore += 1;
            else if (junkGigabytes >= 2.0) penaltyScore += 1;

            string statusText;
            double healthScore = 1.0;

            if (penaltyScore >= 4)
            {
                statusText = ResourceString.GetString("Health_Poor") ?? "Poor - Action Required";
                healthScore = 0.25;
            }
            else if (penaltyScore >= 2)
            {
                statusText = ResourceString.GetString("Health_Warning") ?? "Fair - Optimization Recommended";
                healthScore = 0.65;
            }
            else if (penaltyScore == 1)
            {
                statusText = ResourceString.GetString("Health_Good") ?? "Good - Minor Cleanup Needed";
                healthScore = 0.85;
            }
            else
            {
                statusText = ResourceString.GetString("Health_Good") ?? "Excellent - System is Healthy";
                healthScore = 1.0;
            }

            return new SystemHealthResult(statusText, penaltyScore, healthScore, ramPercentage, vRamPercentage, junkGigabytes);
        }

        public static double ParseSizeToGigabytes(string sizeString)
        {
            if (string.IsNullOrWhiteSpace(sizeString) || sizeString.Contains("Scanning", StringComparison.OrdinalIgnoreCase)) return 0;

            try
            {
                string cleanString = sizeString.ToUpper().Trim().Replace(" ", "");

                if (cleanString.Contains(",") && cleanString.Contains("."))
                {
                    if (cleanString.IndexOf(",") > cleanString.IndexOf("."))
                        cleanString = cleanString.Replace(".", "").Replace(",", ".");
                    else
                        cleanString = cleanString.Replace(",", "");
                }
                else if (cleanString.Contains(","))
                {
                    cleanString = cleanString.Replace(",", ".");
                }

                var match = Regex.Match(cleanString, @"([\d\.]+)\s*(TB|GB|MB|KB|B|TO|GO|MO|KO)");

                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                {
                    string unit = match.Groups[2].Value;
                    double resultInGb = unit switch
                    {
                        "TB" or "TO" => value * 1024.0,
                        "GB" or "GO" => value,
                        "MB" or "MO" => value / 1024.0,
                        "KB" or "KO" => value / 1048576.0,
                        "B" => value / 1073741824.0,
                        _ => 0
                    };

                    if (resultInGb < 3.5) return 0.0;

                    return resultInGb;
                }
            }
            catch { }

            return 0;
        }
    }
}