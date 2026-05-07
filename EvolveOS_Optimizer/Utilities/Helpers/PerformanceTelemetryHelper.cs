// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

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
                        foreach (ManagementBaseObject os in collection)
                        {
                            using (os)
                            {
                                double freeRamKb = Convert.ToDouble(os["FreePhysicalMemory"]);
                                double totalRamKb = Convert.ToDouble(os["TotalVisibleMemorySize"]);
                                double freePercentage = (freeRamKb / totalRamKb) * 100;

                                if (freePercentage < 15.0) // Less than 15% RAM available
                                {
                                    string source = ResourceString.GetString("diag_alert_source_ram") ?? "Memory Manager";
                                    string msgTemplate = ResourceString.GetString("diag_alert_msg_ram") ?? "CRITICAL: System RAM is low ({0:F1}% free).";

                                    performanceAlerts.Add(CreateAlert(
                                        8001,
                                        source,
                                        string.Format(msgTemplate, freePercentage)
                                    ));
                                }
                            }
                        }
                    }

                    // 2. Check CPU Bottleneck (Sustained High Load)
                    using (var searcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"))
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementBaseObject cpu in collection)
                        {
                            using (cpu)
                            {
                                int cpuLoad = Convert.ToInt32(cpu["PercentProcessorTime"]);

                                if (cpuLoad > 90) // CPU over 90%
                                {
                                    string source = ResourceString.GetString("diag_alert_source_cpu") ?? "Processor Telemetry";
                                    string msgTemplate = ResourceString.GetString("diag_alert_msg_cpu") ?? "WARNING: CPU load is high ({0}%).";

                                    performanceAlerts.Add(CreateAlert(
                                        8002,
                                        source,
                                        string.Format(msgTemplate, cpuLoad)
                                    ));
                                }
                            }
                        }
                    }

                    // 3. Check Disk I/O Saturation (Active Time)
                    using (var searcher = new ManagementObjectSearcher("SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='_Total'"))
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementBaseObject disk in collection)
                        {
                            using (disk)
                            {
                                int diskLoad = Convert.ToInt32(disk["PercentDiskTime"]);

                                if (diskLoad > 95)
                                {
                                    string source = ResourceString.GetString("diag_alert_source_disk") ?? "Storage Controller";
                                    string message = ResourceString.GetString("diag_alert_msg_disk") ?? "WARNING: Disk I/O is saturated.";

                                    performanceAlerts.Add(CreateAlert(
                                        8003,
                                        source,
                                        message
                                    ));
                                }
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
                Level = 1,
                Message = message,
                IsFixable = true
            };
        }
    }
}