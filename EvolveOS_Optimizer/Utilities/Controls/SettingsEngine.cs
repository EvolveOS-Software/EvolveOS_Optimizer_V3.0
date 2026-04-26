using System.IO;
using System.Reflection;
using System.Text.Json;
using EvolveOS_Optimizer.Core;
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

            ["DashboardCardOrder"] = "CardWeather,CardDns,CardSecurity,CardGamingMode,CardMaintenance,CardCpuGraph,CardGpuGraph,CardRamGraph,CardNetworkGraph,CardCpu,CardGpu,CardRam,CardNetwork,CardDisk",
            ["Dashboard_CardWeather"] = true,
            ["Dashboard_CardNetwork"] = true,
            ["Dashboard_CardRam"] = true,
            ["Dashboard_CardCpu"] = true,
            ["Dashboard_CardGpu"] = true,
            ["Dashboard_CardDisk"] = true,
            ["Dashboard_CardGamingMode"] = true,
            ["Dashboard_CardDns"] = true,
            ["Dashboard_CardHealth"] = true,
            ["Dashboard_CardSecurity"] = true,
            ["Dashboard_CardCpuGraph"] = true,
            ["Dashboard_CardRamGraph"] = true,
            ["Dashboard_CardNetworkGraph"] = true,
            ["Dashboard_CardGpuGraph"] = true,
            ["Dashboard_GraphTimeframe"] = 0,
            ["AutoLoginSessionHours"] = 4,

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
            ["KeepBackupEnabled"] = false
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
        internal static bool Dashboard_CardCpuGraph { get => (bool)_cachedSettings["Dashboard_CardCpuGraph"]; set => ChangingParameters("Dashboard_CardCpuGraph", value); }
        internal static bool Dashboard_CardRamGraph { get => (bool)_cachedSettings["Dashboard_CardRamGraph"]; set => ChangingParameters("Dashboard_CardRamGraph", value); }
        internal static bool Dashboard_CardNetworkGraph { get => (bool)_cachedSettings["Dashboard_CardNetworkGraph"]; set => ChangingParameters("Dashboard_CardNetworkGraph", value); }
        internal static bool Dashboard_CardGpuGraph { get => (bool)_cachedSettings["Dashboard_CardGpuGraph"]; set => ChangingParameters("Dashboard_CardGpuGraph", value); }
        internal static int Dashboard_GraphTimeframe { get => (int)_cachedSettings["Dashboard_GraphTimeframe"]; set => ChangingParameters("Dashboard_GraphTimeframe", value); }

        internal static bool IsAutoThemeEnabled { get => (bool)_cachedSettings["EnableAutoTheme"]; set => ChangingParameters("EnableAutoTheme", value); }
        internal static TimeSpan LightThemeTime { get => TimeSpan.TryParse((string)_cachedSettings["LightThemeTimeStr"], out TimeSpan result) ? result : new TimeSpan(8, 0, 0); set => ChangingParameters("LightThemeTimeStr", value.ToString(@"hh\:mm\:ss")); }
        internal static TimeSpan DarkThemeTime { get => TimeSpan.TryParse((string)_cachedSettings["DarkThemeTimeStr"], out TimeSpan result) ? result : new TimeSpan(20, 0, 0); set => ChangingParameters("DarkThemeTimeStr", value.ToString(@"hh\:mm\:ss")); }
        internal static bool SyncOsThemeWithApp { get => (bool)_cachedSettings["SyncOsThemeWithApp"]; set => ChangingParameters("SyncOsThemeWithApp", value); }

        internal static string DatabaseBackupPath { get => (string)_cachedSettings["DatabaseBackupPath"]; set => ChangingParameters("DatabaseBackupPath", value); }
        internal static bool PerformDbBackup { get => (bool)_cachedSettings["PerformDbBackup"]; set => ChangingParameters("PerformDbBackup", value); }
        internal static bool EncryptDbBackupCopies { get => (bool)_cachedSettings["EncryptDbBackupCopies"]; set => ChangingParameters("EncryptDbBackupCopies", value); }
        internal static bool KeepBackupEnabled { get => (bool)_cachedSettings["KeepBackupEnabled"]; set => ChangingParameters("KeepBackupEnabled", value); }

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

        internal static async void SelfReboot(string injectedCommand = "")
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {

                string extra = string.IsNullOrWhiteSpace(injectedCommand) ? "" : $"{injectedCommand} & ";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c ping 127.0.0.1 -n 3 > nul & {extra}start \"\" \"{exePath}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
            }

            Application.Current.Exit();
        }


        public static void Reset()
        {
            foreach (var kvp in _defaultSettings)
            {
                ChangingParameters(kvp.Key, kvp.Value);
            }

            // Optional: Clear user scripts
            // AllUserScriptsPaths = new List<string>(); 
        }

        public static Dictionary<string, object> ExportSettings()
        {
            return new Dictionary<string, object>(_cachedSettings);
        }

        public static void ImportSettings(Dictionary<string, object> importedSettings)
        {
            foreach (var kvp in importedSettings)
            {
                if (_defaultSettings.ContainsKey(kvp.Key))
                {
                    object finalValue = kvp.Value;

                    if (kvp.Value is System.Text.Json.JsonElement element)
                    {
                        switch (element.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.True:
                            case System.Text.Json.JsonValueKind.False:
                                finalValue = element.GetBoolean();
                                break;
                            case System.Text.Json.JsonValueKind.Number:
                                finalValue = element.GetInt32();
                                break;
                            case System.Text.Json.JsonValueKind.String:
                                finalValue = element.GetString() ?? string.Empty;
                                break;
                        }
                    }

                    ChangingParameters(kvp.Key, finalValue);
                }
            }
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
            ["RunOnPriority"] = (int)Enums.Priority.Low,
            ["UseHotkey"] = false,
            ["OptimizationKey"] = (int)VirtualKey.M,
            ["OptimizationModifiers"] = (int)(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift),
            ["MemoryAreas"] = (int)(Enums.Memory.Areas.CombinedPageList | Enums.Memory.Areas.ModifiedFileCache | Enums.Memory.Areas.ModifiedPageList | Enums.Memory.Areas.RegistryCache |
                        Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.SystemFileCache | Enums.Memory.Areas.WorkingSet | Enums.Memory.Areas.DiskCleanup | Enums.Memory.Areas.FlushDns),
            ["EnableDeveloperMode"] = false,
            ["IsFirstRun"] = true,
            ["EnableTranslationHotkey"] = false,
            ["TranslationHotkeyModifier"] = (int)(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift),
            ["TranslationHotkeyKey"] = (int)VirtualKey.L,
            ["EnableStartupMonitor"] = true,
            ["EnableLiveDiagnostics"] = false,
            ["DiagnosticsGraphTime"] = 60,
            ["LastCachePurgeTime"] = DateTime.MinValue.ToString("o")
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);
        public static SortedSet<string> ProcessExclusionList { get; private set; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        public static HashSet<string> DismissedEventsList { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        internal static bool IsDeveloperMode { get => (bool)_cachedSettings["EnableDeveloperMode"]; set => ChangingParameters("EnableDeveloperMode", value); }
        internal static bool IsFirstRun { get => (bool)_cachedSettings["IsFirstRun"]; set => ChangingParameters("IsFirstRun", value); }
        internal static bool IsTranslationHotkeyEnabled { get => (bool)_cachedSettings["EnableTranslationHotkey"]; set => ChangingParameters("EnableTranslationHotkey", value); }
        internal static int TranslationHotkeyModifier { get => (int)_cachedSettings["TranslationHotkeyModifier"]; set => ChangingParameters("TranslationHotkeyModifier", value); }
        internal static int TranslationHotkeyKey { get => (int)_cachedSettings["TranslationHotkeyKey"]; set => ChangingParameters("TranslationHotkeyKey", value); }
        internal static bool EnableStartupMonitor { get => (bool)_cachedSettings["EnableStartupMonitor"]; set => ChangingParameters("EnableStartupMonitor", value); }
        internal static bool EnableLiveDiagnostics { get => (bool)_cachedSettings["EnableLiveDiagnostics"]; set => ChangingParameters("EnableLiveDiagnostics", value); }
        internal static int DiagnosticsGraphTime { get => (int)_cachedSettings["DiagnosticsGraphTime"]; set => ChangingParameters("DiagnosticsGraphTime", value); }
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
                ChangingParameters(kvp.Key, kvp.Value);
            }

            ProcessExclusionList.Clear();
            SaveExclusionList();
        }

        public static Dictionary<string, object> ExportSettings()
        {
            return new Dictionary<string, object>(_cachedSettings);
        }

        public static void ImportSettings(Dictionary<string, object> importedSettings)
        {
            foreach (var kvp in importedSettings)
            {
                if (_defaultSettings.ContainsKey(kvp.Key))
                {
                    object finalValue = kvp.Value;

                    if (kvp.Value is System.Text.Json.JsonElement element)
                    {
                        switch (element.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.True:
                            case System.Text.Json.JsonValueKind.False:
                                finalValue = element.GetBoolean();
                                break;
                            case System.Text.Json.JsonValueKind.Number:
                                finalValue = element.GetInt32();
                                break;
                            case System.Text.Json.JsonValueKind.String:
                                finalValue = element.GetString() ?? string.Empty;
                                break;
                        }
                    }

                    ChangingParameters(kvp.Key, finalValue);
                }
            }
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

    #endregion
    }
}