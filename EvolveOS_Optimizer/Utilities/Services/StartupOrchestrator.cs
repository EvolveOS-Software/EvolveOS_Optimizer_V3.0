// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.EventHandlers;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services;

public class StartupOrchestrator : IStartupOrchestrator
{
    private readonly ICompatibleSettingsRegistry _settingsRegistry;
    private readonly IGlobalSettingsPreloader _settingsPreloader;
    private readonly TooltipRefreshEventHandler _tooltipEventHandler;
    private readonly IUserPreferencesService _preferencesService;
    private readonly INewBadgeService _newBadgeService;
    private readonly ILogService _logService;

    public StartupOrchestrator(
        ICompatibleSettingsRegistry settingsRegistry,
        IGlobalSettingsPreloader settingsPreloader,
        TooltipRefreshEventHandler tooltipEventHandler,
        IUserPreferencesService preferencesService,
        INewBadgeService newBadgeService,
        ILogService logService)
    {
        _settingsRegistry = settingsRegistry;
        _settingsPreloader = settingsPreloader;
        _tooltipEventHandler = tooltipEventHandler;
        _preferencesService = preferencesService;
        _newBadgeService = newBadgeService;
        _logService = logService;
    }

    public async Task<StartupResult> RunStartupSequenceAsync(
        IProgress<string> statusProgress,
        IProgress<TaskProgressDetail> detailedProgress)
    {
        bool isFirstLaunch = false;

        statusProgress.Report("Loading_InitializingSettings");
        try
        {
            await _settingsRegistry.InitializeAsync().ConfigureAwait(false);
            await _settingsPreloader.PreloadAllSettingsAsync().ConfigureAwait(false);

            try
            {
                var allAddedInVersions = CollectAddedInVersions(_settingsRegistry);
                _newBadgeService.Initialize(allAddedInVersions);
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"New badge service init failed: {ex.Message}");
            }

            _ = _tooltipEventHandler;

            RegeditIconProvider.GetIconAsync().FireAndForget(_logService);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to initialize settings registry: {ex.Message}");
        }

        return new StartupResult { IsFirstLaunch = isFirstLaunch };
    }

    private static IEnumerable<string?> CollectAddedInVersions(ICompatibleSettingsRegistry registry)
    {
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>>? filtered = null;
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>>? bypassed = null;

        try { filtered = registry.GetAllFilteredSettings(); } catch { /* registry not ready */ }
        try { bypassed = registry.GetAllBypassedSettings(); } catch { /* registry not ready */ }

        var results = new List<string?>();
        if (filtered is not null)
        {
            foreach (var kvp in filtered)
                foreach (var s in kvp.Value)
                    results.Add(s.AddedInVersion);
        }
        if (bypassed is not null)
        {
            foreach (var kvp in bypassed)
                foreach (var s in kvp.Value)
                    results.Add(s.AddedInVersion);
        }
        return results;
    }
}
