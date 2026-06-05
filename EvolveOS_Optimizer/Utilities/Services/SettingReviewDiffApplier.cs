// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Utilities.Services;

public class SettingReviewDiffApplier : ISettingReviewDiffApplier
{
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly ILocalizationService _localizationService;

    public SettingReviewDiffApplier(
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        ILocalizationService localizationService)
    {
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _localizationService = localizationService;
    }

    public void ApplyReviewDiffToViewModel(SettingItemViewModel viewModel, SettingStateResult currentState)
    {
        var config = _configReviewModeService.ActiveConfig;
        if (config == null) return;

        viewModel.IsInReviewMode = true;

        var existingDiff = _configReviewDiffService.GetDiffForSetting(viewModel.SettingId);
        if (existingDiff != null)
        {
            bool hasDiffValues = !string.IsNullOrEmpty(existingDiff.CurrentValueDisplay) && !string.IsNullOrEmpty(existingDiff.ConfigValueDisplay);
            bool hasAction = existingDiff.IsActionSetting && !string.IsNullOrEmpty(existingDiff.ActionConfirmationMessage);

            if (hasDiffValues)
            {
                var diffFormat = _localizationService.GetString("Review_Mode_Diff_Toggle") ?? "Current: {0} \u2192 Config: {1}";
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = string.Format(diffFormat, existingDiff.CurrentValueDisplay, existingDiff.ConfigValueDisplay);
            }

            if (hasAction && hasDiffValues)
            {
                viewModel.HasReviewAction = true;
                viewModel.ReviewActionMessage = existingDiff.ActionConfirmationMessage;
            }
            else if (hasAction)
            {
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = existingDiff.ActionConfirmationMessage;
            }

            if (existingDiff.IsReviewed)
            {
                if (existingDiff.IsApproved)
                    viewModel.IsReviewApproved = true;
                else
                    viewModel.IsReviewRejected = true;
            }

            if (existingDiff.IsActionReviewed)
            {
                if (existingDiff.IsActionApproved)
                    viewModel.IsReviewActionApproved = true;
                else
                    viewModel.IsReviewActionRejected = true;
            }

            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };

            viewModel.ReviewActionApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetActionApproval(viewModel.SettingId, approved);
            };
            return;
        }

        var (configItem, featureModuleId) = FindConfigItemForSetting(viewModel.SettingId, config);
        if (configItem == null)
        {
            return;
        }

        var (hasDiff, currentDisplay, configDisplay) = ComputeDiff(viewModel, configItem, currentState);

        if (hasDiff)
        {
            var diffFormat = _localizationService.GetString("Review_Mode_Diff_Toggle") ?? "Current: {0} \u2192 Config: {1}";
            viewModel.HasReviewDiff = true;
            viewModel.ReviewDiffMessage = string.Format(diffFormat, currentDisplay, configDisplay);
            viewModel.IsReviewApproved = false;

            var diff = new ConfigReviewDiff
            {
                SettingId = viewModel.SettingId,
                SettingName = viewModel.Name,
                FeatureModuleId = featureModuleId ?? string.Empty,
                CurrentValueDisplay = currentDisplay,
                ConfigValueDisplay = configDisplay,
                ConfigItem = configItem,
                IsApproved = false,
                InputType = viewModel.InputType
            };
            _configReviewDiffService.RegisterDiff(diff);

            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };
        }
    }

    private (ConfigurationItem? item, string? featureId) FindConfigItemForSetting(string settingId, UnifiedConfigurationFile config)
    {
        foreach (var feature in config.Optimize.Features)
        {
            var item = feature.Value.Items.FirstOrDefault(i => i.Id == settingId);
            if (item != null) return (item, feature.Key);
        }

        foreach (var feature in config.Customize.Features)
        {
            var item = feature.Value.Items.FirstOrDefault(i => i.Id == settingId);
            if (item != null) return (item, feature.Key);
        }

        return (null, null);
    }

    private (bool hasDiff, string currentDisplay, string configDisplay) ComputeDiff(
        SettingItemViewModel viewModel,
        ConfigurationItem configItem,
        SettingStateResult currentState)
    {
        var onText = _localizationService.GetString("Common_On") ?? "On";
        var offText = _localizationService.GetString("Common_Off") ?? "Off";

        switch (viewModel.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
                {
                    var currentBool = currentState.IsEnabled;
                    var configBool = configItem.IsSelected ?? false;
                    if (currentBool != configBool)
                    {
                        return (true, currentBool ? onText : offText, configBool ? onText : offText);
                    }
                    return (false, string.Empty, string.Empty);
                }

            case InputType.Selection:
                {
                    var currentIndex = viewModel.SelectedValue is int idx ? idx : -1;

                    if (configItem.PowerPlanGuid != null)
                        return (false, string.Empty, string.Empty);

                    if (configItem.CustomStateValues != null)
                    {
                        var currentDisplayName = GetComboBoxDisplayName(viewModel, currentIndex);
                        var configDisplayName = configItem.PowerPlanName ?? "Custom";
                        if (!string.Equals(currentDisplayName, configDisplayName, StringComparison.OrdinalIgnoreCase))
                            return (true, currentDisplayName, configDisplayName);
                        return (false, string.Empty, string.Empty);
                    }

                    if (configItem.SelectedIndex == null)
                        return (false, string.Empty, string.Empty);

                    var configIndex = configItem.SelectedIndex.Value;

                    if (currentIndex != configIndex)
                    {
                        var currentDisplayName = GetComboBoxDisplayName(viewModel, currentIndex);
                        var configDisplayName = GetComboBoxDisplayName(viewModel, configIndex);
                        return (true, currentDisplayName, configDisplayName);
                    }
                    return (false, string.Empty, string.Empty);
                }

            case InputType.NumericRange:
                {
                    var currentVal = currentState.CurrentValue is int cv ? cv : viewModel.NumericValue;
                    if (configItem.PowerSettings != null)
                    {
                        if (configItem.PowerSettings.TryGetValue("ACValue", out var acVal) && acVal is int acInt)
                        {
                            if (currentVal != acInt)
                                return (true, currentVal.ToString(), acInt.ToString());
                        }
                    }
                    return (false, string.Empty, string.Empty);
                }

            default:
                return (false, string.Empty, string.Empty);
        }
    }

    private static string GetComboBoxDisplayName(SettingItemViewModel viewModel, int index)
    {
        if (index >= 0 && index < viewModel.ComboBoxOptions.Count)
        {
            return viewModel.ComboBoxOptions[index].DisplayText ?? index.ToString();
        }
        return index.ToString();
    }
}
