// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Automation.Peers;
using Windows.Globalization.NumberFormatting;
using AsyncRelayCommand = CommunityToolkit.Mvvm.Input.AsyncRelayCommand;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class SettingItemViewModel : BaseViewModel
{
    #region Fields
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService? _userPreferencesService;
    private readonly INewBadgeService? _newBadgeService;
    private readonly SettingStatusBannerManager _statusBannerManager;
    private readonly TechnicalDetailsManager _technicalDetailsManager;
    private readonly OSCompressionService _osCompressionService;
    private volatile bool _isUpdatingFromEvent;
    private bool _hasChangedThisSession;
    private object? _pendingValue;
    #endregion

    #region Core Properties
    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }

    public SettingDefinition? SettingDefinition { get; set; }

    [ObservableProperty]
    public partial string SettingId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial string GroupName { get; set; }

    [ObservableProperty]
    public partial string Icon { get; set; }

    [ObservableProperty]
    public partial string IconPack { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string? StatusBannerMessage { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity StatusBannerSeverity { get; set; }

    public bool HasStatusBanner => !string.IsNullOrEmpty(StatusBannerMessage);

    partial void OnStatusBannerMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusBanner));
    }

    [ObservableProperty]
    public partial InputType InputType { get; set; }

    [ObservableProperty]
    public partial object? SelectedValue { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ComboBoxDisplayOption> ComboBoxOptions { get; set; }

    [ObservableProperty]
    public partial int NumericValue { get; set; }

    [ObservableProperty]
    public partial int AcValue { get; set; }

    [ObservableProperty]
    public partial int DcValue { get; set; }

    [ObservableProperty]
    public partial int AcNumericValue { get; set; }

    [ObservableProperty]
    public partial int DcNumericValue { get; set; }

    [ObservableProperty]
    public partial bool HasBattery { get; set; }

    [ObservableProperty]
    public partial int MinValue { get; set; }

    [ObservableProperty]
    public partial int MaxValue { get; set; }

    [ObservableProperty]
    public partial string Units { get; set; }

    public string OnText { get; set; } = "On";
    public string OffText { get; set; } = "Off";
    public string ActionText => SettingDefinition?.ActionText ?? _localizationService?.GetString("Button_Apply") ?? "Apply";

    [ObservableProperty]
    public partial bool ShowPreferenceBadge { get; set; }

    [ObservableProperty]
    public partial bool ShowRecommendedBadge { get; set; }

    [ObservableProperty]
    public partial bool ShowDefaultBadge { get; set; }

    [ObservableProperty]
    public partial bool ShowCustomBadge { get; set; }

    private bool _isRestartRequired;
    public bool IsRestartRequired
    {
        get => _isRestartRequired;
        set => SetProperty(ref _isRestartRequired, value); // Assuming ViewModelBase has SetProperty
    }


    #endregion

    #region Technical Details Properties
    [ObservableProperty]
    public partial bool IsTechnicalDetailsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsTechnicalDetailsGloballyVisible { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<TechnicalDetailSection> TechnicalDetailSections { get; set; }
        = Array.Empty<TechnicalDetailSection>();

    public bool HasTechnicalDetails => TechnicalDetailSections.Count > 0;

    public bool ShowTechnicalDetailsBar => HasTechnicalDetails && IsTechnicalDetailsGloballyVisible;

    public Microsoft.UI.Xaml.CornerRadius TechnicalDetailsToggleCornerRadius =>
        IsTechnicalDetailsExpanded
            ? new Microsoft.UI.Xaml.CornerRadius(0)
            : new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4);

    partial void OnIsTechnicalDetailsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(TechnicalDetailsToggleCornerRadius));
    }

    partial void OnIsTechnicalDetailsGloballyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
        if (!value) IsTechnicalDetailsExpanded = false;
    }

    public string TechnicalDetailsLabel =>
        _localizationService.GetString("View_TechnicalDetails") ?? "Technical Details";

    public string OpenRegeditTooltip =>
        _localizationService.GetString("TechnicalDetails_OpenRegedit") ?? "Open in Registry Editor";

    public IRelayCommand<string> OpenRegeditCommand { get; }
    #endregion

    #region AI Explainer

    private string? _aiExplanation;
    public string? AiExplanation
    {
        get => _aiExplanation;
        set => SetProperty(ref _aiExplanation, value);
    }

    public bool IsAiReady => IsAiEnabled();

    public IAsyncRelayCommand FetchAiExplanationCommand { get; }

    private async Task FetchAiExplanationAsync()
    {
        string loadingText = ResourceString.GetString("ai_explainer_loading") ?? "Generating AI explanation...";

        if (!string.IsNullOrEmpty(AiExplanation) && AiExplanation != loadingText) return;

        AiExplanation = loadingText;

        try
        {
            AiExplanation = await AiExplainerService.ExplainGenericItemAsync(
                itemName: this.Name,
                itemCategory: "Windows configuration setting",
                contextDetails: this.Description ?? string.Empty
            );
        }
        catch (System.Exception ex)
        {
            ErrorLogging.LogDebug($"AI Error: {ex.Message}");
            AiExplanation = ResourceString.GetString("ai_explainer_error") ?? "Unable to load AI explanation.";
        }
    }

    public bool IsAiEnabled()
    {
        var activeProvider = LocalMachineSettingsEngine.ActiveAiProvider;
        return activeProvider switch
        {
            AiProvider.Groq => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GroqApiKey),
            AiProvider.Gemini => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GeminiApiKey),
            AiProvider.OpenRouter => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.OpenRouterApiKey),
            AiProvider.Cohere => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.CohereApiKey),
            AiProvider.Mistral => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.MistralApiKey),
            _ => false
        };
    }

    #endregion

    #region Visibility & Badges State
    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public string? CrossGroupInfoMessage { get; set; }

    [ObservableProperty]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial bool IsNewBadgeGloballyVisible { get; set; } = true;

    public string NewBadgeText => _localizationService.GetString("Badge_New") ?? "NEW";

    public bool ShowNewBadge => IsNew && IsNewBadgeGloballyVisible;

    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(ShowNewBadge));
    partial void OnIsNewBadgeGloballyVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowNewBadge));

    [ObservableProperty]
    public partial bool IsInfoBadgeGloballyVisible { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BadgePillState> BadgeRow { get; set; } = Array.Empty<BadgePillState>();

    public bool HasBadgeData { get; set; }

    public bool ShowInfoBadge => IsInfoBadgeGloballyVisible && HasBadgeData;

    partial void OnIsInfoBadgeGloballyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInfoBadge));
        OnPropertyChanged(nameof(ShowNumericQuickSetButtons));
        OnPropertyChanged(nameof(ShowToggleQuickSetButtons));
        OnPropertyChanged(nameof(ShowSelectionQuickSetButtons));
        OnPropertyChanged(nameof(ShowAcSelectionQuickSetButtons));
        OnPropertyChanged(nameof(ShowDcSelectionQuickSetButtons));
    }
    #endregion

    #region Numeric Quick Set & State
    public int? NumericRecommendedValue
    {
        get
        {
            if (SettingDefinition == null) return null;
            var pcfg = SettingDefinition.PowerCfgSettings?
                .FirstOrDefault(p => p.PowerModeSupport != PowerModeSupport.Separate);
            if (pcfg?.RecommendedValueAC is int rac) return rac;

            var reg = SettingDefinition.RegistrySettings?
                .FirstOrDefault(r => r.IsPrimary) ?? SettingDefinition.RegistrySettings?.FirstOrDefault();
            return TryConvertToInt(reg?.RecommendedValue);
        }
    }

    public int? NumericDefaultValue
    {
        get
        {
            if (SettingDefinition == null) return null;
            var pcfg = SettingDefinition.PowerCfgSettings?
                .FirstOrDefault(p => p.PowerModeSupport != PowerModeSupport.Separate);
            if (pcfg?.DefaultValueAC is int dac) return dac;

            var reg = SettingDefinition.RegistrySettings?
                .FirstOrDefault(r => r.IsPrimary) ?? SettingDefinition.RegistrySettings?.FirstOrDefault();
            return TryConvertToInt(reg?.DefaultValue);
        }
    }

    public int? AcRecommendedValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueAC;

    public int? AcDefaultValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueAC;

    public int? DcRecommendedValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueDC;

    public int? DcDefaultValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueDC;

    private static int? TryConvertToInt(object? value)
    {
        if (value == null) return null;
        try { return Convert.ToInt32(value); }
        catch { return null; }
    }

    private string FormatValueTooltip(string key, object value)
    {
        var template = _localizationService?.GetString(key);
        if (!string.IsNullOrEmpty(template))
            return template.Replace("{0}", value?.ToString() ?? string.Empty);
        return key == "InfoBadge_Numeric_SetToRecommended_Tooltip"
            ? $"Set to Recommended ({value})"
            : $"Set to Default ({value})";
    }

    public string RecommendedValueTooltip =>
       NumericRecommendedValue is int rec
           ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ConvertFromSystemUnits(rec))
           : string.Empty;

    public string DefaultValueTooltip =>
        NumericDefaultValue is int def
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ConvertFromSystemUnits(def))
            : string.Empty;

    public string RecommendedAcValueTooltip =>
        AcRecommendedValue is int rec
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ConvertFromSystemUnits(rec))
            : string.Empty;

    public string DefaultAcValueTooltip =>
        AcDefaultValue is int def
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ConvertFromSystemUnits(def))
            : string.Empty;

    public string RecommendedDcValueTooltip =>
        DcRecommendedValue is int rec
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ConvertFromSystemUnits(rec))
            : string.Empty;

    public string DefaultDcValueTooltip =>
        DcDefaultValue is int def
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ConvertFromSystemUnits(def))
            : string.Empty;


    public bool ShowNumericQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.NumericRange) return false;
            return NumericRecommendedValue.HasValue
                || NumericDefaultValue.HasValue
                || AcRecommendedValue.HasValue
                || AcDefaultValue.HasValue
                || DcRecommendedValue.HasValue
                || DcDefaultValue.HasValue;
        }
    }

    public IRelayCommand SetNumericToRecommendedCommand => _setNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (NumericRecommendedValue is int v)
            {
                var display = ConvertFromSystemUnits(v);
                NumericValue = display;
                HandleValueChangedAsync(display).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setNumericToRecommendedCommand;

    public IRelayCommand SetNumericToDefaultCommand => _setNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (NumericDefaultValue is int v)
            {
                var display = ConvertFromSystemUnits(v);
                NumericValue = display;
                HandleValueChangedAsync(display, resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setNumericToDefaultCommand;

    public IRelayCommand SetAcNumericToRecommendedCommand => _setAcNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (AcRecommendedValue is int v)
            {
                AcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcNumericToRecommendedCommand;

    public IRelayCommand SetAcNumericToDefaultCommand => _setAcNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (AcDefaultValue is int v)
            {
                AcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync(resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcNumericToDefaultCommand;

    public IRelayCommand SetDcNumericToRecommendedCommand => _setDcNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (DcRecommendedValue is int v)
            {
                DcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcNumericToRecommendedCommand;

    public IRelayCommand SetDcNumericToDefaultCommand => _setDcNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (DcDefaultValue is int v)
            {
                DcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync(resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcNumericToDefaultCommand;
    #endregion

    #region Toggle Quick Set & State
    private RegistrySetting? PrimaryRegistrySetting =>
        SettingDefinition?.RegistrySettings?.FirstOrDefault(r => r.IsPrimary)
        ?? SettingDefinition?.RegistrySettings?.FirstOrDefault();

    public bool? ToggleRecommendedState =>
        SettingDefinition is { } sd ? SettingDefinitionToggleState.GetRecommendedToggleState(sd) : null;

    public bool? ToggleDefaultState =>
        SettingDefinition is { } sd ? SettingDefinitionToggleState.GetDefaultToggleState(sd) : null;

    internal static bool? ToggleTargetState(object? targetValue, object?[]? enabledValue, object?[]? disabledValue) =>
        SettingDefinitionToggleState.ToggleTargetState(targetValue, enabledValue, disabledValue);

    private string ToggleStateText(bool state) => state ? OnText : OffText;

    public string ToggleRecommendedTooltip =>
        ToggleRecommendedState is bool s
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ToggleStateText(s))
            : string.Empty;

    public string ToggleDefaultTooltip =>
        ToggleDefaultState is bool s
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ToggleStateText(s))
            : string.Empty;

    public bool ShowToggleQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Toggle && InputType != InputType.CheckBox) return false;
            return ToggleRecommendedState.HasValue || ToggleDefaultState.HasValue;
        }
    }

    public IRelayCommand SetToggleToRecommendedCommand => _setToggleToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (ToggleRecommendedState is bool v)
                HandleToggleAsync(v).FireAndForget(_logService);
        });
    private RelayCommand? _setToggleToRecommendedCommand;

    public IRelayCommand SetToggleToDefaultCommand => _setToggleToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (ToggleDefaultState is bool v)
                HandleToggleAsync(v, resetToDefault: true).FireAndForget(_logService);
        });
    private RelayCommand? _setToggleToDefaultCommand;
    #endregion

    #region Action Quick Set & State

    public bool ShowActionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            return SettingId == "gaming-performance-os-compression";
        }
    }

    public IRelayCommand SetActionToRecommendedCommand => _setActionToRecommendedCommand ??= new RelayCommand(() =>
    {
        if (Status != null && Status.Contains("Status: Compressed"))
        {
            ExecuteActionCommand.Execute(null);
        }
    });
    private RelayCommand? _setActionToRecommendedCommand;

    public IRelayCommand SetActionToDefaultCommand => _setActionToDefaultCommand ??= new RelayCommand(() =>
    {
        if (Status != null && Status.Contains("Status: Compressed"))
        {
            ExecuteActionCommand.Execute(null);
        }
    });
    private RelayCommand? _setActionToDefaultCommand;

    #endregion

    #region Selection Quick Set & State
    private int? FindOptionIndex(Func<ComboBoxOption, bool> predicate)
    {
        var opts = SettingDefinition?.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (predicate(opts[i])) return i;
        return null;
    }

    public int? SelectionRecommendedIndex => FindOptionIndex(o => o.IsRecommended);
    public int? SelectionDefaultIndex => FindOptionIndex(o => o.IsDefault);

    private string? OptionDisplayText(int? index)
    {
        if (index is not int i) return null;
        if (ComboBoxOptions == null || i < 0 || i >= ComboBoxOptions.Count) return null;
        return ComboBoxOptions[i].DisplayText;
    }

    public string SelectionRecommendedTooltip =>
        OptionDisplayText(SelectionRecommendedIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", label)
            : string.Empty;

    public string SelectionDefaultTooltip =>
        OptionDisplayText(SelectionDefaultIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", label)
            : string.Empty;

    public bool ShowSelectionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Selection) return false;
            if (IsPowerPlanSetting) return false;
            if (SupportsSeparateACDC) return false;
            return SelectionRecommendedIndex.HasValue || SelectionDefaultIndex.HasValue;
        }
    }

    public IRelayCommand SetSelectionToRecommendedCommand => _setSelectionToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (SelectionRecommendedIndex is int i)
                HandleValueChangedAsync(i).FireAndForget(_logService);
        });
    private RelayCommand? _setSelectionToRecommendedCommand;

    public IRelayCommand SetSelectionToDefaultCommand => _setSelectionToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (SelectionDefaultIndex is int i)
                HandleValueChangedAsync(i, resetToDefault: true).FireAndForget(_logService);
        });
    private RelayCommand? _setSelectionToDefaultCommand;
    #endregion

    #region AC/DC Selection Quick Set & State
    private int? FindPowerCfgOptionIndex(int? targetValue)
    {
        if (targetValue is not int target) return null;
        var opts = SettingDefinition?.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
        {
            if (opts[i].ValueMappings is { } m && m.TryGetValue("PowerCfgValue", out var v) && v != null)
            {
                try { if (Convert.ToInt32(v) == target) return i; }
                catch { }
            }
        }
        return null;
    }

    public int? AcSelectionRecommendedIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueAC);

    public int? AcSelectionDefaultIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueAC);

    public int? DcSelectionRecommendedIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueDC);

    public int? DcSelectionDefaultIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueDC);

    public string AcSelectionRecommendedTooltip =>
        OptionDisplayText(AcSelectionRecommendedIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", label)
            : string.Empty;

    public string AcSelectionDefaultTooltip =>
        OptionDisplayText(AcSelectionDefaultIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", label)
            : string.Empty;

    public string DcSelectionRecommendedTooltip =>
        OptionDisplayText(DcSelectionRecommendedIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", label)
            : string.Empty;

    public string DcSelectionDefaultTooltip =>
        OptionDisplayText(DcSelectionDefaultIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", label)
            : string.Empty;

    public bool ShowAcSelectionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Selection) return false;
            if (SettingDefinition?.PowerCfgSettings?.Any() != true) return false;
            return AcSelectionRecommendedIndex.HasValue || AcSelectionDefaultIndex.HasValue;
        }
    }

    public bool ShowDcSelectionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Selection) return false;
            if (!SupportsSeparateACDC) return false;
            return DcSelectionRecommendedIndex.HasValue || DcSelectionDefaultIndex.HasValue;
        }
    }

    public IRelayCommand SetAcSelectionToRecommendedCommand => _setAcSelectionToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (AcSelectionRecommendedIndex is int i)
            {
                AcValue = i;
                HandleACDCSelectionChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcSelectionToRecommendedCommand;

    public IRelayCommand SetAcSelectionToDefaultCommand => _setAcSelectionToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (AcSelectionDefaultIndex is int i)
            {
                AcValue = i;
                HandleACDCSelectionChangedAsync(resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcSelectionToDefaultCommand;

    public IRelayCommand SetDcSelectionToRecommendedCommand => _setDcSelectionToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (DcSelectionRecommendedIndex is int i)
            {
                DcValue = i;
                HandleACDCSelectionChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcSelectionToRecommendedCommand;

    public IRelayCommand SetDcSelectionToDefaultCommand => _setDcSelectionToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (DcSelectionDefaultIndex is int i)
            {
                DcValue = i;
                HandleACDCSelectionChangedAsync(resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcSelectionToDefaultCommand;
    #endregion

    #region Advanced Unlock State
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public bool RequiresAdvancedUnlock => SettingDefinition?.RequiresAdvancedUnlock == true;
    public string ClickToUnlockText => _localizationService.GetString("Common_ClickToUnlock") ?? "Click to unlock";
    public IAsyncRelayCommand UnlockCommand { get; }
    #endregion

    #region Hierarchy & Feature State
    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    [ObservableProperty]
    public partial bool ParentIsEnabled { get; set; }

    partial void OnParentIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    public bool EffectiveIsEnabled => IsEnabled && ParentIsEnabled;
    public bool IsToggleType => InputType == InputType.Toggle;
    public bool IsSelectionType => InputType == InputType.Selection;
    public bool IsNumericType => InputType == InputType.NumericRange;
    public bool IsSliderType => InputType == InputType.NumericRange && SettingDefinition?.NumericRange?.UseSlider == true;
    public bool IsActionType => InputType == InputType.Action;
    public bool IsCheckBoxType => InputType == InputType.CheckBox;
    public bool IsSubSetting => !string.IsNullOrEmpty(SettingDefinition?.ParentSettingId);

    [ObservableProperty]
    public partial ObservableCollection<SettingItemViewModel>? Children { get; set; }

    public bool IsParentSetting => Children != null && Children.Count > 0;

    [ObservableProperty]
    public partial bool IsExpanderExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLastChild { get; set; }

    public Microsoft.UI.Xaml.CornerRadius ChildCornerRadius =>
        IsLastChild ? new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4) : new Microsoft.UI.Xaml.CornerRadius(0);

    partial void OnIsLastChildChanged(bool value) => OnPropertyChanged(nameof(ChildCornerRadius));

    public void ToggleExpander(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => IsExpanderExpanded = !IsExpanderExpanded;

    public bool IsPowerPlanSetting => InputType == InputType.Selection &&
        SettingDefinition?.Recommendation?.LoadDynamicOptions == true;

    public bool SupportsSeparateACDC =>
        SettingDefinition?.PowerCfgSettings?.Any(p =>
            p.PowerModeSupport == PowerModeSupport.Separate) == true;

    public string PluggedInText =>
        _localizationService.GetString("PowerStatus_PluggedIn") ?? "Plugged In";
    public string OnBatteryText =>
        _localizationService.GetString("PowerStatus_OnBattery") ?? "On Battery";

    public IAsyncRelayCommand ExecuteActionCommand { get; }
    #endregion

    #region Constructor
    public SettingItemViewModel(
        SettingItemViewModelConfig config,
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        OSCompressionService osCompressionService,
        IEventBus? eventBus = null,
        IUserPreferencesService? userPreferencesService = null,
        IRegeditLauncher? regeditLauncher = null,
        INewBadgeService? newBadgeService = null)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _osCompressionService = osCompressionService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;

        _localizationService.LanguageChanged += OnLanguageChanged;

        FetchAiExplanationCommand = new AsyncRelayCommand(FetchAiExplanationAsync);

        SettingDefinition = config.SettingDefinition;
        ParentFeatureViewModel = config.ParentFeatureViewModel;
        SettingId = config.SettingId;
        Name = config.Name;
        Description = config.Description;
        GroupName = config.GroupName;
        Icon = config.Icon;
        IconPack = config.IconPack;
        InputType = config.InputType;
        IsSelected = config.IsSelected;
        OnText = config.OnText;
        OffText = config.OffText;

        Status = string.Empty;
        ComboBoxOptions = new ObservableCollection<ComboBoxDisplayOption>();
        MaxValue = 100;
        Units = string.Empty;
        IsVisible = true;
        IsEnabled = true;
        ParentIsEnabled = true;

        ExecuteActionCommand = new AsyncRelayCommand(HandleActionAsync);
        UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);

        IsNew = _newBadgeService?.IsSettingNew(
            config.SettingDefinition?.AddedInVersion, config.SettingId) == true;

        _statusBannerManager = new SettingStatusBannerManager(localizationService);
        _technicalDetailsManager = new TechnicalDetailsManager(
            () => SettingId,
            newSections =>
            {
                TechnicalDetailSections = newSections;
                OnPropertyChanged(nameof(HasTechnicalDetails));
                OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
            },
            logService,
            dispatcherService,
            regeditLauncher,
            eventBus,
            _localizationService,
            new TechnicalDetailLabels
            {
                Path = _localizationService.GetString("TechnicalDetails_Path") ?? "Path",
                Value = _localizationService.GetString("TechnicalDetails_Value") ?? "Value",
                Current = _localizationService.GetString("TechnicalDetails_Current") ?? "Current",
                Recommended = _localizationService.GetString("TechnicalDetails_Recommended") ?? "Recommended",
                Default = _localizationService.GetString("TechnicalDetails_DefaultValue") ?? "Default",
                ValueNotExist = _localizationService.GetString("TechnicalDetails_ValueNotExist") ?? "doesn't exist",
                On = _localizationService.GetString("Common_On") ?? "On",
                Off = _localizationService.GetString("Common_Off") ?? "Off",
                SectionRegistry = _localizationService.GetString("TechnicalDetails_Section_Registry") ?? "Registry Changes",
                SectionScheduledTasks = _localizationService.GetString("TechnicalDetails_Section_ScheduledTasks") ?? "Scheduled Tasks",
                SectionPowerSettings = _localizationService.GetString("TechnicalDetails_Section_PowerSettings") ?? "Power Settings",
                SectionScripts = _localizationService.GetString("TechnicalDetails_Section_Scripts") ?? "PowerShell Scripts",
                SectionRegContent = _localizationService.GetString("TechnicalDetails_Section_RegContent") ?? "Registry Content",
                SectionDependencies = _localizationService.GetString("TechnicalDetails_Section_Dependencies") ?? "Depends On",
                ScriptOnEnable = _localizationService.GetString("TechnicalDetails_Script_OnEnable") ?? "On Enable",
                ScriptOnDisable = _localizationService.GetString("TechnicalDetails_Script_OnDisable") ?? "On Disable",
                RegContentOnEnable = _localizationService.GetString("TechnicalDetails_RegContent_OnEnable") ?? "On Enable",
                RegContentOnDisable = _localizationService.GetString("TechnicalDetails_RegContent_OnDisable") ?? "On Disable",
                DependencyEquals = _localizationService.GetString("TechnicalDetails_Dependency_Equals") ?? "=",
                DependencyNotEquals = _localizationService.GetString("TechnicalDetails_Dependency_NotEquals") ?? "≠",
                PowerCfgSubgroup = _localizationService.GetString("TechnicalDetails_PowerCfg_Subgroup") ?? "Subgroup",
                PowerCfgSetting = _localizationService.GetString("TechnicalDetails_PowerCfg_Setting") ?? "Setting"
            });
        OpenRegeditCommand = _technicalDetailsManager.OpenRegeditCommand;

        InitializeHasBadgeData();
        ComputeBadgeState();

        _ = RefreshCompressionStatusDetailsAsync();
    }
    #endregion

    #region State Synchronization
    public void UpdateVisibility(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            IsVisible = true;
            return;
        }

        IsVisible = Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(GroupName) && GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    public void UpdateStateFromEvent(bool isEnabled, object? value)
    {
        _isUpdatingFromEvent = true;
        try
        {
            if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
            {
                IsSelected = isEnabled;
            }
            else if (InputType == InputType.Selection)
            {
                if (SupportsSeparateACDC && value is System.Collections.Generic.Dictionary<string, object?> selDict)
                {
                    if (selDict.TryGetValue("ACValue", out var ac) && TryReadInt(ac, out var acIdx))
                        AcValue = acIdx;

                    if (HasBattery && selDict.TryGetValue("DCValue", out var dc) && TryReadInt(dc, out var dcIdx))
                        DcValue = dcIdx;
                }
                else if (value != null)
                {
                    SelectedValue = value;
                }
            }
            else if (InputType == InputType.NumericRange)
            {
                if (SupportsSeparateACDC && value is System.Collections.Generic.Dictionary<string, object?> numDict)
                {
                    if (numDict.TryGetValue("ACValue", out var ac) && TryReadInt(ac, out var acNum))
                        AcNumericValue = acNum;
                    if (HasBattery && numDict.TryGetValue("DCValue", out var dc) && TryReadInt(dc, out var dcNum))
                        DcNumericValue = dcNum;
                }
                else if (TryReadInt(value, out int intValue))
                {
                    NumericValue = intValue;
                }
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
            ComputeBadgeState();
        }
    }

    private static bool TryReadInt(object? value, out int result)
    {
        switch (value)
        {
            case int i: result = i; return true;
            case long l: result = (int)l; return true;
            case double d: result = (int)d; return true;
            case float f: result = (int)f; return true;
            case string s when int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed): result = parsed; return true;
            default:
                if (value != null)
                {
                    try { result = Convert.ToInt32(value); return true; }
                    catch { }
                }
                result = 0; return false;
        }
    }

    public void UpdateStateFromSystemState(SettingStateResult state)
    {
        if (!state.Success) return;
        _isUpdatingFromEvent = true;
        try
        {
            switch (InputType)
            {
                case InputType.Toggle:
                case InputType.CheckBox:
                    IsSelected = state.IsEnabled;
                    break;
                case InputType.Selection:
                    if (SupportsSeparateACDC && state.RawValues != null &&
                        SettingDefinition?.ComboBox?.Options is { } selectionOptions)
                    {
                        if (state.RawValues.TryGetValue("ACValue", out var acRaw) && acRaw != null)
                            AcValue = FindIndexForPowerCfgValue(selectionOptions, Convert.ToInt32(acRaw));
                        if (state.RawValues.TryGetValue("DCValue", out var dcRaw) && dcRaw != null)
                            DcValue = FindIndexForPowerCfgValue(selectionOptions, Convert.ToInt32(dcRaw));
                    }
                    else if (state.CurrentValue != null)
                    {
                        SelectedValue = state.CurrentValue;
                    }
                    break;
                case InputType.NumericRange:
                    if (SupportsSeparateACDC && state.RawValues != null)
                    {
                        if (state.RawValues.TryGetValue("ACValue", out var acNum) && TryReadInt(acNum, out int acInt))
                            AcNumericValue = ConvertFromSystemUnits(acInt);
                        if (state.RawValues.TryGetValue("DCValue", out var dcNum) && TryReadInt(dcNum, out int dcInt))
                            DcNumericValue = ConvertFromSystemUnits(dcInt);
                    }
                    else if (TryReadInt(state.CurrentValue, out int intValue))
                    {
                        NumericValue = ConvertFromSystemUnits(intValue);
                    }
                    break;
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
            ComputeBadgeState();
        }
    }

    private static int FindIndexForPowerCfgValue(IReadOnlyList<ComboBoxOption> options, int targetValue)
    {
        for (int i = 0; i < options.Count; i++)
        {
            var mapping = options[i].ValueMappings;
            if (mapping != null
                && mapping.TryGetValue("PowerCfgValue", out var val)
                && val != null
                && Convert.ToInt32(val) == targetValue)
            {
                return i;
            }
        }
        return 0;
    }

    private int ConvertFromSystemUnits(int systemValue)
    {
        var displayUnits = SettingDefinition?.NumericRange?.Units;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }
    #endregion

    #region UI Event Handlers

    public void OnToggleSwitchToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
            HandleToggleAsync(toggle.IsOn).FireAndForget(_logService);
    }

    public void OnCheckBoxClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            HandleToggleAsync(checkBox.IsChecked == true).FireAndForget(_logService);
    }

    public void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.FocusState == Microsoft.UI.Xaml.FocusState.Unfocused)
            return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxDisplayOption option)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(comboBox)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(comboBox);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.CurrentThenMostRecent,
                option.DisplayText,
                "ComboBoxSelection");
        }
    }

    public void OnComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedValue is { } value)
            HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void ApplySelectionValue(object value)
    {
        _logService.LogDebug($"[SettingItemViewModel] ApplySelectionValue called with value={value}, SettingId={SettingId}");
        HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void OnNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
            HandleValueChangedAsync((int)e.NewValue).FireAndForget(_logService);
    }

    public void OnSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            HandleValueChangedAsync((int)e.NewValue).FireAndForget(_logService);
        }
    }

    public void OnACComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            AcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnDCComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            DcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnACNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            AcNumericValue = (int)e.NewValue;
            HandleACDCNumericChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnDCNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            DcNumericValue = (int)e.NewValue;
            HandleACDCNumericChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnNumberBoxLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is NumberBox nb)
            nb.NumberFormatter = CreateInvariantNumberFormatter();
    }

    private static DecimalFormatter CreateInvariantNumberFormatter()
    {
        var formatter = new DecimalFormatter(new[] { "en-US" }, "US")
        {
            FractionDigits = 0,
            IsGrouped = false
        };
        return formatter;
    }

    #endregion

    #region Apply Logic

    protected virtual async Task HandleToggleAsync(bool newValue, bool resetToDefault = false)
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        if (newValue == IsSelected) return;

        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(newValue);
            if (!confirmed)
            {
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {newValue}");

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = newValue, ResetToDefault = resetToDefault, CheckboxResult = checkboxChecked });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' apply failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsSelected = newValue;
            _hasChangedThisSession = true;
            ComputeBadgeState();
            ShowRestartBannerIfNeeded();
            _logService.Log(LogLevel.Info, $"Successfully toggled setting {SettingId} to {newValue}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error toggling setting {SettingId}: {ex.Message}");
            OnPropertyChanged(nameof(IsSelected));
        }
        finally
        {
            IsApplying = false;
        }
    }

    protected virtual async Task HandleValueChangedAsync(object? value, bool resetToDefault = false)
    {
        _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync called: value={value}, IsApplying={IsApplying}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, SelectedValue={SelectedValue}");

        if (_isUpdatingFromEvent || SettingDefinition == null || value == null)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync early return: _isUpdatingFromEvent={_isUpdatingFromEvent}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, value={(value == null ? "null" : "not null")}");
            return;
        }

        if (IsApplying)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: queuing pending value {value} for {SettingId}");
            _pendingValue = value;
            return;
        }

        if (Equals(value, SelectedValue))
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: value equals SelectedValue, skipping");
            return;
        }

        _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: proceeding with value change");
        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(value);
            if (!confirmed)
            {
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Changing value for setting: {SettingId} to {value}");
            _logService.LogDebug($"[SettingItemViewModel] Calling ApplySettingAsync for {SettingId} with value={value}");

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = value, ResetToDefault = resetToDefault, CheckboxResult = checkboxChecked });

            _logService.LogDebug($"[SettingItemViewModel] ApplySettingAsync completed for {SettingId}");

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' value change failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            SelectedValue = value;

            if (value is int intValue)
            {
                NumericValue = intValue;

                if (intValue != ComboBoxConstants.CustomStateIndex)
                {
                    var customOption = ComboBoxOptions.FirstOrDefault(
                        o => o.Value is int v && v == ComboBoxConstants.CustomStateIndex);
                    if (customOption != null)
                        ComboBoxOptions.Remove(customOption);
                }
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
            UpdateStatusBanner(value);
            ShowRestartBannerIfNeeded();

            _logService.Log(LogLevel.Info, $"Successfully changed value for setting {SettingId}");
            _logService.LogDebug($"[SettingItemViewModel] SelectedValue set to {value} for {SettingId}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing value for setting {SettingId}: {ex.Message}");
            OnPropertyChanged(nameof(SelectedValue));
            OnPropertyChanged(nameof(NumericValue));
        }
        finally
        {
            IsApplying = false;
            await ProcessPendingValueAsync();
        }
    }

    private async Task ProcessPendingValueAsync()
    {
        var pending = _pendingValue;
        _pendingValue = null;

        if (pending != null && !Equals(pending, SelectedValue))
        {
            _logService.LogDebug($"[SettingItemViewModel] Processing pending value {pending} for {SettingId}");
            await HandleValueChangedAsync(pending);
        }
    }

    protected virtual async Task HandleACDCSelectionChangedAsync(bool resetToDefault = false)
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcValue, ["DCValue"] = DcValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC selection for setting: {SettingId} AC={AcValue}, DC={DcValue}");
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict, ResetToDefault = resetToDefault });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' AC/DC selection failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(AcValue));
                OnPropertyChanged(nameof(DcValue));
                return;
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
            ShowRestartBannerIfNeeded();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing AC/DC selection for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    protected virtual async Task HandleACDCNumericChangedAsync(bool resetToDefault = false)
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcNumericValue, ["DCValue"] = DcNumericValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC numeric for setting: {SettingId} AC={AcNumericValue}, DC={DcNumericValue}");
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict, ResetToDefault = resetToDefault });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' AC/DC numeric failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(AcNumericValue));
                OnPropertyChanged(nameof(DcNumericValue));
                return;
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
            ShowRestartBannerIfNeeded();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing AC/DC numeric for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    protected virtual async Task HandleActionAsync()
    {
        if (IsApplying || SettingDefinition == null) return;

        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(null);
            if (!confirmed)
                return;

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Executing action for setting: {SettingId}");

            await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = true,
                CheckboxResult = checkboxChecked,
                CommandString = SettingDefinition.ActionCommand,
                ApplyRecommended = checkboxChecked
            });

            _logService.Log(LogLevel.Info, $"Successfully executed action for setting {SettingId}");

            if (checkboxChecked && ParentFeatureViewModel != null)
            {
                _logService.Log(LogLevel.Info, $"Refreshing parent ViewModel after applying recommended settings for {SettingId}");
                await ParentFeatureViewModel.RefreshSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error executing action for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task<(bool confirmed, bool checkboxChecked)> HandleConfirmationIfNeededAsync(object? value)
    {
        if (SettingDefinition == null || !SettingDefinition.RequiresConfirmation)
            return (true, false);

        var title = _localizationService.GetString($"Setting_{SettingId}_ConfirmTitle");
        var message = _localizationService.GetString($"Setting_{SettingId}_ConfirmMessage");
        var checkboxText = _localizationService.GetString($"Setting_{SettingId}_ConfirmCheckbox");

        if (SettingId == SettingIds.ThemeModeWindows && value is int comboBoxIndex)
        {
            var themeMode = comboBoxIndex == 1
                ? _localizationService.GetString("Setting_theme-mode-windows_Option_1")
                : _localizationService.GetString("Setting_theme-mode-windows_Option_0");
            message = message.Replace("{themeMode}", themeMode);
            checkboxText = checkboxText.Replace("{themeMode}", themeMode);
        }

        var continueText = _localizationService.GetString("Button_Continue");
        var cancelText = _localizationService.GetString("Button_Cancel");

        return await _dialogService.ShowConfirmationWithCheckboxAsync(
            message,
            checkboxText,
            title,
            continueText,
            cancelText);
    }

    #endregion

    #region Advanced Unlock

    private async Task HandleUnlockAsync()
    {
        if (!IsLocked) return;

        var message = _localizationService.GetString("Dialog_AdvancedPowerWarning_Message");
        var checkboxText = _localizationService.GetString("Dialog_AdvancedPowerWarning_DontShowAgain");
        var title = _localizationService.GetString("Dialog_AdvancedPowerWarning_Title");
        var unlockText = _localizationService.GetString("Button_Unlock") ?? "Unlock";
        var cancelText = _localizationService.GetString("Button_Cancel") ?? "Cancel";

        var (confirmed, dontShowAgain) = await _dialogService.ShowConfirmationWithCheckboxAsync(
            message,
            checkboxText,
            title,
            unlockText,
            cancelText);

        if (!confirmed) return;

        IsLocked = false;
        _logService.Log(LogLevel.Info, $"Unlocked advanced setting: {SettingId}");

        if (dontShowAgain && _userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync("AdvancedPowerSettingsUnlocked", true);
            _logService.Log(LogLevel.Info, "User permanently unlocked advanced power settings");

            if (ParentFeatureViewModel != null)
            {
                foreach (var setting in ParentFeatureViewModel.Settings.OfType<SettingItemViewModel>())
                {
                    if (setting.RequiresAdvancedUnlock && setting != this)
                    {
                        setting.IsLocked = false;
                    }
                }
            }
        }
    }

    #endregion

    #region Status Banner

    public void InitializeCompatibilityBanner()
    {
        var banner = _statusBannerManager.GetCompatibilityBanner(SettingDefinition);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    public void UpdateStatusBanner(object? value)
    {
        var banner = _statusBannerManager.ComputeBannerForValue(SettingDefinition, value, CrossGroupInfoMessage);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    private void ShowRestartBannerIfNeeded()
    {
        var banner = _statusBannerManager.GetRestartBanner(SettingDefinition, _hasChangedThisSession);

        if (!banner.HasValue)
        {
            IsRestartRequired = false;
            return;
        }

        IsRestartRequired = true;
        ApplyBanner(banner.Value);
    }

    private void ApplyBanner(SettingStatusBannerManager.BannerState state)
    {
        StatusBannerMessage = state.Message;
        StatusBannerSeverity = state.Severity;
    }

    #endregion

    #region InfoBadge State Computation

    public void ComputeBadgeState()
    {
        if (!HasBadgeData || SettingDefinition == null)
            return;

        if (SettingId == "gaming-performance-os-compression") return;

        bool matchesRecommended = true;
        bool matchesDefault = true;

        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (isToggleLike)
        {
            if (SettingDefinition.RecommendedToggleState.HasValue
                && IsSelected != SettingDefinition.RecommendedToggleState.Value)
                matchesRecommended = false;
            if (SettingDefinition.DefaultToggleState.HasValue
                && IsSelected != SettingDefinition.DefaultToggleState.Value)
                matchesDefault = false;
        }

        foreach (var reg in SettingDefinition.RegistrySettings)
        {
            var (currentMatchesRecommended, currentMatchesDefault) = EvaluateRegistrySetting(reg);
            if (!currentMatchesRecommended) matchesRecommended = false;
            if (!currentMatchesDefault) matchesDefault = false;
        }

        foreach (var task in SettingDefinition.ScheduledTaskSettings)
        {
            if (task.RecommendedState.HasValue)
            {
                if (IsSelected != task.RecommendedState.Value)
                    matchesRecommended = false;
            }

            if (task.DefaultState.HasValue)
            {
                if (IsSelected != task.DefaultState.Value)
                    matchesDefault = false;
            }
        }

        if (SettingDefinition.PowerCfgSettings != null)
        {
            foreach (var pcfg in SettingDefinition.PowerCfgSettings)
            {
                if (pcfg.PowerModeSupport == PowerModeSupport.Separate)
                {
                    bool considerDc = HasBattery;

                    if (InputType == InputType.Selection)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && !PowerCfgIndexMatchesValue(AcValue, pcfg.RecommendedValueAC.Value))
                            matchesRecommended = false;
                        if (considerDc && pcfg.RecommendedValueDC.HasValue && !PowerCfgIndexMatchesValue(DcValue, pcfg.RecommendedValueDC.Value))
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && !PowerCfgIndexMatchesValue(AcValue, pcfg.DefaultValueAC.Value))
                            matchesDefault = false;
                        if (considerDc && pcfg.DefaultValueDC.HasValue && !PowerCfgIndexMatchesValue(DcValue, pcfg.DefaultValueDC.Value))
                            matchesDefault = false;
                    }
                    else if (InputType == InputType.NumericRange)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && AcNumericValue != ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value))
                            matchesRecommended = false;
                        if (considerDc && pcfg.RecommendedValueDC.HasValue && DcNumericValue != ConvertFromSystemUnits(pcfg.RecommendedValueDC.Value))
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && AcNumericValue != ConvertFromSystemUnits(pcfg.DefaultValueAC.Value))
                            matchesDefault = false;
                        if (considerDc && pcfg.DefaultValueDC.HasValue && DcNumericValue != ConvertFromSystemUnits(pcfg.DefaultValueDC.Value))
                            matchesDefault = false;
                    }
                }
                else
                {
                    if (InputType == InputType.NumericRange)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && NumericValue != ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value))
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && NumericValue != ConvertFromSystemUnits(pcfg.DefaultValueAC.Value))
                            matchesDefault = false;
                    }
                    else if (InputType == InputType.Selection)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && SelectedValue is int selVal && selVal != pcfg.RecommendedValueAC.Value)
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && SelectedValue is int selVal2 && selVal2 != pcfg.DefaultValueAC.Value)
                            matchesDefault = false;
                    }
                }
            }
        }

        var row = new List<BadgePillState>(capacity: 8);

        if (SettingDefinition.IsSubjectivePreference)
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Preference);
            row.Add(new BadgePillState(SettingBadgeKind.Preference, IsHighlighted: true, label, tooltip));
        }

        bool perModeBadges = SupportsSeparateACDC
            && HasBattery
            && SettingDefinition.PowerCfgSettings is { Count: > 0 } pcfgList
            && pcfgList[0].PowerModeSupport == PowerModeSupport.Separate;

        if (perModeBadges)
        {
            var pcfg = SettingDefinition.PowerCfgSettings![0];
            AddAcDcRecommendedPills(row, pcfg);
            AddAcDcDefaultPills(row, pcfg);
            AddAcDcCustomPills(row, pcfg);
        }
        else
        {
            if (HasAnyRecommendedData())
            {
                var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Recommended);
                row.Add(new BadgePillState(SettingBadgeKind.Recommended, IsHighlighted: matchesRecommended, label, tooltip));
            }

            if (HasAnyDefaultData())
            {
                var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Default);
                row.Add(new BadgePillState(SettingBadgeKind.Default, IsHighlighted: matchesDefault, label, tooltip));
            }

            bool isCustom = InputType switch
            {
                InputType.Selection => !IsKnownSelectionValue(),
                InputType.NumericRange => (HasAnyRecommendedData() || HasAnyDefaultData())
                    && !matchesRecommended && !matchesDefault,
                _ => false
            };
            var (cLabel, cTooltip) = ResolvePillStrings(SettingBadgeKind.Custom);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, IsHighlighted: isCustom, cLabel, cTooltip));
        }

        BadgeRow = row;
    }

    private void AddAcDcRecommendedPills(List<BadgePillState> row, PowerCfgSetting pcfg)
    {
        if (pcfg.RecommendedValueAC.HasValue)
        {
            bool match = InputType == InputType.NumericRange
                ? AcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value)
                : PowerCfgIndexMatchesValue(AcValue, pcfg.RecommendedValueAC.Value);
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Recommended, SettingBadgeMode.AC);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, label, tooltip, SettingBadgeMode.AC));
        }
        if (pcfg.RecommendedValueDC.HasValue)
        {
            bool match = InputType == InputType.NumericRange
                ? DcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueDC.Value)
                : PowerCfgIndexMatchesValue(DcValue, pcfg.RecommendedValueDC.Value);
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Recommended, SettingBadgeMode.DC);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, label, tooltip, SettingBadgeMode.DC));
        }
    }

    private void AddAcDcDefaultPills(List<BadgePillState> row, PowerCfgSetting pcfg)
    {
        if (pcfg.DefaultValueAC.HasValue)
        {
            bool match = InputType == InputType.NumericRange
                ? AcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueAC.Value)
                : PowerCfgIndexMatchesValue(AcValue, pcfg.DefaultValueAC.Value);
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Default, SettingBadgeMode.AC);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, label, tooltip, SettingBadgeMode.AC));
        }
        if (pcfg.DefaultValueDC.HasValue)
        {
            bool match = InputType == InputType.NumericRange
                ? DcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueDC.Value)
                : PowerCfgIndexMatchesValue(DcValue, pcfg.DefaultValueDC.Value);
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Default, SettingBadgeMode.DC);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, label, tooltip, SettingBadgeMode.DC));
        }
    }

    private void AddAcDcCustomPills(List<BadgePillState> row, PowerCfgSetting pcfg)
    {
        bool acCustom = false, dcCustom = false;

        if (InputType == InputType.Selection)
        {
            var options = SettingDefinition?.ComboBox?.Options;
            if (pcfg.RecommendedValueAC.HasValue || pcfg.DefaultValueAC.HasValue)
            {
                bool acRec = pcfg.RecommendedValueAC.HasValue && PowerCfgIndexMatchesValue(AcValue, pcfg.RecommendedValueAC.Value);
                bool acDef = pcfg.DefaultValueAC.HasValue && PowerCfgIndexMatchesValue(AcValue, pcfg.DefaultValueAC.Value);
                bool acOutOfRange = options != null && (AcValue < 0 || AcValue >= options.Count);
                acCustom = acOutOfRange || (!acRec && !acDef);
            }
            if (pcfg.RecommendedValueDC.HasValue || pcfg.DefaultValueDC.HasValue)
            {
                bool dcRec = pcfg.RecommendedValueDC.HasValue && PowerCfgIndexMatchesValue(DcValue, pcfg.RecommendedValueDC.Value);
                bool dcDef = pcfg.DefaultValueDC.HasValue && PowerCfgIndexMatchesValue(DcValue, pcfg.DefaultValueDC.Value);
                bool dcOutOfRange = options != null && (DcValue < 0 || DcValue >= options.Count);
                dcCustom = dcOutOfRange || (!dcRec && !dcDef);
            }
        }
        else if (InputType == InputType.NumericRange)
        {
            if (pcfg.RecommendedValueAC.HasValue || pcfg.DefaultValueAC.HasValue)
            {
                bool acRec = pcfg.RecommendedValueAC.HasValue && AcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value);
                bool acDef = pcfg.DefaultValueAC.HasValue && AcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueAC.Value);
                acCustom = !acRec && !acDef;
            }
            if (pcfg.RecommendedValueDC.HasValue || pcfg.DefaultValueDC.HasValue)
            {
                bool dcRec = pcfg.RecommendedValueDC.HasValue && DcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueDC.Value);
                bool dcDef = pcfg.DefaultValueDC.HasValue && DcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueDC.Value);
                dcCustom = !dcRec && !dcDef;
            }
        }

        if (pcfg.RecommendedValueAC.HasValue || pcfg.DefaultValueAC.HasValue)
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Custom, SettingBadgeMode.AC);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, acCustom, label, tooltip, SettingBadgeMode.AC));
        }
        if (pcfg.RecommendedValueDC.HasValue || pcfg.DefaultValueDC.HasValue)
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Custom, SettingBadgeMode.DC);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, dcCustom, label, tooltip, SettingBadgeMode.DC));
        }
    }

    private (bool matchesRecommended, bool matchesDefault) EvaluateRegistrySetting(RegistrySetting reg)
    {
        bool matchesRecommended = true;
        bool matchesDefault = true;

        if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
        {
            bool? recommendedState = SettingDefinition?.RecommendedToggleState
                ?? (reg.RecommendedValue == null
                    ? (bool?)null
                    : ToggleTargetState(reg.RecommendedValue, reg.EnabledValue, reg.DisabledValue));
            matchesRecommended = recommendedState == IsSelected;

            if (reg.IsGroupPolicy && reg.DefaultValue == null)
            {
                var gpDefaultState = ToggleTargetState(reg.DefaultValue, reg.EnabledValue, reg.DisabledValue);
                if (gpDefaultState.HasValue)
                    matchesDefault = gpDefaultState == IsSelected;
            }
            else if (IsKeyExistenceToggle(reg))
            {
                matchesDefault = IsSelected == true;
            }
            else
            {
                var defaultState = ToggleTargetState(reg.DefaultValue, reg.EnabledValue, reg.DisabledValue);
                matchesDefault = defaultState == IsSelected;
            }
        }
        else if (InputType == InputType.Selection)
        {
            var options = SettingDefinition?.ComboBox?.Options;
            if (options != null && SelectedValue is int currentIndex
                && currentIndex >= 0 && currentIndex < options.Count)
            {
                matchesRecommended = options.Any(o => o.IsRecommended) && options[currentIndex].IsRecommended;
                matchesDefault = options.Any(o => o.IsDefault) && options[currentIndex].IsDefault;
            }
            else
            {
                matchesRecommended = false;
                matchesDefault = false;
            }
        }
        else if (InputType == InputType.NumericRange)
        {
            if (reg.RecommendedValue != null)
                matchesRecommended = ValuesEqual(NumericValue, reg.RecommendedValue);
            else
                matchesRecommended = false;

            if (reg.DefaultValue != null)
                matchesDefault = ValuesEqual(NumericValue, reg.DefaultValue);
            else
                matchesDefault = false;
        }

        return (matchesRecommended, matchesDefault);
    }

    private static bool IsValueInArray(object value, object?[]? array)
    {
        if (array == null) return false;
        return array.Any(v => ValuesEqual(value, v));
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;

        try
        {
            var aVal = Convert.ToInt64(a);
            var bVal = Convert.ToInt64(b);
            return aVal == bVal;
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool PowerCfgIndexMatchesValue(int index, int targetPowerCfgValue)
    {
        var options = SettingDefinition?.ComboBox?.Options;
        if (options == null || index < 0 || index >= options.Count) return false;

        if (options[index].ValueMappings is { } mapping &&
            mapping.TryGetValue("PowerCfgValue", out var val) && val != null)
        {
            return Convert.ToInt32(val) == targetPowerCfgValue;
        }
        return false;
    }

    private bool HasAnyRecommendedData()
    {
        if (SettingDefinition is null) return false;

        if ((InputType == InputType.Toggle || InputType == InputType.CheckBox)
            && SettingDefinition.RecommendedToggleState.HasValue)
            return true;
        if (SettingDefinition.RegistrySettings.Any(r => r.RecommendedValue != null))
            return true;
        if (InputType == InputType.Selection
            && SettingDefinition.ComboBox?.Options?.Any(o => o.IsRecommended) == true)
            return true;
        if (SettingDefinition.ScheduledTaskSettings.Any(t => t.RecommendedState.HasValue))
            return true;
        if (SettingDefinition.PowerCfgSettings?.Any(
                p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true)
            return true;
        return false;
    }

    private static bool IsKeyExistenceToggle(RegistrySetting r) =>
        r.ValueName == null
        && r.EnabledValue == null
        && r.DisabledValue == null
        && r.ValueType == Microsoft.Win32.RegistryValueKind.None;

    private bool HasAnyDefaultData()
    {
        if (SettingDefinition is null) return false;
        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;

        if (isToggleLike && SettingDefinition.DefaultToggleState.HasValue)
            return true;

        if (SettingDefinition.RegistrySettings.Any(r =>
                (!(r.IsGroupPolicy && r.DefaultValue == null)
                 || (isToggleLike && ToggleTargetState(r.DefaultValue, r.EnabledValue, r.DisabledValue).HasValue))
                && (isToggleLike
                    ? IsKeyExistenceToggle(r)
                      || ToggleTargetState(r.DefaultValue, r.EnabledValue, r.DisabledValue).HasValue
                    : r.DefaultValue != null)))
            return true;
        if (InputType == InputType.Selection
            && SettingDefinition.ComboBox?.Options?.Any(o => o.IsDefault) == true)
            return true;
        if (SettingDefinition.ScheduledTaskSettings.Any(t => t.DefaultState.HasValue))
            return true;
        if (SettingDefinition.PowerCfgSettings?.Any(
                p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true)
            return true;
        return false;
    }

    private bool IsKnownSelectionValue()
    {
        if (InputType != InputType.Selection) return true;
        var options = SettingDefinition?.ComboBox?.Options;
        if (options == null || options.Count == 0) return true;

        if (SupportsSeparateACDC)
            return AcValue >= 0 && AcValue < options.Count
                && DcValue >= 0 && DcValue < options.Count;
        return SelectedValue is int idx && idx >= 0 && idx < options.Count;
    }

    private (string Label, string Tooltip) ResolvePillStrings(SettingBadgeKind kind, SettingBadgeMode mode = SettingBadgeMode.None)
    {
        var (baseLabel, tooltip) = kind switch
        {
            SettingBadgeKind.Recommended => (
                _localizationService?.GetString("InfoBadge_Recommended") ?? "Recommended",
                _localizationService?.GetString("InfoBadge_Recommended_Tooltip") ?? "Winhance's recommended value"),
            SettingBadgeKind.Default => (
                _localizationService?.GetString("InfoBadge_Default") ?? "Default",
                _localizationService?.GetString("InfoBadge_Default_Tooltip") ?? "Windows factory value"),
            SettingBadgeKind.Custom => (
                _localizationService?.GetString("InfoBadge_Custom") ?? "Custom",
                _localizationService?.GetString("InfoBadge_Custom_Tooltip") ?? "Custom value (not a known option)"),
            SettingBadgeKind.Preference => (
                _localizationService?.GetString("InfoBadge_Preference") ?? "Preference",
                _localizationService?.GetString("InfoBadge_Preference_Tooltip") ?? "Personal preference"),
            _ => ("", ""),
        };

        var label = mode switch
        {
            SettingBadgeMode.AC => $"{baseLabel} (AC)",
            SettingBadgeMode.DC => $"{baseLabel} (DC)",
            _ => baseLabel,
        };
        return (label, tooltip);
    }

    private void InitializeHasBadgeData()
    {
        if (SettingDefinition == null)
        {
            HasBadgeData = false;
            return;
        }

        bool isToggleLike = SettingDefinition.InputType == InputType.Toggle
            || SettingDefinition.InputType == InputType.CheckBox;
        bool hasRegistryData = SettingDefinition.RegistrySettings.Any(r =>
            r.RecommendedValue != null
            || (isToggleLike
                ? ToggleTargetState(r.DefaultValue, r.EnabledValue, r.DisabledValue).HasValue
                : r.DefaultValue != null));

        if (isToggleLike && SettingDefinition.RecommendedToggleState.HasValue)
            hasRegistryData = true;

        bool hasSelectionOptionData = SettingDefinition.InputType == InputType.Selection
            && SettingDefinition.ComboBox?.Options?.Any(o => o.IsRecommended || o.IsDefault) == true;

        bool hasTaskData = SettingDefinition.ScheduledTaskSettings.Any(t =>
            t.RecommendedState.HasValue || t.DefaultState.HasValue);

        bool hasPowerCfgData = SettingDefinition.PowerCfgSettings?.Any(p =>
            p.RecommendedValueAC.HasValue || p.DefaultValueAC.HasValue) == true;

        HasBadgeData = hasRegistryData || hasSelectionOptionData || hasTaskData || hasPowerCfgData;
    }

    #endregion

    #region Technical Details

    public void ToggleTechnicalDetails() => IsTechnicalDetailsExpanded = !IsTechnicalDetailsExpanded;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(NewBadgeText));
        OnPropertyChanged(nameof(TechnicalDetailsLabel));
        OnPropertyChanged(nameof(OpenRegeditTooltip));
        OnPropertyChanged(nameof(ClickToUnlockText));
        OnPropertyChanged(nameof(PluggedInText));
        OnPropertyChanged(nameof(OnBatteryText));
        OnPropertyChanged(nameof(RecommendedValueTooltip));
        OnPropertyChanged(nameof(DefaultValueTooltip));
        OnPropertyChanged(nameof(RecommendedAcValueTooltip));
        OnPropertyChanged(nameof(DefaultAcValueTooltip));
        OnPropertyChanged(nameof(RecommendedDcValueTooltip));
        OnPropertyChanged(nameof(DefaultDcValueTooltip));
        OnPropertyChanged(nameof(ToggleRecommendedTooltip));
        OnPropertyChanged(nameof(ToggleDefaultTooltip));
        OnPropertyChanged(nameof(SelectionRecommendedTooltip));
        OnPropertyChanged(nameof(SelectionDefaultTooltip));
        OnPropertyChanged(nameof(AcSelectionRecommendedTooltip));
        OnPropertyChanged(nameof(AcSelectionDefaultTooltip));
        OnPropertyChanged(nameof(DcSelectionRecommendedTooltip));
        OnPropertyChanged(nameof(DcSelectionDefaultTooltip));
    }

    public async Task RefreshCompressionStatusDetailsAsync()
    {
        if (SettingId != "gaming-performance-os-compression") return;

        var status = await _osCompressionService.GetCompressionStatusAsync();
        bool isCompressed = status.Contains("is in the Compact state", StringComparison.OrdinalIgnoreCase);

        _dispatcherService.RunOnUIThread(() =>
        {
            Status = isCompressed ? "Status: Compressed" : "Status: Uncompressed";

            var badgeList = new List<BadgePillState>();

            var (pLabel, pTooltip) = ResolvePillStrings(SettingBadgeKind.Preference);
            var (rLabel, rTooltip) = ResolvePillStrings(SettingBadgeKind.Recommended);
            var (dLabel, dTooltip) = ResolvePillStrings(SettingBadgeKind.Default);
            var (cLabel, cTooltip) = ResolvePillStrings(SettingBadgeKind.Custom);

            badgeList.Add(new BadgePillState(SettingBadgeKind.Preference, IsHighlighted: false, pLabel, pTooltip));
            badgeList.Add(new BadgePillState(SettingBadgeKind.Recommended, IsHighlighted: !isCompressed, rLabel, rTooltip));
            badgeList.Add(new BadgePillState(SettingBadgeKind.Default, IsHighlighted: !isCompressed, dLabel, dTooltip));
            badgeList.Add(new BadgePillState(SettingBadgeKind.Custom, IsHighlighted: isCompressed, cLabel, cTooltip));

            HasBadgeData = true;
            BadgeRow = badgeList;
            OnPropertyChanged(nameof(ShowInfoBadge));

            var row = new TechnicalDetailRow
            {
                RowType = DetailRowType.Registry,
                PathLabel = "System",
                RegistryPath = "CompactOS",
                ValueLabel = "Status",
                ValueName = "Current State",
                CurrentLabel = "Info",
                CurrentValue = isCompressed ? "Compressed" : "Uncompressed",
                RecommendedValue = "Uncompressed",
                DefaultValue = "Uncompressed"
            };

            var newSection = new TechnicalDetailSection(
                DetailRowType.Registry,
                "Compression Status",
                true,
                new List<TechnicalDetailRow> { row }
            );

            var updatedSections = TechnicalDetailSections.ToList();
            updatedSections.Add(newSection);
            TechnicalDetailSections = updatedSections;
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _technicalDetailsManager.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}