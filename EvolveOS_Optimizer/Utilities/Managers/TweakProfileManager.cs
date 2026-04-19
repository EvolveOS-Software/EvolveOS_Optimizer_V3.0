// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Utilities.Managers;

internal static class TweakProfileManager
{
    public static TweakProfileBackup GenerateExportProfile()
    {
        var profile = new TweakProfileBackup();

        profile.ServicesTweaks = ExtractBools(ServicesTweaks.ControlStates);
        profile.PrivacyTweaks = ExtractBools(PrivacyTweaks.ControlStates);
        profile.SystemTweaks = ExtractBools(SystemTweaks.ControlStates);
        profile.InterfaceTweaks = ExtractBools(InterfaceTweaks.ControlStates);

        return profile;
    }

    private static Dictionary<string, bool> ExtractBools(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, bool>();
        if (source == null) return result;

        foreach (var kvp in source)
        {
            if (kvp.Value is bool b)
            {
                result[kvp.Key] = b;
            }
            else if (bool.TryParse(kvp.Value?.ToString(), out bool parsedValue))
            {
                result[kvp.Key] = parsedValue;
            }
        }
        return result;
    }

    public static async Task ApplyImportedProfileAsync(TweakProfileBackup profile)
    {
        await Task.Run(async () =>
        {
            try
            {
                // 1. Deploy Services
                if (profile.ServicesTweaks != null)
                {
                    var svcEngine = new ServicesTweaks();
                    foreach (var kvp in profile.ServicesTweaks)
                    {
                        await svcEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }

                // 2. Deploy Privacy
                if (profile.PrivacyTweaks != null)
                {
                    var privEngine = new PrivacyTweaks();
                    foreach (var kvp in profile.PrivacyTweaks)
                    {
                        await privEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }

                // 3. Deploy System
                if (profile.SystemTweaks != null)
                {
                    var sysEngine = new SystemTweaks();
                    foreach (var kvp in profile.SystemTweaks)
                    {
                        await sysEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }

                // 4. Deploy Interface
                if (profile.InterfaceTweaks != null)
                {
                    var uiEngine = new InterfaceTweaks();
                    foreach (var kvp in profile.InterfaceTweaks)
                    {
                        await uiEngine.ApplyTweaks(kvp.Key, kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"[TweakProfileManager] Import Failed: {ex.Message}");
                throw;
            }
        });
    }
}