// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ServiceProcess;
using System.Threading;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

public class ProcessRestartManager(
    IWindowsUIManagementService uiManagementService,
    ILogService logService) : IProcessRestartManager
{
    private int _suppressCount;

    public IDisposable SuppressRestarts()
    {
        Interlocked.Increment(ref _suppressCount);
        return new SuppressScope(this);
    }

    private sealed class SuppressScope(ProcessRestartManager owner) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Interlocked.Decrement(ref owner._suppressCount);
            }
        }
    }

    public async Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting)
    {
        // 1. Check if bulk operation is running (e.g. Apply Recommended)
        if (_suppressCount > 0)
        {
            if (!string.IsNullOrEmpty(setting.RestartProcess))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{setting.RestartProcess}' (restarts suppressed - parent will restart)");
            if (!string.IsNullOrEmpty(setting.RestartService))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping service restart for '{setting.RestartService}' (restarts suppressed - parent will restart)");
            return;
        }

        // 2. Perform isolated restart
        if (!string.IsNullOrEmpty(setting.RestartProcess))
            await RestartProcessByNameAsync(setting.RestartProcess, setting.Id).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(setting.RestartService))
            RestartServiceByName(setting.RestartService, setting.Id);
    }

    public async Task FlushCoalescedRestartsAsync(IEnumerable<SettingDefinition> appliedSettings)
    {
        if (appliedSettings == null) return;

        var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in appliedSettings)
        {
            if (!string.IsNullOrEmpty(s.RestartProcess)) processes.Add(s.RestartProcess);
            if (!string.IsNullOrEmpty(s.RestartService)) services.Add(s.RestartService);
        }

        if (processes.Count == 0 && services.Count == 0) return;

        logService.Log(LogLevel.Info,
            $"[ProcessRestartManager] Flushing coalesced restarts: {processes.Count} process(es), {services.Count} service(s)");

        foreach (var process in processes)
            await RestartProcessByNameAsync(process, settingIdForLog: null).ConfigureAwait(false);

        foreach (var service in services)
            RestartServiceByName(service, settingIdForLog: null);
    }

    private async Task RestartProcessByNameAsync(string processName, string? settingIdForLog)
    {
        if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            var label = settingIdForLog is null
                ? "[ProcessRestartManager] Refreshing Windows UI (coalesced)"
                : $"[ProcessRestartManager] Refreshing Windows UI for setting '{settingIdForLog}'";
            logService.Log(LogLevel.Info, label);
            await uiManagementService.RefreshWindowsGUI(killExplorer: true).ConfigureAwait(false);
            return;
        }
        else if (processName.Equals("intl", StringComparison.OrdinalIgnoreCase))
        {
            logService.Log(LogLevel.Info,
                settingIdForLog != null
                    ? $"[ProcessRestartManager] Broadcasting regional setting change for '{settingIdForLog}'"
                    : "[ProcessRestartManager] Broadcasting regional setting change (coalesced)");
            uiManagementService.BroadcastRegionalSettingChange();
        }
        else
        {
            logService.Log(LogLevel.Info,
                settingIdForLog != null
                    ? $"[ProcessRestartManager] Restarting process '{processName}' for setting '{settingIdForLog}'"
                    : $"[ProcessRestartManager] Restarting process '{processName}' (coalesced)");
            try
            {
                uiManagementService.KillProcess(processName);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart process '{processName}': {ex.Message}");
            }
        }
    }

    private void RestartServiceByName(string serviceName, string? settingIdForLog)
    {
        logService.Log(LogLevel.Info,
            settingIdForLog != null
                ? $"[ProcessRestartManager] Restarting service '{serviceName}' for setting '{settingIdForLog}'"
                : $"[ProcessRestartManager] Restarting service '{serviceName}' (coalesced)");
        try
        {
            if (serviceName.Contains("*"))
            {
                var pattern = serviceName.Replace("*", "");
                var allServices = ServiceController.GetServices();
                try
                {
                    var matchingServices = allServices.Where(s =>
                        s.ServiceName.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var svc in matchingServices)
                    {
                        try
                        {
                            if (svc.Status == ServiceControllerStatus.Running)
                            {
                                svc.Stop();
                                svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                                svc.Start();
                            }
                        }
                        catch (Exception svcEx)
                        {
                            logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart service '{svc.ServiceName}': {svcEx.Message}");
                        }
                    }
                }
                finally
                {
                    foreach (var svc in allServices)
                        svc.Dispose();
                }
            }
            else
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    sc.Start();
                }
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart service '{serviceName}': {ex.Message}");
        }
    }
}