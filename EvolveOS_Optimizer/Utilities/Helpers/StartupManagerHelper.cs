// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Win32;
using static EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class StartupManagerHelper
    {
        #region Constants & Paths

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        private static readonly string ProfilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EvolveOS", "Profiles");
        private static readonly string DelayedAppsFile = Path.Combine(ProfilesPath, "DelayedAppsTracker.json");

        #endregion

        #region Core Data Retrieval

        public static async Task<List<StartupApp>> GetStartupAppsAsync()
        {
            var apps = new List<StartupApp>();

            await Task.Run(() =>
            {
                // 1. Current User Registry
                GetRegistryApps(Registry.CurrentUser, RunKey, StartupSourceType.RegistryHKCU, apps);

                // 2. Local Machine Registry
                GetRegistryApps(Registry.LocalMachine, RunKey, StartupSourceType.RegistryHKLM, apps);

                // 3. User Startup Folder
                GetFolderApps(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSourceType.FolderUser, apps);

                // 4. Common (All Users) Startup Folder
                GetFolderApps(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSourceType.FolderCommon, apps);
            });

            return apps;
        }

        private static void GetRegistryApps(RegistryKey root, string keyPath, StartupSourceType type, List<StartupApp> apps)
        {
            try
            {
                using var key = root.OpenSubKey(keyPath);
                if (key == null) return;

                foreach (var valueName in key.GetValueNames())
                {
                    var path = key.GetValue(valueName)?.ToString() ?? string.Empty;
                    bool isEnabled = IsApprovedEnabled(root, ApprovedRunKey, valueName);

                    var cleanPath = path.Split(" -")[0].Split(" /")[0].Replace("\"", "").Trim();

                    string parsedDisplayName = valueName;
                    string publisher = "Unverified Developer";
                    bool isVerified = false;

                    if (File.Exists(cleanPath))
                    {
                        try
                        {
                            var versionInfo = FileVersionInfo.GetVersionInfo(cleanPath);
                            if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                            {
                                parsedDisplayName = versionInfo.FileDescription;
                            }

                            // Suppress SYSLIB0057 because .NET 10's X509CertificateLoader 
                            // does NOT support extracting Authenticode signatures from .exe files.
                            #pragma warning disable SYSLIB0057
                            var cert = new X509Certificate2(cleanPath);
                            #pragma warning restore SYSLIB0057

                            publisher = cert.GetNameInfo(X509NameType.SimpleName, false) ?? "Unknown Verified Publisher";
                            isVerified = true;
                        }
                        catch (Exception ex)
                        {
                            ErrorLogging.LogDebug(ex);
                        }
                    }

                    apps.Add(new StartupApp
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = valueName,
                        DisplayName = parsedDisplayName,
                        Path = path.Replace("\"", ""),
                        IsEnabled = isEnabled,
                        SourceType = type,
                        SourceLocation = type == StartupSourceType.RegistryHKCU ? "HKCU Registry" : "HKLM Registry",
                        RegistryPath = $@"{root.Name}\{keyPath}",
                        Publisher = publisher,
                        IsVerified = isVerified
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        private static void GetFolderApps(string folderPath, StartupSourceType type, List<StartupApp> apps)
        {
            try
            {
                if (!Directory.Exists(folderPath)) return;

                RegistryKey approvedRoot = type == StartupSourceType.FolderUser ? Registry.CurrentUser : Registry.LocalMachine;

                foreach (var file in Directory.GetFiles(folderPath, "*.lnk"))
                {
                    var name = Path.GetFileName(file);
                    bool isEnabled = IsApprovedEnabled(approvedRoot, ApprovedFolderKey, name);

                    apps.Add(new StartupApp
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = name,
                        DisplayName = Path.GetFileNameWithoutExtension(file),
                        Path = file,
                        IsEnabled = isEnabled,
                        SourceType = type,
                        SourceLocation = type == StartupSourceType.FolderUser ? "User Startup Folder" : "Global Startup Folder",
                        Publisher = "Shortcut File",
                        IsVerified = false
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        #endregion

        #region Native Windows Toggling & Deletion

        public static async Task<bool> ToggleStartupAppAsync(StartupApp app, bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    RegistryKey root = (app.SourceType == StartupSourceType.RegistryHKCU || app.SourceType == StartupSourceType.FolderUser)
                        ? Registry.CurrentUser
                        : Registry.LocalMachine;

                    string approvedKey = (app.SourceType == StartupSourceType.RegistryHKCU || app.SourceType == StartupSourceType.RegistryHKLM)
                        ? ApprovedRunKey
                        : ApprovedFolderKey;

                    return SetApprovedState(root, approvedKey, app.Name, enable);
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                    return false;
                }
            });
        }

        private static bool IsApprovedEnabled(RegistryKey root, string approvedKeyPath, string valueName)
        {
            try
            {
                using var key = root.OpenSubKey(approvedKeyPath);
                if (key?.GetValue(valueName) is byte[] data && data.Length > 0)
                {
                    return data[0] != 0x03;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }

            return true;
        }

        private static bool SetApprovedState(RegistryKey root, string approvedKeyPath, string valueName, bool enable)
        {
            try
            {
                using var key = root.CreateSubKey(approvedKeyPath);
                if (key == null) return false;

                if (enable)
                {
                    key.DeleteValue(valueName, false);
                }
                else
                {
                    byte[] disabledData = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    key.SetValue(valueName, disabledData, RegistryValueKind.Binary);
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                return false;
            }
        }

        public static async Task<bool> DeleteStartupAppAsync(StartupApp app)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (app.SourceType == StartupSourceType.RegistryHKCU || app.SourceType == StartupSourceType.RegistryHKLM)
                    {
                        var rootKey = app.SourceType == StartupSourceType.RegistryHKCU ? Registry.CurrentUser : Registry.LocalMachine;

                        using (var key = rootKey.OpenSubKey(RunKey, true))
                        {
                            key?.DeleteValue(app.Name, false);
                        }

                        using (var approvedKey = rootKey.OpenSubKey(ApprovedRunKey, true))
                        {
                            approvedKey?.DeleteValue(app.Name, false);
                        }

                        return true;
                    }
                    else if (app.SourceType == StartupSourceType.FolderUser || app.SourceType == StartupSourceType.FolderCommon)
                    {
                        if (File.Exists(app.Path))
                        {
                            File.Delete(app.Path);

                            var rootKey = app.SourceType == StartupSourceType.FolderUser ? Registry.CurrentUser : Registry.LocalMachine;
                            using (var approvedKey = rootKey.OpenSubKey(ApprovedFolderKey, true))
                            {
                                approvedKey?.DeleteValue(app.Name, false);
                            }

                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug($"[StartupManager] Failed to delete {app.Name}: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion

        #region Premium Feature: Delayed Startup

        public static async Task<bool> DelayStartupAppAsync(StartupApp app, int delaySeconds)
        {
            try
            {
                await ToggleStartupAppAsync(app, false);

                string timeFormat = $"PT{delaySeconds}S";

                string taskName = $"EvolveOS_Delay_{app.Name.Replace(" ", "_")}";
                string script = $@"
                    $trigger = New-ScheduledTaskTrigger -AtLogon;
                    $trigger.Delay = '{timeFormat}';
                    $action = New-ScheduledTaskAction -Execute '{app.Path}';
                    Register-ScheduledTask -TaskName '{taskName}' -Trigger $trigger -Action $action -RunLevel Highest -Force;
                ";

                await CommandExecutor.InvokeRunCommand(script, true);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                return false;
            }
        }

        public static async Task<bool> RemoveDelayStartupAppAsync(StartupApp app)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    string taskName = $"EvolveOS_Delay_{app.Name.Replace(" ", "_")}";
                    string script = $"Unregister-ScheduledTask -TaskName '{taskName}' -Confirm:$false";

                    await CommandExecutor.InvokeRunCommand(script, true);

                    await ToggleStartupAppAsync(app, true);
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                    return false;
                }
            });
        }

        public static async Task SaveDelayedAppStateAsync(string appName, int delaySeconds)
        {
            Directory.CreateDirectory(ProfilesPath);
            var delayedApps = new Dictionary<string, int>();

            if (File.Exists(DelayedAppsFile))
            {
                string existingJson = await File.ReadAllTextAsync(DelayedAppsFile);
                delayedApps = JsonSerializer.Deserialize<Dictionary<string, int>>(existingJson) ?? new Dictionary<string, int>();
            }

            if (delaySeconds > 0)
                delayedApps[appName] = delaySeconds;
            else
                delayedApps.Remove(appName);

            await File.WriteAllTextAsync(DelayedAppsFile, JsonSerializer.Serialize(delayedApps));
        }

        public static async Task<Dictionary<string, int>> GetDelayedAppsStateAsync()
        {
            if (File.Exists(DelayedAppsFile))
            {
                string json = await File.ReadAllTextAsync(DelayedAppsFile);
                return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
            }
            return new Dictionary<string, int>();
        }

        #endregion

        #region Premium Feature: Smart Profiles

        public static async Task<string> DetermineActiveProfileAsync(List<StartupApp> currentApps)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    if (!Directory.Exists(ProfilesPath)) return "Modified";

                    foreach (var file in Directory.GetFiles(ProfilesPath, "*.json"))
                    {
                        if (file.EndsWith("DelayedAppsTracker.json")) continue;

                        var json = await File.ReadAllTextAsync(file);
                        var profileStates = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);

                        if (profileStates == null) continue;

                        bool isExactMatch = true;

                        foreach (var kvp in profileStates)
                        {
                            var liveApp = currentApps.FirstOrDefault(a => a.Name == kvp.Key);

                            if (liveApp != null && liveApp.IsEnabled != kvp.Value)
                            {
                                isExactMatch = false;
                                break;
                            }
                        }

                        if (isExactMatch)
                        {
                            return Path.GetFileNameWithoutExtension(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }

                return "Modified";
            });
        }

        public static async Task SaveProfileAsync(string profileName, List<StartupApp> currentApps)
        {
            try
            {
                Directory.CreateDirectory(ProfilesPath);
                var states = currentApps.ToDictionary(a => a.Name, a => a.IsEnabled);

                string json = JsonSerializer.Serialize(states);
                await File.WriteAllTextAsync(Path.Combine(ProfilesPath, $"{profileName}.json"), json);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        public static async Task ApplyProfileAsync(string profileName, List<StartupApp> currentApps)
        {
            try
            {
                string file = Path.Combine(ProfilesPath, $"{profileName}.json");
                if (!File.Exists(file)) return;

                var states = JsonSerializer.Deserialize<Dictionary<string, bool>>(await File.ReadAllTextAsync(file));
                if (states == null) return;

                foreach (var app in currentApps)
                {
                    if (states.TryGetValue(app.Name, out bool shouldBeEnabled))
                    {
                        if (app.IsEnabled != shouldBeEnabled)
                        {
                            await ToggleStartupAppAsync(app, shouldBeEnabled);
                            app.IsEnabled = shouldBeEnabled;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        #endregion
    }
}