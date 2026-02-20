using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class ProcessManagerPage : Page
{
    #region Fields
    private List<ProcessManagerModel> _allProcesses = [];
    private List<ProcessManagerModel> _filteredProcesses = [];
    private string _currentSort = "Memory";
    private bool _sortAscending;
    #endregion

    #region Constructor & Lifecycle
    public ProcessManagerPage()
    {
        InitializeComponent();
        Loaded += ProcessesPage_Loaded;
    }

    private async void ProcessesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProcessesAsync();
    }

    private async Task LoadProcessesAsync()
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        ProcessListView.Visibility = Visibility.Collapsed;

        try
        {
            _allProcesses = await Task.Run(() =>
            {
                return Process.GetProcesses()
                    .Select(p =>
                    {
                        try
                        {
                            return new ProcessManagerModel
                            {
                                Name = p.ProcessName,
                                Id = p.Id,
                                MemoryMB = p.WorkingSet64 / (1024.0 * 1024.0),
                                ThreadCount = p.Threads.Count
                            };
                        }
                        catch
                        {
                            return new ProcessManagerModel { Name = p.ProcessName, Id = p.Id };
                        }
                    })
                    .OrderByDescending(p => p.MemoryMB)
                    .ToList();
            });

            UpdateSummary();
            ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ProcessListView.Visibility = Visibility.Visible;
        }
    }

    private void UpdateSummary()
    {
        TotalProcessesText.Text = _allProcesses.Count.ToString();
        TotalMemoryText.Text = $"{_allProcesses.Sum(p => p.MemoryMB):F0} MB";
        TotalThreadsText.Text = _allProcesses.Sum(p => p.ThreadCount).ToString();
    }
    #endregion

    #region Filtering & Sorting
    private void ApplyFilterAndSort()
    {
        var query = SearchBox.Text?.ToLowerInvariant() ?? "";

        _filteredProcesses = string.IsNullOrEmpty(query)
            ? [.. _allProcesses]
            : _allProcesses
                .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           p.Id.ToString().Contains(query))
                .ToList();

        SortProcesses();
        ProcessListView.ItemsSource = _filteredProcesses;
    }

    private void SortProcesses()
    {
        _filteredProcesses = _currentSort switch
        {
            "Name" => _sortAscending
                ? [.. _filteredProcesses.OrderBy(p => p.Name)]
                : [.. _filteredProcesses.OrderByDescending(p => p.Name)],
            "PID" => _sortAscending
                ? [.. _filteredProcesses.OrderBy(p => p.Id)]
                : [.. _filteredProcesses.OrderByDescending(p => p.Id)],
            "Memory" => _sortAscending
                ? [.. _filteredProcesses.OrderBy(p => p.MemoryMB)]
                : [.. _filteredProcesses.OrderByDescending(p => p.MemoryMB)],
            "Threads" => _sortAscending
                ? [.. _filteredProcesses.OrderBy(p => p.ThreadCount)]
                : [.. _filteredProcesses.OrderByDescending(p => p.ThreadCount)],
            _ => _filteredProcesses
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

    private void SortHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string column)
        {
            _sortAscending = _currentSort == column && !_sortAscending;
            _currentSort = column;
            ApplyFilterAndSort();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadProcessesAsync();
    }

    private async void EndTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int processId)
        {
            await EndProcessAsync(processId);
        }
    }
    #endregion

    #region Process Management Actions
    private async Task EndProcessAsync(int processId)
    {
        try
        {
            var processItem = _allProcesses.FirstOrDefault(p => p.Id == processId);
            var processName = processItem?.Name ?? "Unknown";

            await ErrorLogging.LogInfo($"Ending process: {processName} (PID: {processId})");

            await Task.Run(() =>
            {
                using var process = Process.GetProcessById(processId);
                process.Kill();
                process.WaitForExit(5000);
            });

            App.ShowNotification("Process Ended", $"Process '{processName}' (PID: {processId}) was terminated successfully.", InfoBarSeverity.Success, 3000);
            await LoadProcessesAsync();
        }
        catch (ArgumentException)
        {
            App.ShowNotification("Process Not Found", $"Process with PID {processId} no longer exists.", InfoBarSeverity.Warning, 3000);
            await LoadProcessesAsync();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            App.ShowNotification("Error", $"Failed to end process: {ex.Message}", InfoBarSeverity.Error, 5000);
        }
    }
    #endregion
}