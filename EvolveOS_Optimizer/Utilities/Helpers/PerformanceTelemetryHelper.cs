// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License

using System.Management;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class PerformanceTelemetryHelper
    {
        public async Task<List<SystemEventItem>> AnalyzePerformanceBottlenecksAsync()
        {
            var performanceAlerts = new List<SystemEventItem>();

            await Task.Run(() =>
            {
                try
                {
                    // 1. Check RAM Exhaustion
                    using (var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                    using (var collection = searcher.Get())
                    {
                        foreach (var os in collection)
                        {
                            double freeRamKb = Convert.ToDouble(os["FreePhysicalMemory"]);
                            double totalRamKb = Convert.ToDouble(os["TotalVisibleMemorySize"]);
                            double freePercentage = (freeRamKb / totalRamKb) * 100;

                            if (freePercentage < 15.0) // Less than 15% RAM available
                            {
                                performanceAlerts.Add(CreateAlert(
                                    8001,
                                    "Memory Manager",
                                    $"CRITICAL: System RAM is nearly exhausted ({freePercentage:F1}% free). High risk of pagefile thrashing and system stuttering."
                                ));
                            }
                        }
                    }

                    // 2. Check CPU Bottleneck (Sustained High Load)
                    using (var searcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"))
                    using (var collection = searcher.Get())
                    {
                        foreach (var cpu in collection)
                        {
                            int cpuLoad = Convert.ToInt32(cpu["PercentProcessorTime"]);

                            if (cpuLoad > 90) // CPU over 90%
                            {
                                performanceAlerts.Add(CreateAlert(
                                    8002,
                                    "Processor Telemetry",
                                    $"WARNING: CPU load is exceptionally high ({cpuLoad}%). Background tasks may be restricting foreground performance."
                                ));
                            }
                        }
                    }

                    // 3. Check Disk I/O Saturation (Active Time)
                    using (var searcher = new ManagementObjectSearcher("SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='_Total'"))
                    using (var collection = searcher.Get())
                    {
                        foreach (var disk in collection)
                        {
                            int diskLoad = Convert.ToInt32(disk["PercentDiskTime"]);

                            if (diskLoad > 95)
                            {
                                performanceAlerts.Add(CreateAlert(
                                    8003,
                                    "Storage Controller",
                                    $"WARNING: Disk I/O is saturated. System responsiveness is severely degraded. Check for aggressive background indexing or failing drives."
                                ));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Performance Telemetry Error] {ex.Message}");
                }
            });

            return performanceAlerts;
        }

        private SystemEventItem CreateAlert(int eventId, string source, string message)
        {
            return new SystemEventItem
            {
                TimeCreated = DateTime.Now,
                SourceName = source,
                EventId = eventId,
                Level = 1, // 1 = Critical/Warning UI
                Message = message,
                IsFixable = true
            };
        }
    }
}