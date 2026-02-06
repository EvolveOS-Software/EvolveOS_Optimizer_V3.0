using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal sealed class RegistryHelp : TakingOwnership
    {
        private static string GeneralRegistry(RegistryKey registrykey)
        {
            // registrykey.Name can technically be null, so we handle it with ??
            return (registrykey.Name ?? "").Split('\\').First() switch
            {
                "HKEY_LOCAL_MACHINE" => $@"MACHINE\",
                "HKEY_CLASSES_ROOT" => $@"CLASSES_ROOT\",
                "HKEY_CURRENT_USER" => $@"CURRENT_USER\",
                "HKEY_USERS" => $@"USERS\",
                _ => $@"CURRENT_CONFIG\",
            };
        }

        internal static void DeleteValue(RegistryKey registrykey, string subkey, string value, bool isTakingOwner = false)
        {
            Task.Run(delegate
            {
                using (var checkKey = registrykey.OpenSubKey(subkey))
                {
                    if (checkKey == null || checkKey.GetValue(value, null) == null)
                    {
                        return;
                    }
                }

                try
                {
                    if (isTakingOwner)
                    {
                        GrantAdministratorsAccess($"{GeneralRegistry(registrykey)}{subkey}", SE_OBJECT_TYPE.SE_REGISTRY_KEY);
                    }

                    using var writeKey = registrykey.OpenSubKey(subkey, true);
                    writeKey?.DeleteValue(value);
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }).GetAwaiter().GetResult();
        }

        internal static void Write<T>(RegistryKey registrykey, string subkey, string? name, T data, RegistryValueKind kind, bool isTakingOwner = false) where T : notnull
        {
            Task.Run(delegate
            {
                try
                {
                    if (isTakingOwner)
                    {
                        GrantAdministratorsAccess($"{GeneralRegistry(registrykey)}{subkey}", SE_OBJECT_TYPE.SE_REGISTRY_KEY);
                    }

                    using var writeKey = registrykey.CreateSubKey(subkey, true);
                    writeKey?.SetValue(name, data, kind);
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }).GetAwaiter().GetResult();
        }

        internal static void CreateFolder(RegistryKey registrykey, string subkey)
        {
            Task.Run(delegate
            {
                try { registrykey.CreateSubKey(subkey); }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }).GetAwaiter().GetResult();
        }

        internal static void DeleteFolderTree(RegistryKey registrykey, string subkey, bool isTakingOwner = false)
        {
            Task.Run(delegate
            {
                try
                {
                    if (isTakingOwner)
                    {
                        GrantAdministratorsAccess($"{GeneralRegistry(registrykey)}{subkey}", SE_OBJECT_TYPE.SE_REGISTRY_KEY);
                    }

                    using (RegistryKey? registryFolder = registrykey.OpenSubKey(subkey, true))
                    {
                        if (registryFolder != null)
                        {
                            foreach (string value in registryFolder.GetValueNames())
                            {
                                try { registryFolder.DeleteValue(value); }
                                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                            }
                        }
                    }
                    registrykey.DeleteSubKeyTree(subkey, false);
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }).GetAwaiter().GetResult();
        }

        internal static bool KeyExists(RegistryKey registryKey, string subKey, bool invert = false)
        {
            using RegistryKey? opened = registryKey.OpenSubKey(subKey);
            bool result = opened != null;
            return invert ? !result : result;
        }

        internal static bool ValueExists(string subKey, string? valueName, bool invert = false)
        {
            bool result = Registry.GetValue(subKey, valueName ?? "", null) != null;
            return invert ? !result : result;
        }

        internal static bool CheckValue(string subKey, string? valueName, string? expectedValue, bool invert = false)
        {
            string value = Registry.GetValue(subKey, valueName ?? "", null)?.ToString() ?? "";
            bool result = !string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase);
            return invert ? !result : result;
        }

        internal static bool CheckValueBytes(string subkey, string? valueName, string? expectedValue)
        {
            var val = Registry.GetValue(subkey, valueName ?? "", null);
            if (!(val is byte[] bytes))
            {
                return true;
            }

            return string.Concat(bytes) != expectedValue;
        }

        internal static T GetValue<T>(string subKey, string? valueName, T defaultValue) where T : notnull
        {
            try
            {
                object? val = Registry.GetValue(subKey, valueName ?? "", defaultValue);
                if (val == null) return defaultValue;
                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch { return defaultValue; }
        }

        internal static T GetSubKeyNames<T>(RegistryKey baseKey, string subKeyPath) where T : ICollection<string>, new()
        {
            try
            {
                using RegistryKey? key = baseKey.OpenSubKey(subKeyPath);
                if (key == null)
                {
                    return new T();
                }

                T result = new T();
                foreach (var name in key.GetSubKeyNames())
                {
                    result.Add(name);
                }

                return result;
            }
            catch { return new T(); }
        }

        public static HashSet<string> GetInstalledAppsSnapshot()
        {
            var installedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] ghostExclusions = {
                "OneDrive Setup",
                "Microsoft Teams Update",
                "Teams Machine-Wide Installer",
                "Microsoft Edge Update",
                "Edge Update"
            };

            try
            {
                string[] standardPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };
                RegistryKey[] roots = { Registry.LocalMachine, Registry.CurrentUser };

                foreach (var root in roots)
                {
                    foreach (var path in standardPaths)
                    {
                        using (RegistryKey? key = root.OpenSubKey(path, false))
                        {
                            if (key == null) continue;

                            foreach (string subkeyName in key.GetSubKeyNames())
                            {
                                using (RegistryKey? subkey = key.OpenSubKey(subkeyName, false))
                                {
                                    if (subkey == null) continue;

                                    if (subkey.GetValue("SystemComponent") is int sysComp && sysComp == 1) continue;
                                    if (subkey.GetValue("ParentKeyName") != null) continue;

                                    if (subkey.GetValue("DisplayName") is string dn)
                                    {
                                        if (ghostExclusions.Any(ghost => dn.Equals(ghost, StringComparison.OrdinalIgnoreCase))) continue;

                                        if (dn.Contains("Microsoft Edge", StringComparison.OrdinalIgnoreCase) &&
                                            string.IsNullOrEmpty(subkey.GetValue("UninstallString")?.ToString()))
                                        {
                                            continue;
                                        }

                                        installedApps.Add(dn);
                                    }
                                }
                            }
                        }
                    }
                }

                string[] appxPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\InboxApplications",
                    @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"
                };

                foreach (var path in appxPaths)
                {
                    RegistryKey root = path.StartsWith("Software\\Classes", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;

                    using (RegistryKey? key = root.OpenSubKey(path, false))
                    {
                        if (key == null) continue;

                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            string cleanName = subkeyName.Split('_')[0];
                            installedApps.Add(cleanName);

                            using (RegistryKey? subkey = key.OpenSubKey(subkeyName))
                            {
                                if (subkey?.GetValue("DisplayName") is string dn && !string.IsNullOrWhiteSpace(dn))
                                {
                                    installedApps.Add(dn);
                                }
                            }
                        }
                    }
                }

                using (RegistryKey? customKey = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer\PackageManager", false))
                {
                    if (customKey != null)
                    {
                        foreach (string valName in customKey.GetValueNames())
                        {
                            installedApps.Add(valName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Registry Snapshot Error: {ex.Message}");
            }

            return installedApps;
        }
    }
}