using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    internal sealed class INIManager
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern long WritePrivateProfileString(string? section, string? key, string? value, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetPrivateProfileString(string? section, string? key, string? _default, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetPrivateProfileSection(string? section, StringBuilder retVal, int size, string filePath);

        private readonly string _pathToConfig;

        internal static Dictionary<string, string>
            TempTweaksConf = new Dictionary<string, string>(),
            TempTweaksIntf = new Dictionary<string, string>(),
            TempTweaksSvc = new Dictionary<string, string>(),
            TempTweaksSys = new Dictionary<string, string>();

        internal static bool IsAllTempDictionaryEmpty => TempTweaksConf.Count == 0 && TempTweaksIntf.Count == 0 && TempTweaksSvc.Count == 0 && TempTweaksSys.Count == 0;

        internal const string SectionConf = "Confidentiality Tweaks";
        internal const string SectionIntf = "Interface Tweaks";
        internal const string SectionSvc = "Services Tweaks";
        internal const string SectionSys = "System Tweaks";

        internal INIManager(string iniPath)
        {
            if (string.IsNullOrWhiteSpace(iniPath))
                throw new ArgumentNullException(nameof(iniPath));

            _pathToConfig = new FileInfo(iniPath).FullName;
        }

        internal string Read(string section, string key)
        {
            StringBuilder retValue = new StringBuilder(255);

            GetPrivateProfileString(section, key, string.Empty, retValue, 255, _pathToConfig);
            return retValue.ToString();
        }

        internal void Write(string section, string key, string value)
            => WritePrivateProfileString(section, key, value, _pathToConfig);

        internal void WriteAll(string section, Dictionary<string, string> selectedDictionary)
        {
            if (selectedDictionary == null || selectedDictionary.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<string, string> addKeyValue in selectedDictionary)
            {
                WritePrivateProfileString(section, addKeyValue.Key, addKeyValue.Value, _pathToConfig);
            }
        }

        internal static void TempWrite<T>(Dictionary<string, string> selectedDictionary, string tweak, T value)
        {
            if (selectedDictionary == null) return;

            string stringValue = value?.ToString() ?? string.Empty;

            if (selectedDictionary.ContainsKey(tweak))
            {
                selectedDictionary[tweak] = stringValue;
            }
            else
            {
                selectedDictionary.Add(tweak, stringValue);
            }
        }

        internal bool IsThereSection(string section)
        {
            StringBuilder retValue = new StringBuilder(255);
            GetPrivateProfileSection(section, retValue, 255, _pathToConfig);
            return !string.IsNullOrEmpty(retValue.ToString());
        }

        internal List<string> GetKeysOrValue(string section, bool isGetKey = true)
        {
            var result = new List<string>();

            if (!File.Exists(_pathToConfig)) return result;

            string[] lines = File.ReadAllLines(_pathToConfig);

            bool inSection = false;
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    inSection = line.Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase);
                }
                else if (inSection && line.Contains("="))
                {
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        if (isGetKey)
                        {
                            result.Add(line.Substring(0, equalsIndex).Trim());
                        }
                        else
                        {
                            string val = line.Substring(equalsIndex + 1).Trim();
                            result.Add(val);
                        }
                    }
                }
            }
            return result;
        }
    }
}