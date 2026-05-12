// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using static EvolveOS_Optimizer.Core.Structs.Windows;

namespace EvolveOS_Optimizer.Pages;

public class ProcessGroup : ObservableCollection<ProcessManagerModel>
{
    public string Name { get; set; }
    public ProcessGroup(string name) { Name = name; }
}

public sealed partial class ProcessManagerPage : Page, IPurgeable
{
    #region Fields
    private List<ProcessManagerModel> _allProcesses = [];

    private readonly ObservableCollection<ProcessGroup> _groupedProcesses = [];
    private ProcessGroup _appsGroup = new(ResourceString.GetString("process_manager_page_group_apps") ?? "Apps");
    private ProcessGroup _backgroundGroup = new(ResourceString.GetString("process_manager_page_group_background") ?? "Background");
    private ProcessGroup _windowsGroup = new(ResourceString.GetString("process_manager_page_group_windows") ?? "Windows");

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

        if (SettingsEngine.IsHighPerformanceModeEnabled)
        {
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }
        else
        {
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        _groupedProcesses.Add(_appsGroup);
        _groupedProcesses.Add(_backgroundGroup);
        _groupedProcesses.Add(_windowsGroup);
        CVSProcesses.Source = _groupedProcesses;

        Loaded += ProcessesPage_Loaded;
        Unloaded += ProcessesPage_Unloaded;
    }

    private async void ProcessesPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_cts != null)
        {
            try { _cts.Cancel(); _cts.Dispose(); } catch { }
        }
        _cts = new CancellationTokenSource();

        if (this.FindName("MemoryModeToggle") is ToggleSwitch ts) ts.IsOn = _showPrivateMemory;

        if (DiagnosticsPageViewModel.Current != null)
        {
            DiagnosticsPageViewModel.Current.OnOptimizeCommandCompleted -= OnGlobalOptimizationCompleted;
            DiagnosticsPageViewModel.Current.OnOptimizeCommandCompleted += OnGlobalOptimizationCompleted;
        }

        if (_allProcesses.Count == 0)
        {
            await LoadProcessesAsync();
        }
        else
        {
            await RefreshProcessesAsync();
        }

        StartAutoRefresh();
    }

    private void ProcessesPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _ = Purge();
    }
    #endregion

    #region Auto-Refresh Logic
    private void StartAutoRefresh()
    {
        if (_refreshTimer != null) return;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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
            var snapshot = await GetProcessSnapshotAsync(_cts.Token, _showPrivateMemory);

            if (_cts == null || _cts.IsCancellationRequested) return;

            _allProcesses = snapshot;
            UpdateSummary();
            ApplyFilterAndSort();
        }
        catch (TaskCanceledException) { Debug.WriteLine("[ProcessManager] Refresh aborted (Navigation)."); }
        catch (OperationCanceledException) { Debug.WriteLine("[ProcessManager] Refresh aborted (Navigation)."); }
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

            if (token.IsCancellationRequested || _cts == null) return;

            UpdateSummary();
            ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
        finally
        {
            if (LoadingRing != null)
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
            if (ProcessListView != null)
            {
                ProcessListView.Visibility = Visibility.Visible;
            }
        }
    }

    private static async Task<List<ProcessManagerModel>> GetProcessSnapshotAsync(CancellationToken token, bool usePrivateMemory)
    {
        string strApps = ResourceString.GetString("process_manager_page_group_apps") ?? "Apps";
        string strBackground = ResourceString.GetString("process_manager_page_group_background") ?? "Background processes";
        string strWindows = ResourceString.GetString("process_manager_page_group_windows") ?? "Windows processes";

        return await Task.Run(() =>
        {
            return Process.GetProcesses()
                .Select(p =>
                {
                    if (token.IsCancellationRequested) return null;
                    try
                    {
                        double memoryMb = 0;

                        if (usePrivateMemory)
                        {
                            try
                            {
                                VM_COUNTERS_EX2 info = new VM_COUNTERS_EX2();
                                int size = Marshal.SizeOf(typeof(VM_COUNTERS_EX2));

                                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                                if (hProcess != IntPtr.Zero)
                                {
                                    if (NtQueryInformationProcess(hProcess, 3, out info, size, IntPtr.Zero) == 0)
                                    {
                                        memoryMb = (double)info.PrivateWorkingSetSize / (1024.0 * 1024.0);
                                    }
                                    else
                                    {
                                        memoryMb = p.WorkingSet64 / (1024.0 * 1024.0);
                                    }
                                    CloseHandle(hProcess);
                                }
                                else
                                {
                                    memoryMb = p.WorkingSet64 / (1024.0 * 1024.0);
                                }
                            }
                            catch
                            {
                                memoryMb = p.WorkingSet64 / (1024.0 * 1024.0);
                            }
                        }
                        else
                        {
                            memoryMb = p.WorkingSet64 / (1024.0 * 1024.0);
                        }

                        if (memoryMb < 0.1) memoryMb = 0.1;

                        string priorityStatus = "-";
                        string category = strBackground;
                        byte[]? iconBytes = null;
                        bool isEcoMode = false;

                        try
                        {
                            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                            if (hProcess != IntPtr.Zero)
                            {
                                var state = new PROCESS_POWER_THROTTLING_STATE { Version = 1 };
                                if (GetProcessInformation(hProcess, 11, ref state, (uint)Marshal.SizeOf(state)))
                                {
                                    isEcoMode = (state.ControlMask & 1u) == 1u && (state.StateMask & 1u) == 1u;
                                }
                                CloseHandle(hProcess);
                            }
                        }
                        catch { /* Ignore API failures */ }

                        try
                        {
                            priorityStatus = p.PriorityClass.ToString();
                            if (p.PriorityClass == ProcessPriorityClass.Idle)
                            {
                                isEcoMode = true; // Fallback Native Windows behavior
                            }
                        }
                        catch { /* Access Denied on sandboxed Chrome/System processes */ }

                        try
                        {
                            bool isApp = p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle);
                            if (isApp) category = strApps;
                            else if (p.SessionId == 0) category = strWindows;

                            if (isApp)
                            {
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
                                            icon.ToBitmap().Save(ms, ImageFormat.Png);
                                            iconBytes = ms.ToArray();
                                            _iconCache[path] = iconBytes;
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Access Denied on MainModule */ }

                        return new ProcessManagerModel
                        {
                            Name = p.ProcessName,
                            Id = p.Id,
                            MemoryMB = memoryMb,
                            ThreadCount = p.Threads.Count,
                            Priority = priorityStatus,
                            Category = category,
                            IconBytes = iconBytes,
                            IsEfficiencyMode = isEcoMode
                        };
                    }
                    catch
                    {
                        return new ProcessManagerModel { Name = p.ProcessName, Id = p.Id, MemoryMB = 0.1, Priority = "-", Category = strWindows, IsEfficiencyMode = false };
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

        string strApps = ResourceString.GetString("process_manager_page_group_apps") ?? "Apps";
        string strBackground = ResourceString.GetString("process_manager_page_group_background") ?? "Background processes";
        string strWindows = ResourceString.GetString("process_manager_page_group_windows") ?? "Windows processes";

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
    private void SearchBox_TextChanged(Microsoft.UI.Xaml.Controls.AutoSuggestBox sender, Microsoft.UI.Xaml.Controls.AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == Microsoft.UI.Xaml.Controls.AutoSuggestionBoxTextChangeReason.UserInput) ApplyFilterAndSort();
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

    private void OnGlobalOptimizationCompleted(Enums.Memory.Optimization.Reason reason, string message)
    {
        Task.Delay(200).ContinueWith(async _ =>
        {
            DispatcherQueue?.TryEnqueue(async () =>
            {
                await RefreshProcessesAsync();
            });
        });
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessInformation(IntPtr hProcess, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE processInformation, uint processInformationSize);
    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr processHandle);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr processHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct VM_COUNTERS_EX2
    {
        public UIntPtr PeakVirtualSize;
        public UIntPtr VirtualSize;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage;
        public UIntPtr PrivateWorkingSetSize;
        public UIntPtr SharedCommitCharge;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        out VM_COUNTERS_EX2 processInformation,
        int processInformationSize,
        IntPtr returnLength);

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
        ExecuteProcessAction(sender, p => NtSuspendProcess(p.Handle), ResourceString.GetString("process_manager_page_success_suspend_title") ?? "Suspended", ResourceString.GetString("process_manager_page_success_suspend_msg") ?? "Suspended");

    private void ResumeTask_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => NtResumeProcess(p.Handle), ResourceString.GetString("process_manager_page_success_resume_title") ?? "Resumed", ResourceString.GetString("process_manager_page_success_resume_msg") ?? "Resumed");

    private void SetPriorityHigh_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.High, ResourceString.GetString("process_manager_page_success_priority_title") ?? "Priority Set", ResourceString.GetString("process_manager_page_success_priority_high_msg") ?? "Priority Set");

    private void SetPriorityAboveNormal_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.AboveNormal, ResourceString.GetString("process_manager_page_success_priority_title") ?? "Priority Set", ResourceString.GetString("process_manager_page_success_priority_abovenormal_msg") ?? "Priority Set");

    private void SetPriorityNormal_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.Normal, ResourceString.GetString("process_manager_page_success_priority_title") ?? "Priority Set", ResourceString.GetString("process_manager_page_success_priority_normal_msg") ?? "Priority Set");

    private void SetPriorityLow_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => p.PriorityClass = ProcessPriorityClass.Idle, ResourceString.GetString("process_manager_page_success_priority_title") ?? "Priority Set", ResourceString.GetString("process_manager_page_success_priority_low_msg") ?? "Priority Set");

    private void EnableEfficiencyMode_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => SetEfficiencyMode(p.Handle, true), ResourceString.GetString("process_manager_page_success_eco_enabled_title") ?? "Eco Enabled", ResourceString.GetString("process_manager_page_success_eco_enabled_msg") ?? "Eco Enabled");

    private void DisableEfficiencyMode_Click(object sender, RoutedEventArgs e) =>
        ExecuteProcessAction(sender, p => SetEfficiencyMode(p.Handle, false), ResourceString.GetString("process_manager_page_success_eco_disabled_title") ?? "Eco Disabled", ResourceString.GetString("process_manager_page_success_eco_disabled_msg") ?? "Eco Disabled");

    #endregion

    #region Purge Page
    public async Task Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        StopAutoRefresh();

        if (DiagnosticsPageViewModel.Current != null)
        {
            DiagnosticsPageViewModel.Current.OnOptimizeCommandCompleted -= OnGlobalOptimizationCompleted;
        }

        if (_cts != null)
        {
            try { _cts.Cancel(); _cts.Dispose(); } catch (ObjectDisposedException) { }
            _cts = null;
        }

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Process Collections...");

            _allProcesses.Clear();
            _appsGroup.Clear();
            _backgroundGroup.Clear();
            _windowsGroup.Clear();
            _groupedProcesses.Clear();
            _iconCache.Clear();

            this.Loaded -= ProcessesPage_Loaded;
            this.Unloaded -= ProcessesPage_Unloaded;

            if (CVSProcesses != null) CVSProcesses.Source = null;
            if (ProcessListView != null) ProcessListView.ItemsSource = null;

            this.DataContext = null;
            this.Content = null;
            this.Bindings?.StopTracking();

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