// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class RegistrySnapshotEngine
    {
        #region Snapshot Methods

        public static async Task<RegistrySnapshot> TakeSnapshotAsync(HKEY rootHive, string basePath, Action<int> progressCallback, CancellationToken token)
        {
            var snapshot = new RegistrySnapshot { RootHive = rootHive, BasePath = basePath };
            int keysScanned = 0;
            DateTime lastUpdate = DateTime.Now;

            await Task.Run(() =>
            {
                RecursiveSnapshot(rootHive, basePath, snapshot, ref keysScanned, ref lastUpdate, progressCallback, token);
            }, token);

            return snapshot;
        }

        public static async Task<List<RegistryChange>> CompareSnapshotsAsync(RegistrySnapshot snapA, RegistrySnapshot snapB, CancellationToken token)
        {
            var changes = new List<RegistryChange>();

            await Task.Run(() =>
            {
                foreach (var kvp in snapB.Keys)
                {
                    if (token.IsCancellationRequested) return;

                    if (!snapA.Keys.TryGetValue(kvp.Key, out ulong timeA))
                    {
                        changes.Add(new RegistryChange { Type = ChangeType.Added, Path = kvp.Key, Details = "Key created." });
                    }
                    else if (timeA != kvp.Value)
                    {
                        var prefix = kvp.Key + "\\";
                        var valuesB = snapB.Values.Where(x => x.Key.StartsWith(prefix)).ToList();

                        foreach (var valB in valuesB)
                        {
                            if (!snapA.Values.TryGetValue(valB.Key, out string? valAData))
                            {
                                changes.Add(new RegistryChange { Type = ChangeType.Added, Path = valB.Key, Details = $"Value created: {valB.Value}" });
                            }
                            else if (valAData != valB.Value)
                            {
                                changes.Add(new RegistryChange { Type = ChangeType.Modified, Path = valB.Key, Details = $"Changed from '{valAData}' to '{valB.Value}'" });
                            }
                        }
                    }
                }

                foreach (var keyA in snapA.Keys.Keys)
                {
                    if (token.IsCancellationRequested) return;

                    if (!snapB.Keys.ContainsKey(keyA))
                    {
                        changes.Add(new RegistryChange { Type = ChangeType.Deleted, Path = keyA, Details = "Key deleted." });
                    }
                }
            }, token);

            return changes;
        }

        #endregion

        #region Internal Logic

        private static void RecursiveSnapshot(HKEY root, string currentPath, RegistrySnapshot snapshot, ref int keysScanned, ref DateTime lastUpdate, Action<int> progressCallback, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var openResult = RegOpenKeyEx(root, currentPath, 0, REGSAM.KEY_QUERY_VALUE | REGSAM.KEY_ENUMERATE_SUB_KEYS, out var hKey);
            if (openResult.Failed) return;

            using (hKey)
            {
                var queryResult = RegQueryInfoKey(hKey, null, ref Unsafe.NullRef<uint>(), default, out var subKeyCount, out _, out _, out var valueCount, out var maxValNameLen, out var maxValLen, out _, out var lastWriteTime);

                if (queryResult.Succeeded)
                {
                    snapshot.Keys[currentPath] = lastWriteTime.ToUInt64();
                }

                uint index = 0;
                uint cchValueName = maxValNameLen + 1;
                var valueName = new StringBuilder((int)cchValueName);
                uint cbData = Math.Max(maxValLen + (maxValLen % 2), 4);

                using (var dataHandle = new SafeHGlobalHandle((int)cbData))
                {
                    while (RegEnumValue(hKey, index, valueName, ref cchValueName, default, out var type, dataHandle, ref cbData).Succeeded)
                    {
                        if (token.IsCancellationRequested) return;

                        string vName = valueName.ToString();
                        string dataStr = "";

                        if (type == REG_VALUE_TYPE.REG_SZ || type == REG_VALUE_TYPE.REG_EXPAND_SZ)
                        {
                            dataStr = dataHandle.ToString(-1, CharSet.Auto) ?? "";
                        }
                        else if (type == REG_VALUE_TYPE.REG_DWORD)
                        {
                            dataStr = dataHandle.ToStructure<uint>().ToString();
                        }

                        snapshot.Values[$"{currentPath}\\{vName}"] = dataStr;

                        index++; cchValueName = maxValNameLen + 1; valueName.Clear(); cbData = Math.Max(maxValLen + (maxValLen % 2), 4);
                    }
                }

                keysScanned++;
                if ((DateTime.Now - lastUpdate).TotalMilliseconds > 250)
                {
                    lastUpdate = DateTime.Now;
                    progressCallback.Invoke(keysScanned);
                }

                uint subKeyIndex = 0;
                uint cchKeyName = 256;
                var subKeyName = new StringBuilder((int)cchKeyName);

                while (RegEnumKeyEx(hKey, subKeyIndex, subKeyName, ref cchKeyName, default, null, ref Unsafe.NullRef<uint>(), out _).Succeeded)
                {
                    string nextPath = string.IsNullOrEmpty(currentPath) ? subKeyName.ToString() : $"{currentPath}\\{subKeyName}";
                    RecursiveSnapshot(root, nextPath, snapshot, ref keysScanned, ref lastUpdate, progressCallback, token);

                    subKeyIndex++; cchKeyName = 256; subKeyName.Clear();
                }
            }
        }

        #endregion
    }
}