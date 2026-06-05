// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Utilities.Services;

public class SettingsLoadingService : ISettingsLoadingService
{
    #region Fields & Constructor

    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;
    private readonly IInitializationService _initializationService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ISettingPreparationPipeline _preparationPipeline;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISettingViewModelFactory _viewModelFactory;

    public SettingsLoadingService(
        ISystemSettingsDiscoveryService discoveryService,
        IEventBus eventBus,
        ILogService logService,
        IInitializationService initializationService,
        IComboBoxResolver comboBoxResolver,
        ISettingPreparationPipeline preparationPipeline,
        IUserPreferencesService userPreferencesService,
        ISettingViewModelFactory viewModelFactory)
    {
        _discoveryService = discoveryService;
        _eventBus = eventBus;
        _logService = logService;
        _initializationService = initializationService;
        _comboBoxResolver = comboBoxResolver;
        _preparationPipeline = preparationPipeline;
        _userPreferencesService = userPreferencesService;
        _viewModelFactory = viewModelFactory;
    }

    #endregion

    #region Core Settings Loading

    public async Task<ObservableCollection<SettingItemViewModel>> LoadConfiguredSettingsAsync(
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null)
    {
        try
        {
            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Starting to load settings for '{featureModuleId}'");
            _initializationService.StartFeatureInitialization(featureModuleId);

            var settingsList = _preparationPipeline.PrepareSettings(featureModuleId);

            var settingViewModels = new ObservableCollection<SettingItemViewModel>();

            var showTechnicalDetails = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowTechnicalDetails, false);

            _logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
            var batchStates = await _discoveryService.GetSettingStatesAsync(settingsList);

            await ResolveComboBoxStatesAsync(settingsList, batchStates);

            foreach (var setting in settingsList)
            {
                if (batchStates.TryGetValue(setting.Id, out var settingState) && !settingState.Success)
                {
                    _logService.Log(LogLevel.Debug, $"Skipping setting '{setting.Id}': {settingState.ErrorMessage}");
                    continue;
                }

                var currentState = batchStates.TryGetValue(setting.Id, out var s) ? s : new SettingStateResult();
                var viewModel = await _viewModelFactory.CreateAsync(setting, currentState, parentViewModel);
                viewModel.IsTechnicalDetailsGloballyVisible = showTechnicalDetails;
                settingViewModels.Add(viewModel);
            }

            foreach (var kvp in batchStates)
            {
                if (kvp.Value.TooltipData != null)
                {
                    _eventBus.Publish(new TooltipUpdatedEvent(kvp.Key, kvp.Value.TooltipData));
                }
            }
            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Finished loading {settingViewModels.Count} settings for '{featureModuleId}'");
            _initializationService.CompleteFeatureInitialization(featureModuleId);

            return settingViewModels;
        }
        catch (Exception ex)
        {
            _initializationService.CompleteFeatureInitialization(featureModuleId);
            _logService.Log(LogLevel.Error, $"Error loading settings for {featureModuleId}: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region State Management & Refresh

    public async Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings)
    {
        var settingsList = settings.ToList();
        var definitions = settingsList
            .Where(s => s.SettingDefinition != null)
            .Select(s => s.SettingDefinition!)
            .ToList();

        if (definitions.Count == 0)
            return new Dictionary<string, SettingStateResult>();

        var batchStates = await _discoveryService.GetSettingStatesAsync(definitions);

        await ResolveComboBoxStatesAsync(definitions, batchStates);

        return batchStates;
    }

    private async Task ResolveComboBoxStatesAsync(
        IEnumerable<SettingDefinition> settings,
        Dictionary<string, SettingStateResult> batchStates)
    {
        foreach (var setting in settings.Where(s => s.InputType == InputType.Selection))
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
                    _logService.Log(LogLevel.Warning, $"Failed to resolve combo box value for '{setting.Id}': {ex.Message}");
                }
            }
        }
    }

    #endregion
}

