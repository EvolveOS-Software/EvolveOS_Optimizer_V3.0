using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Navigation;
using System.Threading;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class GroupPolicyPage : Page
{
    private CancellationTokenSource? _cancellationTokenSource;
    private IReadOnlyList<GroupPolicyHelper.PolicyState>? _policyStates;
    private string? _pendingScrollTarget;

    public List<PolicyStateViewModel> ConfiguredPolicies => _policyStates?
        .Where(s => s.IsConfigured)
        .Select(s => new PolicyStateViewModel(s))
        .OrderBy(p => p.Policy.Category)
        .ThenBy(p => p.Policy.Name)
        .ToList() ?? new List<PolicyStateViewModel>();

    public GroupPolicyPage()
    {
        InitializeComponent();

        ErrorLogging.LogDebug(new Exception(ResourceString.GetString("Initializing GroupPolicyPage")));

        NavigationCacheMode = NavigationCacheMode.Required;
        Loaded += GroupPolicyPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
        {
            _pendingScrollTarget = optionTag;
        }
    }

    private async void GroupPolicyPage_Loaded(object sender, RoutedEventArgs e)
    {
        ConfiguredPoliciesListView.SelectionChanged += ConfiguredPoliciesListView_SelectionChanged;

        await ScanPoliciesAsync();

        if (!string.IsNullOrEmpty(_pendingScrollTarget))
        {
            await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
            _pendingScrollTarget = null;
        }
    }

    private void ConfiguredPoliciesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RemoveSelectedButton.IsEnabled = ConfiguredPoliciesListView.SelectedItems.Count > 0;
    }

    private async Task ScanPoliciesAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            ScanProgressRing.Visibility = Visibility.Visible;
            ScanProgressRing.IsActive = true;
            SummaryText.Text = ResourceString.GetString("GroupPolicyPage_ScanningPolicies");
            RefreshButton.IsEnabled = false;
            RemoveAllButton.IsEnabled = false;

            _policyStates = await GroupPolicyHelper.DetectPolicyStatesAsync(_cancellationTokenSource.Token);

            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateSummary();
                UpdateCategorySummary();
                UpdateConfiguredPoliciesList();
            });
        }
        catch (OperationCanceledException)
        {
            // Scan was cancelled
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);

            DispatcherQueue.TryEnqueue(() =>
            {
                SummaryText.Text = ResourceString.GetString("GroupPolicyPage_ScanError");
            });
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ScanProgressRing.Visibility = Visibility.Collapsed;
                ScanProgressRing.IsActive = false;
                RefreshButton.IsEnabled = true;
            });
        }
    }

    private void UpdateSummary()
    {
        if (_policyStates == null)
            return;

        var configuredCount = _policyStates.Count(s => s.IsConfigured);
        var totalCount = _policyStates.Count;

        if (configuredCount == 0)
        {
            SummaryText.Text = ResourceString.GetString("GroupPolicyPage_NoPoliciesDetected");
            RemoveAllButton.IsEnabled = false;
        }
        else
        {
            SummaryText.Text = string.Format(
                ResourceString.GetString("GroupPolicyPage_ConfiguredPoliciesCount"),
                configuredCount,
                totalCount);
            RemoveAllButton.IsEnabled = true;
        }
    }

    private void UpdateCategorySummary()
    {
        if (_policyStates == null)
            return;

        var categoryGroups = _policyStates
            .GroupBy(s => s.Policy.Category)
            .Select(g => new CategorySummaryItem
            {
                Category = g.Key,
                TotalCount = g.Count(),
                ConfiguredCount = g.Count(s => s.IsConfigured),
                IconGlyph = GetCategoryIcon(g.Key)
            })
            .OrderByDescending(c => c.ConfiguredCount)
            .ThenBy(c => c.Category)
            .ToList();

        CategorySummaryRepeater.ItemsSource = categoryGroups;
    }

    private void UpdateConfiguredPoliciesList()
    {
        if (_policyStates == null)
            return;

        var policies = ConfiguredPolicies;

        if (policies.Count == 0)
        {
            ConfiguredPoliciesListView.Visibility = Visibility.Collapsed;
            NoPoliciesPanel.Visibility = Visibility.Visible;
            ConfiguredPoliciesListView.ItemsSource = null;
        }
        else
        {
            ConfiguredPoliciesListView.Visibility = Visibility.Visible;
            NoPoliciesPanel.Visibility = Visibility.Collapsed;

            ConfiguredPoliciesListView.ItemsSource = policies;
        }
    }

    private static string GetCategoryIcon(string category)
    {
        return category switch
        {
            "Windows Update" => "\uE777",
            "Privacy & Telemetry" => "\uE72E",
            "Cortana & Search" => "\uE721",
            "Windows Store" => "\uE719",
            "OneDrive" => "\uE753",
            "Security" => "\uE72E",
            "Error Reporting" => "\uE783",
            "System Restore" => "\uE777",
            "Windows Insider" => "\uF1AD",
            "Input & Privacy" => "\uE765",
            "App Privacy" => "\uE71D",
            "Windows Ink" => "\uE929",
            "Biometrics" => "\uE928",
            "Location" => "\uE81D",
            "Find My Device" => "\uE707",
            "Messaging" => "\uE715",
            "Clipboard" => "\uE77F",
            "Speech" => "\uE720",
            "Activity History" => "\uE823",
            "Gaming" => "\uE7FC",
            "Widgets & Feeds" => "\uE71B",
            "Copilot" => "\uE946",
            "Windows Recall" => "\uE946",
            "Microsoft Edge" => "\uE774",
            "File History" => "\uE8F1",
            "Search" => "\uE721",
            "Start Menu" => "\uE80F",
            _ => "\uE713"
        };
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ScanPoliciesAsync();
    }

    private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_policyStates == null)
            return;

        var configuredPolicies = _policyStates.Where(s => s.IsConfigured).ToList();
        if (configuredPolicies.Count == 0)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("GroupPolicyPage_ConfirmRemoveAllTitle"),
            Content = string.Format(
                ResourceString.GetString("GroupPolicyPage_ConfirmRemoveAllContent"),
                configuredPolicies.Count),
            PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_Remove"),
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            CloseButtonText = ResourceString.GetString("Cancel")
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        await RemovePoliciesAsync(configuredPolicies.Select(s => s.Policy));
    }

    private async void CategoryRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string category)
            return;

        if (_policyStates == null)
            return;

        var categoryPolicies = _policyStates
            .Where(s => s.IsConfigured && s.Policy.Category == category)
            .ToList();

        if (categoryPolicies.Count == 0)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("GroupPolicyPage_ConfirmRemoveCategoryTitle"),
            Content = string.Format(
                ResourceString.GetString("GroupPolicyPage_ConfirmRemoveCategoryContent"),
                categoryPolicies.Count,
                category),
            PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_Remove"),
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            CloseButtonText = ResourceString.GetString("Cancel")
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        await RemovePoliciesAsync(categoryPolicies.Select(s => s.Policy));
    }

    private async void PolicyRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string policyId)
            return;

        if (_policyStates == null)
            return;

        var policy = _policyStates.FirstOrDefault(s => s.Policy.Id == policyId);
        if (policy == null)
            return;

        await RemovePoliciesAsync([policy.Policy]);
    }

    private async void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ConfiguredPoliciesListView.SelectedItems
            .OfType<PolicyStateViewModel>()
            .ToList();

        if (selectedItems.Count == 0)
            return;

        // Show confirmation dialog
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("GroupPolicyPage_ConfirmRemoveSelectedTitle"),
            Content = string.Format(
                ResourceString.GetString("GroupPolicyPage_ConfirmRemoveSelectedContent"),
                selectedItems.Count),
            PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_Remove"),
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            CloseButtonText = ResourceString.GetString("Cancel")
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        await RemovePoliciesAsync(selectedItems.Select(s => s.Policy));
    }

    private async Task RemovePoliciesAsync(IEnumerable<GroupPolicyHelper.PolicyEntry> policies)
    {
        var policyList = policies.ToList();
        if (policyList.Count == 0)
            return;

        try
        {
            ScanProgressRing.Visibility = Visibility.Visible;
            ScanProgressRing.IsActive = true;
            SummaryText.Text = ResourceString.GetString("GroupPolicyPage_RemovingPolicies");
            RefreshButton.IsEnabled = false;
            RemoveAllButton.IsEnabled = false;

            var (succeeded, failed) = await GroupPolicyHelper.RemovePolicyOverridesAsync(policyList);

            if (succeeded > 0)
            {
                var restartDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                    Title = ResourceString.GetString("GroupPolicyPage_RestartExplorerTitle"),
                    Content = ResourceString.GetString("GroupPolicyPage_RestartExplorerContent"),
                    PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_RestartNow"),
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    CloseButtonText = ResourceString.GetString("GroupPolicyPage_Later")
                };

                var restartResult = await restartDialog.ShowAsync();
                if (restartResult == ContentDialogResult.Primary)
                {
                    await GroupPolicyHelper.RestartExplorerAsync();
                }
            }

            await ScanPoliciesAsync();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
        finally
        {
            ScanProgressRing.Visibility = Visibility.Collapsed;
            ScanProgressRing.IsActive = false;
            RefreshButton.IsEnabled = true;
        }
    }
}

public sealed class CategorySummaryItem
{
    public required string Category { get; init; }
    public required int TotalCount { get; init; }
    public required int ConfiguredCount { get; init; }
    public required string IconGlyph { get; init; }

    public string? StatusText => ConfiguredCount == 0
        ? ResourceString.GetString("GroupPolicyPage_NotConfigured")
        : string.Format(ResourceString.GetString("GroupPolicyPage_ConfiguredCount"), ConfiguredCount);

    public SolidColorBrush StatusColor => ConfiguredCount == 0
        ? new SolidColorBrush(Colors.Green)
        : (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];

    public bool HasConfiguredPolicies => ConfiguredCount > 0;

    public string? ButtonText => ResourceString.GetString("GroupPolicyPage_RemoveOverrides");
}

public sealed class PolicyStateViewModel
{
    private readonly GroupPolicyHelper.PolicyState _state;

    public PolicyStateViewModel(GroupPolicyHelper.PolicyState state)
    {
        _state = state;
    }

    public GroupPolicyHelper.PolicyEntry Policy => _state.Policy;

    public string HiveDisplay => _state.Policy.Hive switch
    {
        Microsoft.Win32.RegistryHive.LocalMachine => "HKLM",
        Microsoft.Win32.RegistryHive.CurrentUser => "HKCU",
        _ => _state.Policy.Hive.ToString()
    };

    public string CurrentValueDisplay
    {
        get
        {
            if (_state.CurrentValue == null)
                return ResourceString.GetString("Not set");

            return _state.ActualValueKind switch
            {
                Microsoft.Win32.RegistryValueKind.DWord => $"{ResourceString.GetString("Value")}: {_state.CurrentValue}",
                Microsoft.Win32.RegistryValueKind.String => $"{ResourceString.GetString("Value")}: \"{_state.CurrentValue}\"",
                _ => $"{ResourceString.GetString("Value")}: {_state.CurrentValue}"
            };
        }
    }
}