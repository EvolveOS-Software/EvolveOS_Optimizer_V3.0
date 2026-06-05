// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Core.EventHandlers;

public class TooltipRefreshEventHandler : IDisposable
{
    #region Fields & Dependencies
    private readonly IEventBus _eventBus;
    private readonly ITooltipDataService _tooltipDataService;
    private readonly IGlobalSettingsRegistry _settingsRegistry;
    private ISubscriptionToken? _settingAppliedSubscriptionToken;
    #endregion

    #region Constructor
    public TooltipRefreshEventHandler(
        IEventBus eventBus,
        ITooltipDataService tooltipDataService,
        IGlobalSettingsRegistry settingsRegistry)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _tooltipDataService = tooltipDataService ?? throw new ArgumentNullException(nameof(tooltipDataService));
        _settingsRegistry = settingsRegistry ?? throw new ArgumentNullException(nameof(settingsRegistry));

        _settingAppliedSubscriptionToken = eventBus.SubscribeAsync<SettingAppliedEvent>(HandleSettingAppliedAsync);
    }
    #endregion

    #region Event Handlers
    private async Task HandleSettingAppliedAsync(SettingAppliedEvent settingAppliedEvent)
    {
        try
        {
            var settingItem = _settingsRegistry.GetSetting(settingAppliedEvent.SettingId);

            if (settingItem == null)
            {
                await Task.Delay(100).ConfigureAwait(false);
                settingItem = _settingsRegistry.GetSetting(settingAppliedEvent.SettingId);
            }

            if (settingItem is SettingDefinition settingDefinition)
            {
                var tooltipData = await _tooltipDataService.RefreshTooltipDataAsync(settingAppliedEvent.SettingId, settingDefinition).ConfigureAwait(false);
                if (tooltipData != null)
                {
                    _eventBus.Publish(new TooltipUpdatedEvent(settingAppliedEvent.SettingId, tooltipData));
                }

                var siblings = FindCompositeStringSiblings(settingDefinition);
                foreach (var sibling in siblings)
                {
                    try
                    {
                        var siblingTooltip = await _tooltipDataService.RefreshTooltipDataAsync(sibling.Id, sibling).ConfigureAwait(false);
                        if (siblingTooltip != null)
                        {
                            _eventBus.Publish(new TooltipUpdatedEvent(sibling.Id, siblingTooltip));
                        }
                    }
                    catch (Exception siblingEx)
                    {
                        ErrorLogging.LogDebug($"Failed to refresh sibling tooltip for '{sibling.Id}': {siblingEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug($"Failed to refresh tooltip for '{settingAppliedEvent.SettingId}': {ex.Message}");
        }
    }
    #endregion

    #region Helpers
    private List<SettingDefinition> FindCompositeStringSiblings(SettingDefinition appliedSetting)
    {
        var siblings = new List<SettingDefinition>();
        var compositeRegSettings = appliedSetting.RegistrySettings
            .Where(rs => rs.CompositeStringKey != null)
            .ToList();

        if (compositeRegSettings.Count == 0)
            return siblings;

        foreach (var setting in _settingsRegistry.GetAllSettings())
        {
            if (setting is not SettingDefinition def || def.Id == appliedSetting.Id)
                continue;

            foreach (var regSetting in compositeRegSettings)
            {
                if (def.RegistrySettings.Any(rs =>
                    rs.KeyPath == regSetting.KeyPath &&
                    rs.ValueName == regSetting.ValueName &&
                    rs.CompositeStringKey != null))
                {
                    siblings.Add(def);
                    break;
                }
            }
        }

        return siblings;
    }
    #endregion

    #region IDisposable Implementation
    public void Dispose()
    {
        _settingAppliedSubscriptionToken?.Dispose();
    }
    #endregion
}