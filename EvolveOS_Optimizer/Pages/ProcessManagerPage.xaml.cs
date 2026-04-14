using System.Collections.ObjectModel;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class ProcessManagerPage : Page
{
    #region Fields
    private List<ProcessManagerModel> _allProcesses = [];
    private readonly ObservableCollection<ProcessManagerModel> _filteredProcesses = [];

    private string _currentSort = "Memory";
    private bool _sortAscending;
    private bool _isUpdating;

    private DispatcherTimer? _refreshTimer;
    private CancellationTokenSource? _cts;
    #endregion

    #region Constructor & Lifecycle
    public ProcessManagerPage()
    {
        InitializeComponent();
        ProcessListView.ItemsSource = _filteredProcesses;

        Loaded += ProcessesPage_Loaded;
        Unloaded += ProcessesPage_Unloaded;
    }

    private async void ProcessesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();

        await LoadProcessesAsync();

        StartAutoRefresh();
    }

    private void ProcessesPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Purge();
    }
    #endregion

    #region Auto-Refresh Logic
    private void StartAutoRefresh()
    {
        if (_refreshTimer != null) return;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) => await RefreshProcessesAsync();
        _refreshTimer.Start();
    }

    private void StopAutoRefresh()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private async Task RefreshProcessesAsync()
    {
        if (_isUpdating || _cts == null || _cts.IsCancellationRequested) return;

        _isUpdating = true;
        try
        {
            _allProcesses = await GetProcessSnapshotAsync(_cts.Token);
            UpdateSummary();
            ApplyFilterAndSort();
        }
        catch (OperationCanceledException) { /* Ignored */ }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessManager] Refresh Error: {ex.Message}");
        }
        finally
        {
            _isUpdating = false;
        }
    }
    #endregion

    #region Data Loading
    private async Task LoadProcessesAsync()
    {
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        ProcessListView.Visibility = Visibility.Collapsed;

        try
        {
            var token = _cts?.Token ?? default;
            _allProcesses = await GetProcessSnapshotAsync(token);

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

    private static async Task<List<ProcessManagerModel>> GetProcessSnapshotAsync(CancellationToken token)
    {
        return await Task.Run(() =>
        {
            return Process.GetProcesses()
                .Select(p =>
                {
                    if (token.IsCancellationRequested) return null;
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
                .OfType<ProcessManagerModel>()
                .ToList();
        }, token);
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

        var filtered = string.IsNullOrEmpty(query)
            ? _allProcesses
            : _allProcesses.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                       p.Id.ToString().Contains(query)).ToList();

        var sorted = SortProcesses(filtered);

        MergeInto(_filteredProcesses, sorted);
    }

    private List<ProcessManagerModel> SortProcesses(List<ProcessManagerModel> source)
    {
        return _currentSort switch
        {
            "Name" => _sortAscending ? source.OrderBy(p => p.Name).ToList() : source.OrderByDescending(p => p.Name).ToList(),
            "PID" => _sortAscending ? source.OrderBy(p => p.Id).ToList() : source.OrderByDescending(p => p.Id).ToList(),
            "Memory" => _sortAscending ? source.OrderBy(p => p.MemoryMB).ToList() : source.OrderByDescending(p => p.MemoryMB).ToList(),
            "Threads" => _sortAscending ? source.OrderBy(p => p.ThreadCount).ToList() : source.OrderByDescending(p => p.ThreadCount).ToList(),
            _ => source
        };
    }

    private static void MergeInto(ObservableCollection<ProcessManagerModel> target, List<ProcessManagerModel> source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (i < target.Count)
            {
                if (target[i].Id == source[i].Id)
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
            await RefreshProcessesAsync();
            StartAutoRefresh();
            LiveMonitoringIcon.Glyph = "\uE769";
            LiveMonitoringIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            LiveMonitoringText.Text = ResourceString.GetString("process_manager_page_live_monitor");
        }
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
                process.WaitForExit(3000);
            });

            App.ShowNotification("Process Ended", $"'{processName}' was terminated successfully.", InfoBarSeverity.Success, 3000);
            await RefreshProcessesAsync();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            App.ShowNotification("Error", $"Failed to end process: {ex.Message}", InfoBarSeverity.Error, 5000);
        }
    }
    #endregion

    #region Purge Page
    public void Purge()
    {
        Debug.WriteLine("[ProcessManagerPage] Purge initiated...");

        StopAutoRefresh();

        LiveMonitoringButton.Click -= LiveMonitoringButton_Click;

        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        Loaded -= ProcessesPage_Loaded;
        Unloaded -= ProcessesPage_Unloaded;

        _allProcesses?.Clear();
        _filteredProcesses?.Clear();
        ProcessListView.ItemsSource = null;

        this.DataContext = null;
        this.Content = null;

        Debug.WriteLine("[ProcessManagerPage] Purge Complete.");
    }
    #endregion
}