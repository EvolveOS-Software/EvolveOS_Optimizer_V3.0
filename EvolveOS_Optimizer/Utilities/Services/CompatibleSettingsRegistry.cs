// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

public class CompatibleSettingsRegistry : ICompatibleSettingsRegistry
{
    #region Fields & Properties
    private readonly IWindowsCompatibilityFilter _windowsFilter;
    private readonly IHardwareCompatibilityFilter _hardwareFilter;
    private readonly IPowerSettingsValidationService _powerValidation;
    private readonly ILogService _logService;

    private bool _isInitialized;
    private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
    private readonly Dictionary<string, IEnumerable<SettingDefinition>> _preFilteredSettings = new();
    private readonly Dictionary<string, IEnumerable<SettingDefinition>> _windowsFilterBypassedSettings = new();
    private Dictionary<string, SettingDefinition> _filteredById = new();
    private Dictionary<string, SettingDefinition> _bypassedById = new();
    private Dictionary<string, string> _filteredSettingIdToFeatureId = new();
    private Dictionary<string, string> _bypassedSettingIdToFeatureId = new();
    private bool _filterEnabled = true;

    public bool IsInitialized => _isInitialized;
    #endregion

    #region Constructor
    public CompatibleSettingsRegistry(
        IWindowsCompatibilityFilter windowsFilter,
        IHardwareCompatibilityFilter hardwareFilter,
        IPowerSettingsValidationService powerValidation,
        ILogService logService)
    {
        _windowsFilter = windowsFilter ?? throw new ArgumentNullException(nameof(windowsFilter));
        _hardwareFilter = hardwareFilter ?? throw new ArgumentNullException(nameof(hardwareFilter));
        _powerValidation = powerValidation ?? throw new ArgumentNullException(nameof(powerValidation));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }
    #endregion

    #region Initialization
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isInitialized) return;

            _logService.Log(LogLevel.Info, "Initializing compatible settings registry with auto-discovery");

            await PreFilterAllFeatureSettingsAsync().ConfigureAwait(false);

            RebuildIdIndexes();
            _isInitialized = true;
            _logService.Log(LogLevel.Info, $"Compatible settings registry initialized with {_preFilteredSettings.Count} pre-filtered features");
        }
        finally
        {
            _initializationLock.Release();
        }
    }
    #endregion

    #region Public API (Filtering & Retrieval)
    public SettingDefinition? GetById(string settingId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized. Call InitializeAsync first.");

        ArgumentNullException.ThrowIfNull(settingId);

        var index = _filterEnabled ? _filteredById : _bypassedById;
        return index.TryGetValue(settingId, out var s) ? s : null;
    }

    public string? GetFeatureIdForSetting(string settingId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized. Call InitializeAsync first.");

        ArgumentNullException.ThrowIfNull(settingId);

        var index = _filterEnabled ? _filteredSettingIdToFeatureId : _bypassedSettingIdToFeatureId;
        return index.TryGetValue(settingId, out var f) ? f : null;
    }

    public IEnumerable<SettingDefinition> GetFilteredSettings(string featureId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized");

        _logService.Log(LogLevel.Debug, $"GetFilteredSettings for {featureId}: Filter enabled = {_filterEnabled}");

        if (_filterEnabled)
        {
            return _preFilteredSettings.TryGetValue(featureId, out var settings)
                ? settings
                : Enumerable.Empty<SettingDefinition>();
        }
        else
        {
            return _windowsFilterBypassedSettings.TryGetValue(featureId, out var settings)
                ? settings
                : Enumerable.Empty<SettingDefinition>();
        }
    }

    public void SetFilterEnabled(bool enabled)
    {
        _filterEnabled = enabled;
    }

    public IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllFilteredSettings()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized. Call InitializeAsync first.");

        return _filterEnabled ? _preFilteredSettings : _windowsFilterBypassedSettings;
    }

    public IEnumerable<SettingDefinition> GetBypassedSettings(string featureId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized");

        return _windowsFilterBypassedSettings.TryGetValue(featureId, out var settings)
            ? settings
            : Enumerable.Empty<SettingDefinition>();
    }

    public IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllBypassedSettings()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized");

        return _windowsFilterBypassedSettings;
    }
    #endregion

    #region Internal Processing & Helpers
    private void RebuildIdIndexes()
    {
        _filteredById = new Dictionary<string, SettingDefinition>();
        _filteredSettingIdToFeatureId = new Dictionary<string, string>();
        foreach (var (featureId, settings) in _preFilteredSettings)
        {
            foreach (var s in settings)
            {
                if (_filteredSettingIdToFeatureId.TryGetValue(s.Id, out var prevFeature))
                {
                    _logService.Log(LogLevel.Warning,
                        $"Duplicate setting id '{s.Id}' — previously registered under feature '{prevFeature}', now overwritten by '{featureId}'");
                }
                _filteredById[s.Id] = s;
                _filteredSettingIdToFeatureId[s.Id] = featureId;
            }
        }

        _bypassedById = new Dictionary<string, SettingDefinition>();
        _bypassedSettingIdToFeatureId = new Dictionary<string, string>();
        foreach (var (featureId, settings) in _windowsFilterBypassedSettings)
        {
            foreach (var s in settings)
            {
                if (_bypassedSettingIdToFeatureId.TryGetValue(s.Id, out var prevFeature))
                {
                    _logService.Log(LogLevel.Warning,
                        $"Duplicate setting id '{s.Id}' — previously registered under feature '{prevFeature}', now overwritten by '{featureId}' (bypassed)");
                }
                _bypassedById[s.Id] = s;
                _bypassedSettingIdToFeatureId[s.Id] = featureId;
            }
        }
    }

    private async Task PreFilterAllFeatureSettingsAsync()
    {
        _logService.Log(LogLevel.Info, "Pre-filtering settings for all features");

        var featureProviders = GetKnownFeatureProviders();
        _logService.Log(LogLevel.Info, $"Found {featureProviders.Count} feature providers");

        foreach (var (featureId, provider) in featureProviders)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Loading raw settings for {featureId}");
                var rawSettings = provider().ToList();
                _logService.Log(LogLevel.Info, $"Loaded {rawSettings.Count} raw settings for {featureId}");

                var filteredSettings = _windowsFilter.FilterSettingsByWindowsVersion(rawSettings);

                if (featureId == FeatureIds.Power)
                {
                    filteredSettings = await _hardwareFilter.FilterSettingsByHardwareAsync(filteredSettings).ConfigureAwait(false);
                    filteredSettings = await _powerValidation.FilterSettingsByExistenceAsync(filteredSettings).ConfigureAwait(false);
                }

                _preFilteredSettings[featureId] = filteredSettings;

                IEnumerable<SettingDefinition> bypassedSettings = rawSettings;
                if (featureId == FeatureIds.Power)
                {
                    bypassedSettings = await _hardwareFilter.FilterSettingsByHardwareAsync(bypassedSettings).ConfigureAwait(false);
                    bypassedSettings = await _powerValidation.FilterSettingsByExistenceAsync(bypassedSettings).ConfigureAwait(false);
                }
                var decorated = _windowsFilter.FilterSettingsByWindowsVersion(bypassedSettings, applyFilter: false);
                _windowsFilterBypassedSettings[featureId] = decorated;

                _logService.Log(LogLevel.Info, $"Registered {filteredSettings.Count()} settings for {featureId}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error,
                    $"Error loading settings for {featureId}: {ex.Message}");
                _preFilteredSettings[featureId] = Enumerable.Empty<SettingDefinition>();
                _windowsFilterBypassedSettings[featureId] = Enumerable.Empty<SettingDefinition>();
            }
        }

        _logService.Log(LogLevel.Info, "Pre-filtering completed");
    }

    private static Dictionary<string, Func<IEnumerable<SettingDefinition>>> GetKnownFeatureProviders()
    {
        return new Dictionary<string, Func<IEnumerable<SettingDefinition>>>
        {
            // Customization features
            [FeatureIds.ExplorerCustomization] = () => ExplorerCustomizations.GetExplorerCustomizations().Settings,
            [FeatureIds.StartMenu] = () => StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            [FeatureIds.Taskbar] = () => TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            [FeatureIds.WindowsTheme] = () => WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,

            // Optimizations features
            [FeatureIds.Power] = () => PowerOptimizations.GetPowerOptimizations().Settings,
            [FeatureIds.GamingPerformance] = () => GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            [FeatureIds.Notifications] = () => NotificationOptimizations.GetNotificationOptimizations().Settings,
            [FeatureIds.Privacy] = () => PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            [FeatureIds.Sound] = () => SoundOptimizations.GetSoundOptimizations().Settings,
            [FeatureIds.Update] = () => UpdateOptimizations.GetUpdateOptimizations().Settings,
            [FeatureIds.Advanced] = () => AdvancedOptimizations.GetAdvancedOptimizations().Settings,
        };
    }
    #endregion
}
