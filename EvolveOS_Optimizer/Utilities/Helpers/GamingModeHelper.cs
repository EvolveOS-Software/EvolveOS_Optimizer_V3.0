// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Maintenance;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class GamingModeHelper
    {
        public static bool IsTestModeEnabled { get; set; } = false;

        private static readonly string BackupFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EvolveOS_Optimizer",
            "GamingModeBackup.json");

        public static bool IsGamingModeActive { get; private set; } = File.Exists(BackupFilePath);

        private static Dictionary<string, int> _originalServiceStates = new();
        private static List<string> _tasksDisabledByUs = new();
        private static Dictionary<string, int> _originalRegistryValues = new();
        private static Dictionary<string, string> _originalStringRegistryValues = new();
        private static string _originalPowerPlanGuid = string.Empty;
        private static Dictionary<string, int> _originalGpuRegistryValues = new();

        private static readonly string[] ProcessesToKill =
        {
            "OneDrive.exe", "MicrosoftEdge.exe", "Widgets.exe", "WidgetService.exe",
            "GameBarPresenceWriter.exe", "bcastdvr.exe", "TIWorker.exe", "MoUsoCoreWorker.exe",
            "CompatTelRunner.exe", "software_reporter_tool.exe", "MicrosoftEdgeUpdate.exe",
            "backgroundTaskHost.exe", "ScreenClippingHost.exe", "SearchApp.exe",
            "SynTPEnh.exe", "Microsoft.Photos.exe", "WinRAR.exe", "unrar.exe", "rar.exe",
            "msedgewebview2.exe", "LocalBridge.exe", "WinStore.App.exe", "SkypeApp.exe",
            "SkypeBridge.exe", "SkypeBackgroundHost.exe", "HxTsr.exe", "HxOutlook.exe",
            "HxCalendarAppImm.exe", "HxAccounts.exe"
        };

        private static readonly string[] ServicesToSuspend =
        {
            "DiagTrack", "DPS", "WdiServiceHost", "WdiSystemHost", "diagnosticshub.standardcollector.service",
            "DiagSvc", "Wecsvc", "WerSvc", "PcaSvc", "SgrmBroker", "PimIndexMaintenanceSvc",
            "wuauserv", "bits", "UsoSvc", "WaaSMedicSvc", "DoSvc", "InstallService", "AppReadiness",
            "Spooler", "PrintNotify", "Fax", "stisvc", "WbioSrvc", "WPDBusEnum",
            "SCardSvr", "ScDeviceEnum", "SensrSvc", "SensorService", "SensorDataService",
            "WSearch", "MapsBroker", "TrkWks", "lfsvc", "FontCache",
            "TermService", "UmRdpService", "SessionEnv", "RemoteRegistry", "LanmanServer",
            "lmhosts", "SharedAccess", "icssvc", "lltdsvc",
            "SysMain", "dmwappushservice", "WpnService", "RetailDemo", "PhoneSvc",
            "WalletService", "CscService", "fhsvc", "EntAppSvc", "AppVClient",
            "NetTcpPortSharing", "WMPNetworkSvc", "spectrum", "SEMgrSvc", "shpamsvc",
            "TabletInputService", "MixedRealityOpenXRSvc",
            "edgeupdate", "edgeupdatem", "MicrosoftEdgeElevationService", "PushToInstall",
            "NvTelemetryContainer", "AMD Crash Defender Service", "AUEPLauncher", "cphs",
            "wmiApSrv", "pla", "PerfHost", "WFDSConMgrSvc", "CDPSvc", "WwanSvc", "wlpasvc",
            "DusmSvc", "autotimesvc", "PolicyAgent", "IKEEXT", "p2pimsvc", "WebClient", "SCPolicySvc",
            "CertPropSvc", "AssignedAccessManagerSvc", "LxpSvc", "WarpJITSvc", "TroubleshootingSvc",
            "workfolderssvc", "dot3svc", "DevQueryBroker", "AppMgmt", "vmicvmsession", "vmictimesync",
            "vmicshutdown", "vmicrdv", "vmickvpexchange", "vmicheartbeat", "vmicguestinterface", "HvHost", "vmicvss",
            "iphlpsvc", "W32Time", "jhi_service", "LMS", "Beep", "msiserver", "igccservice", "cplspcon",
            "esifsvc", "ibtsiva", "DSAService", "DSAUpdateService", "igfxCUIService2.0.0.0", "RstMwService",
            "iaStorAfsService", "SSDPSRV", "upnphost", "camsvc", "XTU3SERVICE", "Dptf", "esif_vsec",
            "AMDACP", "AMD Link Hub", "SecurityHealthService", "DisplayEnhancementService", "diagsvc",
            "DialogBlockingService", "WinHttpAutoProxySvc", "DeviceAssociationService", "LanmanWorkstation"
        };

        private static readonly string[] TasksToSuspend =
        {
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            @"\Microsoft\Windows\Application Experience\StartupAppTask",
            @"\Microsoft\Windows\Application Experience\PcaPatchDbTask",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Uploader",
            @"\Microsoft\Windows\Defrag\ScheduledDefrag",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticResolver",
            @"\Microsoft\Windows\Maintenance\WinSAT",
            @"\Microsoft\Windows\TaskScheduler\Idle Maintenance",
            @"\Microsoft\Windows\TaskScheduler\Maintenance Configurator",
            @"\Microsoft\Windows\WindowsUpdate\Scheduled Start",
            @"\Microsoft\Windows\UpdateOrchestrator\Schedule Scan",
            @"\Microsoft\Windows\Maps\MapsUpdateTask",
            @"\Microsoft\Windows\Maps\MapsToastTask",
            @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataReporting",
            @"\Microsoft\Windows\Application Experience\AitAgent",
            @"\Microsoft\Windows\Feedback\Siuf\DmClient",
            @"\Microsoft\Windows\DiskFootprint\Diagnostics"
        };

        private static readonly HashSet<string> GamingWhitelist = new(StringComparer.OrdinalIgnoreCase)
        {
            // Launchers
            "steam", "steamwebhelper", "epicgameslauncher", "upc", "uplay", "origin", "eadesktop",
            "eabackgroundservice", "battle.net", "agent", "riotclientux", "gog galaxy",
            // Anti-Cheats
            "vgtray", "vgc", "easyanticheat", "beservice", "anticheatinc",
            // Communication & Streaming
            "discord", "ts3client_win64", "obs32", "obs64", "twitch",
            // Drivers & Peripherals
            "nvcontainer", "nvdisplay.container", "nvidia share", "nvidia web helper", "nvyun",
            "radeonsoftware", "amddvr", "lghub", "lghub_agent", "icue", "razer synapse", "rzsynapse"
        };

        private class BackupStateModel
        {
            public Dictionary<string, int>? ServiceStates { get; set; }
            public List<string>? TasksDisabled { get; set; }
            public Dictionary<string, int>? RegistryValues { get; set; }
            public Dictionary<string, string>? StringRegistryValues { get; set; }
            public string? PowerPlanGuid { get; set; }
            public Dictionary<string, int>? GpuRegistryValues { get; set; }
        }

        
        public static async Task<bool> ToggleGamingModeAsync(bool enable, IProgress<string>? progress = null)
        {
            try
            {
                #region Test Mode

                if (IsTestModeEnabled)
                {
                    progress?.Report($"[TEST MODE] {(enable ? "Initializing" : "Deactivating")} Gaming Mode...");
                    await Task.Delay(800);

                    progress?.Report($"[TEST MODE] {(enable ? "Simulating system optimizations..." : "Simulating configuration restoration...")}");
                    await Task.Delay(1200);

                    progress?.Report($"[TEST MODE] Generating fake logs...");
                    await Task.Delay(800);

                    progress?.Report($"[TEST MODE] {(enable ? "Engine Ready" : "System Restored")}");

                    IsGamingModeActive = enable;
                    return true;
                }

                #endregion

                if (enable)
                {
                    await EnableGamingModeAsync(progress);
                    IsGamingModeActive = true;
                }
                else
                {
                    await DisableGamingModeAsync(progress);
                    IsGamingModeActive = false;
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception($"[GamingMode] Error: {ex.Message}", ex));
                progress?.Report("Error: " + ex.Message);
                return false;
            }
        }
        

        private static async Task EnableGamingModeAsync(IProgress<string>? progress)
        {
            progress?.Report(ResourceString.GetString("gm_progress_snapshot"));
            CaptureSystemState();

            StringBuilder cmd1 = new StringBuilder("/c ");
            progress?.Report(ResourceString.GetString("gm_progress_power_plan"));
            cmd1.Append("powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c & ");
            cmd1.Append("powercfg -change -disk-timeout-ac 0 & ");
            cmd1.Append("powercfg -change -disk-timeout-dc 0 & ");

            cmd1.Append("powercfg -change -standby-timeout-ac 0 & ");
            cmd1.Append("powercfg -change -standby-timeout-dc 0 & ");
            cmd1.Append("powercfg -setacvalueindex scheme_current SUB_PCIEXPRESS ASPM 0 & ");
            cmd1.Append("powercfg -setdcvalueindex scheme_current SUB_PCIEXPRESS ASPM 0 & ");
            cmd1.Append("powercfg -setactive scheme_current & ");

            foreach (var proc in ProcessesToKill)
            {
                progress?.Report($"{ResourceString.GetString("gm_progress_terminating")}: {proc}");
                cmd1.Append($"taskkill /f /im {proc} >nul 2>&1 & ");
            }
            await CommandExecutor.RunCommand(cmd1.ToString(), isPowerShell: false);

            progress?.Report(ResourceString.GetString("gm_progress_cleaning_apps"));
            await Task.Run(() => SmartKillNonEssentialApps());

            progress?.Report(ResourceString.GetString("gm_progress_gpu"));
            SetGpuMaxPerformance();

            SaveStateToBackupFile();

            progress?.Report(ResourceString.GetString("gm_progress_cpu"));
            _ = Task.Run(() => OptimizeGamingProcessCores());

            StringBuilder cmd2 = new StringBuilder("/c ");
            foreach (var service in ServicesToSuspend)
            {
                progress?.Report($"{ResourceString.GetString("gm_progress_suspending_svc")}: {service}");
                cmd2.Append($"sc config \"{service}\" start= disabled >nul 2>&1 & ");
                cmd2.Append($"net stop \"{service}\" /y >nul 2>&1 & ");
            }
            await CommandExecutor.RunCommand(cmd2.ToString(), isPowerShell: false);

            StringBuilder cmd3 = new StringBuilder("/c ");

            foreach (var task in _tasksDisabledByUs)
            {
                progress?.Report($"{ResourceString.GetString("gm_progress_disabling_task")}: {task}");
                cmd3.Append($"schtasks /change /tn \"{task}\" /disable >nul 2>&1 & ");
            }

            progress?.Report(ResourceString.GetString("gm_progress_registry"));
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"" /v ""NetworkThrottlingIndex"" /t REG_DWORD /d 4294967295 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"" /v ""SystemResponsiveness"" /t REG_DWORD /d 0 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl"" /v ""Win32PrioritySeparation"" /t REG_DWORD /d 38 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\System\GameConfigStore"" /v ""GameDVR_Enabled"" /t REG_DWORD /d 0 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR"" /v ""AllowGameDVR"" /t REG_DWORD /d 0 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Microsoft\Windows\DWM"" /v ""DisableProcessWindowsGhosting"" /t REG_DWORD /d 1 /f >nul 2>&1 & ");

            cmd3.Append(@"reg add ""HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"" /v ""GlobalUserDisabled"" /t REG_DWORD /d 1 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Search"" /v ""BackgroundAppGlobalToggle"" /t REG_DWORD /d 0 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy"" /v ""LetAppsRunInBackground"" /t REG_DWORD /d 2 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling"" /v ""PowerThrottlingOff"" /t REG_DWORD /d 1 /f >nul 2>&1 & ");

            cmd3.Append(@"reg add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v ""EnableTransparency"" /t REG_DWORD /d 0 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"" /v ""VisualFXSetting"" /t REG_DWORD /d 2 /f >nul 2>&1 & ");

            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games"" /v ""GPU Priority"" /t REG_DWORD /d 8 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games"" /v ""Priority"" /t REG_DWORD /d 6 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games"" /v ""Scheduling Category"" /t REG_SZ /d ""High"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games"" /v ""SFIO Priority"" /t REG_SZ /d ""High"" /f >nul 2>&1 & ");

            cmd3.Append(@"reg add ""HKLM\SYSTEM\CurrentControlSet\Services\kbdclass\Parameters"" /v ""KeyboardDataQueueSize"" /t REG_DWORD /d 16 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SYSTEM\CurrentControlSet\Services\mouclass\Parameters"" /v ""MouseDataQueueSize"" /t REG_DWORD /d 16 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Mouse"" /v ""MouseSpeed"" /t REG_SZ /d ""0"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Mouse"" /v ""MouseThreshold1"" /t REG_SZ /d ""0"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Mouse"" /v ""MouseThreshold2"" /t REG_SZ /d ""0"" /f >nul 2>&1 & ");

            cmd3.Append(@"reg add ""HKCU\Control Panel\Accessibility\StickyKeys"" /v ""Flags"" /t REG_SZ /d ""506"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Accessibility\Keyboard Response"" /v ""Flags"" /t REG_SZ /d ""122"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Accessibility\ToggleKeys"" /v ""Flags"" /t REG_SZ /d ""58"" /f >nul 2>&1 & ");

            cmd3.Append(@"reg add ""HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces"" /v ""TcpAckFrequency"" /t REG_DWORD /d 1 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces"" /v ""TCPNoDelay"" /t REG_DWORD /d 1 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Kernel"" /v ""GlobalTimerResolutionRequests"" /t REG_DWORD /d 1 /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Desktop"" /v ""AutoEndTasks"" /t REG_SZ /d ""1"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Desktop"" /v ""HungAppTimeout"" /t REG_SZ /d ""1000"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Desktop"" /v ""WaitToKillAppTimeout"" /t REG_SZ /d ""2000"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Desktop"" /v ""MenuShowDelay"" /t REG_SZ /d ""0"" /f >nul 2>&1 & ");
            cmd3.Append(@"reg add ""HKCU\Control Panel\Desktop"" /v ""ForegroundLockTimeout"" /t REG_DWORD /d 0 /f >nul 2>&1 & ");

            string[] eventLogs = { "Application", "Security", "Setup", "System" };
            foreach (var log in eventLogs) cmd3.Append($"wevtutil cl {log} >nul 2>&1 & ");

            cmd3.Append("netsh int tcp set global timestamps=disabled >nul 2>&1 & ");
            cmd3.Append("netsh int tcp set heuristics disabled >nul 2>&1 & ");

            progress?.Report(ResourceString.GetString("gm_progress_network_routing"));
            cmd3.Append("ipconfig /flushdns >nul 2>&1 & ");
            cmd3.Append("arp -d * >nul 2>&1 & ");

            await CommandExecutor.RunCommand(cmd3.ToString(), isPowerShell: false);

            progress?.Report(ResourceString.GetString("gm_progress_memory"));
            await ClearingMemory.StartMemoryCleanup(
                clearRamCache: true,
                optimizeWorkingSet: true,
                shouldRemoveWinOld: false,
                shouldFlushDns: false
            );
        }

        private static async Task DisableGamingModeAsync(IProgress<string>? progress)
        {
            if (_originalRegistryValues.Count == 0 && File.Exists(BackupFilePath))
            {
                progress?.Report("Recovering state from crash backup...");
                LoadStateFromBackupFile();
            }

            progress?.Report(ResourceString.GetString("gm_progress_restoring_config"));
            StringBuilder cmd1 = new StringBuilder("/c ");

            if (!string.IsNullOrEmpty(_originalPowerPlanGuid))
            {
                cmd1.Append($"powercfg -setactive {_originalPowerPlanGuid} & ");
            }

            foreach (var kvp in _originalServiceStates)
            {
                string service = kvp.Key;
                progress?.Report($"{ResourceString.GetString("gm_progress_restoring_svc")}: {service}");
                int originalStartType = kvp.Value;

                string startStr = originalStartType switch
                {
                    2 => "auto",
                    3 => "demand",
                    4 => "disabled",
                    _ => "demand"
                };

                cmd1.Append($"sc config \"{service}\" start= {startStr} >nul 2>&1 & ");

                if (originalStartType != 4)
                {
                    cmd1.Append($"net start \"{service}\" >nul 2>&1 & ");
                }
            }
            await CommandExecutor.RunCommand(cmd1.ToString(), isPowerShell: false);

            StringBuilder cmd2 = new StringBuilder("/c ");

            foreach (var task in _tasksDisabledByUs)
            {
                progress?.Report($"{ResourceString.GetString("gm_progress_re-enabling_task")}: {task}");
                cmd2.Append($"schtasks /change /tn \"{task}\" /enable >nul 2>&1 & ");
            }

            progress?.Report(ResourceString.GetString("gm_progress_reverting_registry"));

            Action<string, string, string, string, string> addReg = (path, name, type, dicKey, dictType) =>
            {
                if (dictType == "int" && _originalRegistryValues.TryGetValue(dicKey, out int valInt))
                    cmd2.Append($@"reg add ""{path}"" /v ""{name}"" /t {type} /d {valInt} /f >nul 2>&1 & ");
                else if (dictType == "string" && _originalStringRegistryValues.TryGetValue(dicKey, out string? valStr))
                    cmd2.Append($@"reg add ""{path}"" /v ""{name}"" /t {type} /d ""{valStr}"" /f >nul 2>&1 & ");
            };

            addReg(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", "REG_DWORD", "NetworkThrottlingIndex", "int");
            addReg(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", "REG_DWORD", "SystemResponsiveness", "int");
            addReg(@"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", "REG_DWORD", "Win32PrioritySeparation", "int");
            addReg(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", "REG_DWORD", "GameDVR_Enabled", "int");
            addReg(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", "REG_DWORD", "AllowGameDVR", "int");
            addReg(@"HKLM\SOFTWARE\Microsoft\Windows\DWM", "DisableProcessWindowsGhosting", "REG_DWORD", "DisableProcessWindowsGhosting", "int");

            addReg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", "REG_DWORD", "GlobalUserDisabled", "int");
            addReg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", "REG_DWORD", "BackgroundAppGlobalToggle", "int");
            addReg(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", "REG_DWORD", "LetAppsRunInBackground", "int");
            addReg(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", "REG_DWORD", "PowerThrottlingOff", "int");

            addReg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", "REG_DWORD", "EnableTransparency", "int");
            addReg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", "REG_DWORD", "VisualFXSetting", "int");

            addReg(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", "REG_DWORD", "MMCSS_GpuPriority", "int");
            addReg(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", "REG_DWORD", "MMCSS_Priority", "int");
            addReg(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "REG_SZ", "MMCSS_SchedulingCategory", "string");
            addReg(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", "REG_SZ", "MMCSS_SFIOPriority", "string");

            addReg(@"HKLM\SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", "KeyboardDataQueueSize", "REG_DWORD", "KeyboardDataQueueSize", "int");
            addReg(@"HKLM\SYSTEM\CurrentControlSet\Services\mouclass\Parameters", "MouseDataQueueSize", "REG_DWORD", "MouseDataQueueSize", "int");
            addReg(@"HKCU\Control Panel\Mouse", "MouseSpeed", "REG_SZ", "MouseSpeed", "string");
            addReg(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "REG_SZ", "MouseThreshold1", "string");
            addReg(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "REG_SZ", "MouseThreshold2", "string");

            addReg(@"HKCU\Control Panel\Accessibility\StickyKeys", "Flags", "REG_SZ", "StickyKeys_Flags", "string");
            addReg(@"HKCU\Control Panel\Accessibility\Keyboard Response", "Flags", "REG_SZ", "FilterKeys_Flags", "string");
            addReg(@"HKCU\Control Panel\Accessibility\ToggleKeys", "Flags", "REG_SZ", "ToggleKeys_Flags", "string");

            addReg(@"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", "TcpAckFrequency", "REG_DWORD", "TcpAckFrequency", "int");
            addReg(@"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", "TCPNoDelay", "REG_DWORD", "TCPNoDelay", "int");
            addReg(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Kernel", "GlobalTimerResolutionRequests", "REG_DWORD", "GlobalTimerResolutionRequests", "int");
            addReg(@"HKCU\Control Panel\Desktop", "AutoEndTasks", "REG_SZ", "AutoEndTasks", "string");
            addReg(@"HKCU\Control Panel\Desktop", "HungAppTimeout", "REG_SZ", "HungAppTimeout", "string");
            addReg(@"HKCU\Control Panel\Desktop", "WaitToKillAppTimeout", "REG_SZ", "WaitToKillAppTimeout", "string");
            addReg(@"HKCU\Control Panel\Desktop", "MenuShowDelay", "REG_SZ", "MenuShowDelay", "string");
            addReg(@"HKCU\Control Panel\Desktop", "ForegroundLockTimeout", "REG_DWORD", "ForegroundLockTimeout", "int");

            cmd2.Append("netsh int tcp set global timestamps=enabled >nul 2>&1 & ");
            cmd2.Append("netsh int tcp set heuristics default >nul 2>&1 & ");

            await CommandExecutor.RunCommand(cmd2.ToString(), isPowerShell: false);

            progress?.Report(ResourceString.GetString("gm_progress_restoring_gpu"));
            RestoreGpuPowerStates();

            progress?.Report(ResourceString.GetString("gm_progress_reverting_cpu"));
            _ = Task.Run(() => RestoreGamingProcessCores());

            _originalServiceStates.Clear();
            _tasksDisabledByUs.Clear();
            _originalRegistryValues.Clear();
            _originalStringRegistryValues.Clear();
            _originalPowerPlanGuid = string.Empty;

            if (File.Exists(BackupFilePath))
            {
                File.Delete(BackupFilePath);
            }
        }

        private static void SmartKillNonEssentialApps()
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id == currentProcessId) continue;

                    if (proc.SessionId == 0) continue;

                    if (GamingWhitelist.Contains(proc.ProcessName)) continue;

                    string? exePath = proc.MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath)) continue;

                    if (exePath.StartsWith(windowsDirectory, StringComparison.OrdinalIgnoreCase)) continue;

                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrEmpty(versionInfo.CompanyName) &&
                        versionInfo.CompanyName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Debug.WriteLine($"[GamingMode] Terminating 3rd Party App: {proc.ProcessName}");
                    proc.Kill();
                }
                catch { /* Access Denied (Protected process) or already exited - skip safely */ }
            }
        }

        private static void SetGpuMaxPerformance()
        {
            try
            {
                string displayClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using var displayClassKey = Registry.LocalMachine.OpenSubKey(displayClassPath, writable: true);
                if (displayClassKey == null) return;

                foreach (var subKeyName in displayClassKey.GetSubKeyNames())
                {
                    if (subKeyName.Length != 4) continue;

                    using var gpuKey = displayClassKey.OpenSubKey(subKeyName, writable: true);
                    if (gpuKey == null) continue;

                    string provider = gpuKey.GetValue("ProviderName")?.ToString() ?? "";
                    string fullPath = $@"{displayClassPath}\{subKeyName}";

                    if (provider.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    {
                        var origVal = gpuKey.GetValue("PreferredPerformanceMode");
                        if (origVal != null)
                        {
                            _originalGpuRegistryValues[$@"{fullPath}\PreferredPerformanceMode"] = Convert.ToInt32(origVal);
                        }
                        else
                        {
                            _originalGpuRegistryValues[$@"{fullPath}\PreferredPerformanceMode"] = 3;
                        }

                        gpuKey.SetValue("PreferredPerformanceMode", 1, RegistryValueKind.DWord);
                        Debug.WriteLine($"[GamingMode] NVIDIA GPU set to Maximum Performance on {subKeyName}");
                    }
                    else if (provider.Contains("AMD", StringComparison.OrdinalIgnoreCase) || provider.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase))
                    {
                        var origVal = gpuKey.GetValue("EnableUlps");
                        if (origVal != null)
                        {
                            _originalGpuRegistryValues[$@"{fullPath}\EnableUlps"] = Convert.ToInt32(origVal);
                        }
                        else
                        {
                            _originalGpuRegistryValues[$@"{fullPath}\EnableUlps"] = 1;
                        }

                        gpuKey.SetValue("EnableUlps", 0, RegistryValueKind.DWord);
                        Debug.WriteLine($"[GamingMode] AMD GPU ULPS Disabled on {subKeyName}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception($"[GamingMode] GPU Power Tweak Error: {ex.Message}", ex));
            }
        }

        private static void RestoreGpuPowerStates()
        {
            foreach (var kvp in _originalGpuRegistryValues)
            {
                try
                {
                    string fullPath = kvp.Key;
                    int lastSlash = fullPath.LastIndexOf('\\');
                    if (lastSlash == -1) continue;

                    string keyPath = fullPath.Substring(0, lastSlash);
                    string valueName = fullPath.Substring(lastSlash + 1);

                    using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                    if (key != null)
                    {
                        key.SetValue(valueName, kvp.Value, RegistryValueKind.DWord);
                    }
                }
                catch { }
            }
            _originalGpuRegistryValues.Clear();
        }

        private static void OptimizeGamingProcessCores()
        {
            try
            {
                Task.Delay(3000).Wait();

                foreach (var proc in Process.GetProcesses())
                {
                    if (GamingWhitelist.Contains(proc.ProcessName))
                    {
                        try
                        {
                            if (proc.PriorityClass != ProcessPriorityClass.High)
                            {
                                proc.PriorityClass = ProcessPriorityClass.High;
                                Debug.WriteLine($"[GamingMode] Elevated Priority for {proc.ProcessName}");
                            }

                            long allCoresMask = (1L << Environment.ProcessorCount) - 1;
                            if (proc.ProcessorAffinity.ToInt64() != allCoresMask)
                            {
                                proc.ProcessorAffinity = (IntPtr)allCoresMask;
                                Debug.WriteLine($"[GamingMode] Unlocked full CPU Affinity for {proc.ProcessName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[GamingMode] Could not optimize CPU for {proc.ProcessName}: {ex.Message}");
                        }
                    }
                }
            }
            catch { }
        }

        private static void RestoreGamingProcessCores()
        {
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    if (GamingWhitelist.Contains(proc.ProcessName))
                    {
                        try
                        {
                            if (proc.PriorityClass == ProcessPriorityClass.High)
                            {
                                proc.PriorityClass = ProcessPriorityClass.Normal;
                                Debug.WriteLine($"[GamingMode] Restored Priority for {proc.ProcessName}");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void CaptureSystemState()
        {
            _originalServiceStates.Clear();
            _tasksDisabledByUs.Clear();
            _originalRegistryValues.Clear();
            _originalStringRegistryValues.Clear();
            _originalGpuRegistryValues.Clear();

            _originalPowerPlanGuid = GetActivePowerPlan();

            foreach (var service in ServicesToSuspend)
            {
                _originalServiceStates[service] = GetRegistryValue($@"SYSTEM\CurrentControlSet\Services\{service}", "Start", 3);
            }

            _originalRegistryValues["NetworkThrottlingIndex"] = GetRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 10);
            _originalRegistryValues["SystemResponsiveness"] = GetRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 20);
            _originalRegistryValues["Win32PrioritySeparation"] = GetRegistryValue(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 2);
            _originalRegistryValues["AllowGameDVR"] = GetRegistryValue(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 1);
            _originalRegistryValues["DisableProcessWindowsGhosting"] = GetRegistryValue(@"SOFTWARE\Microsoft\Windows\DWM", "DisableProcessWindowsGhosting", 0);
            _originalRegistryValues["GameDVR_Enabled"] = GetRegistryValue(@"System\GameConfigStore", "GameDVR_Enabled", 1, isHKCU: true);

            _originalRegistryValues["GlobalUserDisabled"] = GetRegistryValue(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0, isHKCU: true);
            _originalRegistryValues["BackgroundAppGlobalToggle"] = GetRegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 1, isHKCU: true);
            _originalRegistryValues["LetAppsRunInBackground"] = GetRegistryValue(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 0);
            _originalRegistryValues["PowerThrottlingOff"] = GetRegistryValue(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 0);

            _originalRegistryValues["EnableTransparency"] = GetRegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1, isHKCU: true);
            _originalRegistryValues["VisualFXSetting"] = GetRegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, isHKCU: true);

            _originalRegistryValues["MMCSS_GpuPriority"] = GetRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8);
            _originalRegistryValues["MMCSS_Priority"] = GetRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 2);
            _originalStringRegistryValues["MMCSS_SchedulingCategory"] = GetStringRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "Medium");
            _originalStringRegistryValues["MMCSS_SFIOPriority"] = GetStringRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", "Normal");

            _originalRegistryValues["KeyboardDataQueueSize"] = GetRegistryValue(@"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", "KeyboardDataQueueSize", 100);
            _originalRegistryValues["MouseDataQueueSize"] = GetRegistryValue(@"SYSTEM\CurrentControlSet\Services\mouclass\Parameters", "MouseDataQueueSize", 100);

            _originalStringRegistryValues["StickyKeys_Flags"] = GetStringRegistryValue(@"Control Panel\Accessibility\StickyKeys", "Flags", "510", isHKCU: true);
            _originalStringRegistryValues["FilterKeys_Flags"] = GetStringRegistryValue(@"Control Panel\Accessibility\Keyboard Response", "Flags", "126", isHKCU: true);
            _originalStringRegistryValues["ToggleKeys_Flags"] = GetStringRegistryValue(@"Control Panel\Accessibility\ToggleKeys", "Flags", "62", isHKCU: true);

            _originalStringRegistryValues["MouseSpeed"] = GetStringRegistryValue(@"Control Panel\Mouse", "MouseSpeed", "1", isHKCU: true);
            _originalStringRegistryValues["MouseThreshold1"] = GetStringRegistryValue(@"Control Panel\Mouse", "MouseThreshold1", "6", isHKCU: true);
            _originalStringRegistryValues["MouseThreshold2"] = GetStringRegistryValue(@"Control Panel\Mouse", "MouseThreshold2", "10", isHKCU: true);

            _originalRegistryValues["TcpAckFrequency"] = GetRegistryValue(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", "TcpAckFrequency", 0);
            _originalRegistryValues["TCPNoDelay"] = GetRegistryValue(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", "TCPNoDelay", 0);
            _originalRegistryValues["GlobalTimerResolutionRequests"] = GetRegistryValue(@"SYSTEM\CurrentControlSet\Control\Session Manager\Kernel", "GlobalTimerResolutionRequests", 0);
            _originalStringRegistryValues["AutoEndTasks"] = GetStringRegistryValue(@"Control Panel\Desktop", "AutoEndTasks", "0", isHKCU: true);
            _originalStringRegistryValues["HungAppTimeout"] = GetStringRegistryValue(@"Control Panel\Desktop", "HungAppTimeout", "5000", isHKCU: true);
            _originalStringRegistryValues["WaitToKillAppTimeout"] = GetStringRegistryValue(@"Control Panel\Desktop", "WaitToKillAppTimeout", "5000", isHKCU: true);
            _originalStringRegistryValues["MenuShowDelay"] = GetStringRegistryValue(@"Control Panel\Desktop", "MenuShowDelay", "400", isHKCU: true);
            _originalRegistryValues["ForegroundLockTimeout"] = GetRegistryValue(@"Control Panel\Desktop", "ForegroundLockTimeout", 200000, isHKCU: true);

            foreach (var task in TasksToSuspend)
            {
                if (IsTaskEnabled(task))
                {
                    _tasksDisabledByUs.Add(task);
                }
            }
        }

        private static void SaveStateToBackupFile()
        {
            try
            {
                var backup = new BackupStateModel
                {
                    ServiceStates = _originalServiceStates,
                    TasksDisabled = _tasksDisabledByUs,
                    RegistryValues = _originalRegistryValues,
                    StringRegistryValues = _originalStringRegistryValues,
                    PowerPlanGuid = _originalPowerPlanGuid,
                    GpuRegistryValues = _originalGpuRegistryValues
                };

                string directory = Path.GetDirectoryName(BackupFilePath)!;
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(backup);
                File.WriteAllText(BackupFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GamingMode Backup] Failed to save state to disk: {ex.Message}");
            }
        }

        private static void LoadStateFromBackupFile()
        {
            try
            {
                string json = File.ReadAllText(BackupFilePath);
                var backup = JsonSerializer.Deserialize<BackupStateModel>(json);

                if (backup != null)
                {
                    _originalServiceStates = backup.ServiceStates ?? new();
                    _tasksDisabledByUs = backup.TasksDisabled ?? new();
                    _originalRegistryValues = backup.RegistryValues ?? new();
                    _originalStringRegistryValues = backup.StringRegistryValues ?? new();
                    _originalPowerPlanGuid = backup.PowerPlanGuid ?? "381b4222-f694-41f0-9685-ff5bb260df2e";
                    _originalGpuRegistryValues = backup.GpuRegistryValues ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GamingMode Backup] Failed to load state from disk: {ex.Message}");
            }
        }

        private static string GetActivePowerPlan()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "-getactivescheme",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                string output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit();

                var match = Regex.Match(output, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                if (match.Success) return match.Value;
            }
            catch { }

            return "381b4222-f694-41f0-9685-ff5bb260df2e";
        }

        private static int GetRegistryValue(string keyPath, string valueName, int defaultValue, bool isHKCU = false)
        {
            try
            {
                using (var baseKey = isHKCU ? Registry.CurrentUser : Registry.LocalMachine)
                {
                    using (var key = baseKey.OpenSubKey(keyPath))
                    {
                        if (key != null)
                        {
                            var val = key.GetValue(valueName);
                            if (val != null) return Convert.ToInt32(val);
                        }
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        private static string GetStringRegistryValue(string keyPath, string valueName, string defaultValue, bool isHKCU = false)
        {
            try
            {
                using (var baseKey = isHKCU ? Registry.CurrentUser : Registry.LocalMachine)
                {
                    using (var key = baseKey.OpenSubKey(keyPath))
                    {
                        if (key != null)
                        {
                            var val = key.GetValue(valueName);
                            if (val != null) return val.ToString() ?? defaultValue;
                        }
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        private static bool IsTaskEnabled(string taskName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/query /tn \"{taskName}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                string output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit();

                return !output.Contains("Disabled", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}