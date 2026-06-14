// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Model.Profiles;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Core.ViewModel.Builder;

public partial class ProfileBuilderViewModel : ObservableObject
{
    #region Fields & Dependencies

    private readonly IPowerPlanComboBoxService _powerPlanService;
    private readonly ICompatibleSettingsRegistry _settingsRegistry;
    private readonly IFileSystemService _fileSystemService;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly OSCompressionService _osCompressionService;

    private ObservableCollection<BuilderFeatureCategory> _categories;
    private BuilderFeatureCategory? _selectedCategory;

    #endregion

    #region Properties

    public ObservableCollection<BuilderFeatureCategory> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public BuilderFeatureCategory? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public IAsyncRelayCommand SeedFromCurrentSystemCommand { get; }

    #endregion

    #region Constructor

    public ProfileBuilderViewModel(
        ICompatibleSettingsRegistry settingsRegistry,
        IFileSystemService fileSystemService,
        ISystemSettingsDiscoveryService discoveryService,
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        OSCompressionService osCompressionService,
        IPowerPlanComboBoxService powerPlanService)
    {
        _settingsRegistry = settingsRegistry;
        _fileSystemService = fileSystemService;
        _discoveryService = discoveryService;
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _osCompressionService = osCompressionService;
        _powerPlanService = powerPlanService;

        _categories = new ObservableCollection<BuilderFeatureCategory>();

        SeedFromCurrentSystemCommand = new AsyncRelayCommand(SeedFromCurrentSystemAsync);

        _ = InitializeCategoriesAsync();
    }

    #endregion

    #region Initialization

    private async Task InitializeCategoriesAsync()
    {
        var featureDefinitions = new[]
        {
        (Id: FeatureIds.Privacy, Name: "Privacy", Glyph: "\uE72E"),
        (Id: FeatureIds.Power, Name: "Power", Glyph: "\uE7E6"),
        (Id: FeatureIds.GamingPerformance, Name: "Gaming", Glyph: "\uE7FC"),
        (Id: FeatureIds.Update, Name: "Update", Glyph: "\uE895"),
        (Id: FeatureIds.Notifications, Name: "Notifications", Glyph: "\uEA8F"),
        (Id: FeatureIds.Sound, Name: "Sound", Glyph: "\uE767"),
        (Id: FeatureIds.WindowsTheme, Name: "Theme", Glyph: "\uE771"),
        (Id: FeatureIds.StartMenu, Name: "Start Menu", Glyph: "\uE718"),
        (Id: FeatureIds.Taskbar, Name: "Taskbar", Glyph: "\uE90E"),
        (Id: FeatureIds.ExplorerCustomization, Name: "Explorer", Glyph: "\uEC50")
    };

        foreach (var def in featureDefinitions)
        {
            var category = new BuilderFeatureCategory(def.Id, def.Name, def.Glyph);
            var settings = _settingsRegistry.GetFilteredSettings(def.Id);

            foreach (var setting in settings)
            {
                var config = new SettingItemViewModelConfig
                {
                    SettingDefinition = setting,
                    SettingId = setting.Id,
                    Name = setting.Name,
                    Description = setting.Description ?? string.Empty,
                    GroupName = setting.GroupName ?? "General",
                    InputType = setting.InputType
                };

                var vm = new BuilderSettingViewModel(config, _settingApplicationService, _logService,
                    _dispatcherService, _dialogService, _localizationService, _osCompressionService);

                vm.IsLocked = setting.RequiresAdvancedUnlock;

                if (setting.InputType == Enums.InputType.Selection)
                {
                    if (setting.Recommendation?.LoadDynamicOptions == true)
                    {
                        var result = await _powerPlanService.SetupPowerPlanComboBoxAsync(setting, null);

                        foreach (var opt in result.Options)
                        {
                            vm.ComboBoxOptions.Add(opt);
                        }

                        if (result.SelectedValue != null)
                        {
                            vm.SelectedValue = result.SelectedValue;
                        }
                    }
                    else if (setting.ComboBox?.Options != null)
                    {
                        int index = 0;
                        object? fallbackValue = null;

                        foreach (var opt in setting.ComboBox.Options)
                        {
                            string localizedText = _localizationService.GetString(opt.DisplayName);
                            if (string.IsNullOrEmpty(localizedText)) localizedText = opt.DisplayName;

                            fallbackValue ??= index;
                            vm.ComboBoxOptions.Add(new ComboBoxDisplayOption(localizedText, index));

                            if (opt.IsDefault) vm.SelectedValue = index;

                            index++;
                        }

                        if (vm.SelectedValue == null && fallbackValue != null)
                        {
                            vm.SelectedValue = fallbackValue;
                        }
                    }
                }

                if (setting.InputType == Enums.InputType.NumericRange && setting.NumericRange != null)
                {
                    vm.MinValue = setting.NumericRange.MinValue;
                    vm.MaxValue = setting.NumericRange.MaxValue;
                }

                category.Settings.Add(vm);
            }

            if (category.Settings.Count > 0)
                Categories.Add(category);
        }

        SelectedCategory = Categories.FirstOrDefault();
    }

    #endregion

    #region Commands & Operations

    private async Task SeedFromCurrentSystemAsync()
    {
        var allRegisteredSettings = _settingsRegistry.GetAllFilteredSettings()
            .SelectMany(kvp => kvp.Value)
            .ToDictionary(s => s.Id);

        var settingsToDiscover = Categories
            .SelectMany(c => c.Settings)
            .Select(s => allRegisteredSettings.TryGetValue(s.SettingId, out var def) ? def : null)
            .Where(s => s != null)
            .ToList();

        var systemStates = await _discoveryService.GetSettingStatesAsync(settingsToDiscover!);

        foreach (var category in Categories)
        {
            foreach (var builderItem in category.Settings.OfType<BuilderSettingViewModel>())
            {
                if (systemStates.TryGetValue(builderItem.SettingId, out var state))
                {
                    builderItem.UpdateStateFromSystemState(state);
                }
            }
        }
    }

    #region Search

    private ObservableCollection<SearchSuggestionItem> _searchSuggestions = new();
    public ObservableCollection<SearchSuggestionItem> SearchSuggestions
    {
        get => _searchSuggestions;
        set => SetProperty(ref _searchSuggestions, value);
    }

    public void UpdateSearchSuggestions(string query)
    {
        SearchSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var category in Categories)
                foreach (var setting in category.Settings)
                    setting.IsVisible = true;
            return;
        }

        foreach (var category in Categories)
        {
            foreach (var setting in category.Settings)
            {
                bool matches = setting.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               setting.Description.Contains(query, StringComparison.OrdinalIgnoreCase);

                setting.IsVisible = matches;

                if (matches)
                {
                    SearchSuggestions.Add(new SearchSuggestionItem
                    {
                        SettingName = setting.Name,
                        SectionDisplayName = category.DisplayName,
                        SectionKey = category.FeatureId
                    });
                }
            }
        }
    }
    #endregion

    #region Quick Actions
    public void ApplyAllRecommended()
    {
        foreach (var category in Categories)
        {
            foreach (var setting in category.Settings.OfType<BuilderSettingViewModel>())
            {
                if (setting.IsToggleType || setting.IsCheckBoxType)
                {
                    if (setting.ToggleRecommendedState.HasValue)
                        setting.IsSelected = setting.ToggleRecommendedState.Value;
                }
                else if (setting.IsSelectionType)
                {
                    if (setting.SelectionRecommendedIndex.HasValue)
                        setting.SelectedValue = setting.SelectionRecommendedIndex.Value;
                }
                else if (setting.IsNumericType || setting.IsSliderType)
                {
                    if (setting.NumericRecommendedValue.HasValue)
                        setting.NumericValue = setting.NumericRecommendedValue.Value;
                }
            }
        }
    }

    public void ApplyAllDefaults()
    {
        foreach (var category in Categories)
        {
            foreach (var setting in category.Settings.OfType<BuilderSettingViewModel>())
            {
                if (setting.IsToggleType || setting.IsCheckBoxType)
                {
                    if (setting.ToggleDefaultState.HasValue)
                        setting.IsSelected = setting.ToggleDefaultState.Value;
                }
                else if (setting.IsSelectionType)
                {
                    if (setting.SelectionDefaultIndex.HasValue)
                        setting.SelectedValue = setting.SelectionDefaultIndex.Value;
                }
                else if (setting.IsNumericType || setting.IsSliderType)
                {
                    if (setting.NumericDefaultValue.HasValue)
                        setting.NumericValue = setting.NumericDefaultValue.Value;
                }
            }
        }
    }
    #endregion

    public void PurgeProfile()
    {
        foreach (var category in Categories)
        {
            foreach (var setting in category.Settings.OfType<BuilderSettingViewModel>())
            {
                if (setting.IsToggleType || setting.IsCheckBoxType)
                {
                    setting.IsSelected = false;
                }

                if (setting.IsSelectionType && setting.ComboBoxOptions.Count > 0)
                {
                    var defaultOpt = setting.ComboBoxOptions.FirstOrDefault(o => o.IsDefault);
                    setting.SelectedValue = defaultOpt != null ? defaultOpt.Value : setting.ComboBoxOptions[0].Value;
                }

                if (setting.IsNumericType || setting.IsSliderType)
                {
                    setting.NumericValue = setting.NumericDefaultValue ?? setting.MinValue;
                }
            }
        }
    }

    public void SaveProfile(string filePath)
    {
        var profile = new EvolveOSProfile();

        foreach (var category in Categories)
        {
            var profileFeature = new ProfileFeature();

            foreach (var builderItem in category.Settings.OfType<BuilderSettingViewModel>())
            {
                var profileSetting = new ProfileSettingItem
                {
                    Id = builderItem.SettingId,
                    IsEnabled = builderItem.IsToggleType ? builderItem.IsSelected : null,
                    SelectedIndex = builderItem.IsSelectionType ? (int?)builderItem.SelectedValue : null,
                    NumericValue = (builderItem.IsNumericType || builderItem.IsSliderType) ? builderItem.NumericValue : null,
                    CustomValue = builderItem.CustomValue
                };

                profileFeature.Settings.Add(profileSetting);
            }

            if (FeatureDefinitions.OptimizeFeatures.Contains(category.FeatureId))
                profile.Optimize.Features[category.FeatureId] = profileFeature;
            else
                profile.Customize.Features[category.FeatureId] = profileFeature;
        }

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(profile, jsonOptions);
        _fileSystemService.WriteAllText(filePath, json);
    }

    public void LoadProfile(string filePath, bool applyImmediately)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var json = _fileSystemService.ReadAllText(filePath);
        var profile = JsonSerializer.Deserialize<EvolveOSProfile>(json, jsonOptions);

        if (profile == null) throw new Exception("Invalid profile format.");

        var allProfileFeatures = new Dictionary<string, ProfileFeature>();

        if (profile.Optimize?.Features != null)
        {
            foreach (var kvp in profile.Optimize.Features) allProfileFeatures[kvp.Key] = kvp.Value;
        }
        if (profile.Customize?.Features != null)
        {
            foreach (var kvp in profile.Customize.Features) allProfileFeatures[kvp.Key] = kvp.Value;
        }

        foreach (var category in Categories)
        {
            if (allProfileFeatures.TryGetValue(category.FeatureId, out var profileFeature))
            {
                var profileSettingsDict = profileFeature.Settings.ToDictionary(s => s.Id);

                foreach (var builderItem in category.Settings.OfType<BuilderSettingViewModel>())
                {
                    if (profileSettingsDict.TryGetValue(builderItem.SettingId, out var savedSetting))
                    {
                        if (applyImmediately)
                        {
                            // --- LIVE APPLY MODE ---
                            if (builderItem.IsToggleType || builderItem.IsCheckBoxType)
                            {
                                if (savedSetting.IsEnabled.HasValue) builderItem.IsSelected = savedSetting.IsEnabled.Value;
                            }
                            else if (builderItem.IsSelectionType)
                            {
                                if (savedSetting.SelectedIndex.HasValue) builderItem.SelectedValue = savedSetting.SelectedIndex.Value;
                            }
                            else if (builderItem.IsNumericType || builderItem.IsSliderType)
                            {
                                if (savedSetting.NumericValue.HasValue) builderItem.NumericValue = (int)savedSetting.NumericValue.Value;
                            }

                            if (savedSetting.CustomValue != null)
                            {
                                builderItem.CustomValue = savedSetting.CustomValue;
                            }
                        }
                        else
                        {
                            // --- PREVIEW / STAGED MODE ---
                            if (builderItem.IsToggleType || builderItem.IsCheckBoxType)
                            {
                                if (savedSetting.IsEnabled.HasValue) builderItem.StagedIsSelected = savedSetting.IsEnabled.Value;
                            }
                            else if (builderItem.IsSelectionType)
                            {
                                if (savedSetting.SelectedIndex.HasValue) builderItem.StagedSelectedValue = savedSetting.SelectedIndex.Value;
                            }
                            else if (builderItem.IsNumericType || builderItem.IsSliderType)
                            {
                                if (savedSetting.NumericValue.HasValue) builderItem.StagedNumericValue = (int)savedSetting.NumericValue.Value; // <-- ADDED (int)
                            }

                            if (savedSetting.CustomValue != null)
                            {
                                builderItem.StagedCustomValue = savedSetting.CustomValue;
                            }

                            // Trigger the badge on the UI!
                            builderItem.IsStaged = true;
                        }
                    }
                }
            }
        }
    }

    #endregion
}