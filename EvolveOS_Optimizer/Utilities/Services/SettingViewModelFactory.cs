// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services;

public class SettingViewModelFactory : ISettingViewModelFactory
{
    private readonly SettingViewModelDependencies _viewModelDeps;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly INewBadgeService _newBadgeService;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ISettingViewModelEnricher _enricher;
    private readonly OSCompressionService _osCompressionService;

    public SettingViewModelFactory(
        SettingViewModelDependencies viewModelDeps,
        ILogService logService,
        ILocalizationService localizationService,
        IUserPreferencesService userPreferencesService,
        INewBadgeService newBadgeService,
        IComboBoxSetupService comboBoxSetupService,
        IComboBoxResolver comboBoxResolver,
        ISettingViewModelEnricher enricher,
        OSCompressionService osCompressionService)
    {
        _viewModelDeps = viewModelDeps;
        _logService = logService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;
        _comboBoxSetupService = comboBoxSetupService;
        _comboBoxResolver = comboBoxResolver;
        _enricher = enricher;
        _osCompressionService = osCompressionService;
    }

    public async Task<SettingItemViewModel> CreateAsync(
        SettingDefinition setting,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel)
    {
        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = setting,
            ParentFeatureViewModel = parentViewModel,
            SettingId = setting.Id,
            Name = setting.Name,
            Description = setting.Description,
            GroupName = setting.GroupName ?? string.Empty,
            Icon = setting.Icon ?? string.Empty,
            IconPack = setting.IconPack ?? "Material",
            InputType = setting.InputType,
            IsSelected = currentState.IsEnabled,
            OnText = _localizationService.GetString("Common_On") ?? "On",
            OffText = _localizationService.GetString("Common_Off") ?? "Off",
        };

        var viewModel = new SettingItemViewModel(
            config,
            _viewModelDeps.SettingApplicationService,
            _viewModelDeps.LogService,
            _viewModelDeps.DispatcherService,
            _viewModelDeps.DialogService,
            _localizationService,
            _osCompressionService,
            _viewModelDeps.EventBus,
            _userPreferencesService,
            _viewModelDeps.RegeditLauncher,
            _newBadgeService);

        if (setting.RequiresAdvancedUnlock)
        {
            var unlocked = await _userPreferencesService.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false);
            viewModel.IsLocked = !unlocked;
        }

        if (viewModel.SupportsSeparateACDC)
        {
            await _enricher.DetectBatteryAsync(viewModel);

            if (setting.InputType == InputType.NumericRange && currentState.RawValues != null)
            {
                if (currentState.RawValues.TryGetValue("ACValue", out var acVal) && acVal is int acInt)
                    viewModel.AcNumericValue = ConvertFromSystemUnits(acInt, setting);
                if (currentState.RawValues.TryGetValue("DCValue", out var dcVal) && dcVal is int dcInt)
                    viewModel.DcNumericValue = ConvertFromSystemUnits(dcInt, setting);
            }
        }

        if (setting.InputType != InputType.Selection)
        {
            viewModel.SelectedValue = currentState.CurrentValue;
        }

        if (setting.InputType == InputType.NumericRange && setting.NumericRange != null)
        {
            viewModel.MaxValue = setting.NumericRange.MaxValue;
            viewModel.MinValue = setting.NumericRange.MinValue;
            viewModel.Units = setting.NumericRange.Units ?? "";

            if (currentState.CurrentValue is int intValue)
            {
                viewModel.NumericValue = ConvertFromSystemUnits(intValue, setting);
            }
        }

        if (setting.InputType == InputType.Selection)
        {
            try
            {
                var comboBoxResult = await _comboBoxSetupService.SetupComboBoxOptionsAsync(setting, currentState.CurrentValue);
                viewModel.ComboBoxOptions.Clear();

                var isPowerPlanSetting = setting.Recommendation?.LoadDynamicOptions == true;

                foreach (var option in comboBoxResult.Options)
                {
                    if (isPowerPlanSetting && option.DisplayText.StartsWith("PowerPlan_"))
                    {
                        option.DisplayText = _localizationService.GetString(option.DisplayText);
                    }

                    viewModel.ComboBoxOptions.Add(option);
                }

                _enricher.SetCrossGroupInfoMessage(viewModel, setting);

                if (comboBoxResult.SelectedValue != null)
                {
                    viewModel.SelectedValue = comboBoxResult.SelectedValue;
                    viewModel.UpdateStatusBanner(comboBoxResult.SelectedValue);
                }
                else if (currentState.CurrentValue != null)
                {
                    viewModel.SelectedValue = currentState.CurrentValue;
                    viewModel.UpdateStatusBanner(currentState.CurrentValue);
                }

                if (viewModel.SupportsSeparateACDC && currentState.RawValues != null)
                {
                    var rawAcVal = currentState.RawValues.GetValueOrDefault("ACValue");
                    var rawDcVal = currentState.RawValues.GetValueOrDefault("DCValue");

                    var acRaw = currentState.RawValues.ToDictionary(kv => kv.Key, kv => kv.Value); acRaw["PowerCfgValue"] = rawAcVal;
                    var dcRaw = currentState.RawValues.ToDictionary(kv => kv.Key, kv => kv.Value); dcRaw["PowerCfgValue"] = rawDcVal;
                    var acIndex = await _comboBoxResolver.ResolveCurrentValueAsync(setting, acRaw);
                    var dcIndex = await _comboBoxResolver.ResolveCurrentValueAsync(setting, dcRaw);

                    viewModel.AcValue = acIndex is int ai ? ai : 0;
                    viewModel.DcValue = dcIndex is int di ? di : 0;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to setup combo box for '{setting.Id}': {ex.Message}");
            }
        }
        else
        {
            viewModel.InitializeCompatibilityBanner();
        }

        viewModel.ComputeBadgeState();

        return viewModel;
    }

    private static int ConvertFromSystemUnits(int systemValue, SettingDefinition setting)
    {
        var displayUnits = setting.NumericRange?.Units;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }
}
