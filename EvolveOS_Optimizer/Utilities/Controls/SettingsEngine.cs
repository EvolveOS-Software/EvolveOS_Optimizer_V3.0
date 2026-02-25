using System.Reflection;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Win32;
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
        public static readonly string[] AvailableBackdrops = { "None", "Mica", "MicaAlt", "Acrylic", "AcrylicThin" };

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
            ["Language"] = "en-us",
            ["EnableHoverGlow"] = true,
            ["EnableSelectionGlow"] = true,
            ["ScriptsPath"] = string.Empty,
            ["AllScriptsPaths"] = string.Empty,
            ["EnableIpBlur"] = true,
            ["LastLocation"] = "Paris",

            ["DashboardCardOrder"] = "CardNetwork,CardRam,CardCpu,CardGpu,CardDisk",
            ["Dashboard_CardNetwork"] = true,
            ["Dashboard_CardRam"] = true,
            ["Dashboard_CardCpu"] = true,
            ["Dashboard_CardGpu"] = true,
            ["Dashboard_CardDisk"] = true
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);

        internal static bool IsUpdateCheckRequired { get => (bool)_cachedSettings["EnableAutoUpdate"]; set => ChangingParameters("EnableAutoUpdate", value); }
        internal static string Backdrop { get => (string)_cachedSettings["Backdrop"]; set => ChangingParameters("Backdrop", value); }
        internal static string AppTheme { get => (string)_cachedSettings["AppTheme"]; set => ChangingParameters("AppTheme", value); }
        internal static string AccentColor { get => (string)_cachedSettings["AccentColor"]; set => ChangingParameters("AccentColor", value); }
        internal static string AcrylicTintColor { get => (string)_cachedSettings["AcrylicTintColor"]; set => ChangingParameters("AcrylicTintColor", value); }
        internal static double AcrylicOpacity { get => Convert.ToDouble(_cachedSettings["AcrylicOpacity"]); set => ChangingParameters("AcrylicOpacity", value); }
        internal static double AcrylicLuminosity { get => Convert.ToDouble(_cachedSettings["AcrylicLuminosity"]); set => ChangingParameters("AcrylicLuminosity", value); }
        internal static string Language { get => (string)_cachedSettings["Language"]; set => ChangingParameters("Language", value); }
        internal static bool IsHoverGlowEnabled { get => (bool)_cachedSettings["EnableHoverGlow"]; set => ChangingParameters("EnableHoverGlow", value); }
        internal static bool IsSelectionGlowEnabled { get => (bool)_cachedSettings["EnableSelectionGlow"]; set => ChangingParameters("EnableSelectionGlow", value); }
        internal static string UserScriptsPath { get => (string)_cachedSettings["ScriptsPath"]; set => ChangingParameters("ScriptsPath", value); }
        internal static string LastLocation { get => (string)_cachedSettings["LastLocation"]; set => ChangingParameters("LastLocation", value); }
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
        internal static string DashboardCardOrder { get => (string)_cachedSettings["DashboardCardOrder"]; set => ChangingParameters("DashboardCardOrder", value); }
        internal static bool Dashboard_CardNetwork { get => (bool)_cachedSettings["Dashboard_CardNetwork"]; set => ChangingParameters("Dashboard_CardNetwork", value); }
        internal static bool Dashboard_CardRam { get => (bool)_cachedSettings["Dashboard_CardRam"]; set => ChangingParameters("Dashboard_CardRam", value); }
        internal static bool Dashboard_CardCpu { get => (bool)_cachedSettings["Dashboard_CardCpu"]; set => ChangingParameters("Dashboard_CardCpu", value); }
        internal static bool Dashboard_CardGpu { get => (bool)_cachedSettings["Dashboard_CardGpu"]; set => ChangingParameters("Dashboard_CardGpu", value); }
        internal static bool Dashboard_CardDisk { get => (bool)_cachedSettings["Dashboard_CardDisk"]; set => ChangingParameters("Dashboard_CardDisk", value); }

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
            if (App.Current is not App currentApp) return;

            if (key == "Language")
            {
                SetAppLanguage(value.ToString() ?? "en-us");
            }

            if (key == "AppTheme")
            {
                UpdateTheme((string)value);
            }

            if (currentApp.MainWindow is MainWindow mainWindow)
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
            if (safeCode == "en") safeCode = "en-us";
            if (safeCode == "fr") safeCode = "fr-fr";
            if (safeCode == "nl") safeCode = "nl-nl";

            LocalizationService.Instance.LoadLanguage(safeCode);

            Debug.WriteLine($"[Settings] Language logic completed via C# Cache for: {safeCode}");
        }

        public static void UpdateTheme(string themeStr, Window? targetWindow = null)
        {
            var window = targetWindow ?? App.Current.MainWindow;

            if (window?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = themeStr switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
        }

        private static void UpdateAppInstance()
        {
            if (App.Current is not App currentApp) return;

            if (currentApp.MainWindow is MainWindow target)
            {
                target.SetBackdropByName(Backdrop);
            }
        }
    }
    #endregion

    #region Local Machine Settings Engine

    internal sealed class LocalMachineSettingsEngine
    {
        private static readonly Dictionary<string, object> _defaultSettings = new Dictionary<string, object>
        {
            ["AutoOptimizationInterval"] = 0,
            ["AutoOptimizationMemoryUsage"] = 0,
            ["ShowDiskSpace"] = true,
            ["RestartExplorerAfterOptimization"] = false,
            ["DisableAllOptimizationResults"] = false,
            ["ShowOptimizationNotifications"] = true,
            ["ShowVirtualMemory"] = false,
            ["RunOnPriority"] = (int)Enums.Priority.Low,
            ["UseHotkey"] = false,
            ["OptimizationKey"] = (int)VirtualKey.M,
            ["OptimizationModifiers"] = (int)(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift),
            ["MemoryAreas"] = (int)(Enums.Memory.Areas.CombinedPageList | Enums.Memory.Areas.ModifiedFileCache | Enums.Memory.Areas.ModifiedPageList | Enums.Memory.Areas.RegistryCache | 
                        Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.SystemFileCache | Enums.Memory.Areas.WorkingSet | Enums.Memory.Areas.DiskCleanup | Enums.Memory.Areas.FlushDns)
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);
        public static SortedSet<string> ProcessExclusionList { get; private set; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

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
        internal static Enums.Priority RunOnPriority { get => (Enums.Priority)(int)_cachedSettings["RunOnPriority"]; set => ChangingParameters("RunOnPriority", (int)value); }
        internal static VirtualKey OptimizationKey { get => (VirtualKey)Convert.ToInt32(_cachedSettings["OptimizationKey"]); set => ChangingParameters("OptimizationKey", (int)value); }
        internal static VirtualKeyModifiers OptimizationModifiers { get => (VirtualKeyModifiers)Convert.ToInt32(_cachedSettings["OptimizationModifiers"]); set => ChangingParameters("OptimizationModifiers", (int)value); }
        internal static Enums.Memory.Areas MemoryAreas { get => (Enums.Memory.Areas)(int)_cachedSettings["MemoryAreas"]; set => ChangingParameters("MemoryAreas", (int)value); }
        #endregion

        private static void ChangingParameters(string key, object? value)
        {
            if (value == null) return;

            _cachedSettings[key] = value;

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
            ProcessExclusionList.Clear();

            //Save();
            SaveExclusionList();
        }

        internal static void Load()
        {
            string systemDrive = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";

            if (System.IO.Directory.Exists(System.IO.Path.Combine(systemDrive, "Windows.old")))
            {
                int currentAreas = Convert.ToInt32(_cachedSettings["MemoryAreas"]);
                _cachedSettings["MemoryAreas"] = currentAreas | (int)Enums.Memory.Areas.WindowsOld;
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
    }

    #endregion
}