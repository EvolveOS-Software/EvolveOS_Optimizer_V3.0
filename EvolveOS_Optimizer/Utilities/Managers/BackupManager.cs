// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Utilities.Managers;

internal static class BackupManager
{
    private static readonly string BackupFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EvolveOS",
        "Backups",
        "InitialTweakState.json");

    internal static void CreateInitialSnapshot()
    {
        if (File.Exists(BackupFilePath))
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BackupFilePath)!);

            var backup = new TweakBackupModel
            {
                System = new Dictionary<string, object>(SystemTweaks.ControlStates),
                Interface = new Dictionary<string, object>(InterfaceTweaks.ControlStates),
                Privacy = new Dictionary<string, object>(PrivacyTweaks.ControlStates),
                Services = new Dictionary<string, object>(ServicesTweaks.ControlStates)
            };

            string json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupFilePath, json);

            System.Diagnostics.Debug.WriteLine("[BackupManager] Initial system snapshot created successfully.");
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
    }

    internal static async Task RestoreInitialSnapshotAsync(CancellationToken token = default)
    {
        if (!File.Exists(BackupFilePath))
            throw new FileNotFoundException("No initial backup found to restore.");

        try
        {
            string json = await File.ReadAllTextAsync(BackupFilePath, token);
            var backup = JsonSerializer.Deserialize<TweakBackupModel>(json);

            if (backup == null) return;

            // 1. Restore System Tweaks
            var sysTweaks = new SystemTweaks();
            foreach (var item in backup.System)
            {
                if (token.IsCancellationRequested) return;

                await ApplyStateAsync(item,
                    async (k, v) => await sysTweaks.ApplyTweaks(k, v, false, token),
                    (k, v) => sysTweaks.ApplyTweaksSlider(k, v));
            }

            // 2. Restore Interface Tweaks
            var intTweaks = new InterfaceTweaks();
            foreach (var item in backup.Interface)
            {
                if (token.IsCancellationRequested) return;

                await ApplyStateAsync(item,
                    async (k, v) => await intTweaks.ApplyTweaks(k, v),
                    null);
            }

            // 3. Restore Privacy Tweaks
            var privTweaks = new PrivacyTweaks();
            foreach (var item in backup.Privacy)
            {
                if (token.IsCancellationRequested) return;

                await ApplyStateAsync(item,
                    async (k, v) => await privTweaks.ApplyTweaks(k, v, token),
                    null);
            }

            // 4. Restore Services Tweaks
            var svcTweaks = new ServicesTweaks();
            foreach (var item in backup.Services)
            {
                if (token.IsCancellationRequested) return;

                await ApplyStateAsync(item,
                    async (k, v) => await svcTweaks.ApplyTweaks(k, v, token),
                    null);
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            throw;
        }
    }

    internal static async Task RestoreToFactoryDefaultsAsync(CancellationToken token = default)
    {
        try
        {
            // 1. Force System Tweaks to Windows Defaults
            var sysTweaks = new SystemTweaks();
            sysTweaks.AnalyzeAndUpdate();

            var sysKeys = new List<string>(SystemTweaks.ControlStates.Keys);
            foreach (var key in sysKeys)
            {
                if (token.IsCancellationRequested) return;

                if (key.StartsWith("TglButton"))
                {
                    await sysTweaks.ApplyTweaks(key, false, canShowWindow: false, token);
                }
                else if (key == "Slider1") sysTweaks.ApplyTweaksSlider(key, 10); // Windows Default Mouse Sens
                else if (key == "Slider2") sysTweaks.ApplyTweaksSlider(key, 1);  // Windows Default Keyboard Delay
                else if (key == "Slider3") sysTweaks.ApplyTweaksSlider(key, 31); // Windows Default Keyboard Speed
            }

            // 2. Force Interface Tweaks to Windows Defaults
            var intTweaks = new InterfaceTweaks();
            intTweaks.AnalyzeAndUpdate();
            var intKeys = new List<string>(InterfaceTweaks.ControlStates.Keys);
            foreach (var key in intKeys)
            {
                if (token.IsCancellationRequested) return;
                if (key.StartsWith("TglButton"))
                    await intTweaks.ApplyTweaks(key, false);
            }

            // 3. Force Privacy Tweaks to Windows Defaults
            var privTweaks = new PrivacyTweaks();
            privTweaks.AnalyzeAndUpdate();
            var privKeys = new List<string>(PrivacyTweaks.ControlStates.Keys);
            foreach (var key in privKeys)
            {
                if (token.IsCancellationRequested) return;
                if (key.StartsWith("TglButton"))
                    await privTweaks.ApplyTweaks(key, false, token);
            }

            // 4. Force Services Tweaks to Windows Defaults
            var svcTweaks = new ServicesTweaks();
            svcTweaks.AnalyzeAndUpdate();
            var svcKeys = new List<string>(ServicesTweaks.ControlStates.Keys);
            foreach (var key in svcKeys)
            {
                if (token.IsCancellationRequested) return;
                if (key.StartsWith("TglButton"))
                    await svcTweaks.ApplyTweaks(key, false, token);
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            throw;
        }
    }

    private static async Task ApplyStateAsync(
        KeyValuePair<string, object> item,
        Func<string, bool, Task> applyBool,
        Action<string, uint>? applyUint)
    {
        if (item.Value is JsonElement element)
        {
            // Handle Toggles (Booleans)
            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
            {
                bool originalState = element.GetBoolean();
                await applyBool(item.Key, originalState);
            }
            // Handle Sliders (Numbers)
            else if (element.ValueKind == JsonValueKind.Number && applyUint != null)
            {
                uint originalValue = element.GetUInt32();
                applyUint(item.Key, originalValue);
            }
        }
    }
}