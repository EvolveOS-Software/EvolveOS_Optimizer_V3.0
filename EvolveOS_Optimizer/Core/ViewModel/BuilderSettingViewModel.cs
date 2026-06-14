// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class BuilderSettingViewModel : SettingItemViewModel
{
    public BuilderSettingViewModel(SettingItemViewModelConfig config,
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        OSCompressionService osCompressionService)
        : base(config, settingApplicationService, logService, dispatcherService, dialogService, localizationService, osCompressionService)
    {
        IsInfoBadgeGloballyVisible = true;
    }

    #region Staged Import / Preview Mode

    // Triggers the UI badge to appear
    [ObservableProperty]
    public partial bool IsStaged { get; set; }

    // Holding variables for the proposed changes
    public bool? StagedIsSelected { get; set; }
    public int? StagedSelectedValue { get; set; }
    public int? StagedNumericValue { get; set; }

    public object? CustomValue { get; set; } // The actual live value
    public object? StagedCustomValue { get; set; }

    [RelayCommand]
    public void AcceptStaged()
    {
        // Move the staged values into the live UI values
        if (StagedIsSelected.HasValue) IsSelected = StagedIsSelected.Value;
        if (StagedSelectedValue.HasValue) SelectedValue = StagedSelectedValue.Value;
        if (StagedNumericValue.HasValue) NumericValue = StagedNumericValue.Value;
        if (StagedCustomValue != null) CustomValue = StagedCustomValue;

        ClearStaged();
    }

    [RelayCommand]
    public void RejectStaged()
    {
        // Discard the proposed changes
        ClearStaged();
    }

    private void ClearStaged()
    {
        StagedIsSelected = null;
        StagedSelectedValue = null;
        StagedNumericValue = null;
        StagedCustomValue = null;
        IsStaged = false; // Hides the UI badge
    }

    #endregion

    // OVERRIDE: We override the application logic so it doesn't touch the Registry
    // When the user changes a setting, it now just updates the VM state without a system call
    protected async Task HandleValueChangedAsync(object? value, bool resetToDefault = false)
    {
        // In the builder, we just update the local property. 
        // We do NOT call _settingApplicationService.ApplySettingAsync()
        SelectedValue = value;
        ComputeBadgeState();
        await Task.CompletedTask;
    }
}