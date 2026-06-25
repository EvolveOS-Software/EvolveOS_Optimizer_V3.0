// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.Win32;
using System.Management;

namespace EvolveOS_Optimizer.Utilities.Services;

public class RegistryMonitorService
{
    public static RegistryMonitorService Instance { get; } = new RegistryMonitorService();

    private const string TargetKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings";

    private int _desiredState = 1;
    private int _desiredInterval = 1440; 
    private int _desiredRetention = 4320;

    private ManagementEventWatcher? _watcher;
    private bool _isHardLockEnabled = false;

    private RegistryMonitorService() { }

    public void InitializeDesiredValues(bool isLocked, int state, int interval, int retention)
    {
        _isHardLockEnabled = isLocked;
        _desiredState = state;
        _desiredInterval = interval;
        _desiredRetention = retention;

        if (_isHardLockEnabled)
        {
            EnsureValuesAreLocked();
        }
    }

    public void UpdateLockState(bool isLocked)
    {
        _isHardLockEnabled = isLocked;
        if (_isHardLockEnabled) EnsureValuesAreLocked();
    }

    public void UpdateDesiredValue(string valueName, int newValue)
    {
        switch (valueName)
        {
            case "Active_UX": _desiredState = newValue; break;
            case "SnapshotInterval_UX": _desiredInterval = newValue; break;
            case "MaxTimespan_UX": _desiredRetention = newValue; break;
        }

        if (_isHardLockEnabled) EnsureValuesAreLocked();
    }

    public void StartMonitoring()
    {
        if (_watcher != null) return;

        try
        {
            string query = $"SELECT * FROM RegistryKeyChangeEvent WHERE Hive='HKEY_LOCAL_MACHINE' AND KeyPath='{TargetKey.Replace(@"\", @"\\")}'";
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += (s, e) =>
            {
                if (_isHardLockEnabled) EnsureValuesAreLocked();
            };
            _watcher.Start();
        }
        catch
        {
            ErrorLogging.LogDebug("App is not running as Administrator, monitor failed to start.");
        }
    }

    private void EnsureValuesAreLocked()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(TargetKey, true);
            if (key != null)
            {
                bool fixedState = CheckAndEnforce(key, "Active_UX", _desiredState);
                bool fixedInterval = CheckAndEnforce(key, "SnapshotInterval_UX", _desiredInterval);
                bool fixedRetention = CheckAndEnforce(key, "MaxTimespan_UX", _desiredRetention);

                if (fixedState || fixedInterval || fixedRetention)
                {
                    string title = ResourceString.GetString("Notification_PITR_Protected_Title");
                    string message = ResourceString.GetString("Notification_PITR_Protected_Message");

                    if (string.IsNullOrEmpty(title)) title = "Settings Protected";
                    if (string.IsNullOrEmpty(message)) message = "Windows attempted to modify your Point-in-Time Restore preferences.\nEvolveOS has successfully blocked the change.";

                    NotificationManager.Show(title, message)
                        .WithSeverity(NotificationManager.NoticeSeverity.Success)
                        .Perform();
                }
            }
        }
        catch { /* Silently fail if locked by system */ }
    }

    private bool CheckAndEnforce(RegistryKey key, string valueName, int desiredValue)
    {
        var currentValue = (int?)key.GetValue(valueName);
        if (currentValue != desiredValue)
        {
            key.SetValue(valueName, desiredValue, RegistryValueKind.DWord);
            return true;
        }
        return false; 
    }
}