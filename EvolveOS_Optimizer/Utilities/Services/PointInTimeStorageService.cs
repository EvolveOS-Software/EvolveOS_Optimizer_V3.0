// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

public class PointInTimeStorageService : ISpecialSettingHandler
{
    private const string PitrRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings";

    public Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
    {
        if (setting.Id != "PointInTimeRestore_MaxStorage") return Task.FromResult(false);

        try
        {
            int gigabytes = 2; // Default
            if (value is JsonElement je && je.ValueKind == JsonValueKind.Number)
            {
                gigabytes = je.GetInt32();
            }
            else if (value is IConvertible)
            {
                gigabytes = Convert.ToInt32(value);
            }

            int megabytes = gigabytes * 1024;

            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var regKey = baseKey.CreateSubKey(PitrRegistryPath, true);
            if (regKey != null)
            {
                regKey.SetValue("MaxGlobalSize_UX", megabytes, RegistryValueKind.DWord);
                return Task.FromResult(true);
            }
        }
        catch { /* Ignore access errors */ }

        return Task.FromResult(false);
    }

    public Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(IEnumerable<SettingDefinition> settings)
    {
        var results = new Dictionary<string, Dictionary<string, object?>>();

        try
        {
            int megabytes = 2048;
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var regKey = baseKey.OpenSubKey(PitrRegistryPath, false);
            if (regKey?.GetValue("MaxGlobalSize_UX") is int val)
            {
                megabytes = val;
            }

            int gigabytes = megabytes / 1024;

            gigabytes = Math.Clamp(gigabytes, 2, 50);

            results["PointInTimeRestore_MaxStorage"] = new Dictionary<string, object?>
            {
                { "PointInTimeRestore_MaxStorage", gigabytes }
            };
        }
        catch { /* Ignore read errors */ }

        return Task.FromResult(results);
    }
}