// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public record SystemHealthResult(string ImagePath, string StatusText, int PenaltyScore);

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
            double totalRamGb = 0;
            double vRamPercentage = 0;
            double totalVRamGb = 0;
            double junkGigabytes = 0.0;

            await Task.Run(() =>
            {
                MEMORYSTATUSEX memInfo = new MEMORYSTATUSEX();
                memInfo.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memInfo))
                {
                    ramPercentage = memInfo.dwMemoryLoad;
                    totalRamGb = memInfo.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);

                    if (memInfo.ullTotalPageFile > 0)
                    {
                        double usedVram = memInfo.ullTotalPageFile - memInfo.ullAvailPageFile;
                        vRamPercentage = (usedVram / memInfo.ullTotalPageFile) * 100.0;
                    }
                    totalVRamGb = memInfo.ullTotalPageFile / (1024.0 * 1024.0 * 1024.0);
                }

                if (DiagnosticsPageViewModel.Current != null && !string.IsNullOrEmpty(DiagnosticsPageViewModel.Current.TotalSpaceToFree))
                {
                    junkGigabytes = ParseSizeToGigabytes(DiagnosticsPageViewModel.Current.TotalSpaceToFree);
                }
            });

            int penaltyScore = 0;

            if (ramPercentage >= 90.0) penaltyScore += 2;
            else if (ramPercentage >= 80.0) penaltyScore += 1;

            if (vRamPercentage >= 98.0) penaltyScore += 1;

            if (junkGigabytes >= 20.0) penaltyScore += 2;
            else if (junkGigabytes >= 10.0) penaltyScore += 1;

            string imagePath;
            string statusText;

            if (penaltyScore >= 4)
            {
                imagePath = "ms-appx:///Assets/PngImages/health_critical.png";
                statusText = ResourceString.GetString("Health_Poor") ?? "Poor - Action Required";
            }
            else if (penaltyScore >= 2)
            {
                imagePath = "ms-appx:///Assets/PngImages/health_warning.png";
                statusText = ResourceString.GetString("Health_Warning") ?? "Fair - Optimization Recommended";
            }
            else
            {
                imagePath = "ms-appx:///Assets/PngImages/health_good.png";
                statusText = ResourceString.GetString("Health_Good") ?? "Good - System is Healthy";
            }

            return new SystemHealthResult(imagePath, statusText, penaltyScore);
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