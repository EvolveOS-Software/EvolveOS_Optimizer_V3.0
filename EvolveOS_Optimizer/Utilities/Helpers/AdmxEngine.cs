// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Xml.Linq;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Helpers;

public static class AdmxEngine
{
    public static async Task<List<GroupPolicyHelper.PolicyEntry>> LoadAllLocalPoliciesAsync(CancellationToken token)
    {
        return await Task.Run(() =>
        {
            var policies = new List<GroupPolicyHelper.PolicyEntry>();
            string policyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "PolicyDefinitions");

            string langDir = Path.Combine(policyDir, "en-US");

            if (!Directory.Exists(policyDir)) return policies;

            var admxFiles = Directory.GetFiles(policyDir, "*.admx");

            foreach (var admxPath in admxFiles)
            {
                if (token.IsCancellationRequested) break;

                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(admxPath);
                    string admlPath = Path.Combine(langDir, fileName + ".adml");

                    Dictionary<string, string> stringTable = new();

                    if (File.Exists(admlPath))
                    {
                        var admlDoc = XDocument.Load(admlPath);
                        var ns = admlDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                        var strings = admlDoc.Descendants(ns + "string");
                        foreach (var str in strings)
                        {
                            var id = str.Attribute("id")?.Value;
                            var val = str.Value;
                            if (!string.IsNullOrEmpty(id) && !stringTable.ContainsKey(id))
                            {
                                stringTable[id] = val.Trim();
                            }
                        }
                    }

                    var admxDoc = XDocument.Load(admxPath);
                    var admxNs = admxDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                    var policyNodes = admxDoc.Descendants(admxNs + "policy");
                    foreach (var node in policyNodes)
                    {
                        string name = node.Attribute("name")?.Value ?? "Unknown";
                        string dispClass = node.Attribute("class")?.Value ?? "Machine";
                        string displayNameRef = node.Attribute("displayName")?.Value ?? "";
                        string explainRef = node.Attribute("explainText")?.Value ?? "";
                        string key = node.Attribute("key")?.Value ?? "";
                        string valueName = node.Attribute("valueName")?.Value ?? "";

                        if (string.IsNullOrEmpty(valueName) || string.IsNullOrEmpty(key)) continue;

                        string ExtractStringRef(string raw)
                        {
                            if (raw.StartsWith("$(string.") && raw.EndsWith(")"))
                            {
                                string id = raw.Substring(9, raw.Length - 10);
                                return stringTable.TryGetValue(id, out string? translated) ? translated : name;
                            }
                            return raw;
                        }

                        policies.Add(new GroupPolicyHelper.PolicyEntry
                        {
                            Id = name,
                            Name = ExtractStringRef(displayNameRef),
                            Description = ExtractStringRef(explainRef),
                            Category = fileName,
                            Hive = dispClass.Equals("Machine", StringComparison.OrdinalIgnoreCase) ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                            RegistryPath = key,
                            ValueName = valueName,
                            ValueKind = RegistryValueKind.DWord
                        });
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            }

            return policies;
        }, token);
    }
}