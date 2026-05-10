// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class DetectionService
    {
        public bool IsInstalled(CleanerEntry entry)
        {
            if (entry.SpecialDetect is not null)
            {
                if (TryCheckSpecialDetect(entry.SpecialDetect, out bool result))
                    return result;
            }

            foreach (var reg in entry.DetectKeys) if (CheckRegistry(reg)) return true;
            foreach (var file in entry.DetectFiles) if (CheckFile(file)) return true;

            return false;
        }

        private static bool CheckRegistry(string regPath)
        {
            try
            {
                var (hive, subKey, valueName) = SplitRegPath(regPath);
                using var key = OpenKey(hive, subKey);
                if (key is null) return false;
                return valueName is null || key.GetValue(valueName) is not null;
            }
            catch { return false; }
        }

        private static (string hive, string subKey, string? valueName) SplitRegPath(string path)
        {
            string regPath = path;
            string? valueName = null;

            var pipeIdx = path.LastIndexOf('|');
            if (pipeIdx >= 0)
            {
                regPath = path[..pipeIdx];
                valueName = path[(pipeIdx + 1)..];
            }

            var slashIdx = regPath.IndexOf('\\');
            var hive = slashIdx >= 0 ? regPath[..slashIdx].ToUpperInvariant() : regPath.ToUpperInvariant();
            var subKey = slashIdx >= 0 ? regPath[(slashIdx + 1)..] : "";
            return (hive, subKey, valueName);
        }

        private static RegistryKey? OpenKey(string hive, string subKey) =>
            RegistryHelp.OpenHive(hive)?.OpenSubKey(subKey, writable: false);

        private bool CheckFile(string rawPath)
        {
            try
            {
                var expanded = PathLocator.ExpandVariables(rawPath);
                if (expanded.Contains('*') || expanded.Contains('?'))
                    return PathLocator.ResolvePaths(rawPath).Count > 0;

                return File.Exists(expanded) || Directory.Exists(expanded);
            }
            catch { return false; }
        }

        private bool TryCheckSpecialDetect(string code, out bool result)
        {
            switch (code.ToUpperInvariant())
            {
                case "DET_CHROME":
                    result = CheckFile(@"%LocalAppData%\Google\Chrome\User Data"); return true;
                case "DET_FIREFOX":
                    result = CheckFile(@"%AppData%\Mozilla\Firefox"); return true;
                case "DET_IE":
                    result = CheckRegistry(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\IEXPLORE.EXE"); return true;
                case "DET_THUNDERBIRD":
                    result = CheckFile(@"%AppData%\Thunderbird"); return true;
                case "DET_OPERA":
                    result = CheckFile(@"%AppData%\Opera Software\Opera Stable"); return true;
                case "DET_EDGE":
                    result = CheckFile(@"%LocalAppData%\Microsoft\Edge\User Data"); return true;
                case "DET_WINSTORE":
                    result = CheckFile(@"%LocalAppData%\Packages"); return true;
                default:
                    result = false; return false;
            }
        }
    }
}