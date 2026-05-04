// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ServiceProcess;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Win32;
using static EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class ServiceManagerPage : Page
{
    #region Fields
    private List<ServiceManagerModel> _allServices = [];
    private readonly ObservableCollection<ServiceManagerModel> _filteredServices = [];

    private string _currentSort = "Name";
    private bool _sortAscending = true;
    private string _currentFilter = "All";
    private bool _hideMicrosoftServices = false;
    private bool _isLoaded;
    private bool _isUpdatingStartupType;
    private HashSet<ComboBox> _userInteractedComboBoxes = [];

    private DispatcherTimer? _refreshTimer;
    private bool _isUpdating;
    #endregion

    #region Constructor & Lifecycle
    public ServiceManagerPage()
    {
        InitializeComponent();
        ServicesListView.ItemsSource = _filteredServices;

        Loaded += ServicesPage_Loaded;
        Unloaded += ServicesPage_Unloaded;
    }

    private async void ServicesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        await LoadServicesAsync();
        StartAutoRefresh();
    }

    private void ServicesPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Purge();
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
        if (_isUpdating) return;

        _isUpdating = true;
        try
        {
            await FetchServicesDataAsync();
            UpdateSummary();
            ApplyFilterAndSort();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private async Task FetchServicesDataAsync()
    {
        _allServices = await Task.Run(() =>
        {
            return ServiceController.GetServices()
                .Select(s =>
                {
                    var details = GetServiceDetails(s.ServiceName);
                    var isRunning = s.Status == ServiceControllerStatus.Running;
                    var canStop = s.Status == ServiceControllerStatus.Running && s.CanStop;

                    return new ServiceManagerModel
                    {
                        Name = s.ServiceName,
                        DisplayName = s.DisplayName,
                        Status = s.Status.ToString(),
                        StartType = details.StartType,
                        ExecutablePath = details.ImagePath,
                        IsMicrosoftService = details.IsMicrosoft,
                        CanStart = !isRunning && details.StartType != "Disabled",
                        CanStop = canStop
                    };
                })
                .OrderBy(s => s.DisplayName)
                .ToList();
        });
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

        ResultsText.Text = string.Format(ResourceString.GetString("service_manager_page_showing_results"), _filteredServices.Count, _allServices.Count);
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
            LiveMonitoringIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Gray);
            LiveMonitoringText.Text = ResourceString.GetString("process_manager_page_start_monitor");
        }
        else
        {
            await RefreshServicesAsync();
            StartAutoRefresh();
            LiveMonitoringIcon.Glyph = "\uE769";
            LiveMonitoringIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LimeGreen);
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

            string localizedStartType = ResourceString.GetString($"service_manager_page_{internalStartType.ToLower()}");

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

    public void Purge()
    {
        StopAutoRefresh();
        Loaded -= ServicesPage_Loaded;
        Unloaded -= ServicesPage_Unloaded;
        LiveMonitoringButton.Click -= LiveMonitoringButton_Click;
        _allServices?.Clear();
        _filteredServices?.Clear();
        ServicesListView.ItemsSource = null;
        this.DataContext = null;
        this.Content = null;
    }
    #endregion
}