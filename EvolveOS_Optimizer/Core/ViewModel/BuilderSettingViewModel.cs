// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Extensions.DependencyInjection;

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

    [ObservableProperty]
    public partial bool IsStaged { get; set; }

    public bool? StagedIsSelected { get; set; }
    public int? StagedSelectedValue { get; set; }
    public int? StagedNumericValue { get; set; }

    public object? CustomValue { get; set; }
    public object? StagedCustomValue { get; set; }

    [RelayCommand]
    public void AcceptStaged()
    {
        if (StagedIsSelected.HasValue) IsSelected = StagedIsSelected.Value;
        if (StagedSelectedValue.HasValue) SelectedValue = StagedSelectedValue.Value;
        if (StagedNumericValue.HasValue) NumericValue = StagedNumericValue.Value;
        if (StagedCustomValue != null) CustomValue = StagedCustomValue;

        ClearStaged();
    }

    [RelayCommand]
    public void RejectStaged()
    {
        ClearStaged();
    }

    private void ClearStaged()
    {
        StagedIsSelected = null;
        StagedSelectedValue = null;
        StagedNumericValue = null;
        StagedCustomValue = null;
        IsStaged = false;
    }

    #endregion

    #region TOTAL OFFLINE SANDBOX (OVERRIDES)
    // By overriding all these methods, we guarantee that clicking anything in the Builder 
    // ONLY updates the UI and Export button, without ever calling _settingApplicationService!

    protected override async Task HandleValueChangedAsync(object? value, bool resetToDefault = false)
    {
        SelectedValue = value;
        if (value is int intValue) NumericValue = intValue; // Sync numeric if applicable

        ComputeBadgeState();
        App.Services.GetService<ProfileBuilderViewModel>()?.EvaluateExportState();
        await Task.CompletedTask;
    }

    protected override async Task HandleToggleAsync(bool newValue, bool resetToDefault = false)
    {
        IsSelected = newValue;

        ComputeBadgeState();
        App.Services.GetService<ProfileBuilderViewModel>()?.EvaluateExportState();
        await Task.CompletedTask;
    }

    protected override async Task HandleACDCSelectionChangedAsync(bool resetToDefault = false)
    {
        // AcValue and DcValue are already set by the UI event handlers before this is called
        ComputeBadgeState();
        App.Services.GetService<ProfileBuilderViewModel>()?.EvaluateExportState();
        await Task.CompletedTask;
    }

    protected override async Task HandleACDCNumericChangedAsync(bool resetToDefault = false)
    {
        // AcNumericValue and DcNumericValue are already set by the UI event handlers
        ComputeBadgeState();
        App.Services.GetService<ProfileBuilderViewModel>()?.EvaluateExportState();
        await Task.CompletedTask;
    }

    protected override async Task HandleActionAsync()
    {
        // Action buttons (like OS Compression) should do absolutely nothing in an offline builder profile
        await Task.CompletedTask;
    }
    #endregion
}