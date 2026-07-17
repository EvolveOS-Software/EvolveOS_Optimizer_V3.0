// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal static class ThermalActionHelper
    {
        internal static void LogThermalEvent(string message)
        {
            if (!LocalMachineSettingsEngine.EnableThermalLogging) return;

            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer");
                Directory.CreateDirectory(logDir);

                string logFile = Path.Combine(logDir, "Thermal_Log.txt");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Silently fail if file is locked or inaccessible
            }
        }
        internal static void ExecuteSniperMode()
        {
            try
            {
                var protectedProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "idle", "system", "svchost", "explorer", "csrss", "wininit", "services", "lsass",
                    "smss", "winlogon", "taskmgr", "evolveos", "dwm", "spoolsv"
                };

                var topProcess = Process.GetProcesses()
                    .Where(p => !protectedProcs.Contains(p.ProcessName))
                    .OrderByDescending(p => p.WorkingSet64)
                    .FirstOrDefault();

                if (topProcess != null)
                {
                    LogThermalEvent($"SNIPER MODE: Terminated process tree for {topProcess.ProcessName}.exe (PID: {topProcess.Id}) to reduce heat.");

                    _ = CommandExecutor.StartInCmd($"taskkill /F /T /PID {topProcess.Id}");
                }
            }
            catch
            {
                // Silently fail if access is denied to the process
            }
        }
    }
}