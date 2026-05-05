// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
using System.IO;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using static EvolveOS_Optimizer.Core.Structs.Windows;

namespace EvolveOS_Optimizer.Pages;

public class ProcessGroup : ObservableCollection<ProcessManagerModel>
{
    public string Name { get; set; }
    public ProcessGroup(string name) { Name = name; }
}

public sealed partial class ProcessManagerPage : Page
{
    #region Fields
    private List<ProcessManagerModel> _allProcesses = [];

    private readonly ObservableCollection<ProcessGroup> _groupedProcesses = [];
    private ProcessGroup _appsGroup = new(ResourceString.GetString("process_manager_page_group_apps"));
    private ProcessGroup _backgroundGroup = new(ResourceString.GetString("process_manager_page_group_background"));
    private ProcessGroup _windowsGroup = new(ResourceString.GetString("process_manager_page_group_windows"));

    private string _currentSort = "Memory";
    private bool _sortAscending;
    private bool _isUpdating;
    private bool _showPrivateMemory = true;

    private DispatcherTimer? _refreshTimer;
    private CancellationTokenSource? _cts;

    private static readonly Dictionary<string, byte[]> _iconCache = new();
    #endregion

    #region Constructor & Lifecycle
    public ProcessManagerPage()
    {
        InitializeComponent();

        _groupedProcesses.Add(_appsGroup);
        _groupedProcesses.Add(_backgroundGroup);
        _groupedProcesses.Add(_windowsGroup);
        CVSProcesses.Source = _groupedProcesses;

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
            _allProcesses = await GetProcessSnapshotAsync(_cts.Token, _showPrivateMemory);
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
            _allProcesses = await GetProcessSnapshotAsync(token, _showPrivateMemory);
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

    private static async Task<List<ProcessManagerModel>> GetProcessSnapshotAsync(CancellationToken token, bool usePrivateMemory)
    {
        string strApps = ResourceString.GetString("process_manager_page_group_apps");
        string strBackground = ResourceString.GetString("process_manager_page_group_background");
        string strWindows = ResourceString.GetString("process_manager_page_group_windows");

        return await Task.Run(() =>
        {
            Dictionary<int, double> privateMemoryDict = new();

            if (usePrivateMemory)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT IDProcess, WorkingSetPrivate FROM Win32_PerfRawData_PerfProc_Process");
                    using var results = searcher.Get();

                    foreach (var mo in results)
                    {
                        var pid = Convert.ToInt32(mo["IDProcess"]);
                        var wsPrivate = Convert.ToDouble(mo["WorkingSetPrivate"]) / (1024.0 * 1024.0);
                        privateMemoryDict[pid] = wsPrivate;
                    }
                }
                catch { /* Ignored */ }
            }

            return Process.GetProcesses()
                .Select(p =>
                {
                    if (token.IsCancellationRequested) return null;
                    try
                    {
                        double memoryMb = usePrivateMemory && privateMemoryDict.TryGetValue(p.Id, out double privateMb)
                            ? privateMb
                            : p.WorkingSet64 / (1024.0 * 1024.0);

                        string priorityStatus = "-";
                        string category = strBackground;
                        byte[]? iconBytes = null;

                        try
                        {
                            priorityStatus = p.PriorityClass.ToString();

                            if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                            {
                                category = strApps;
                            }
                            else if (p.SessionId == 0)
                            {
                                category = strWindows;
                            }

                            string path = p.MainModule?.FileName ?? "";
                            if (!string.IsNullOrEmpty(path))
                            {
                                if (_iconCache.TryGetValue(path, out var cachedBytes))
                                {
                                    iconBytes = cachedBytes;
                                }
                                else
                                {
                                    var icon = Icon.ExtractAssociatedIcon(path);
                                    if (icon != null)
                                    {
                                        using var ms = new MemoryStream();
                                        icon.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                        iconBytes = ms.ToArray();
                                        _iconCache[path] = iconBytes;
                                    }
                                }
                            }
                        }
                        catch { /* Access Denied for System Processes */ }

                        return new ProcessManagerModel
                        {
                            Name = p.ProcessName,
                            Id = p.Id,
                            MemoryMB = memoryMb,
                            ThreadCount = p.Threads.Count,
                            Priority = priorityStatus,
                            Category = category,
                            IconBytes = iconBytes
                        };
                    }
                    catch
                    {
                        return new ProcessManagerModel { Name = p.ProcessName, Id = p.Id, Priority = "-", Category = strWindows };
                    }
                })
                .Where(p => p != null)
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

    #region Filtering, Sorting & Grouping
    private void ApplyFilterAndSort()
    {
        var query = SearchBox.Text?.ToLowerInvariant() ?? "";

        var filtered = string.IsNullOrEmpty(query)
            ? _allProcesses
            : _allProcesses.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                       p.Id.ToString().Contains(query)).ToList();

        var sorted = SortProcesses(filtered);

        string strApps = ResourceString.GetString("process_manager_page_group_apps");
        string strBackground = ResourceString.GetString("process_manager_page_group_background");
        string strWindows = ResourceString.GetString("process_manager_page_group_windows");

        SyncGroup(_appsGroup, sorted.Where(p => p.Category == strApps).ToList());
        SyncGroup(_backgroundGroup, sorted.Where(p => p.Category == strBackground).ToList());
        SyncGroup(_windowsGroup, sorted.Where(p => p.Category == strWindows).ToList());
    }

    private List<ProcessManagerModel> SortProcesses(List<ProcessManagerModel> source)
    {
        return _currentSort switch
        {
            "Name" => _sortAscending ? source.OrderBy(p => p.Name).ToList() : source.OrderByDescending(p => p.Name).ToList(),
            "PID" => _sortAscending ? source.OrderBy(p => p.Id).ToList() : source.OrderByDescending(p => p.Id).ToList(),
            "Memory" => _sortAscending ? source.OrderBy(p => p.MemoryMB).ToList() : source.OrderByDescending(p => p.MemoryMB).ToList(),
            "Threads" => _sortAscending ? source.OrderBy(p => p.ThreadCount).ToList() : source.OrderByDescending(p => p.ThreadCount).ToList(),
            "Priority" => _sortAscending ? source.OrderBy(p => p.Priority).ToList() : source.OrderByDescending(p => p.Priority).ToList(),
            _ => source
        };
    }

    private void SyncGroup(ProcessGroup targetGroup, List<ProcessManagerModel> source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (source[i].IconBytes != null && source[i].ProcessIcon == null)
            {
                var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                using var ms = new MemoryStream(source[i].IconBytes!);
                bmp.SetSource(ms.AsRandomAccessStream());
                source[i].ProcessIcon = bmp;
            }

            if (i < targetGroup.Count)
            {
                if (targetGroup[i].Id == source[i].Id)
                {
                    targetGroup[i].UpdateFrom(source[i]);
                }
                else
                {
                    targetGroup[i] = source[i];
                }
            }
            else
            {
                targetGroup.Add(source[i]);
            }
        }

        while (targetGroup.Count > source.Count)
        {
            targetGroup.RemoveAt(targetGroup.Count - 1);
        }
    }
    #endregion

    #region UI Event Handlers
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) ApplyFilterAndSort();
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
        if (sender is FrameworkElement element && element.Tag is int processId)
        {
            await EndProcessAsync(processId);
        }
    }

    private async void MemoryModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _showPrivateMemory = toggleSwitch.IsOn;
            if (!_isUpdating) await RefreshProcessesAsync();
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

            App.ShowNotification(
                ResourceString.GetString("process_manager_page_success_end_title"),
                string.Format(ResourceString.GetString("process_manager_page_success_end_msg"), processName),
                InfoBarSeverity.Success, 3000);

            await RefreshProcessesAsync();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            App.ShowNotification(
                ResourceString.GetString("process_manager_page_error_title"),
                string.Format(ResourceString.GetString("process_manager_page_error_end_msg"), ex.Message),
                InfoBarSeverity.Error, 5000);
        }
    }
    #endregion

    #region Advanced Process Actions (Deep Control & Tweaks)

    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr processHandle);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr processHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr hProcess, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE processInformation, uint processInformationSize);

    private void ExecuteProcessAction(object sender, Action<Process> action, string successTitle, string successMessageTemplate)
    {
        if (sender is FrameworkElement element && element.Tag is int processId)
        {
            Task.Run(() =>
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    action(process);

                    string safeProcessName = process.ProcessName;

                    DispatcherQueue.TryEnqueue(() =>
                        App.ShowNotification(
                            successTitle,
                            string.Format(successMessageTemplate, safeProcessName),
                            InfoBarSeverity.Success, 3000));
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                    DispatcherQueue.TryEnqueue(() =>
                        App.ShowNotification(
                            ResourceString.GetString("process_manager_page_error_access_denied_title") ?? "Access Denied",
                            string.Format(ResourceString.GetString("process_manager_page_error_access_denied_msg") ?? "Error: {0}", ex.Message),
                            InfoBarSeverity.Error, 5000));
                }
            });
        }
    }

    private void SetEfficiencyMode(IntPtr handle, bool enable)
    {
        var state = new PROCESS_POWER_THROTTLING_STATE { Version = 1, ControlMask = 1, StateMask = enable ? 1u : 0u };
        bool success = SetProcessInformation(handle, 11, ref state, (uint)Marshal.SizeOf(state));
        if (!success) throw new Exception($"SetProcessInformation failed with Win32 Error: {Marshal.GetLastWin32Error()}");
    }

    private void SuspendTask_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => NtSuspendProcess(p.Handle), ResourceString.GetString("process_manager_page_success_suspend_title"), ResourceString.GetString("process_manager_page_success_suspend_msg"));

    private void ResumeTask_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => NtResumeProcess(p.Handle), ResourceString.GetString("process_manager_page_success_resume_title"), ResourceString.GetString("process_manager_page_success_resume_msg"));

    private void SetPriorityHigh_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.High, ResourceString.GetString("process_manager_page_success_priority_title"), ResourceString.GetString("process_manager_page_success_priority_high_msg"));

    private void SetPriorityAboveNormal_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.AboveNormal, ResourceString.GetString("process_manager_page_success_priority_title"), ResourceString.GetString("process_manager_page_success_priority_abovenormal_msg"));

    private void SetPriorityNormal_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.Normal, ResourceString.GetString("process_manager_page_success_priority_title"), ResourceString.GetString("process_manager_page_success_priority_normal_msg"));

    private void SetPriorityLow_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.Idle, ResourceString.GetString("process_manager_page_success_priority_title"), ResourceString.GetString("process_manager_page_success_priority_low_msg"));

    private void EnableEfficiencyMode_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => SetEfficiencyMode(p.Handle, true), ResourceString.GetString("process_manager_page_success_eco_enabled_title"), ResourceString.GetString("process_manager_page_success_eco_enabled_msg"));

    private void DisableEfficiencyMode_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => SetEfficiencyMode(p.Handle, false), ResourceString.GetString("process_manager_page_success_eco_disabled_title"), ResourceString.GetString("process_manager_page_success_eco_disabled_msg"));

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
        _appsGroup?.Clear();
        _backgroundGroup?.Clear();
        _windowsGroup?.Clear();
        _groupedProcesses?.Clear();
        ProcessListView.ItemsSource = null;

        this.DataContext = null;
        this.Content = null;
        Debug.WriteLine("[ProcessManagerPage] Purge Complete.");
    }
    #endregion
}