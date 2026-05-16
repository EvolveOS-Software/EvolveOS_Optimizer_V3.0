// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ServiceProcess;
using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Win32;
using static EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class ServiceManagerPage : Page, IPurgeable
{
    #region Fields
    private List<ServiceManagerModel> _allServices = [];
    private readonly ObservableCollection<ServiceManagerModel> _filteredServices = [];
    private static readonly Dictionary<string, (string StartType, string ImagePath, bool IsMicrosoft)> _registryCache = new();

    private string _currentSort = "Name";
    private bool _sortAscending = true;
    private string _currentFilter = "All";
    private bool _hideMicrosoftServices = false;
    private bool _isLoaded;
    private bool _isUpdatingStartupType;
    private HashSet<ComboBox> _userInteractedComboBoxes = [];

    private DispatcherTimer? _refreshTimer;
    private bool _isUpdating;

    private CancellationTokenSource? _cts;
    #endregion

    #region Constructor & Lifecycle
    public ServiceManagerPage()
    {
        InitializeComponent();

        if (SettingsEngine.IsHighPerformanceModeEnabled)
        {
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }
        else
        {
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        ServicesListView.ItemsSource = _filteredServices;

        Loaded += ServicesPage_Loaded;
        Unloaded += ServicesPage_Unloaded;
    }

    private async void ServicesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;

        if (_cts != null)
        {
            try { _cts.Cancel(); _cts.Dispose(); } catch { }
        }
        _cts = new CancellationTokenSource();

        if (_allServices.Count == 0)
        {
            await LoadServicesAsync();
        }
        else
        {
            await RefreshServicesAsync();
        }

        AiExplainerService.PreWarmConnection();

        StartAutoRefresh();
    }

    private void ServicesPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _ = Purge();
    }
    #endregion

    #region Live Monitoring Logic
    private void StartAutoRefresh()
    {
        if (_refreshTimer != null) return;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await RefreshServicesAsync();
        _refreshTimer.Start();
    }

    private void StopAutoRefresh()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private async Task RefreshServicesAsync()
    {
        if (_isUpdating || _cts == null || _cts.IsCancellationRequested) return;

        _isUpdating = true;
        try
        {
            await FetchServicesDataAsync(_cts.Token);

            if (_cts.IsCancellationRequested) return;

            UpdateSummary();
            ApplyFilterAndSort();
        }
        catch (TaskCanceledException) { Debug.WriteLine("[ServiceManager] Refresh aborted (Navigation)."); }
        catch (OperationCanceledException) { Debug.WriteLine("[ServiceManager] Refresh aborted (Navigation)."); }
        finally
        {
            _isUpdating = false;
        }
    }

    private async Task FetchServicesDataAsync(CancellationToken token = default)
    {
        _allServices = await Task.Run(() =>
        {
            var servicesList = new List<ServiceManagerModel>();
            var allSystemServices = ServiceController.GetServices();

            foreach (var s in allSystemServices)
            {
                if (token.IsCancellationRequested) break;

                if (!_registryCache.TryGetValue(s.ServiceName, out var details))
                {
                    details = GetServiceDetails(s.ServiceName);
                    _registryCache[s.ServiceName] = details;
                }

                var isRunning = s.Status == ServiceControllerStatus.Running;
                var canStop = s.Status == ServiceControllerStatus.Running && s.CanStop;

                servicesList.Add(new ServiceManagerModel
                {
                    Name = s.ServiceName,
                    DisplayName = s.DisplayName,
                    Status = s.Status.ToString(),
                    StartType = details.StartType,
                    ExecutablePath = details.ImagePath,
                    IsMicrosoftService = details.IsMicrosoft,
                    CanStart = !isRunning && details.StartType != "Disabled",
                    CanStop = canStop
                });
            }

            return servicesList.OrderBy(s => s.DisplayName).ToList();
        }, token);
    }
    #endregion

    #region Loading Logic
    private async Task LoadServicesAsync()
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        ServicesListView.Visibility = Visibility.Collapsed;

        try
        {
            await FetchServicesDataAsync();
            UpdateSummary();
            ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            await ErrorLogging.LogInfo($"Error loading services: {ex.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ServicesListView.Visibility = Visibility.Visible;
        }
    }

    private void UpdateSummary()
    {
        var runningCount = _allServices.Count(s => s.Status == "Running");
        var stoppedCount = _allServices.Count(s => s.Status == "Stopped");

        TotalServicesText.Text = _allServices.Count.ToString();
        RunningServicesText.Text = runningCount.ToString();
        StoppedServicesText.Text = stoppedCount.ToString();
    }
    #endregion

    #region Filtering & Sorting
    private void ApplyFilterAndSort()
    {
        if (!_isLoaded) return;

        var query = SearchBox.Text?.ToLowerInvariant() ?? "";

        var filtered = _allServices.Where(s =>
        {
            var matchesFilter = true;
            if (_currentFilter == ResourceString.GetString("service_manager_page_running")) matchesFilter = s.Status == "Running";
            else if (_currentFilter == ResourceString.GetString("service_manager_page_stopped")) matchesFilter = s.Status == "Stopped";
            else if (_currentFilter == ResourceString.GetString("service_manager_page_automatic")) matchesFilter = s.StartType == "Automatic";
            else if (_currentFilter == ResourceString.GetString("service_manager_page_manual")) matchesFilter = s.StartType == "Manual";
            else if (_currentFilter == ResourceString.GetString("service_manager_page_disabled")) matchesFilter = s.StartType == "Disabled";

            var matchesSearch = string.IsNullOrEmpty(query) ||
                s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

            var matchesMicrosoft = !_hideMicrosoftServices || !s.IsMicrosoftService;

            return matchesFilter && matchesSearch && matchesMicrosoft;
        }).ToList();

        var sorted = SortServices(filtered);

        MergeInto(_filteredServices, sorted);

        ResultsText.Text = string.Format(ResourceString.GetString("service_manager_page_showing_results") ?? "Showing {0} of {1} services", _filteredServices.Count, _allServices.Count);
    }

    private List<ServiceManagerModel> SortServices(List<ServiceManagerModel> source)
    {
        return _currentSort switch
        {
            "Name" => _sortAscending
                ? [.. source.OrderBy(s => s.DisplayName)]
                : [.. source.OrderByDescending(s => s.DisplayName)],
            "Status" => _sortAscending
                ? [.. source.OrderBy(s => s.Status)]
                : [.. source.OrderByDescending(s => s.Status)],
            "StartType" => _sortAscending
                ? [.. source.OrderBy(s => s.StartType)]
                : [.. source.OrderByDescending(s => s.StartType)],
            _ => source
        };
    }

    private static void MergeInto(ObservableCollection<ServiceManagerModel> target, List<ServiceManagerModel> source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (i < target.Count)
            {
                if (target[i].Name == source[i].Name)
                {
                    target[i].UpdateFrom(source[i]);
                }
                else
                {
                    target[i] = source[i];
                }
            }
            else
            {
                target.Add(source[i]);
            }
        }

        while (target.Count > source.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }
    #endregion

    #region UI Event Handlers
    private async void LiveMonitoringButton_Click(object sender, RoutedEventArgs e)
    {
        if (_refreshTimer?.IsEnabled == true)
        {
            StopAutoRefresh();
            LiveMonitoringIcon.Glyph = "\uE768";
            LiveMonitoringIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            LiveMonitoringText.Text = ResourceString.GetString("process_manager_page_start_monitor");
        }
        else
        {
            await RefreshServicesAsync();
            StartAutoRefresh();
            LiveMonitoringIcon.Glyph = "\uE769";
            LiveMonitoringIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            LiveMonitoringText.Text = ResourceString.GetString("process_manager_page_live_monitor");
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplyFilterAndSort();
        }
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;

        if (FilterComboBox.SelectedItem is ComboBoxItem item)
        {
            _currentFilter = item.Content?.ToString() ?? "All";
            ApplyFilterAndSort();
        }
    }

    private void HideMicrosoftCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            _hideMicrosoftServices = cb.IsChecked == true;
            ApplyFilterAndSort();
        }
    }

    private void SortHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string column)
        {
            _sortAscending = _currentSort != column || !_sortAscending;
            _currentSort = column;
            ApplyFilterAndSort();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadServicesAsync();
    }

    private async void StartService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string serviceName)
        {
            await ControlServiceAsync(serviceName, ServiceControlAction.Start);
        }
    }

    private async void StopService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string serviceName)
        {
            await ControlServiceAsync(serviceName, ServiceControlAction.Stop);
        }
    }

    private async void RestartService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string serviceName)
        {
            await ControlServiceAsync(serviceName, ServiceControlAction.Restart);
        }
    }

    private async void StartupType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingStartupType) return;
        if (e.AddedItems.Count == 0) return;

        if (sender is ComboBox comboBox &&
            comboBox.Tag is string serviceName &&
            comboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            if (!_userInteractedComboBoxes.Remove(comboBox)) return;

            var startupTypeLocalized = selectedItem.Content?.ToString();
            if (string.IsNullOrEmpty(startupTypeLocalized)) return;

            string internalStartType = "Unknown";
            if (startupTypeLocalized == ResourceString.GetString("service_manager_page_automatic")) internalStartType = "Automatic";
            else if (startupTypeLocalized == ResourceString.GetString("service_manager_page_manual")) internalStartType = "Manual";
            else if (startupTypeLocalized == ResourceString.GetString("service_manager_page_disabled")) internalStartType = "Disabled";

            var service = _allServices.FirstOrDefault(s => s.Name == serviceName);
            if (service == null || service.StartType == internalStartType) return;

            await ChangeStartupTypeAsync(serviceName, internalStartType);
        }
    }

    private void StartupType_DropDownOpened(object sender, object e)
    {
        if (sender is ComboBox comboBox)
        {
            _userInteractedComboBoxes.Add(comboBox);
        }
    }

    private void StartupType_DropDownClosed(object sender, object e)
    {
        if (sender is ComboBox comboBox)
        {
            _userInteractedComboBoxes.Remove(comboBox);
        }
    }

    private async void ExplainService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ServiceManagerModel vm)
        {
            var flyout = button.Flyout as Flyout;
            if (flyout == null) return;

            var stackPanel = flyout.Content as StackPanel;
            var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Name == "AiExplanationText");

            if (textBlock == null) return;

            textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

            string context = $"Internal Name: {vm.Name}\n" +
                             $"Path: {vm.ExecutablePath}\n" +
                             $"Current Status: {vm.Status}";

            string category = ResourceString.GetString("service_manager_page_category_name") ?? "Windows Service";

            string explanation = await AiExplainerService.ExplainGenericItemAsync(
                itemName: vm.DisplayName,
                itemCategory: category,
                contextDetails: context
            );

            textBlock.Text = explanation;
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

    #region Service Management Actions
    private async Task ControlServiceAsync(string serviceName, ServiceControlAction action)
    {
        try
        {
            var actionText = action switch
            {
                ServiceControlAction.Start => "Starting",
                ServiceControlAction.Stop => "Stopping",
                ServiceControlAction.Restart => "Restarting",
                _ => "Processing"
            };

            await ErrorLogging.LogInfo($"{actionText} service: {serviceName}");

            await Task.Run(() =>
            {
                using var service = new ServiceController(serviceName);
                var timeout = TimeSpan.FromSeconds(30);

                switch (action)
                {
                    case ServiceControlAction.Start:
                        if (service.Status != ServiceControllerStatus.Running)
                        {
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        }
                        break;

                    case ServiceControlAction.Stop:
                        if (service.Status == ServiceControllerStatus.Running && service.CanStop)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                        }
                        break;

                    case ServiceControlAction.Restart:
                        if (service.Status == ServiceControllerStatus.Running && service.CanStop)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                        }
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        break;
                }
            });

            var successText = action switch
            {
                ServiceControlAction.Start => ResourceString.GetString("service_manager_action_started"),
                ServiceControlAction.Stop => ResourceString.GetString("service_manager_action_stopped"),
                ServiceControlAction.Restart => ResourceString.GetString("service_manager_action_restarted"),
                _ => ResourceString.GetString("service_manager_action_processed")
            };

            App.ShowNotification(
                ResourceString.GetString("service_manager_notif_control_title"),
                string.Format(ResourceString.GetString("service_manager_notif_control_success"), serviceName, successText),
                InfoBarSeverity.Success, 3000);

            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            await ErrorLogging.LogInfo($"Error controlling service {serviceName}: {ex.Message}");
            App.ShowNotification(
                ResourceString.GetString("service_manager_notif_control_error_title"),
                string.Format(ResourceString.GetString("service_manager_notif_control_error"), ex.Message),
                InfoBarSeverity.Error, 5000);
        }
    }

    private async Task ChangeStartupTypeAsync(string serviceName, string internalStartType)
    {
        try
        {
            await ErrorLogging.LogInfo($"Changing startup type for service {serviceName} to {internalStartType}");

            var startValue = internalStartType switch
            {
                "Automatic" => 2,
                "Manual" => 3,
                "Disabled" => 4,
                _ => 3
            };

            await Task.Run(() =>
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", true);
                key?.SetValue("Start", startValue, RegistryValueKind.DWord);
            });

            _registryCache.Remove(serviceName);

            string localizedStartType = ResourceString.GetString($"service_manager_page_{internalStartType.ToLower()}") ?? internalStartType;

            App.ShowNotification(
                ResourceString.GetString("service_manager_notif_startup_title"),
                string.Format(ResourceString.GetString("service_manager_notif_startup_success"), serviceName, localizedStartType),
                InfoBarSeverity.Success, 3000);

            var service = _allServices.FirstOrDefault(s => s.Name == serviceName);
            if (service != null)
            {
                service.StartType = internalStartType;
                service.CanStart = service.Status != "Running" && internalStartType != "Disabled";
            }
        }
        catch (Exception ex)
        {
            await ErrorLogging.LogInfo($"Error changing startup type for {serviceName}: {ex.Message}");
            App.ShowNotification(
                ResourceString.GetString("service_manager_notif_error"),
                string.Format(ResourceString.GetString("service_manager_notif_startup_error"), ex.Message),
                InfoBarSeverity.Error, 5000);

            _isUpdatingStartupType = true;
            await LoadServicesAsync();
            _isUpdatingStartupType = false;
        }
    }
    #endregion

    #region Helpers
    private static (string StartType, string ImagePath, bool IsMicrosoft) GetServiceDetails(string serviceName)
    {
        string startType = "Unknown";
        string imagePath = string.Empty;
        bool isMicrosoft = false;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key != null)
            {
                if (key.GetValue("Start") is int typeInt)
                {
                    startType = typeInt switch
                    {
                        0 => "Boot",
                        1 => "System",
                        2 => "Automatic",
                        3 => "Manual",
                        4 => "Disabled",
                        _ => "Unknown"
                    };
                }

                var pathRaw = key.GetValue("ImagePath")?.ToString();
                if (!string.IsNullOrEmpty(pathRaw))
                {
                    imagePath = pathRaw.Replace("\\SystemRoot\\", "C:\\Windows\\").Replace("system32", "System32", StringComparison.OrdinalIgnoreCase);

                    if (imagePath.Contains("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                        imagePath.Contains("svchost.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        isMicrosoft = true;
                    }
                }
            }
        }
        catch { }

        return (startType, imagePath, isMicrosoft);
    }
    #endregion

    #region Purge Page
    public async Task Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        StopAutoRefresh();

        if (_cts != null)
        {
            try { _cts.Cancel(); _cts.Dispose(); } catch (ObjectDisposedException) { }
            _cts = null;
        }

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Service Collections...");

            _allServices.Clear();
            _filteredServices.Clear();
            _registryCache.Clear();
            _userInteractedComboBoxes.Clear();

            this.Loaded -= ServicesPage_Loaded;
            this.Unloaded -= ServicesPage_Unloaded;

            if (ServicesListView != null) ServicesListView.ItemsSource = null;

            this.DataContext = null;
            this.Content = null;
            //this.Bindings?.StopTracking();

            _ = Task.Run(() =>
            {
                DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
            });
        }
        else
        {
            Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
        }
    }
    #endregion
}