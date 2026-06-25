// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class ResourceHelper
    {
        public static string GetResourceString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (Application.Current == null) return $"[{key}]";

            var res = Application.Current.Resources;
            if (res.ContainsKey(key)) return res[key]?.ToString() ?? $"[{key}]";

            foreach (var dict in res.MergedDictionaries)
            {
                var found = FindKeyRecursive(dict, key);
                if (found != null) return found;
            }

            return $"[{key}]";
        }

        private static string? FindKeyRecursive(ResourceDictionary dict, string key)
        {
            if (dict.TryGetValue(key, out object? value))
            {
                return value?.ToString();
            }

            foreach (var merged in dict.MergedDictionaries)
            {
                var result = FindKeyRecursive(merged, key);
                if (result != null) return result;
            }

            return null;
        }

        private static string? FindInDictionaries(IList<ResourceDictionary> dictionaries, string key)
        {
            foreach (var dict in dictionaries)
            {
                if (dict.TryGetValue(key, out object? val))
                {
                    return val?.ToString();
                }

                if (dict.MergedDictionaries.Count > 0)
                {
                    var nestedResult = FindInDictionaries(dict.MergedDictionaries, key);
                    if (nestedResult != null) return nestedResult;
                }
            }
            return null;
        }

        public static string GetPluralizedString(string baseKey, int count)
        {
            if (count <= 0)
            {
                string disabled = GetResourceString("txt_disabled");
                return disabled.StartsWith("[") ? "Disabled" : disabled;
            }

            string suffix = (count == 1) ? "_singular" : "_plural";
            string resourceKey = baseKey + suffix;

            string format = GetResourceString(resourceKey);

            if (format.StartsWith("["))
            {
                format = GetResourceString(baseKey);
            }

            if (format.StartsWith("["))
            {
                return count == 1 ? $"Every {count} hour" : $"Every {count} hours";
            }

            try
            {
                return string.Format(format, count);
            }
            catch
            {
                return format;
            }
        }

        public static string GetOptimizationResultMessage(
            string reason,
            KeyValuePair<double, Memory.Unit> physical,
            KeyValuePair<double, Memory.Unit> virtualMem,
            KeyValuePair<double, Memory.Unit> disk,
            bool showVirtual,
            bool showDisk)
        {
            string physUnitStr = GetLocalizedUnit(physical.Value);
            string virtUnitStr = GetLocalizedUnit(virtualMem.Value);
            string diskUnitStr = GetLocalizedUnit(disk.Value);

            string header = ResourceString.GetString("msg_mem_optimized");
            if (string.IsNullOrEmpty(header) || header == "[msg_mem_optimized]")
                header = "Memory successfully optimized!";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine();

            string lblReason = ResourceString.GetString("label_reason");
            if (string.IsNullOrEmpty(lblReason) || lblReason == "[label_reason]") lblReason = "Reason";

            string lblPhys = ResourceString.GetString("label_physical_mem");
            if (string.IsNullOrEmpty(lblPhys) || lblPhys == "[label_physical_mem]") lblPhys = "Physical Memory";

            string lblVirt = ResourceString.GetString("label_virtual_mem");
            if (string.IsNullOrEmpty(lblVirt) || lblVirt == "[label_virtual_mem]") lblVirt = "Virtual Memory";

            string lblDisk = ResourceString.GetString("label_disk_recovered");
            if (string.IsNullOrEmpty(lblDisk) || lblDisk == "[label_disk_recovered]") lblDisk = "Disk Space Recovered";

            sb.AppendFormat("{0}: {1}", lblReason, reason);
            sb.AppendLine();

            sb.AppendFormat("{0}: {1:N2} {2}", lblPhys, physical.Key, physUnitStr);

            if (showVirtual)
            {
                sb.AppendLine();
                sb.AppendFormat("{0}: {1:N2} {2}", lblVirt, virtualMem.Key, virtUnitStr);
            }

            if (showDisk)
            {
                sb.AppendLine();
                sb.AppendFormat("{0}: {1:N2} {2}", lblDisk, disk.Key, diskUnitStr);
            }

            return sb.ToString();
        }

        public static string GetLocalizedUnit(Memory.Unit unit)
        {
            string resourceKey = "unit_" + unit.ToString();
            string localizedUnit = ResourceString.GetString(resourceKey);

            return localizedUnit != $"[{resourceKey}]" ? localizedUnit : unit.ToString();
        }

        private static object? GetFallbackResource(string key)
        {
            try
            {
                if (Application.Current == null) return null;

                var enDict = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/en/Localize.xaml", StringComparison.OrdinalIgnoreCase));

                if (enDict != null && enDict.TryGetValue(key, out object? resource))
                {
                    return resource;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FALLBACK ERROR] Failed to search English dictionary: {ex.Message}");
            }

            return null;
        }

        public static void InjectXmlNode(string path, XNamespace nv, XNamespace x, string key, string value)
        {
            if (!File.Exists(path)) return;

            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null) return;

                if (root.Attribute(XNamespace.Xmlns + "sys") == null)
                {
                    root.Add(new XAttribute(XNamespace.Xmlns + "sys", nv.NamespaceName));
                }

                bool exists = root.Descendants().Any(e => e.Attribute(x + "Key")?.Value == key);

                if (!exists)
                {
                    XElement newElement = new XElement(nv + "String", value);
                    newElement.Add(new XAttribute(x + "Key", key));

                    root.Add(newElement);
                    doc.Save(path, SaveOptions.None);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[XML INJECT ERROR] {ex.Message}");
            }
        }

        public static void MergeMissingStringsToXaml(string langCode)
        {
            string langDir = Path.Combine(AppContext.BaseDirectory, "Languages");
            string jsonPath = Path.Combine(langDir, $"MissingStrings_{langCode}.json");
            string xamlPath = Path.Combine(langDir, $"{langCode}.xaml");

            if (!File.Exists(jsonPath))
            {
                Debug.WriteLine($"[Merge] No missing strings JSON found for {langCode}. Everything is up to date.");
                return;
            }

            if (!File.Exists(xamlPath))
            {
                Debug.WriteLine($"[Merge] Target XAML dictionary {xamlPath} does not exist.");
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                var missingStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                if (missingStrings == null || missingStrings.Count == 0) return;

                var doc = XDocument.Load(xamlPath);
                var root = doc.Root;
                if (root == null) return;

                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
                XNamespace sys = "clr-namespace:System;assembly=mscorlib";

                if (root.Attribute(XNamespace.Xmlns + "sys") == null)
                {
                    root.Add(new XAttribute(XNamespace.Xmlns + "sys", sys.NamespaceName));
                }

                bool wasModified = false;

                foreach (var kvp in missingStrings)
                {
                    bool exists = root.Descendants(sys + "String").Any(e => e.Attribute(x + "Key")?.Value == kvp.Key);

                    if (!exists)
                    {
                        XElement newElement = new XElement(sys + "String", kvp.Value);
                        newElement.Add(new XAttribute(x + "Key", kvp.Key));
                        root.Add(newElement);
                        wasModified = true;
                        Debug.WriteLine($"[Merge] Injected: {kvp.Key}");
                    }
                }

                if (wasModified)
                {
                    doc.Save(xamlPath, SaveOptions.None);
                    Debug.WriteLine($"[Merge] Successfully updated {langCode}.xaml");

                    File.WriteAllText(jsonPath, "{}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Merge Error] {ex.Message}");
            }
        }
    }
}