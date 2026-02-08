using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.Storage
{
    internal class TaskStorage
    {
        // Data Collection Tasks
        internal static readonly string[] dataCollectTasks = {
            @"\Microsoft\Windows\Maintenance\WinSAT",
            @"\Microsoft\Windows\Autochk\Proxy",
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            @"\Microsoft\Windows\Application Experience\StartupAppTask",
            @"\Microsoft\Windows\PI\Sqm-Tasks",
            @"\Microsoft\Windows\NetTrace\GatherNetworkInfo",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            @"\Microsoft\Windows\Customer Experience Improvement Program\KernelCeipTask",
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticResolver",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"
        };

        // Telemetry Tasks
        internal static readonly string[] telemetryTasks = {
            @"\Microsoft\Office\Office ClickToRun Service Monitor",
            @"\Microsoft\Office\OfficeTelemetry\AgentFallBack2016",
            @"\Microsoft\Office\OfficeTelemetry\OfficeTelemetryAgentLogOn2016",
            @"\Microsoft\Office\OfficeTelemetryAgentFallBack2016",
            @"\Microsoft\Office\OfficeTelemetryAgentLogOn2016",
            @"\Microsoft\Office\OfficeTelemetryAgentFallBack",
            @"\Microsoft\Office\OfficeTelemetryAgentLogOn",
            @"\Microsoft\Office\Office 15 Subscription Heartbeat"
        };

        // NVIDIA Specific Tasks
        internal static readonly string[] nvidiaTasks = {
            @"\NvTmRepOnLogon_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}",
            @"\NvTmRep_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}",
            @"\NvTmMon_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}"
        };

        // Windows Update Orchestration Tasks
        internal static readonly string[] winUpdatesTasks = {
            @"\Microsoft\Windows\UpdateOrchestrator\Report policies",
            @"\Microsoft\Windows\UpdateOrchestrator\Schedule Maintenance Work",
            @"\Microsoft\Windows\UpdateOrchestrator\Schedule Scan",
            @"\Microsoft\Windows\UpdateOrchestrator\Schedule Scan Static Task",
            @"\Microsoft\Windows\UpdateOrchestrator\Schedule Wake To Work",
            @"\Microsoft\Windows\UpdateOrchestrator\Schedule Work",
            @"\Microsoft\Windows\UpdateOrchestrator\Start Oobe Expedite Work",
            @"\Microsoft\Windows\UpdateOrchestrator\StartOobeAppsScanAfterUpdate",
            @"\Microsoft\Windows\UpdateOrchestrator\StartOobeAppsScan_LicenseAccepted",
            @"\Microsoft\Windows\UpdateOrchestrator\UIEOrchestrator",
            @"\Microsoft\Windows\UpdateOrchestrator\USO_UxBroker",
            @"\Microsoft\Windows\UpdateOrchestrator\UUS Failover Task",
            @"\Microsoft\Windows\WindowsUpdate\Refresh Group Policy Cache",
            @"\Microsoft\Windows\WindowsUpdate\Scheduled Start"
        };

        // Xbox and Gaming Tasks
        internal static readonly string[] xboxTasks = {
            @"\Microsoft\XblGameSave\XblGameSaveTask",
            @"\Microsoft\XblGameSave\XblGameSaveTaskLogon",
            @"\Microsoft\Xbox\XblGameSaveTask",
            @"\Microsoft\Xbox\Maintenance\MaintenanceTask",
            @"\Microsoft\Xbox\XGamingServices\GameServicesTask"
        };

        internal static readonly string bluetoothTask = @"\Microsoft\Windows\Bluetooth\UninstallDeviceTask";

        // Maps Tasks (Referenced in UninstallingPakages)
        internal static readonly string[] mapsTasks = {
            @"\Microsoft\Windows\Maps\MapsToastTask",
            @"\Microsoft\Windows\Maps\MapsUpdateTask"
        };

        // OneDrive Tasks (Referenced in UninstallingPakages)
        internal static readonly string[] oneDriveTask = {
            @"\Microsoft\Windows\OneDrive\OneDrive Standalone Update Task",
            TaskSchedulerManager.GetTaskFullPath("OneDrive Startup")
        };

        // Edge Tasks (Referenced in UninstallingPakages)
        internal static readonly string[] edgeTasks = {
            TaskSchedulerManager.GetTaskFullPath("MicrosoftEdgeUpdateTaskMachineUA"),
            TaskSchedulerManager.GetTaskFullPath("MicrosoftEdgeUpdateTaskMachineCore"),
            TaskSchedulerManager.GetTaskFullPath("MicrosoftEdgeUpdateTaskUser")
        };

        // Windows Defender Tasks
        internal static readonly string[] winDefenderTasks = {
            @"\Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Verification",
        };

        // Memory Diagnostics
        internal static readonly string[] memoryDiagTasks = {
            @"\Microsoft\Windows\MemoryDiagnostic\ProcessMemoryDiagnosticEvents",
            @"\Microsoft\Windows\MemoryDiagnostic\RunFullMemoryDiagnostic"
        };

        // Windows Insider / Flighting Tasks
        internal static readonly string[] winInsiderTasks = {
            @"\Microsoft\Windows\Flighting\FeatureConfig\BootstrapUsageDataReporting",
            @"\Microsoft\Windows\Flighting\FeatureConfig\GovernedFeatureUsageProcessing",
            @"\Microsoft\Windows\Flighting\FeatureConfig\ReconcileConfigs",
            @"\Microsoft\Windows\Flighting\FeatureConfig\ReconcileFeatures",
            @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataFlushing",
            @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataReceiver",
            @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataReporting",
            @"\Microsoft\Windows\Flighting\OneSettings\RefreshCache",
            @"\Microsoft\Windows\Flighting\OneSettings\CollectUsageData",
            @"\Microsoft\Windows\Flighting\OneSettings\SyncConfigs",
            @"\Microsoft\Windows\Flighting\OneSettings\TrackFlighting"
        };

        internal static readonly string restoreTask = @"\Microsoft\Windows\SystemRestore\SR";

        internal static readonly string defragTask = @"\Microsoft\Windows\Defrag\ScheduledDefrag";
    }
}