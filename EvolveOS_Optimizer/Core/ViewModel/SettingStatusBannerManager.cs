// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.ViewModel;

internal sealed class SettingStatusBannerManager
{
    private readonly ILocalizationService _localizationService;

    internal readonly record struct BannerState(string? Message, InfoBarSeverity Severity)
    {
        public static BannerState Clear => new(null, InfoBarSeverity.Informational);
    }

    public SettingStatusBannerManager(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public BannerState? GetCompatibilityBanner(SettingDefinition? definition)
    {
        if (definition?.VersionCompatibilityMessage is { } messageText)
        {
            return new BannerState(messageText, InfoBarSeverity.Warning);
        }
        return null;
    }

    public BannerState? ComputeBannerForValue(
        SettingDefinition? definition, object? value, string? crossGroupInfoMessage)
    {
        if (definition == null || value is not int selectedIndex)
        {
            if (definition?.VersionCompatibilityMessage == null)
            {
                return BannerState.Clear;
            }
            return null;
        }

        if (definition.ComboBox?.Options is { } warningOptions
            && selectedIndex >= 0 && selectedIndex < warningOptions.Count
            && warningOptions[selectedIndex].Warning is { } warning)
        {
            return new BannerState(warning, InfoBarSeverity.Error);
        }

        if (definition.CrossGroupChildSettings != null)
        {
            return ComputeCrossGroupBanner(definition, selectedIndex, crossGroupInfoMessage);
        }

        if (definition.VersionCompatibilityMessage is { } compatText)
        {
            return new BannerState(compatText, InfoBarSeverity.Warning);
        }

        return BannerState.Clear;
    }

    public BannerState? GetRestartBanner(SettingDefinition? definition, bool hasChangedThisSession)
    {
        if (!hasChangedThisSession) return null;
        if (definition?.RequiresRestart != true) return null;

        return new BannerState(
            _localizationService.GetString("Common_RestartRequired"),
            InfoBarSeverity.Warning);
    }

    private BannerState ComputeCrossGroupBanner(
        SettingDefinition definition, int selectedIndex, string? crossGroupInfoMessage)
    {
        var options = definition.ComboBox?.Options;

        if (options == null || options.Count == 0)
            return BannerState.Clear;

        var customOptionIndex = options.Count - 1;
        bool isCustomState = selectedIndex == customOptionIndex ||
            selectedIndex == ComboBoxConstants.CustomStateIndex;

        if (!isCustomState)
            return BannerState.Clear;

        if (!string.IsNullOrEmpty(crossGroupInfoMessage))
            return new BannerState(crossGroupInfoMessage, InfoBarSeverity.Warning);

        var header = _localizationService.GetString("Setting_CrossGroupWarning_Header");
        if (!string.IsNullOrEmpty(header))
            return new BannerState(header, InfoBarSeverity.Warning);

        return BannerState.Clear;
    }
}

