// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Concurrent;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

public class ConfigReviewService : IConfigReviewService, IConfigReviewModeService, IConfigReviewDiffService, IConfigReviewBadgeService, IDisposable
{
    #region Fields & Constants

    private bool _disposed;
    private readonly ILogService _logService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ILocalizationService _localizationService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly ConcurrentDictionary<string, ConfigReviewDiff> _diffs = new();
    private readonly ConcurrentDictionary<string, int> _configItemCounts = new();
    private readonly ConcurrentDictionary<string, byte> _featuresInConfig = new();
    private readonly ConcurrentDictionary<string, byte> _visitedFeatures = new();

    private static readonly HashSet<string> ActionSettingIds = new()
    {
        SettingIds.ThemeModeWindows,
        "taskbar-clean",
        "start-menu-clean-10",
        "start-menu-clean-11"
    };

    #endregion

    #region Constructor & Disposal

    public ConfigReviewService(
        ILogService logService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISystemSettingsDiscoveryService discoveryService,
        IComboBoxSetupService comboBoxSetupService,
        IComboBoxResolver comboBoxResolver,
        ILocalizationService localizationService,
        IWindowsVersionService windowsVersionService)
    {
        _logService = logService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _discoveryService = discoveryService;
        _comboBoxSetupService = comboBoxSetupService;
        _comboBoxResolver = comboBoxResolver;
        _localizationService = localizationService;
        _windowsVersionService = windowsVersionService;

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }

    #endregion

    #region Interface Properties & Events

    public bool IsInReviewMode { get; private set; }
    public bool IsWindowsDefaults { get; private set; }
    public UnifiedConfigurationFile? ActiveConfig { get; private set; }
    public int TotalChanges => _diffs.Count;
    public int ApprovedChanges => _diffs.Values.Count(static d => d.IsReviewed && d.IsApproved);
    public int ReviewedChanges => _diffs.Values.Count(static d => d.IsReviewed);
    public int TotalConfigItems { get; private set; }
    public bool IsSoftwareAppsReviewed { get; set; }

    public event EventHandler? ReviewModeChanged;
    public event EventHandler? ApprovalCountChanged;
    public event EventHandler? BadgeStateChanged;

    #endregion

    #region Review Mode Lifecycle

    public async Task EnterReviewModeAsync(UnifiedConfigurationFile config, bool isWindowsDefaults = false)
    {
        ActiveConfig = config;
        IsWindowsDefaults = isWindowsDefaults;
        _diffs.Clear();
        _configItemCounts.Clear();
        _featuresInConfig.Clear();
        _visitedFeatures.Clear();
        IsInReviewMode = true;

        ComputeConfigItemCounts(config);

        await ComputeEagerDiffsAsync(config);

        foreach (var featureId in _featuresInConfig.Keys)
        {
            if (FeatureDefinitions.OptimizeFeatures.Contains(featureId) ||
                FeatureDefinitions.CustomizeFeatures.Contains(featureId))
            {
                if (GetFeatureDiffCount(featureId) == 0)
                {
                    _visitedFeatures.TryAdd(featureId, 0);
                }
            }
        }

        _logService.Log(LogLevel.Info,
            $"[ConfigReviewService] Entered review mode with {TotalConfigItems} total config items, {TotalChanges} actual diffs");
        ReviewModeChanged?.Invoke(this, EventArgs.Empty);
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExitReviewMode()
    {
        ActiveConfig = null;
        _diffs.Clear();
        _configItemCounts.Clear();
        _featuresInConfig.Clear();
        _visitedFeatures.Clear();
        TotalConfigItems = 0;
        IsInReviewMode = false;
        IsWindowsDefaults = false;
        _logService.Log(LogLevel.Info, "[ConfigReviewService] Exited review mode");
        ReviewModeChanged?.Invoke(this, EventArgs.Empty);
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Approval & Diff Registration

    public ConfigReviewDiff? GetDiffForSetting(string settingId)
    {
        return _diffs.TryGetValue(settingId, out var diff) ? diff : null;
    }

    public void SetSettingApproval(string settingId, bool approved)
    {
        if (_diffs.TryGetValue(settingId, out var diff))
        {
            _diffs[settingId] = diff with { IsReviewed = true, IsApproved = approved };
            ApprovalCountChanged?.Invoke(this, EventArgs.Empty);
            BadgeStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetActionApproval(string settingId, bool approved)
    {
        if (_diffs.TryGetValue(settingId, out var diff))
        {
            _diffs[settingId] = diff with { IsActionReviewed = true, IsActionApproved = approved };
        }
    }

    public IReadOnlyList<ConfigReviewDiff> GetApprovedDiffs()
    {
        return _diffs.Values.Where(d => d.IsReviewed && d.IsApproved).ToList().AsReadOnly();
    }

    public void RegisterDiff(ConfigReviewDiff diff)
    {
        _diffs[diff.SettingId] = diff;
        _logService.Log(
            LogLevel.Debug,
            $"[ConfigReviewService] Registered diff for '{diff.SettingId}': {diff.CurrentValueDisplay} -> {diff.ConfigValueDisplay}");
        ApprovalCountChanged?.Invoke(this, EventArgs.Empty);
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Badge & Feature Status Queries

    public void NotifyBadgeStateChanged()
    {
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
        ApprovalCountChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkFeatureVisited(string featureId)
    {
        if (_visitedFeatures.TryAdd(featureId, 0))
        {
            _logService.Log(LogLevel.Debug,
                $"[ConfigReviewService] Feature '{featureId}' marked as visited");
            BadgeStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int GetNavBadgeCount(string sectionTag)
    {
        if (!IsInReviewMode) return 0;

        return sectionTag switch
        {
            "Optimize" => FeatureDefinitions.OptimizeFeatures
                .Sum(f => GetFeaturePendingDiffCount(f)),
            "Customize" => FeatureDefinitions.CustomizeFeatures
                .Sum(f => GetFeaturePendingDiffCount(f)),
            _ => 0
        };
    }

    private int GetFeatureConfigItemCount(string featureId)
    {
        return _configItemCounts.TryGetValue(featureId, out var count) ? count : 0;
    }

    public int GetFeatureDiffCount(string featureId)
    {
        return _diffs.Values.Count(d => d.FeatureModuleId == featureId);
    }

    public int GetFeaturePendingDiffCount(string featureId)
    {
        return _diffs.Values.Count(d => d.FeatureModuleId == featureId && !d.IsReviewed);
    }

    public bool IsFeatureInConfig(string featureId)
    {
        return _featuresInConfig.ContainsKey(featureId);
    }

    public bool IsSectionFullyReviewed(string sectionTag)
    {
        if (!IsInReviewMode) return false;

        var featureIds = sectionTag switch
        {
            "Optimize" => FeatureDefinitions.OptimizeFeatures.ToArray(),
            "Customize" => FeatureDefinitions.CustomizeFeatures.ToArray(),
            _ => Array.Empty<string>()
        };

        var relevantFeatures = featureIds.Where(f => _featuresInConfig.ContainsKey(f)).ToList();
        if (relevantFeatures.Count == 0) return false;

        return relevantFeatures.All(IsFeatureFullyReviewed);
    }

    public bool IsFeatureFullyReviewed(string featureId)
    {
        if (!IsInReviewMode) return false;
        if (!_featuresInConfig.ContainsKey(featureId)) return false;

        var featureDiffs = _diffs.Values.Where(d => d.FeatureModuleId == featureId).ToList();
        if (featureDiffs.Count == 0)
        {
            return true;
        }

        return featureDiffs.All(d => d.IsReviewed);
    }

    #endregion

    #region Config Item Counting & Diff Computation

    private void ComputeConfigItemCounts(UnifiedConfigurationFile config)
    {
        int total = 0;

        foreach (var kvp in config.Optimize.Features)
        {
            if (kvp.Value.IsIncluded && kvp.Value.Items.Count > 0)
            {
                _configItemCounts[kvp.Key] = kvp.Value.Items.Count;
                _featuresInConfig.TryAdd(kvp.Key, 0);
                total += kvp.Value.Items.Count;
            }
        }

        foreach (var kvp in config.Customize.Features)
        {
            if (kvp.Value.IsIncluded && kvp.Value.Items.Count > 0)
            {
                _configItemCounts[kvp.Key] = kvp.Value.Items.Count;
                _featuresInConfig.TryAdd(kvp.Key, 0);
                total += kvp.Value.Items.Count;
            }
        }

        TotalConfigItems = total;
    }

    private async Task ComputeEagerDiffsAsync(UnifiedConfigurationFile config)
    {
        var onText = _localizationService.GetString("Common_On") ?? "On";
        var offText = _localizationService.GetString("Common_Off") ?? "Off";

        foreach (var feature in config.Optimize.Features)
        {
            if (!feature.Value.IsIncluded || feature.Value.Items.Count == 0) continue;
            await ComputeFeatureDiffsAsync(feature.Key, feature.Value.Items, onText, offText);
        }

        foreach (var feature in config.Customize.Features)
        {
            if (!feature.Value.IsIncluded || feature.Value.Items.Count == 0) continue;
            await ComputeFeatureDiffsAsync(feature.Key, feature.Value.Items, onText, offText);
        }
    }

    private async Task ComputeFeatureDiffsAsync(
        string featureId,
        IReadOnlyList<ConfigurationItem> configItems,
        string onText,
        string offText)
    {
        try
        {
            var settingDefinitions = _compatibleSettingsRegistry.GetFilteredSettings(featureId);
            var settingDefMap = settingDefinitions.ToDictionary(s => s.Id);

            var batchStates = await _discoveryService.GetSettingStatesAsync(settingDefinitions.ToList());

            foreach (var setting in settingDefinitions.Where(s => s.InputType == InputType.Selection))
            {
                if (batchStates.TryGetValue(setting.Id, out var state) && state.RawValues != null)
                {
                    try
                    {
                        var resolvedValue = await _comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues as Dictionary<string, object?>);
                        batchStates[setting.Id] = state with { CurrentValue = resolvedValue };
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning,
                            $"[ConfigReviewService] Failed to resolve combo box for '{setting.Id}': {ex.Message}");
                    }
                }
            }

            foreach (var configItem in configItems)
            {
                if (!settingDefMap.TryGetValue(configItem.Id, out var settingDef))
                    continue;

                var currentState = batchStates.TryGetValue(configItem.Id, out var state)
                    ? state
                    : new SettingStateResult();

                bool isActionSetting = ActionSettingIds.Contains(configItem.Id);

                if (configItem.Id == "start-menu-clean-10" && _windowsVersionService.IsWindows11())
                    continue;
                if (configItem.Id == "start-menu-clean-11" && !_windowsVersionService.IsWindows11())
                    continue;

                var (hasDiff, currentDisplay, configDisplay, currentKey, configKey) = await ComputeEagerDiffAsync(
                    settingDef, configItem, currentState, onText, offText).ConfigureAwait(false);

                if (hasDiff || isActionSetting)
                {
                    var diff = new ConfigReviewDiff
                    {
                        SettingId = configItem.Id,
                        SettingName = settingDef.Name,
                        FeatureModuleId = featureId,
                        CurrentValueDisplay = currentDisplay,
                        ConfigValueDisplay = configDisplay,
                        CurrentDisplayKey = currentKey,
                        ConfigDisplayKey = configKey,
                        ConfigItem = configItem,
                        IsApproved = false,
                        IsReviewed = false,
                        InputType = settingDef.InputType,
                        IsActionSetting = isActionSetting,
                    };

                    if (isActionSetting)
                    {
                        diff = diff with { ActionConfirmationMessage = GetActionConfirmationMessage(configItem) };
                    }

                    _diffs[configItem.Id] = diff;

                    _logService.Log(LogLevel.Debug,
                        $"[ConfigReviewService] Eager diff for '{configItem.Id}' in '{featureId}': " +
                        $"{(isActionSetting ? "[Action] " : "")}{currentDisplay} -> {configDisplay}");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error,
                $"[ConfigReviewService] Error computing eager diffs for '{featureId}': {ex.Message}");
        }
    }

    private async Task<(bool hasDiff, string currentDisplay, string configDisplay, string? currentKey, string? configKey)> ComputeEagerDiffAsync(
        SettingDefinition settingDef,
        ConfigurationItem configItem,
        SettingStateResult currentState,
        string onText,
        string offText)
    {
        switch (settingDef.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
                {
                    var currentBool = currentState.IsEnabled;
                    var configBool = configItem.IsSelected ?? false;
                    if (currentBool != configBool)
                    {
                        var currentKey = currentBool ? "Common_On" : "Common_Off";
                        var configKey = configBool ? "Common_On" : "Common_Off";
                        return (true, currentBool ? onText : offText, configBool ? onText : offText, currentKey, configKey);
                    }
                    return (false, string.Empty, string.Empty, null, null);
                }

            case InputType.Selection:
                {
                    var comboResult = await _comboBoxSetupService.SetupComboBoxOptionsAsync(settingDef, currentState.CurrentValue).ConfigureAwait(false);
                    var currentIndex = comboResult.SelectedValue is int resolvedIdx ? resolvedIdx
                        : (currentState.CurrentValue is int idx ? idx : -1);
                    if (configItem.PowerPlanGuid != null)
                    {
                        string? currentGuid = null;
                        if (currentState.RawValues?.TryGetValue("ActivePowerPlanGuid", out var rawGuid) == true)
                            currentGuid = rawGuid?.ToString();

                        string? currentPlanName = currentState.RawValues?.TryGetValue("ActivePowerPlan", out var rawName) == true
                            ? rawName?.ToString() : null;
                        string? configPlanName = configItem.PowerPlanName;

                        _logService.Log(LogLevel.Debug,
                            $"[ConfigReviewService] PowerPlan comparison: currentGuid='{currentGuid}', configGuid='{configItem.PowerPlanGuid}', " +
                            $"currentName='{currentPlanName}', configName='{configPlanName}'");

                        bool guidsMatch = !string.IsNullOrEmpty(currentGuid) &&
                            NormalizeGuid(currentGuid) == NormalizeGuid(configItem.PowerPlanGuid);

                        if (guidsMatch)
                        {
                            _logService.Log(LogLevel.Debug, "[ConfigReviewService] PowerPlan: GUIDs match directly");
                            return (false, string.Empty, string.Empty, null, null);
                        }

                        var currentPredefined = ResolveToPredefinedPlan(currentGuid, currentPlanName);
                        var configPredefined = ResolveToPredefinedPlan(configItem.PowerPlanGuid, configPlanName);

                        _logService.Log(LogLevel.Debug,
                            $"[ConfigReviewService] PowerPlan resolve: current='{currentPredefined?.Name}' ({currentPredefined?.Guid}), " +
                            $"config='{configPredefined?.Name}' ({configPredefined?.Guid})");

                        if (currentPredefined != null && configPredefined != null &&
                            NormalizeGuid(currentPredefined.Guid) == NormalizeGuid(configPredefined.Guid))
                        {
                            _logService.Log(LogLevel.Debug, "[ConfigReviewService] PowerPlan: Both resolve to same predefined plan");
                            return (false, string.Empty, string.Empty, null, null);
                        }

                        var currentRawKey = GetPowerPlanLocalizationKey(currentGuid) ?? currentPlanName ?? "Unknown";
                        var configRawKey = GetPowerPlanLocalizationKey(configItem.PowerPlanGuid) ?? configPlanName ?? "Custom";

                        var currentDisplayName = LocalizePowerPlanByGuid(currentGuid)
                            ?? currentPlanName ?? "Unknown";
                        var configDisplayName = LocalizePowerPlanByGuid(configItem.PowerPlanGuid)
                            ?? configPlanName ?? "Custom";

                        _logService.Log(LogLevel.Debug,
                            $"[ConfigReviewService] PowerPlan: Diff detected - '{currentDisplayName}' -> '{configDisplayName}'");
                        return (true, currentDisplayName, configDisplayName, currentRawKey, configRawKey);
                    }

                    if (configItem.CustomStateValues != null)
                    {
                        var currentRawKey = currentIndex >= 0 && currentIndex < comboResult.Options.Count
                            ? comboResult.Options[currentIndex].DisplayText : null;
                        var currentDisplayName = currentRawKey != null
                            ? LocalizeComboBoxDisplayText(currentRawKey)
                            : await GetComboBoxDisplayNameFromDefAsync(settingDef, currentIndex, currentState).ConfigureAwait(false);
                        var configDisplayName = configItem.PowerPlanName ?? "Custom";
                        if (!string.Equals(currentDisplayName, configDisplayName, StringComparison.OrdinalIgnoreCase))
                            return (true, currentDisplayName, configDisplayName, currentRawKey, configDisplayName);
                        return (false, string.Empty, string.Empty, null, null);
                    }

                    if (configItem.SelectedIndex == null)
                        return (false, string.Empty, string.Empty, null, null);

                    var configIndex = configItem.SelectedIndex.Value;
                    if (currentIndex != configIndex)
                    {
                        var rawCurrentKey = currentIndex >= 0 && currentIndex < comboResult.Options.Count
                            ? comboResult.Options[currentIndex].DisplayText : null;
                        var rawConfigKey = configIndex >= 0 && configIndex < comboResult.Options.Count
                            ? comboResult.Options[configIndex].DisplayText : null;
                        var currentDisplayName = rawCurrentKey != null
                            ? LocalizeComboBoxDisplayText(rawCurrentKey) : currentIndex.ToString();
                        var configDisplayName = rawConfigKey != null
                            ? LocalizeComboBoxDisplayText(rawConfigKey) : configIndex.ToString();
                        return (true, currentDisplayName, configDisplayName, rawCurrentKey, rawConfigKey);
                    }
                    return (false, string.Empty, string.Empty, null, null);
                }

            case InputType.NumericRange:
                {
                    var currentVal = currentState.CurrentValue is int cv ? cv : 0;
                    if (configItem.PowerSettings != null)
                    {
                        if (configItem.PowerSettings.TryGetValue("ACValue", out var acVal) && acVal is int acInt)
                        {
                            if (currentVal != acInt)
                                return (true, currentVal.ToString(), acInt.ToString(), null, null);
                        }
                    }
                    return (false, string.Empty, string.Empty, null, null);
                }

            default:
                return (false, string.Empty, string.Empty, null, null);
        }
    }

    #endregion

    #region Action Messages & Localization

    private string GetActionConfirmationMessage(ConfigurationItem configItem)
    {
        return configItem.Id switch
        {
            SettingIds.ThemeModeWindows => GetThemeWallpaperMessage(configItem),
            "taskbar-clean" => _localizationService.GetString("Review_Mode_Action_CleanTaskbar")
                ?? "Clean the taskbar as part of this configuration?",
            "start-menu-clean-10" or "start-menu-clean-11" =>
                _localizationService.GetString("Review_Mode_Action_CleanStartMenu")
                ?? "Clean the start menu as part of this configuration?",
            _ => string.Empty
        };
    }

    private string GetThemeWallpaperMessage(ConfigurationItem configItem)
    {
        var themeNameKey = configItem.SelectedIndex == 0 ? "Theme_LightNative" : "Theme_DarkNative";
        var themeName = _localizationService.GetString(themeNameKey) ?? (configItem.SelectedIndex == 0 ? "Light" : "Dark");
        var format = _localizationService.GetString("Review_Mode_Action_ThemeWallpaper")
            ?? "Apply the default {0} wallpaper?";
        return string.Format(format, themeName);
    }

    private async Task<string> GetComboBoxDisplayNameFromDefAsync(
        SettingDefinition settingDef,
        int index,
        SettingStateResult currentState)
    {
        try
        {
            var result = await _comboBoxSetupService.SetupComboBoxOptionsAsync(settingDef, currentState.CurrentValue).ConfigureAwait(false);
            if (index >= 0 && index < result.Options.Count)
            {
                return LocalizeComboBoxDisplayText(result.Options[index].DisplayText ?? index.ToString());
            }

            if (index < 0 && result.SelectedValue is int resolvedIndex &&
                resolvedIndex >= 0 && resolvedIndex < result.Options.Count)
            {
                return LocalizeComboBoxDisplayText(result.Options[resolvedIndex].DisplayText ?? resolvedIndex.ToString());
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"[ConfigReviewService] Failed to get combo box display name for '{settingDef.Id}' index {index}: {ex.Message}");
        }
        return index >= 0 ? index.ToString() : "Unknown";
    }

    private string LocalizeComboBoxDisplayText(string displayText)
    {
        if (string.IsNullOrEmpty(displayText))
            return "Unknown";

        var localized = _localizationService.GetString(displayText);
        if (!string.IsNullOrEmpty(localized) && !(localized.StartsWith("[") && localized.EndsWith("]")))
            return localized;

        return displayText;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!IsInReviewMode) return;
        RelocalizeDisplayStrings();
    }

    private void RelocalizeDisplayStrings()
    {
        foreach (var key in _diffs.Keys)
        {
            if (!_diffs.TryGetValue(key, out var diff))
                continue;
            var updated = diff;
            if (diff.CurrentDisplayKey != null)
                updated = updated with { CurrentValueDisplay = LocalizeComboBoxDisplayText(diff.CurrentDisplayKey) };
            if (diff.ConfigDisplayKey != null)
                updated = updated with { ConfigValueDisplay = LocalizeComboBoxDisplayText(diff.ConfigDisplayKey) };
            if (diff.IsActionSetting && diff.ConfigItem != null)
                updated = updated with { ActionConfirmationMessage = GetActionConfirmationMessage(diff.ConfigItem) };
            _diffs[key] = updated;
        }
    }

    private static string? GetPowerPlanLocalizationKey(string? guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        var normalizedGuid = NormalizeGuid(guid);
        var predefined = PowerPlanDefinitions.BuiltInPowerPlans.FirstOrDefault(
            p => NormalizeGuid(p.Guid) == normalizedGuid);
        return predefined?.LocalizationKey;
    }

    private static PredefinedPowerPlan? ResolveToPredefinedPlan(string? guid, string? name)
    {
        var plans = PowerPlanDefinitions.BuiltInPowerPlans;

        if (!string.IsNullOrEmpty(guid))
        {
            var normalizedGuid = NormalizeGuid(guid);
            var byGuid = plans.FirstOrDefault(p => NormalizeGuid(p.Guid) == normalizedGuid);
            if (byGuid != null) return byGuid;
        }

        if (!string.IsNullOrEmpty(name))
        {
            if (name.Contains("EvolveOS", StringComparison.OrdinalIgnoreCase))
            {
                return plans.FirstOrDefault(p =>
                    p.Name.Contains("EvolveOS", StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    private string? LocalizePowerPlanByGuid(string? guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;

        var normalizedGuid = NormalizeGuid(guid);
        var predefined = PowerPlanDefinitions.BuiltInPowerPlans.FirstOrDefault(
            p => NormalizeGuid(p.Guid) == normalizedGuid);

        if (predefined == null) return null;

        var localized = _localizationService.GetString(predefined.LocalizationKey);
        return !string.IsNullOrEmpty(localized) ? localized : predefined.Name;
    }

    private static string NormalizeGuid(string? guid)
    {
        if (string.IsNullOrEmpty(guid)) return string.Empty;
        return Guid.TryParse(guid, out var parsed) ? parsed.ToString("D").ToLowerInvariant() : guid.ToLowerInvariant();
    }

    #endregion
}