// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class OemManager
    {
        // Known background service names for common RGB/OEM software
        private static readonly string[] OemServices = new[]
        {
            "AsusROGLivenessService",
            "ArmouryCrateControlInterface",
            "Lanthus.LConnectService",
            "LGHUB Background Service",
            "Razer Central Service",
            "Corsair LSCore Service"
        };

        // Known executable process names to terminate if services don't stop them
        private static readonly string[] OemProcesses = new[]
        {
            "ArmouryCrate.UI",
            "L-Connect 3",
            "LGHUB",
            "Razer Central",
            "iCUE"
        };

        public static void OverrideOemSoftware(bool enable)
        {
            if (enable)
            {
                Debug.WriteLine("[OEM] Suspending competing OEM software...");

                // 1. Stop Services
                foreach (var serviceName in OemServices)
                {
                    try
                    {
                        using var sc = new ServiceController(serviceName);
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3));
                            Debug.WriteLine($"[OEM] Stopped service: {serviceName}");
                        }
                    }
                    catch
                    {
                        // Service might not be installed on this specific machine, skip safely
                    }
                }

                // 2. Kill Stub Processes
                foreach (var procName in OemProcesses)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(procName);
                        foreach (var p in processes)
                        {
                            p.Kill();
                            p.WaitForExit(1000);
                            Debug.WriteLine($"[OEM] Terminated process: {procName}");
                        }
                    }
                    catch
                    {
                        // Process might not be running
                    }
                }
            }
            else
            {
                Debug.WriteLine("[OEM] Restoring OEM software services...");

                // Restart Services when toggle is turned off
                foreach (var serviceName in OemServices)
                {
                    try
                    {
                        using var sc = new ServiceController(serviceName);
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            sc.Start();
                            Debug.WriteLine($"[OEM] Restarted service: {serviceName}");
                        }
                    }
                    catch { }
                }
            }
        }
    }
}