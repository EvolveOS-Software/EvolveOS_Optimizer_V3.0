using System.ServiceProcess;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Win32;
using static EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class ServiceManagerPage : Page
{
    #region Fields
    private List<ServiceManagerModel> _allServices = [];
    private List<ServiceManagerModel> _filteredServices = [];
    private string _currentSort = "Name";
    private bool _sortAscending = true;
    private string _currentFilter = "All";
    private bool _isLoaded;
    private bool _isUpdatingStartupType;
    private HashSet<ComboBox> _userInteractedComboBoxes = [];
    #endregion

    #region Constructor & Lifecycle
    public ServiceManagerPage()
    {
        InitializeComponent();

        Loaded += ServicesPage_Loaded;
    }

    private async void ServicesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        await LoadServicesAsync();
    }

    private async Task LoadServicesAsync()
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        ServicesListView.Visibility = Visibility.Collapsed;

        try
        {
            _allServices = await Task.Run(() =>
            {
                return ServiceController.GetServices()
                    .Select(s =>
                    {
                        var startType = GetServiceStartType(s.ServiceName);
                        var isRunning = s.Status == ServiceControllerStatus.Running;
                        var canStop = s.Status == ServiceControllerStatus.Running && s.CanStop;

                        return new ServiceManagerModel
                        {
                            Name = s.ServiceName,
                            DisplayName = s.DisplayName,
                            Status = s.Status.ToString(),
                            StartType = startType,
                            CanStart = !isRunning && startType != "Disabled",
                            CanStop = canStop
                        };
                    })
                    .OrderBy(s => s.DisplayName)
                    .ToList();
            });

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

        _filteredServices = _allServices.Where(s =>
        {
            var matchesFilter = _currentFilter switch
            {
                "Running" => s.Status == "Running",
                "Stopped" => s.Status == "Stopped",
                "Automatic" => s.StartType == "Automatic",
                "Manual" => s.StartType == "Manual",
                "Disabled" => s.StartType == "Disabled",
                _ => true
            };

            var matchesSearch = string.IsNullOrEmpty(query) ||
                s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

            return matchesFilter && matchesSearch;
        }).ToList();

        SortServices();

        ServicesListView.ItemsSource = _filteredServices;
        ResultsText.Text = $"Showing {_filteredServices.Count} of {_allServices.Count} services";
    }

    private void SortServices()
    {
        _filteredServices = _currentSort switch
        {
            "Name" => _sortAscending
                ? [.. _filteredServices.OrderBy(s => s.DisplayName)]
                : [.. _filteredServices.OrderByDescending(s => s.DisplayName)],
            "Status" => _sortAscending
                ? [.. _filteredServices.OrderBy(s => s.Status)]
                : [.. _filteredServices.OrderByDescending(s => s.Status)],
            "StartType" => _sortAscending
                ? [.. _filteredServices.OrderBy(s => s.StartType)]
                : [.. _filteredServices.OrderByDescending(s => s.StartType)],
            _ => _filteredServices
        };
    }
    #endregion

    #region UI Event Handlers
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

            var startupType = selectedItem.Content?.ToString();
            if (string.IsNullOrEmpty(startupType)) return;

            var service = _allServices.FirstOrDefault(s => s.Name == serviceName);
            if (service == null || service.StartType == startupType) return;

            await ChangeStartupTypeAsync(serviceName, startupType);
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
                ServiceControlAction.Start => "started",
                ServiceControlAction.Stop => "stopped",
                ServiceControlAction.Restart => "restarted",
                _ => "processed"
            };

            App.ShowNotification("Service Control", $"Service '{serviceName}' {successText} successfully.", InfoBarSeverity.Success, 3000);
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            await ErrorLogging.LogInfo($"Error controlling service {serviceName}: {ex.Message}");
            App.ShowNotification("Service Control Error", $"Failed to control service: {ex.Message}", InfoBarSeverity.Error, 5000);
        }
    }

    private async Task ChangeStartupTypeAsync(string serviceName, string startupType)
    {
        try
        {
            await ErrorLogging.LogInfo($"Changing startup type for service {serviceName} to {startupType}");

            var startValue = startupType switch
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

            App.ShowNotification("Startup Type Changed", $"Service '{serviceName}' startup type set to {startupType}.", InfoBarSeverity.Success, 3000);

            var service = _allServices.FirstOrDefault(s => s.Name == serviceName);
            if (service != null)
            {
                service.StartType = startupType;
                service.CanStart = service.Status != "Running" && startupType != "Disabled";
            }
        }
        catch (Exception ex)
        {
            await ErrorLogging.LogInfo($"Error changing startup type for {serviceName}: {ex.Message}");
            App.ShowNotification("Error", $"Failed to change startup type: {ex.Message}", InfoBarSeverity.Error, 5000);

            _isUpdatingStartupType = true;
            await LoadServicesAsync();
            _isUpdatingStartupType = false;
        }
    }
    #endregion

    #region Helpers
    private static string GetServiceStartType(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key?.GetValue("Start") is int startType)
            {
                return startType switch
                {
                    0 => "Boot",
                    1 => "System",
                    2 => "Automatic",
                    3 => "Manual",
                    4 => "Disabled",
                    _ => "Unknown"
                };
            }
        }
        catch { }
        return "Unknown";
    }
    #endregion
}