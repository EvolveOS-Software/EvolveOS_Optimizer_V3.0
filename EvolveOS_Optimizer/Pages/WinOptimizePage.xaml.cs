// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Core.WinOptimize.Helpers;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media.Animation;
using IBulkSettingsActionService = EvolveOS_Optimizer.Core.Interfaces.IBulkSettingsActionService;
using ILocalizationService = EvolveOS_Optimizer.Core.Interfaces.ILocalizationService;
using IUserPreferencesService = EvolveOS_Optimizer.Core.Interfaces.IUserPreferencesService;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class WinOptimizePage : Page
{
    #region Fields & Properties
    private static readonly Dictionary<string, string> SectionIconResourceKeys = new()
    {
        { "Privacy", "PrivacyIconPath" },
        { "Power", "PowerIconPath" },
        { "Gaming", "GamingIconPath" },
        { "Update", "UpdateIconSymbol" },
        { "Notification", "NotificationIconPath" },
        { "Sound", "SoundIconSymbol" }
    };

    private static readonly Dictionary<string, string> SectionFeatureIds = new()
    {
        { "Privacy", FeatureIds.Privacy },
        { "Power", FeatureIds.Power },
        { "Gaming", FeatureIds.GamingPerformance },
        { "Update", FeatureIds.Update },
        { "Notification", FeatureIds.Notifications },
        { "Sound", FeatureIds.Sound }
    };

    private IUserPreferencesService? _userPreferencesService;
    private ILocalizationService? _localizationService;
    private IBulkSettingsActionService? _bulkSettingsActionService;

    private ISubscriptionToken? _settingAppliedSubscription;
    private ISubscriptionToken? _settingsRefreshedSubscription;

    private bool _isTechnicalDetailsVisible;
    private bool _isInfoBadgesVisible = true;
    private bool _isNewBadgesVisible = true;

    public OptimizeViewModel ViewModel { get; }
    #endregion

    #region Constructor & ViewModel Event Handlers
    public WinOptimizePage()
    {
        try
        {
            ErrorLogging.LogDebug("OptimizePage", "Constructor starting...");
            this.InitializeComponent();
            ErrorLogging.LogDebug("OptimizePage", "InitializeComponent done, getting ViewModel...");

            PageScrollHelper.Attach(this, OverviewScrollView);
            ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateBreadcrumbMenuItems();

            _userPreferencesService = App.Services.GetService<IUserPreferencesService>();
            _localizationService = App.Services.GetService<ILocalizationService>();
            _bulkSettingsActionService = App.Services.GetService<IBulkSettingsActionService>();

            ErrorLogging.LogDebug("OptimizePage", "ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug("OptimizePage", $"Constructor EXCEPTION: {ex}");
            throw;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.BreadcrumbRootText))
        {
            UpdateBreadcrumbMenuItems();
        }
    }

    private void UpdateBreadcrumbMenuItems()
    {
        SetFlyoutButtonText(FlyoutTextSound, FlyoutButtonSound, "Sound");
        SetFlyoutButtonText(FlyoutTextUpdate, FlyoutButtonUpdate, "Update");
        SetFlyoutButtonText(FlyoutTextNotification, FlyoutButtonNotification, "Notification");
        SetFlyoutButtonText(FlyoutTextPrivacy, FlyoutButtonPrivacy, "Privacy");
        SetFlyoutButtonText(FlyoutTextPower, FlyoutButtonPower, "Power");
        SetFlyoutButtonText(FlyoutTextGaming, FlyoutButtonGaming, "Gaming");
    }

    private void SetFlyoutButtonText(TextBlock textBlock, Button button, string sectionKey)
    {
        var displayName = ViewModel.GetSectionDisplayName(sectionKey);
        textBlock.Text = displayName;
        AutomationProperties.SetName(button, displayName);
    }
    #endregion

    #region Navigation & Lifecycle
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            base.OnNavigatedTo(e);

            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            var eventBus = App.Services.GetService<IEventBus>();
            if (eventBus != null)
            {
                _settingAppliedSubscription?.Dispose();
                _settingAppliedSubscription = eventBus.Subscribe<SettingAppliedEvent>(e =>
                {
                    DispatcherQueue.TryEnqueue(() => { UpdateOverviewBadgePills(); UpdateOverviewNewBadges(); });
                });

                _settingsRefreshedSubscription?.Dispose();
                _settingsRefreshedSubscription = eventBus.Subscribe<SettingsRefreshedEvent>(e =>
                {
                    DispatcherQueue.TryEnqueue(() => SyncViewStateToSettings());
                });
            }

            UpdateBreadcrumbMenuItems();

            ViewModel.CurrentSectionKey = "Overview";
            UpdateContentVisibility();

            ErrorLogging.LogDebug("OptimizePage", "Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();

            SetDropdownLabels();

            await InitializeTechnicalDetailsToggleAsync();
            await InitializeInfoBadgesAsync();
            await InitializeNewBadgesAsync();

            UpdateOverviewBadgePills();
            UpdateOverviewNewBadges();

            ErrorLogging.LogDebug("OptimizePage", "OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug("OptimizePage", $"OnNavigatedTo EXCEPTION: {ex}");
        }
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        await Purge();

        ViewModel.OnNavigatedFrom();
    }

    public async void NavigateToSection(string sectionKey, string? searchText = null)
    {
        Type? pageType = sectionKey switch
        {
            "Sound" => typeof(SoundOptimizePage),
            "Update" => typeof(UpdateOptimizePage),
            "Notification" => typeof(NotificationOptimizePage),
            "Privacy" => typeof(PrivacyOptimizePage),
            "Power" => typeof(PowerOptimizePage),
            "Gaming" => typeof(GamingOptimizePage),
            _ => null
        };

        if (pageType != null)
        {
            ViewModel.IsLoading = true;

            OverviewContent.Visibility = Visibility.Collapsed;
            InnerContentFrame.Visibility = Visibility.Collapsed;

            await Task.Delay(50);

            ViewModel.CurrentSectionKey = sectionKey;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var targetViewModel = ViewModel.GetSectionViewModel(sectionKey);
                targetViewModel?.ApplySearchFilter(searchText);
            }

            InnerContentFrame.Navigate(pageType, searchText, new SuppressNavigationTransitionInfo());

            await Task.Delay(50);

            UpdateContentVisibility();
            ViewModel.IsLoading = false;
        }
        else
        {
            NavigateToOverview();
        }
    }

    public async void NavigateToOverview()
    {
        ViewModel.IsLoading = true;

        OverviewContent.Visibility = Visibility.Collapsed;
        InnerContentFrame.Visibility = Visibility.Collapsed;

        await Task.Delay(50);

        ViewModel.CurrentSectionKey = "Overview";
        InnerContentFrame.Content = null;

        await Task.Delay(50);

        UpdateContentVisibility();
        UpdateOverviewBadgePills();
        UpdateOverviewNewBadges();

        ViewModel.IsLoading = false;
    }

    private void InnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        ViewModel.CurrentSectionKey = e.SourcePageType.Name switch
        {
            nameof(SoundOptimizePage) => "Sound",
            nameof(UpdateOptimizePage) => "Update",
            nameof(NotificationOptimizePage) => "Notification",
            nameof(PrivacyOptimizePage) => "Privacy",
            nameof(PowerOptimizePage) => "Power",
            nameof(GamingOptimizePage) => "Gaming",
            _ => "Overview"
        };
    }

    private void UpdateContentVisibility()
    {
        var isInDetailPage = ViewModel.IsInDetailPage;

        OverviewContent.Visibility = isInDetailPage ? Visibility.Collapsed : Visibility.Visible;
        InnerContentFrame.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;

        BreadcrumbSeparator.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;
        BreadcrumbSection.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;

        if (isInDetailPage)
        {
            BreadcrumbSectionText.Text = ViewModel.CurrentSectionName;
            AutomationProperties.SetName(BreadcrumbSection, ViewModel.CurrentSectionName);

            if (SectionIconResourceKeys.TryGetValue(ViewModel.CurrentSectionKey, out var resourceKey) &&
                Application.Current.Resources.TryGetValue(resourceKey, out var resourceValue) &&
                resourceValue is string iconData)
            {
                bool isSymbol = resourceKey.EndsWith("Symbol");

                BreadcrumbSectionIconBox.Visibility = isSymbol ? Visibility.Collapsed : Visibility.Visible;
                BreadcrumbSectionSymbol.Visibility = isSymbol ? Visibility.Visible : Visibility.Collapsed;

                if (isSymbol)
                {
                    if (Enum.TryParse<FluentIcons.Common.Icon>(iconData, ignoreCase: true, out var symbol))
                    {
                        BreadcrumbSectionSymbol.Icon = symbol;
                    }
                }
                else
                {
                    var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                        typeof(Microsoft.UI.Xaml.Media.Geometry), iconData);
                    BreadcrumbSectionIcon.Data = geometry;
                }
            }
        }
    }
    #endregion

    #region UI Event Handlers (Cards & Breadcrumbs)
    private void SoundCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Sound");
    }

    private void UpdateCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Update");
    }

    private void NotificationCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Notification");
    }

    private void PrivacyCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Privacy");
    }

    private void PowerCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Power");
    }

    private void GamingCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Gaming");
    }

    private void BreadcrumbOverview_Click(object sender, RoutedEventArgs e)
    {
        NavigateToOverview();
    }

    private void NavigateSound_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Sound");
    }

    private void NavigateUpdate_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Update");
    }

    private void NavigateNotification_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Notification");
    }

    private void NavigatePrivacy_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Privacy");
    }

    private void NavigatePower_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Power");
    }

    private void NavigateGaming_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Gaming");
    }
    #endregion

    #region Badge Management
    private void UpdateOverviewBadgePills()
    {
        UpdateFeatureOverviewPills(
            ViewModel.PrivacyViewModel,
            PrivacyOverviewBadges,
            PrivacyRecommendedPill, PrivacyRecommendedText,
            PrivacyDefaultPill, PrivacyDefaultText,
            PrivacyCustomPill, PrivacyCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.PowerViewModel,
            PowerOverviewBadges,
            PowerRecommendedPill, PowerRecommendedText,
            PowerDefaultPill, PowerDefaultText,
            PowerCustomPill, PowerCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.GamingViewModel,
            GamingOverviewBadges,
            GamingRecommendedPill, GamingRecommendedText,
            GamingDefaultPill, GamingDefaultText,
            GamingCustomPill, GamingCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.UpdateViewModel,
            UpdateOverviewPills,
            UpdateRecommendedPill, UpdateRecommendedText,
            UpdateDefaultPill, UpdateDefaultText,
            UpdateCustomPill, UpdateCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.NotificationViewModel,
            NotificationOverviewBadges,
            NotificationRecommendedPill, NotificationRecommendedText,
            NotificationDefaultPill, NotificationDefaultText,
            NotificationCustomPill, NotificationCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.SoundViewModel,
            SoundOverviewBadges,
            SoundRecommendedPill, SoundRecommendedText,
            SoundDefaultPill, SoundDefaultText,
            SoundCustomPill, SoundCustomText);
    }

    private void UpdateOverviewNewBadges()
    {
        UpdateFeatureNewBadge(ViewModel.PrivacyViewModel, PrivacyNewBadge, PrivacyNewText);
        UpdateFeatureNewBadge(ViewModel.PowerViewModel, PowerNewBadge, PowerNewText);
        UpdateFeatureNewBadge(ViewModel.GamingViewModel, GamingNewBadge, GamingNewText);
        UpdateFeatureNewBadge(ViewModel.UpdateViewModel, UpdateNewBadge, UpdateNewText);
        UpdateFeatureNewBadge(ViewModel.NotificationViewModel, NotificationNewBadge, NotificationNewText);
        UpdateFeatureNewBadge(ViewModel.SoundViewModel, SoundNewBadge, SoundNewText);
    }

    private void UpdateFeatureNewBadge(
        ISettingsFeatureViewModel feature,
        Border badge, TextBlock text)
    {
        if (!_isNewBadgesVisible)
        {
            badge.Visibility = Visibility.Collapsed;
            return;
        }

        var summary = FeatureBadgeAggregator.Aggregate(feature);
        if (summary.NewCount > 0)
        {
            badge.Visibility = Visibility.Visible;
            text.Text = $"{_localizationService?.GetString("Badge_New") ?? "NEW"} {summary.NewCount}";
        }
        else
        {
            badge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateFeatureOverviewPills(
        ISettingsFeatureViewModel feature,
        StackPanel container,
        Border recommendedPill, TextBlock recommendedText,
        Border defaultPill, TextBlock defaultText,
        Border customPill, TextBlock customText)
    {
        if (!_isInfoBadgesVisible)
        {
            container.Visibility = Visibility.Collapsed;
            return;
        }

        var summary = FeatureBadgeAggregator.Aggregate(feature);
        int total = summary.TotalWithBadgeData;
        bool showAny = false;

        if (_isInfoBadgesVisible && total > 0)
        {
            showAny = true;
            recommendedPill.Visibility = Visibility.Visible;
            recommendedText.Text = $"{_localizationService?.GetString("InfoBadge_Recommended") ?? "Recommended"} {summary.RecommendedCount}/{total}";
            recommendedPill.Opacity = summary.RecommendedCount > 0 ? 1.0 : 0.4;

            defaultPill.Visibility = Visibility.Visible;
            defaultText.Text = $"{_localizationService?.GetString("InfoBadge_Default") ?? "Default"} {summary.DefaultCount}/{total}";
            defaultPill.Opacity = summary.DefaultCount > 0 ? 1.0 : 0.4;

            if (summary.CustomCount > 0)
            {
                customPill.Visibility = Visibility.Visible;
                customText.Text = $"{_localizationService?.GetString("InfoBadge_Custom") ?? "Custom"} {summary.CustomCount}/{total}";
            }
            else
            {
                customPill.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            recommendedPill.Visibility = Visibility.Collapsed;
            defaultPill.Visibility = Visibility.Collapsed;
            customPill.Visibility = Visibility.Collapsed;
        }

        container.Visibility = showAny ? Visibility.Visible : Visibility.Collapsed;
    }
    #endregion

    #region View & UI Toggles Initialization
    private void SetDropdownLabels()
    {
        QuickActionsLabel.Label = _localizationService?.GetString("QuickActions_Menu") ?? "Quick Actions";
        ApplyRecommendedItem.Text = _localizationService?.GetString("QuickActions_ApplyRecommended") ?? "Apply Recommended Settings";
        ResetDefaultsItem.Text = _localizationService?.GetString("QuickActions_ResetDefaults") ?? "Reset to Windows Defaults";

        ApplyRecommendedIcon.Glyph = "\uE735";
        ResetDefaultsItem.Icon = new PathIcon
        {
            Data = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                typeof(Microsoft.UI.Xaml.Media.Geometry),
                (string)Application.Current.Resources["WindowsLogoIconPath"])
        };

        ViewMenuLabel.Label = _localizationService?.GetString("View_Menu") ?? "View";
        TechnicalDetailsToggleItem.Text = _localizationService?.GetString("View_TechnicalDetails") ?? "Technical Details";
        ToolTipService.SetToolTip(TechnicalDetailsToggleItem, _localizationService?.GetString("View_TechnicalDetails_Tooltip") ?? "Show or hide technical details for each setting");
        InfoBadgesToggleItem.Text = _localizationService?.GetString("View_InfoBadges") ?? "InfoBadges";
        ToolTipService.SetToolTip(InfoBadgesToggleItem, _localizationService?.GetString("View_InfoBadges_Tooltip") ?? "Show or hide status badges on settings cards");
        NewBadgesToggleItem.Text = _localizationService?.GetString("View_NewBadges") ?? "NEW Badges";
        ToolTipService.SetToolTip(NewBadgesToggleItem, _localizationService?.GetString("View_NewBadges_Tooltip") ?? "Show or hide NEW badges on settings added in this release");
    }

    private async Task InitializeTechnicalDetailsToggleAsync()
    {
        if (_userPreferencesService != null)
        {
            _isTechnicalDetailsVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowTechnicalDetails, false);
        }

        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsTechnicalDetailsGloballyVisible = _isTechnicalDetailsVisible;
            }
        }

        TechnicalDetailsToggleItem.IsChecked = _isTechnicalDetailsVisible;
    }

    private async Task InitializeInfoBadgesAsync()
    {
        if (_userPreferencesService != null)
        {
            _isInfoBadgesVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowInfoBadges, true);
        }

        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsInfoBadgeGloballyVisible = _isInfoBadgesVisible;
            }
        }

        InfoBadgesToggleItem.IsChecked = _isInfoBadgesVisible;
    }

    private async Task InitializeNewBadgesAsync()
    {
        if (_userPreferencesService != null)
        {
            _isNewBadgesVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowNewBadges, true);
        }

        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsNewBadgeGloballyVisible = _isNewBadgesVisible;
            }
        }

        NewBadgesToggleItem.IsChecked = _isNewBadgesVisible;
    }

    private void SyncViewStateToSettings()
    {
        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsInfoBadgeGloballyVisible = _isInfoBadgesVisible;
                setting.IsNewBadgeGloballyVisible = _isNewBadgesVisible;
                setting.IsTechnicalDetailsGloballyVisible = _isTechnicalDetailsVisible;
            }
        }

        UpdateOverviewBadgePills();
        UpdateOverviewNewBadges();
    }

    private async void ViewTechnicalDetails_Click(object sender, RoutedEventArgs e)
    {
        _isTechnicalDetailsVisible = TechnicalDetailsToggleItem.IsChecked;

        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsTechnicalDetailsGloballyVisible = _isTechnicalDetailsVisible;
            }
        }

        if (_userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync(
                UserPreferenceKeys.ShowTechnicalDetails, _isTechnicalDetailsVisible);
        }
    }

    private async void ViewInfoBadges_Click(object sender, RoutedEventArgs e)
    {
        _isInfoBadgesVisible = InfoBadgesToggleItem.IsChecked;

        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsInfoBadgeGloballyVisible = _isInfoBadgesVisible;
            }
        }

        if (_userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync(
                UserPreferenceKeys.ShowInfoBadges, _isInfoBadgesVisible);
        }

        UpdateOverviewBadgePills();
    }

    private async void ViewNewBadges_Click(object sender, RoutedEventArgs e)
    {
        _isNewBadgesVisible = NewBadgesToggleItem.IsChecked;

        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsNewBadgeGloballyVisible = _isNewBadgesVisible;
            }
        }

        if (_userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync(
                UserPreferenceKeys.ShowNewBadges, _isNewBadgesVisible);
        }

        UpdateOverviewBadgePills();
        UpdateOverviewNewBadges();
    }
    #endregion

    #region Quick Actions & Bulk Operations
    private async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteBulkActionAsync(BulkActionType.ApplyRecommended);
    }

    private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteBulkActionAsync(BulkActionType.ResetToDefaults);
    }

    private async Task ExecuteBulkActionAsync(BulkActionType actionType)
    {
        if (_bulkSettingsActionService == null) return;

        var settingIds = GetCurrentPageSettingIds();

        var count = await _bulkSettingsActionService.GetAffectedCountAsync(settingIds, actionType);
        if (count == 0) return;

        var confirmMessage = string.Format(
            _localizationService?.GetString("QuickActions_ConfirmMessage") ?? "This will change {0} settings on this page. Continue?",
            count);

        var dialog = new ContentDialog
        {
            Title = _localizationService?.GetString("QuickActions_ConfirmTitle") ?? "Confirm Action",
            Content = confirmMessage,
            PrimaryButtonText = "OK",
            CloseButtonText = _localizationService?.GetString("Button_Cancel") ?? "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        int applied = actionType == BulkActionType.ApplyRecommended
            ? await _bulkSettingsActionService.ApplyRecommendedAsync(settingIds)
            : await _bulkSettingsActionService.ResetToDefaultsAsync(settingIds);
    }

    private List<string> GetCurrentPageSettingIds()
    {
        var settingIds = new List<string>();
        var sectionsToInclude = ViewModel.IsInDetailPage
            ? OptimizeViewModel.Sections.Where(s => s.Key == ViewModel.CurrentSectionKey)
            : OptimizeViewModel.Sections;

        foreach (var section in sectionsToInclude)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                if (!string.IsNullOrEmpty(setting.SettingId))
                {
                    settingIds.Add(setting.SettingId);
                }
            }
        }

        return settingIds;
    }
    #endregion

    #region Search Box Event Handlers
    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestionItem suggestion)
        {
            NavigateToSection(suggestion.SectionKey, suggestion.SettingName);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestionItem suggestion)
        {
            NavigateToSection(suggestion.SectionKey, suggestion.SettingName);
        }
    }
    #endregion

    #region Purge Page
    public async Task Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _settingAppliedSubscription?.Dispose();
            _settingAppliedSubscription = null;

            _settingsRefreshedSubscription?.Dispose();
            _settingsRefreshedSubscription = null;

            InnerContentFrame.Content = null;

            await Task.Run(() =>
            {
                DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup();
            });
        }
        else
        {
            Debug.WriteLine($"[{this.GetType().Name}] State preserved in RAM cache.");
        }
    }
    #endregion
}