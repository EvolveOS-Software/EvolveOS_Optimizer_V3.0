// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Automation.Peers;

namespace EvolveOS_Optimizer.Core.Controls;

public sealed partial class SettingsCardItem : UserControl
{
    #region Dependency Properties & Fields
    public static readonly DependencyProperty SettingProperty =
        DependencyProperty.Register(
            nameof(Setting),
            typeof(SettingItemViewModel),
            typeof(SettingsCardItem),
            new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    private SettingItemViewModel? _subscribedSetting;
    #endregion

    #region Constructor & Lifecycle
    public SettingsCardItem()
    {
        this.InitializeComponent();
        Unloaded += (_, _) => UnsubscribeFromSetting();
    }

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCardItem control)
        {
            control.UnsubscribeFromSetting();
            if (e.NewValue is SettingItemViewModel vm)
            {
                control._subscribedSetting = vm;
                vm.PropertyChanged += control.OnSettingPropertyChanged;
            }
        }
    }

    private void UnsubscribeFromSetting()
    {
        if (_subscribedSetting != null)
        {
            _subscribedSetting.PropertyChanged -= OnSettingPropertyChanged;
            _subscribedSetting = null;
        }
    }
    #endregion

    #region Property Changed & Automation Logic
    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SettingItemViewModel vm) return;

        if (e.PropertyName == nameof(SettingItemViewModel.IsApplying))
        {
            var peer = FrameworkElementAutomationPeer.FromElement(this)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);

            if (vm.IsApplying)
            {
                peer?.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    $"Applying {vm.Name}",
                    "SettingApplying");
            }
            else
            {
                var stateText = GetSettingStateText(vm);
                peer?.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    $"{vm.Name} is now {stateText}",
                    "SettingApplied");
            }
        }
        else if (e.PropertyName == nameof(SettingItemViewModel.IsReviewApproved) && vm.IsReviewApproved)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(this)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                $"{vm.Name}: Apply",
                "ReviewApproved");
        }
        else if (e.PropertyName == nameof(SettingItemViewModel.IsReviewRejected) && vm.IsReviewRejected)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(this)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                $"{vm.Name}: Don't apply",
                "ReviewRejected");
        }
    }

    private static string GetSettingStateText(SettingItemViewModel vm)
    {
        return vm.InputType switch
        {
            InputType.Toggle or InputType.CheckBox => vm.IsSelected ? vm.OnText : vm.OffText,
            InputType.Selection => vm.ComboBoxOptions
                ?.FirstOrDefault(o => Equals(o.Value, vm.SelectedValue))?.DisplayText
                ?? vm.SelectedValue?.ToString() ?? "changed",
            InputType.NumericRange => vm.NumericValue.ToString(),
            _ => "applied"
        };
    }
    #endregion

    #region Specialized Control Events
    private void OnPowerPlanComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not PowerPlanComboBox comboBox)
            return;

        var settingVm = comboBox.Tag as SettingItemViewModel;
        if (settingVm == null)
            return;

        var powerViewModel = settingVm.ParentFeatureViewModel as PowerOptimizationsViewModel;

        try
        {
            var localizationService = App.Services.GetService<ILocalizationService>();
            if (localizationService != null)
            {
                comboBox.ActiveBadgeText = localizationService.GetString("PowerPlan_Active_Badge");
                comboBox.DeleteTooltipText = localizationService.GetString("PowerPlan_Delete_Tooltip");
                comboBox.ExistsTooltipText = localizationService.GetString("PowerPlan_Status_Exists");
                comboBox.NotExistsTooltipText = localizationService.GetString("PowerPlan_Status_NotExists");
            }
        }
        catch
        {
            // Use default
        }

        comboBox.DeleteRequested += (s, plan) =>
        {
            powerViewModel?.DeletePowerPlanCommand.Execute(plan);
        };

        comboBox.DropDownClosed += (s, value) =>
        {
            settingVm.ApplySelectionValue(value);
        };
    }
    #endregion
}