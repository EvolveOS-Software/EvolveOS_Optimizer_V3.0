// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Reflection;
using System.Text.Json;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Windows.System;

namespace EvolveOS_Optimizer.Utilities.Controls
{
    #region СheckingGlobalParameters

    internal sealed class СheckingGlobalParameters
    {
        internal static void Initialize()
        {
            try
            {
                SettingsEngine.CheckingParameters();


                LocalMachineSettingsEngine.CheckingParameters();
            }
            catch (Exception e)
            {

                ErrorLogging.LogDebug(e);
            }
        }
    }

    #endregion

    #region SettingsEngine
    internal sealed class SettingsEngine
    {
        public static readonly string[] AvailableBackdrops = { "None", "Mica", "MicaAlt", "Acrylic", "AcrylicThin", "DarkGlass" };

        internal static string currentRelease = (Assembly.GetEntryAssembly() ?? throw new InvalidOperationException())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split(' ').Last().Trim() ?? "1.0.0";

        internal static readonly string currentName = AppDomain.CurrentDomain.FriendlyName;
        internal static readonly string currentLocation = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

        private const string AppName = "EvolveOS Optimizer";
        private const string ScheduledTaskName = "[EvolveOS Optimizer]";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static readonly Dictionary<string, object> _defaultSettings = new Dictionary<string, object>
        {
            ["EnableAutoUpdate"] = true,
            ["Backdrop"] = "Mica",
            ["AppTheme"] = "Default",
            ["AccentColor"] = "#FF0078D4",
            ["AcrylicOpacity"] = 0.5,
            ["AcrylicLuminosity"] = 0.3,
            ["AcrylicTintColor"] = "#000000",
            ["EnableWindowBorder"] = false,
            ["Language"] = "en-us",
            ["EnableHoverGlow"] = true,
            ["EnableSelectionGlow"] = true,
            ["ScriptsPath"] = string.Empty,
            ["AllScriptsPaths"] = string.Empty,
            ["EnableIpBlur"] = true,
            ["LastLocation"] = "Paris",
            ["EnableRunOnStartup"] = false,
            ["EnableCloseToTray"] = true,
            ["EnableStartMinimized"] = false,
            ["EncryptionMode"] = KeyDerivationMode.Balanced.ToString(),

            ["DashboardCardOrder"] = "CardSecurity,CardWeather,CardPrivacy,CardMaintenance,CardPerformance,CardCpuGraph,CardGpuGraph,CardRamGraph,CardNetworkGraph,CardCpu,CardGpu,CardDisk,CardNetwork,CardDns,CardRamBoost,CardRam,CardGamingMode",
            ["Dashboard_CardWeather"] = true,
            ["Dashboard_CardNetwork"] = true,
            ["Dashboard_CardRam"] = false,
            ["Dashboard_CardCpu"] = true,
            ["Dashboard_CardGpu"] = true,
            ["Dashboard_CardDisk"] = true,
            ["Dashboard_CardGamingMode"] = true,
            ["Dashboard_CardDns"] = true,
            ["Dashboard_CardHealth"] = true,
            ["Dashboard_CardSecurity"] = true,
            ["Dashboard_CardPrivacy"] = true,
            ["Dashboard_CardPerformance"] = true,
            ["Dashboard_CardRamBoost"] = true,
            ["Dashboard_CardCpuGraph"] = true,
            ["Dashboard_CardRamGraph"] = true,
            ["Dashboard_CardNetworkGraph"] = true,
            ["Dashboard_CardGpuGraph"] = true,
            ["Dashboard_GraphTimeframe"] = 0,
            ["Dashboard_AutoScanEnabled"] = false,
            ["Dashboard_AutoScanIntervalHours"] = 24,
            ["Dashboard_GamingGraphicStyle"] = 0,
            ["GamingMode_MuteNotifications"] = true,
            ["GamingMode_ClearRam"] = true,
            ["GamingMode_DisableWinKey"] = false,
            ["GamingMode_KillExplorer"] = false,
            ["GamingMode_ProcessWhitelist"] = "discord.exe, obs64.exe, spotify.exe",

            ["AutoLoginSessionHours"] = 4,

            ["Dashboard_AutoRamOptimize"] = false,
            ["Dashboard_AutoRamThreshold"] = 85,
            ["Dashboard_BoostWorkingSets"] = true,
            ["Dashboard_BoostStandbyCache"] = true,
            ["Dashboard_BoostCombinedPageList"] = true,
            ["Dashboard_BoostModifiedPageList"] = true,
            ["Dashboard_BoostRegistryCache"] = true,

            ["Dashboard_LightingMode"] = 1,
            ["Dashboard_AmbientIntensity"] = 30,
            ["Dashboard_HoverRadius"] = 150,
            ["Dashboard_HoverColor"] = "#FFFFFFFF",

            ["SaveCardExpandedStates"] = true,
            ["IsCpuCardExpanded"] = false,
            ["IsGpuCardExpanded"] = false,
            ["IsDiskCardExpanded"] = false,
            ["IsNetworkCardExpanded"] = false,
            ["IsDnsCardExpanded"] = false,
            ["IsRamBoostCardExpanded"] = false,
            ["IsPrivacyCardExpanded"] = false,
            ["IsPerformanceCardExpanded"] = false,
            ["IsHealthCardExpanded"] = false,
            ["IsSecurityCardExpanded"] = false,
            ["IsGamingModeCardExpanded"] = false,

            ["IsPasswordGenHotkeyEnabled"] = false,
            ["PasswordGenHotkeyModifier"] = 1,
            ["PasswordGenHotkeyKey"] = 80,

            ["EnableAutoTheme"] = false,
            ["LightThemeTimeStr"] = "08:00:00",
            ["DarkThemeTimeStr"] = "20:00:00",
            ["SyncOsThemeWithApp"] = false,

            ["DatabaseBackupPath"] = string.Empty,
            ["PerformDbBackup"] = false,
            ["EncryptDbBackupCopies"] = true,
            ["KeepBackupEnabled"] = false,

            ["IsHighPerformanceModeEnabled"] = true,
            ["IsPostCleanEnabled"] = false,
            ["PostCleanCommands"] = "cleanmgr.exe /autoclean\nstart \"\" ms-settings:storagesense",
            ["CustomWinapp2Path"] = string.Empty,
            ["SelectedCleanerEntries"] = string.Empty,
            ["IsScheduledCleanEnabled"] = false,
            ["ScheduledCleanDayIndex"] = 0,
            ["ScheduledCleanTimeStr"] = "12:00:00",

            ["DevCacheRetentionIndex"] = 0,

            ["HideFanControlWarningDialog"] = false,
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);

        internal static bool IsUpdateCheckRequired { get => (bool)_cachedSettings["EnableAutoUpdate"]; set => ChangingParameters("EnableAutoUpdate", value); }
        internal static string Backdrop { get => (string)_cachedSettings["Backdrop"]; set => ChangingParameters("Backdrop", value); }
        internal static string AppTheme { get => (string)_cachedSettings["AppTheme"]; set => ChangingParameters("AppTheme", value); }
        internal static string AccentColor { get => (string)_cachedSettings["AccentColor"]; set => ChangingParameters("AccentColor", value); }
        internal static string AcrylicTintColor { get => (string)_cachedSettings["AcrylicTintColor"]; set => ChangingParameters("AcrylicTintColor", value); }
        internal static bool IsWindowBorderEnabled { get => (bool)_cachedSettings["EnableWindowBorder"]; set => ChangingParameters("EnableWindowBorder", value); }
        internal static double AcrylicOpacity { get => Convert.ToDouble(_cachedSettings["AcrylicOpacity"]); set => ChangingParameters("AcrylicOpacity", value); }
        internal static double AcrylicLuminosity { get => Convert.ToDouble(_cachedSettings["AcrylicLuminosity"]); set => ChangingParameters("AcrylicLuminosity", value); }
        internal static string Language { get => (string)_cachedSettings["Language"]; set => ChangingParameters("Language", value); }
        internal static bool IsHoverGlowEnabled { get => (bool)_cachedSettings["EnableHoverGlow"]; set => ChangingParameters("EnableHoverGlow", value); }
        internal static bool IsSelectionGlowEnabled { get => (bool)_cachedSettings["EnableSelectionGlow"]; set => ChangingParameters("EnableSelectionGlow", value); }
        internal static string UserScriptsPath { get => (string)_cachedSettings["ScriptsPath"]; set => ChangingParameters("ScriptsPath", value); }
        internal static string LastLocation { get => (string)_cachedSettings["LastLocation"]; set => ChangingParameters("LastLocation", value); }
        internal static bool IsRunOnStartUp { get => (bool)_cachedSettings["EnableRunOnStartup"]; set { if ((bool)_cachedSettings["EnableRunOnStartup"] != value) { ChangingParameters("EnableRunOnStartup", value); ToggleStartup(value, IsStartMinimized); } } }
        internal static bool IsCloseToTrayEnabled { get => (bool)_cachedSettings["EnableCloseToTray"]; set => ChangingParameters("EnableCloseToTray", value); }
        internal static bool IsStartMinimized { get => (bool)_cachedSettings["EnableStartMinimized"]; set { if ((bool)_cachedSettings["EnableStartMinimized"] != value) { ChangingParameters("EnableStartMinimized", value); if (IsRunOnStartUp) { ToggleStartup(true, value); } } } }
        internal static string EncryptionMode { get => (string)_cachedSettings["EncryptionMode"]; set => ChangingParameters("EncryptionMode", value); }
        internal static List<string> AllUserScriptsPaths
        {
            get
            {
                if (_cachedSettings.TryGetValue("AllScriptsPaths", out object? val) &&
                    val is string pathsRaw &&
                    !string.IsNullOrWhiteSpace(pathsRaw))
                {
                    return pathsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                return new List<string>();
            }
            set
            {
                string serialized = (value != null)
                    ? string.Join(";", value.Where(p => !string.IsNullOrEmpty(p)).Distinct())
                    : string.Empty;

                ChangingParameters("AllScriptsPaths", serialized);
            }
        }
        internal static bool IsHiddenIpAddress { get => (bool)_cachedSettings["EnableIpBlur"]; set => ChangingParameters("EnableIpBlur", value); }
        internal static int AutoLoginSessionHours { get => (int)_cachedSettings["AutoLoginSessionHours"]; set => ChangingParameters("AutoLoginSessionHours", value); }
        internal static bool IsPasswordGenHotkeyEnabled { get => (bool)_cachedSettings["IsPasswordGenHotkeyEnabled"]; set => ChangingParameters("IsPasswordGenHotkeyEnabled", value); }
        internal static int PasswordGenHotkeyModifier { get => (int)_cachedSettings["PasswordGenHotkeyModifier"]; set => ChangingParameters("PasswordGenHotkeyModifier", value); }
        internal static int PasswordGenHotkeyKey { get => (int)_cachedSettings["PasswordGenHotkeyKey"]; set => ChangingParameters("PasswordGenHotkeyKey", value); }

        internal static string DashboardCardOrder { get => (string)_cachedSettings["DashboardCardOrder"]; set => ChangingParameters("DashboardCardOrder", value); }
        internal static bool Dashboard_CardWeather { get => (bool)_cachedSettings["Dashboard_CardWeather"]; set => ChangingParameters("Dashboard_CardWeather", value); }
        internal static bool Dashboard_CardNetwork { get => (bool)_cachedSettings["Dashboard_CardNetwork"]; set => ChangingParameters("Dashboard_CardNetwork", value); }
        internal static bool Dashboard_CardRam { get => (bool)_cachedSettings["Dashboard_CardRam"]; set => ChangingParameters("Dashboard_CardRam", value); }
        internal static bool Dashboard_CardCpu { get => (bool)_cachedSettings["Dashboard_CardCpu"]; set => ChangingParameters("Dashboard_CardCpu", value); }
        internal static bool Dashboard_CardGpu { get => (bool)_cachedSettings["Dashboard_CardGpu"]; set => ChangingParameters("Dashboard_CardGpu", value); }
        internal static bool Dashboard_CardDisk { get => (bool)_cachedSettings["Dashboard_CardDisk"]; set => ChangingParameters("Dashboard_CardDisk", value); }
        internal static bool Dashboard_CardGamingMode { get => (bool)_cachedSettings["Dashboard_CardGamingMode"]; set => ChangingParameters("Dashboard_CardGamingMode", value); }
        internal static bool Dashboard_CardDns { get => (bool)_cachedSettings["Dashboard_CardDns"]; set => ChangingParameters("Dashboard_CardDns", value); }
        internal static bool Dashboard_CardHealth { get => (bool)_cachedSettings["Dashboard_CardHealth"]; set => ChangingParameters("Dashboard_CardHealth", value); }
        internal static bool Dashboard_CardSecurity { get => (bool)_cachedSettings["Dashboard_CardSecurity"]; set => ChangingParameters("Dashboard_CardSecurity", value); }
        internal static bool Dashboard_CardPrivacy { get => (bool)_cachedSettings["Dashboard_CardPrivacy"]; set => ChangingParameters("Dashboard_CardPrivacy", value); }
        internal static bool Dashboard_CardPerformance { get => (bool)_cachedSettings["Dashboard_CardPerformance"]; set => ChangingParameters("Dashboard_CardPerformance", value); }
        internal static bool Dashboard_CardRamBoost { get => (bool)_cachedSettings["Dashboard_CardRamBoost"]; set => ChangingParameters("Dashboard_CardRamBoost", value); }
        internal static bool Dashboard_CardCpuGraph { get => (bool)_cachedSettings["Dashboard_CardCpuGraph"]; set => ChangingParameters("Dashboard_CardCpuGraph", value); }
        internal static bool Dashboard_CardRamGraph { get => (bool)_cachedSettings["Dashboard_CardRamGraph"]; set => ChangingParameters("Dashboard_CardRamGraph", value); }
        internal static bool Dashboard_CardNetworkGraph { get => (bool)_cachedSettings["Dashboard_CardNetworkGraph"]; set => ChangingParameters("Dashboard_CardNetworkGraph", value); }
        internal static bool Dashboard_CardGpuGraph { get => (bool)_cachedSettings["Dashboard_CardGpuGraph"]; set => ChangingParameters("Dashboard_CardGpuGraph", value); }
        internal static int Dashboard_GraphTimeframe { get => (int)_cachedSettings["Dashboard_GraphTimeframe"]; set => ChangingParameters("Dashboard_GraphTimeframe", value); }
        internal static bool Dashboard_AutoScanEnabled { get => (bool)_cachedSettings["Dashboard_AutoScanEnabled"]; set => ChangingParameters("Dashboard_AutoScanEnabled", value); }
        internal static int Dashboard_AutoScanIntervalHours { get => (int)_cachedSettings["Dashboard_AutoScanIntervalHours"]; set => ChangingParameters("Dashboard_AutoScanIntervalHours", value); }
        internal static int Dashboard_GamingGraphicStyle { get => (int)_cachedSettings["Dashboard_GamingGraphicStyle"]; set => ChangingParameters("Dashboard_GamingGraphicStyle", value); }
        internal static bool GamingMode_MuteNotifications { get => (bool)_cachedSettings["GamingMode_MuteNotifications"]; set => ChangingParameters("GamingMode_MuteNotifications", value); }
        internal static bool GamingMode_ClearRam { get => (bool)_cachedSettings["GamingMode_ClearRam"]; set => ChangingParameters("GamingMode_ClearRam", value); }
        internal static bool GamingMode_DisableWinKey { get => (bool)_cachedSettings["GamingMode_DisableWinKey"]; set => ChangingParameters("GamingMode_DisableWinKey", value); }
        internal static bool GamingMode_KillExplorer { get => (bool)_cachedSettings["GamingMode_KillExplorer"]; set => ChangingParameters("GamingMode_KillExplorer", value); }
        internal static string GamingMode_ProcessWhitelist { get => (string)_cachedSettings["GamingMode_ProcessWhitelist"]; set => ChangingParameters("GamingMode_ProcessWhitelist", value); }
        internal static bool Dashboard_AutoRamOptimize { get => (bool)_cachedSettings["Dashboard_AutoRamOptimize"]; set => ChangingParameters("Dashboard_AutoRamOptimize", value); }
        internal static int Dashboard_AutoRamThreshold { get => (int)_cachedSettings["Dashboard_AutoRamThreshold"]; set => ChangingParameters("Dashboard_AutoRamThreshold", value); }
        internal static bool Dashboard_BoostWorkingSets { get => (bool)_cachedSettings["Dashboard_BoostWorkingSets"]; set => ChangingParameters("Dashboard_BoostWorkingSets", value); }
        internal static bool Dashboard_BoostStandbyCache { get => (bool)_cachedSettings["Dashboard_BoostStandbyCache"]; set => ChangingParameters("Dashboard_BoostStandbyCache", value); }
        internal static bool Dashboard_BoostCombinedPageList { get => (bool)_cachedSettings["Dashboard_BoostCombinedPageList"]; set => ChangingParameters("Dashboard_BoostCombinedPageList", value); }
        internal static bool Dashboard_BoostModifiedPageList { get => (bool)_cachedSettings["Dashboard_BoostModifiedPageList"]; set => ChangingParameters("Dashboard_BoostModifiedPageList", value); }
        internal static bool Dashboard_BoostRegistryCache { get => (bool)_cachedSettings["Dashboard_BoostRegistryCache"]; set => ChangingParameters("Dashboard_BoostRegistryCache", value); }
        internal static bool SaveCardExpandedStates { get => (bool)_cachedSettings["SaveCardExpandedStates"]; set => ChangingParameters("SaveCardExpandedStates", value); }
        internal static bool IsCpuCardExpanded { get => (bool)_cachedSettings["IsCpuCardExpanded"]; set => ChangingParameters("IsCpuCardExpanded", value); }
        internal static bool IsGpuCardExpanded { get => (bool)_cachedSettings["IsGpuCardExpanded"]; set => ChangingParameters("IsGpuCardExpanded", value); }
        internal static bool IsDiskCardExpanded { get => (bool)_cachedSettings["IsDiskCardExpanded"]; set => ChangingParameters("IsDiskCardExpanded", value); }
        internal static bool IsNetworkCardExpanded { get => (bool)_cachedSettings["IsNetworkCardExpanded"]; set => ChangingParameters("IsNetworkCardExpanded", value); }
        internal static bool IsDnsCardExpanded { get => (bool)_cachedSettings["IsDnsCardExpanded"]; set => ChangingParameters("IsDnsCardExpanded", value); }
        internal static bool IsRamBoostCardExpanded { get => (bool)_cachedSettings["IsRamBoostCardExpanded"]; set => ChangingParameters("IsRamBoostCardExpanded", value); }
        internal static bool IsPrivacyCardExpanded { get => (bool)_cachedSettings["IsPrivacyCardExpanded"]; set => ChangingParameters("IsPrivacyCardExpanded", value); }
        internal static bool IsPerformanceCardExpanded { get => (bool)_cachedSettings["IsPerformanceCardExpanded"]; set => ChangingParameters("IsPerformanceCardExpanded", value); }
        internal static bool IsHealthCardExpanded { get => (bool)_cachedSettings["IsHealthCardExpanded"]; set => ChangingParameters("IsHealthCardExpanded", value); }
        internal static bool IsSecurityCardExpanded { get => (bool)_cachedSettings["IsSecurityCardExpanded"]; set => ChangingParameters("IsSecurityCardExpanded", value); }
        internal static bool IsGamingModeCardExpanded { get => (bool)_cachedSettings["IsGamingModeCardExpanded"]; set => ChangingParameters("IsGamingModeCardExpanded", value); }
        internal static int Dashboard_LightingMode { get => (int)_cachedSettings["Dashboard_LightingMode"]; set => ChangingParameters("Dashboard_LightingMode", value); }
        internal static int Dashboard_AmbientIntensity { get => (int)_cachedSettings["Dashboard_AmbientIntensity"]; set => ChangingParameters("Dashboard_AmbientIntensity", value); }
        internal static int Dashboard_HoverRadius { get => (int)_cachedSettings["Dashboard_HoverRadius"]; set => ChangingParameters("Dashboard_HoverRadius", value); }
        internal static string Dashboard_HoverColor { get => (string)_cachedSettings["Dashboard_HoverColor"]; set => ChangingParameters("Dashboard_HoverColor", value); }

        internal static bool IsAutoThemeEnabled { get => (bool)_cachedSettings["EnableAutoTheme"]; set => ChangingParameters("EnableAutoTheme", value); }
        internal static TimeSpan LightThemeTime { get => TimeSpan.TryParse((string)_cachedSettings["LightThemeTimeStr"], out TimeSpan result) ? result : new TimeSpan(8, 0, 0); set => ChangingParameters("LightThemeTimeStr", value.ToString(@"hh\:mm\:ss")); }
        internal static TimeSpan DarkThemeTime { get => TimeSpan.TryParse((string)_cachedSettings["DarkThemeTimeStr"], out TimeSpan result) ? result : new TimeSpan(20, 0, 0); set => ChangingParameters("DarkThemeTimeStr", value.ToString(@"hh\:mm\:ss")); }
        internal static bool SyncOsThemeWithApp { get => (bool)_cachedSettings["SyncOsThemeWithApp"]; set => ChangingParameters("SyncOsThemeWithApp", value); }

        internal static string DatabaseBackupPath { get => (string)_cachedSettings["DatabaseBackupPath"]; set => ChangingParameters("DatabaseBackupPath", value); }
        internal static bool PerformDbBackup { get => (bool)_cachedSettings["PerformDbBackup"]; set => ChangingParameters("PerformDbBackup", value); }
        internal static bool EncryptDbBackupCopies { get => (bool)_cachedSettings["EncryptDbBackupCopies"]; set => ChangingParameters("EncryptDbBackupCopies", value); }
        internal static bool KeepBackupEnabled { get => (bool)_cachedSettings["KeepBackupEnabled"]; set => ChangingParameters("KeepBackupEnabled", value); }
        internal static bool IsHighPerformanceModeEnabled { get => (bool)_cachedSettings["IsHighPerformanceModeEnabled"]; set => ChangingParameters("IsHighPerformanceModeEnabled", value); }
        internal static bool IsPostCleanEnabled { get => (bool)_cachedSettings["IsPostCleanEnabled"]; set => ChangingParameters("IsPostCleanEnabled", value); }
        internal static string PostCleanCommands { get => (string)_cachedSettings["PostCleanCommands"]; set => ChangingParameters("PostCleanCommands", value); }
        internal static string? CustomWinapp2Path { get => string.IsNullOrEmpty((string)_cachedSettings["CustomWinapp2Path"]) ? null : (string)_cachedSettings["CustomWinapp2Path"]; set => ChangingParameters("CustomWinapp2Path", value ?? string.Empty); }
        internal static bool IsScheduledCleanEnabled { get => (bool)_cachedSettings["IsScheduledCleanEnabled"]; set => ChangingParameters("IsScheduledCleanEnabled", value); }
        internal static int ScheduledCleanDayIndex { get => (int)_cachedSettings["ScheduledCleanDayIndex"]; set => ChangingParameters("ScheduledCleanDayIndex", value); }
        internal static int DevCacheRetentionIndex { get => (int)_cachedSettings["DevCacheRetentionIndex"]; set => ChangingParameters("DevCacheRetentionIndex", value); }
        internal static bool HideFanControlWarningDialog { get => (bool)_cachedSettings["HideFanControlWarningDialog"]; set => ChangingParameters("HideFanControlWarningDialog", value); }

        internal static TimeSpan ScheduledCleanTime
        {
            get => TimeSpan.TryParse((string)_cachedSettings["ScheduledCleanTimeStr"], out TimeSpan result) ? result : new TimeSpan(12, 0, 0);
            set => ChangingParameters("ScheduledCleanTimeStr", value.ToString(@"hh\:mm\:ss"));
        }

        internal static HashSet<string> SelectedCleanerEntries
        {
            get
            {
                if (_cachedSettings.TryGetValue("SelectedCleanerEntries", out object? val) &&
                    val is string pathsRaw &&
                    !string.IsNullOrWhiteSpace(pathsRaw))
                {
                    return pathsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            set
            {
                string serialized = (value != null && value.Count > 0)
                    ? string.Join(";", value.Where(p => !string.IsNullOrEmpty(p)).Distinct())
                    : string.Empty;

                ChangingParameters("SelectedCleanerEntries", serialized);
            }
        }

        private static void ChangingParameters(string key, object value)
        {
            _cachedSettings[key] = value;

            try
            {
                using (RegistryKey? regKey = Registry.CurrentUser.CreateSubKey(PathLocator.Registry.SubKey, true))
                {
                    if (regKey != null)
                    {
                        if (value is bool b)
                            regKey.SetValue(key, b ? 1 : 0, RegistryValueKind.DWord);
                        else if (value is int i)
                            regKey.SetValue(key, i, RegistryValueKind.DWord);
                        else
                            regKey.SetValue(key, value.ToString() ?? "", RegistryValueKind.String);

                        regKey.Flush();
                        Debug.WriteLine($"[Settings] SAVED TO: HKCU\\{PathLocator.Registry.SubKey}\\{key} = {value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] WRITE ERROR: {ex.Message}");
            }

            ApplyLiveSettings(key, value);
        }

        private static void ApplyLiveSettings(string key, object value)
        {
            if (App.Current is not App) return;

            if (key == "Language")
            {
                SetAppLanguage(value.ToString() ?? "en-us");
            }

            if (key == "AppTheme")
            {
                UpdateTheme((string)value);
            }

            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (key == "Backdrop" || key == "AcrylicOpacity" || key == "AcrylicLuminosity" || key == "AcrylicTintColor")
                    {
                        UIHelper.ApplyBackdrop(mainWindow, Backdrop);
                    }
                    else if (key == "AccentColor")
                    {
                        MainWindow.ApplyAccentColor(value.ToString() ?? "#FF0078D4");
                    }
                });
            }
        }

        internal static void CheckingParameters()
        {
            try
            {
                using (RegistryKey? rootKey = Registry.CurrentUser.OpenSubKey(PathLocator.Registry.SubKey, false))
                {
                    foreach (var kv in _defaultSettings)
                    {
                        if (rootKey != null && rootKey.GetValue(kv.Key) != null)
                        {
                            object rawVal = rootKey.GetValue(kv.Key)!;
                            _cachedSettings[kv.Key] = kv.Value switch
                            {
                                bool => Convert.ToInt32(rawVal) != 0,
                                int => Convert.ToInt32(rawVal),
                                _ => rawVal.ToString() ?? kv.Value.ToString()!
                            };
                        }
                    }
                }

                string startLang = Language;
                SetAppLanguage(startLang);
            }
            catch (Exception ex) { Debug.WriteLine($"[Settings] CheckingParameters Error: {ex.Message}"); }

            UpdateAppInstance();
        }

        public static void SetAppLanguage(string langCode)
        {
            string safeCode = langCode.ToLower().Trim();
            if (safeCode == "de") safeCode = "de-de";
            if (safeCode == "en") safeCode = "en-us";
            if (safeCode == "fr") safeCode = "fr-fr";
            if (safeCode == "it") safeCode = "it-it";
            if (safeCode == "nl") safeCode = "nl-nl";

            LocalizationService.Instance.LoadLanguage(safeCode);

            Debug.WriteLine($"[Settings] Language logic completed via C# Cache for: {safeCode}");
        }

        public static void UpdateTheme(string themeStr, Window? targetWindow = null)
        {
            var window = targetWindow ?? App.MainWindow;

            if (window?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = themeStr switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }

            if (SyncOsThemeWithApp)
            {
                SetWindowsSystemTheme(themeStr);
            }
        }

        public static void SetWindowsSystemTheme(string theme)
        {
            try
            {
                int themeValue = theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

                string registryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(registryKeyPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue("AppsUseLightTheme", themeValue, RegistryValueKind.DWord);
                        key.SetValue("SystemUsesLightTheme", themeValue, RegistryValueKind.DWord);
                    }
                }

                Debug.WriteLine($"[System Theme] Successfully switched Windows to {theme} mode.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[System Theme Error] Failed to change OS theme: {ex.Message}");
            }
        }

        private static void UpdateAppInstance()
        {
            if (App.Current is not App) return;

            if (App.MainWindow is MainWindow target)
            {
                target.SetBackdropByName(Backdrop);
            }
        }


        public static void ToggleStartup(bool enable, bool startHidden)
        {
            string exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;

            if (enable)
            {
                bool registrySuccess = false;
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                    {
                        if (key != null)
                        {
                            string command = $"\"{exePath}\"{(startHidden ? " -hidden" : "")}";
                            key.SetValue(AppName, command);
                            registrySuccess = true;

                            DisableTask();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Registry Write Failed: " + e.Message);
                    registrySuccess = false;
                }

                if (!registrySuccess)
                {
                    RemoveRegistryKey();
                    EnableTask(exePath, startHidden);
                }
            }
            else
            {
                RemoveRegistryKey();
                DisableTask();
            }
        }

        private static void EnableTask(string exePath, bool startHidden)
        {
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    TaskDefinition td = taskService.NewTask();
                    td.RegistrationInfo.Description = "Runs EvolveOS Optimizer on startup.";
                    td.Principal.RunLevel = TaskRunLevel.Highest;
                    td.Triggers.Add(new LogonTrigger());

                    string arguments = startHidden ? "-hidden" : "";
                    td.Actions.Add(new ExecAction(exePath, arguments));

                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.StopIfGoingOnBatteries = false;
                    td.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                    taskService.RootFolder.RegisterTaskDefinition(ScheduledTaskName, td);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Task Scheduler Enable Failed: " + e.Message);
            }
        }

        private static void DisableTask()
        {
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    if (taskService.FindTask(ScheduledTaskName) != null)
                    {
                        taskService.RootFolder.DeleteTask(ScheduledTaskName);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Task Scheduler Disable Failed: " + e.Message);
            }
        }

        private static void RemoveRegistryKey()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    key?.DeleteValue(AppName, false);
                }
            }
            catch { }
        }

        internal static void SelfReboot(string injectedCommand = "")
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (!string.IsNullOrEmpty(exePath))
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string extra = string.IsNullOrWhiteSpace(injectedCommand) ? "" : $"{injectedCommand}; ";

                string psScript = $"{extra}Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 800; Start-Process '{exePath}';";

                _ = CommandExecutor.RunCommand(psScript, isPowerShell: true);
            }

            App.ExitApp(ResourceString.GetString("status_rebooting") ?? "Restarting EvolveOS Optimizer");
        }
    }
    #endregion

    #region Local Machine Settings Engine

    internal sealed class LocalMachineSettingsEngine
    {
        private static readonly string _dismissedEventsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer", "DismissedEvents.json");

        private static readonly Dictionary<string, object> _defaultSettings = new Dictionary<string, object>
        {
            ["AutoOptimizationInterval"] = 0,
            ["AutoOptimizationMemoryUsage"] = 0,
            ["ShowDiskSpace"] = true,
            ["RestartExplorerAfterOptimization"] = false,
            ["DisableAllOptimizationResults"] = false,
            ["ShowOptimizationNotifications"] = true,
            ["ShowVirtualMemory"] = false,
            ["RunOnPriority"] = (int)Priority.Low,
            ["UseHotkey"] = false,
            ["OptimizationKey"] = (int)VirtualKey.M,
            ["OptimizationModifiers"] = (int)(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift),
            ["MemoryAreas"] = (int)(Memory.Areas.CombinedPageList | Memory.Areas.ModifiedFileCache | Memory.Areas.ModifiedPageList | Memory.Areas.RegistryCache |
                        Memory.Areas.StandbyList | Memory.Areas.SystemFileCache | Memory.Areas.WorkingSet | Memory.Areas.DiskCleanup | Memory.Areas.FlushDns),
            ["EnableDeveloperMode"] = false,
            ["IsFirstRun"] = true,
            ["HasChosenResourceMode"] = false,
            ["EnableTranslationHotkey"] = false,
            ["TranslationHotkeyModifier"] = (int)(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift),
            ["TranslationHotkeyKey"] = (int)VirtualKey.L,
            ["EnableStartupMonitor"] = true,
            ["EnableLiveDiagnostics"] = false,
            ["DiagnosticsGraphTime"] = 60,
            ["LastCachePurgeTime"] = DateTime.MinValue.ToString("o"),

            ["ShowHardwarePanelInTray"] = false,
            ["ShowCpuInTray"] = true,
            ["ShowRamInTray"] = true,
            ["ShowDiskInTray"] = true,
            ["ShowGpuInTray"] = true,
            ["GuardianPaused"] = false,

            ["ActiveAiProvider"] = (int)AiProvider.Groq,
            ["GroqApiKey"] = "",
            ["GeminiApiKey"] = "",
            ["OpenRouterApiKey"] = "",
            ["CohereApiKey"] = "",
            ["MistralApiKey"] = "",
            ["AiUseLocalization"] = true,

            ["EnableFindHotkey"] = false,
            ["FindHotkeyModifier"] = (int)(VirtualKeyModifiers.Control),
            ["FindHotkeyKey"] = (int)VirtualKey.F,
            ["HideRegistryWarning"] = false,
            ["HidePawnIoPrompt"] = false,
            ["IsModernContextMenuEnabled"] = false,

            ["EnableThermalWarnings"] = false,
            ["EnableThermalShutdown"] = false,
            ["EmergencyThresholdSeconds"] = 5,
            ["EnableAudibleAlarms"] = false,
            ["EnableThermalLogging"] = false,
            ["LastThermalShutdownEvent"] = "",
            ["EmergencyAction"] = 0,
            ["WarningCooldownMinutes"] = 5,
            ["CpuWarningTemp"] = 80,
            ["CpuMaxTemp"] = 95,
            ["GpuWarningTemp"] = 80,
            ["GpuMaxTemp"] = 95,
            ["RamWarningTemp"] = 65,
            ["RamMaxTemp"] = 80,
            ["MoboWarningTemp"] = 60,
            ["MoboMaxTemp"] = 80,

            ["IgnoredSecurityIssues"] = string.Empty
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);
        public static SortedSet<string> ProcessExclusionList { get; private set; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        public static HashSet<string> DismissedEventsList { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static event EventHandler<string> SettingChanged = delegate { };

        internal static bool KeepDevModeOnExit { get; set; } = false;

        #region Properties
        internal static int AutoOptimizationInterval { get => (int)_cachedSettings["AutoOptimizationInterval"]; set => ChangingParameters("AutoOptimizationInterval", value); }
        internal static int AutoOptimizationMemoryUsage { get => (int)_cachedSettings["AutoOptimizationMemoryUsage"]; set => ChangingParameters("AutoOptimizationMemoryUsage", value); }
        internal static bool ShowDiskSpace { get => (bool)_cachedSettings["ShowDiskSpace"]; set => ChangingParameters("ShowDiskSpace", value); }
        internal static bool RestartExplorerAfterOptimization { get => (bool)_cachedSettings["RestartExplorerAfterOptimization"]; set => ChangingParameters("RestartExplorerAfterOptimization", value); }
        internal static bool DisableAllOptimizationResults { get => (bool)_cachedSettings["DisableAllOptimizationResults"]; set => ChangingParameters("DisableAllOptimizationResults", value); }
        internal static bool ShowOptimizationNotifications { get => (bool)_cachedSettings["ShowOptimizationNotifications"]; set => ChangingParameters("ShowOptimizationNotifications", value); }
        internal static bool ShowVirtualMemory { get => (bool)_cachedSettings["ShowVirtualMemory"]; set => ChangingParameters("ShowVirtualMemory", value); }
        internal static bool UseHotkey { get => (bool)_cachedSettings["UseHotkey"]; set => ChangingParameters("UseHotkey", value); }
        internal static Priority RunOnPriority { get => (Priority)(int)_cachedSettings["RunOnPriority"]; set => ChangingParameters("RunOnPriority", (int)value); }
        internal static VirtualKey OptimizationKey { get => (VirtualKey)Convert.ToInt32(_cachedSettings["OptimizationKey"]); set => ChangingParameters("OptimizationKey", (int)value); }
        internal static VirtualKeyModifiers OptimizationModifiers { get => (VirtualKeyModifiers)Convert.ToInt32(_cachedSettings["OptimizationModifiers"]); set => ChangingParameters("OptimizationModifiers", (int)value); }
        internal static Memory.Areas MemoryAreas { get => (Memory.Areas)(int)_cachedSettings["MemoryAreas"]; set => ChangingParameters("MemoryAreas", (int)value); }
        internal static bool IsDeveloperMode { get => (bool)_cachedSettings["EnableDeveloperMode"]; set => ChangingParameters("EnableDeveloperMode", value); }
        internal static bool IsFirstRun { get => (bool)_cachedSettings["IsFirstRun"]; set => ChangingParameters("IsFirstRun", value); }
        internal static bool HasChosenResourceMode { get => (bool)_cachedSettings["HasChosenResourceMode"]; set => ChangingParameters("HasChosenResourceMode", value); }
        internal static bool IsTranslationHotkeyEnabled { get => (bool)_cachedSettings["EnableTranslationHotkey"]; set => ChangingParameters("EnableTranslationHotkey", value); }
        internal static int TranslationHotkeyModifier { get => (int)_cachedSettings["TranslationHotkeyModifier"]; set => ChangingParameters("TranslationHotkeyModifier", value); }
        internal static int TranslationHotkeyKey { get => (int)_cachedSettings["TranslationHotkeyKey"]; set => ChangingParameters("TranslationHotkeyKey", value); }
        internal static bool EnableStartupMonitor { get => (bool)_cachedSettings["EnableStartupMonitor"]; set => ChangingParameters("EnableStartupMonitor", value); }
        internal static bool EnableLiveDiagnostics { get => (bool)_cachedSettings["EnableLiveDiagnostics"]; set => ChangingParameters("EnableLiveDiagnostics", value); }
        internal static int DiagnosticsGraphTime { get => (int)_cachedSettings["DiagnosticsGraphTime"]; set => ChangingParameters("DiagnosticsGraphTime", value); }

        internal static bool ShowHardwarePanelInTray { get => (bool)_cachedSettings["ShowHardwarePanelInTray"]; set => ChangingParameters("ShowHardwarePanelInTray", value); }
        internal static bool ShowCpuInTray { get => (bool)_cachedSettings["ShowCpuInTray"]; set => ChangingParameters("ShowCpuInTray", value); }
        internal static bool ShowRamInTray { get => (bool)_cachedSettings["ShowRamInTray"]; set => ChangingParameters("ShowRamInTray", value); }
        internal static bool ShowDiskInTray { get => (bool)_cachedSettings["ShowDiskInTray"]; set => ChangingParameters("ShowDiskInTray", value); }
        internal static bool ShowGpuInTray { get => (bool)_cachedSettings["ShowGpuInTray"]; set => ChangingParameters("ShowGpuInTray", value); }

        internal static bool IsGuardianPaused { get => (bool)_cachedSettings["GuardianPaused"]; set => ChangingParameters("GuardianPaused", value); }

        internal static AiProvider ActiveAiProvider { get => (AiProvider)(int)_cachedSettings["ActiveAiProvider"]; set => ChangingParameters("ActiveAiProvider", (int)value); }
        internal static string GroqApiKey { get => (string)_cachedSettings["GroqApiKey"]; set => ChangingParameters("GroqApiKey", value); }
        internal static string GeminiApiKey { get => (string)_cachedSettings["GeminiApiKey"]; set => ChangingParameters("GeminiApiKey", value); }
        internal static string OpenRouterApiKey { get => (string)_cachedSettings["OpenRouterApiKey"]; set => ChangingParameters("OpenRouterApiKey", value); }
        internal static string CohereApiKey { get => (string)_cachedSettings["CohereApiKey"]; set => ChangingParameters("CohereApiKey", value); }
        internal static string MistralApiKey { get => (string)_cachedSettings["MistralApiKey"]; set => ChangingParameters("MistralApiKey", value); }
        internal static bool AiUseLocalization { get => (bool)_cachedSettings["AiUseLocalization"]; set => ChangingParameters("AiUseLocalization", value); }

        internal static bool IsFindHotkeyEnabled { get => (bool)_cachedSettings["EnableFindHotkey"]; set => ChangingParameters("EnableFindHotkey", value); }
        internal static int FindHotkeyModifier { get => (int)_cachedSettings["FindHotkeyModifier"]; set => ChangingParameters("FindHotkeyModifier", value); }
        internal static int FindHotkeyKey { get => (int)_cachedSettings["FindHotkeyKey"]; set => ChangingParameters("FindHotkeyKey", value); }
        internal static bool HideRegistryWarning { get => (bool)_cachedSettings["HideRegistryWarning"]; set => ChangingParameters("HideRegistryWarning", value); }
        internal static bool HidePawnIoPrompt { get => (bool)_cachedSettings["HidePawnIoPrompt"]; set => ChangingParameters("HidePawnIoPrompt", value); }
        internal static bool IsModernContextMenuEnabled { get => (bool)_cachedSettings["IsModernContextMenuEnabled"]; set => ChangingParameters("IsModernContextMenuEnabled", value); }

        internal static bool EnableThermalWarnings { get => (bool)_cachedSettings["EnableThermalWarnings"]; set => ChangingParameters("EnableThermalWarnings", value); }
        internal static bool EnableThermalShutdown { get => (bool)_cachedSettings["EnableThermalShutdown"]; set => ChangingParameters("EnableThermalShutdown", value); }
        internal static int EmergencyThresholdSeconds { get => (int)_cachedSettings["EmergencyThresholdSeconds"]; set => ChangingParameters("EmergencyThresholdSeconds", value); }
        internal static bool EnableAudibleAlarms { get => (bool)_cachedSettings["EnableAudibleAlarms"]; set => ChangingParameters("EnableAudibleAlarms", value); }
        internal static bool EnableThermalLogging { get => (bool)_cachedSettings["EnableThermalLogging"]; set => ChangingParameters("EnableThermalLogging", value); }
        internal static string LastThermalShutdownEvent { get => (string)_cachedSettings["LastThermalShutdownEvent"]; set => ChangingParameters("LastThermalShutdownEvent", value); }
        internal static int EmergencyAction { get => (int)_cachedSettings["EmergencyAction"]; set => ChangingParameters("EmergencyAction", value); }
        internal static int WarningCooldownMinutes { get => (int)_cachedSettings["WarningCooldownMinutes"]; set => ChangingParameters("WarningCooldownMinutes", value); }
        internal static int CpuWarningTemp { get => (int)_cachedSettings["CpuWarningTemp"]; set => ChangingParameters("CpuWarningTemp", value); }
        internal static int CpuMaxTemp { get => (int)_cachedSettings["CpuMaxTemp"]; set => ChangingParameters("CpuMaxTemp", value); }
        internal static int GpuWarningTemp { get => (int)_cachedSettings["GpuWarningTemp"]; set => ChangingParameters("GpuWarningTemp", value); }
        internal static int GpuMaxTemp { get => (int)_cachedSettings["GpuMaxTemp"]; set => ChangingParameters("GpuMaxTemp", value); }
        internal static int RamWarningTemp { get => (int)_cachedSettings["RamWarningTemp"]; set => ChangingParameters("RamWarningTemp", value); }
        internal static int RamMaxTemp { get => (int)_cachedSettings["RamMaxTemp"]; set => ChangingParameters("RamMaxTemp", value); }
        internal static int MoboWarningTemp { get => (int)_cachedSettings["MoboWarningTemp"]; set => ChangingParameters("MoboWarningTemp", value); }
        internal static int MoboMaxTemp { get => (int)_cachedSettings["MoboMaxTemp"]; set => ChangingParameters("MoboMaxTemp", value); }
        #endregion

        internal static HashSet<string> IgnoredSecurityIssues
        {
            get
            {
                if (_cachedSettings.TryGetValue("IgnoredSecurityIssues", out object? val) &&
                    val is string pathsRaw &&
                    !string.IsNullOrWhiteSpace(pathsRaw))
                {
                    return pathsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            set
            {
                string serialized = (value != null && value.Count > 0)
                    ? string.Join(";", value.Where(p => !string.IsNullOrEmpty(p)).Distinct())
                    : string.Empty;

                ChangingParameters("IgnoredSecurityIssues", serialized);
            }
        }

        private static void ChangingParameters(string key, object? value)
        {
            if (value == null) return;

            _cachedSettings[key] = value;

            SettingChanged?.Invoke(null, key);

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var regKey = baseKey.CreateSubKey(Win32Helper.Registry.Key.Settings, true);

                if (regKey != null)
                {
                    if (value is bool b)
                        regKey.SetValue(key, b ? 1 : 0, RegistryValueKind.DWord);
                    else if (value is int i)
                        regKey.SetValue(key, i, RegistryValueKind.DWord);
                    else if (value is Enum)
                        regKey.SetValue(key, Convert.ToInt32(value), RegistryValueKind.DWord);
                    else
                        regKey.SetValue(key, value.ToString() ?? "", RegistryValueKind.String);

                    regKey.Flush();
                    Debug.WriteLine($"[Registry] LKM Saved: {key} = {value}");
                }
            }
            catch (Exception e) { ErrorLogging.LogDebug(e); }
        }

        internal static void CheckingParameters()
        {
            Load();

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = baseKey.CreateSubKey(Win32Helper.Registry.Key.Settings, true);

                if (key == null) return;

                foreach (var setting in _defaultSettings)
                {
                    if (key.GetValue(setting.Key) == null)
                    {
                        Debug.WriteLine($"[Registry] Missing key found: {setting.Key}. Writing default...");
                        ChangingParameters(setting.Key, setting.Value);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLogging.LogDebug(e);
            }
        }

        public static void Reset()
        {
            foreach (var kvp in _defaultSettings)
            {
                _cachedSettings[kvp.Key] = kvp.Value;
            }
            //Save();
            SaveExclusionList();
        }

        internal static void Load()
        {
            string systemDrive = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";

            if (System.IO.Directory.Exists(System.IO.Path.Combine(systemDrive, "Windows.old")))
            {
                int currentAreas = Convert.ToInt32(_cachedSettings["MemoryAreas"]);
                _cachedSettings["MemoryAreas"] = currentAreas | (int)Memory.Areas.WindowsOld;
            }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

                using (var key = baseKey.OpenSubKey(Win32Helper.Registry.Key.Settings))
                {
                    if (key != null)
                    {
                        foreach (var settingName in _defaultSettings.Keys.ToList())
                        {
                            object? val = key.GetValue(settingName);
                            if (val != null)
                            {
                                var defaultValue = _defaultSettings[settingName];

                                if (defaultValue is bool)
                                {
                                    _cachedSettings[settingName] = Convert.ToBoolean(val);
                                }
                                else if (defaultValue is int || defaultValue is Enum)
                                {
                                    _cachedSettings[settingName] = Convert.ToInt32(val);
                                }
                                else
                                {
                                    _cachedSettings[settingName] = val.ToString() ?? string.Empty;
                                }
                            }
                        }
                    }
                }

                ProcessExclusionList.Clear();
                using (var key = baseKey.OpenSubKey(Win32Helper.Registry.Key.ProcessExclusionList))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            if (string.IsNullOrWhiteSpace(name)) continue;

                            string cleanName = new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray());
                            ProcessExclusionList.Add(cleanName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).ToLower());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLogging.LogDebug(e);
            }
        }

        /*public static void Save()
        {
            foreach (var setting in _cachedSettings.ToList())
            {
                ChangingParameters(setting.Key, setting.Value);
            }

            SaveExclusionList();
        }*/

        public static void SaveExclusionList()
        {
            try
            {
                Registry.LocalMachine.DeleteSubKey(Win32Helper.Registry.Key.ProcessExclusionList, false);
                if (ProcessExclusionList.Any())
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(Win32Helper.Registry.Key.ProcessExclusionList))
                    {
                        if (key == null) return;

                        foreach (var process in ProcessExclusionList)
                        {
                            if (string.IsNullOrWhiteSpace(process)) continue;

                            string cleanProcess = new string(process.Where(c => !char.IsWhiteSpace(c)).ToArray());
                            key.SetValue(cleanProcess.Replace(".exe", string.Empty).ToLower(), string.Empty, RegistryValueKind.String);
                        }
                    }
                }
            }
            catch (Exception e) { ErrorLogging.LogDebug(e); }
        }

        internal static void LoadDismissedEventsList()
        {
            try
            {
                if (File.Exists(_dismissedEventsFile))
                {
                    string json = File.ReadAllText(_dismissedEventsFile);
                    var loaded = JsonSerializer.Deserialize<HashSet<string>>(json);
                    if (loaded != null) DismissedEventsList = loaded;
                }
            }
            catch { System.Diagnostics.Debug.WriteLine("Failed to load dismissed events."); }
        }

        internal static void SaveDismissedEventsList()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_dismissedEventsFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(DismissedEventsList);
                File.WriteAllText(_dismissedEventsFile, json);
            }
            catch { System.Diagnostics.Debug.WriteLine("Failed to save dismissed events."); }
        }

        internal static DateTime LastCachePurgeTime
        {
            get
            {
                if (DateTime.TryParse(_cachedSettings["LastCachePurgeTime"].ToString(), out DateTime dt))
                    return dt;
                return DateTime.MinValue;
            }
            set => ChangingParameters("LastCachePurgeTime", value.ToString("o"));
        }

        internal static object GetDynamicSetting(string key, object defaultValue)
        {
            if (_cachedSettings.TryGetValue(key, out object? val))
                return val;

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var regKey = baseKey.OpenSubKey(Win32Helper.Registry.Key.Settings);
                if (regKey != null)
                {
                    object? regVal = regKey.GetValue(key);
                    if (regVal != null)
                    {
                        _cachedSettings[key] = regVal;
                        return regVal;
                    }
                }
            }
            catch (Exception e) { ErrorLogging.LogDebug(e); }

            return defaultValue;
        }

        internal static void SetDynamicSetting(string key, object value)
        {
            ChangingParameters(key, value);
        }

    #endregion
    }
}