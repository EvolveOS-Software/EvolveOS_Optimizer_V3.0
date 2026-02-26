using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;
using Windows.System;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class MaintenanceViewModel : ViewModelBase, IDisposable
    {
        private static MaintenanceViewModel? _instance;
        public static MaintenanceViewModel Current => _instance ??= CreateGlobalInstance();

        private static MaintenanceViewModel CreateGlobalInstance()
        {
            // We create the services here so the App can start it without needing the Page
            IComputerService computerService = new EvolveOS_Optimizer.Utilities.Services.ComputerService();
            IHotkeyService? globalHotkeyService = App.GetService<IHotkeyService>();

            return new MaintenanceViewModel(computerService, globalHotkeyService!);
        }

        private CancellationTokenSource? _cancellationTokenSource;
        private Computer? _computer;
        private readonly IComputerService _computerService;
        private readonly IHotkeyService _hotKeyService;
        private bool _isOptimizationKeyValid;
        private bool _isOptimizationRunning;
        private bool _isReiniziliating;
        private DateTimeOffset _lastAutoOptimizationByInterval = DateTimeOffset.Now;
        private DateTimeOffset _lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
        private readonly object _lockObject = new object();
        private byte _optimizationProgressPercentage;
        private string _optimizationProgressStep = ResourceString.GetString("txt_progress_step");
        private byte _optimizationProgressTotal = byte.MaxValue;
        private byte _optimizationProgressValue = byte.MinValue;
        private string? _selectedProcess;
        private bool _isBusy;
        private bool _isUiActive = true;

        public MaintenanceViewModel(IComputerService computerService, IHotkeyService hotKeyService)
        {
            _computerService = computerService;
            _hotKeyService = hotKeyService;

            _isOptimizationKeyValid = true;
            _cancellationTokenSource = new CancellationTokenSource();
            Computer = new Computer();

            AddProcessToExclusionListCommand = new RelayCommand<string>(AddProcessToExclusionList, _ => CanAddProcessToExclusionList);
            OptimizeCommand = new RelayCommand(_ => _ = OptimizeAsync(Enums.Memory.Optimization.Reason.Manual), _ => CanOptimize);
            RemoveProcessFromExclusionListCommand = new RelayCommand<string>(RemoveProcessFromExclusionList);

            MemoryUsageThresholds = Enumerable.Range(1, 99).Select(number => (byte)number).ToList();
            _computerService.OnOptimizeProgressUpdate += OnOptimizeProgressUpdate;
            Computer.OperatingSystem = _computerService.OperatingSystem;

            App.HotkeySettingsChanged += OnHotkeySettingsChanged;

            Thread monitorThread = new Thread(MonitorLoop) { IsBackground = true };
            monitorThread.Start();

            OnPropertyChanged(nameof(MemoryAreaItems));
            OnPropertyChanged(nameof(SystemCleanupAreaItems));

            LoadDriveInfo();

            MonitorAsync();
        }

        public void LoadDriveInfo()
        {
            var drives = DiskInfoService.GetDrivesData();

            DriveCInfo = drives.FirstOrDefault(d => d.Name != null && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                         ?? drives.FirstOrDefault()!;
        }

        #region Properties

        public Computer? Computer
        {
            get => _computer;
            set { _computer = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private DriveSpaceInfo? _driveCInfo;
        public DriveSpaceInfo? DriveCInfo
        {
            get => _driveCInfo;
            set
            {
                _driveCInfo = value;
                OnPropertyChanged();
            }
        }

        public bool CanAddProcessToExclusionList
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SelectedProcess) &&
                       !LocalMachineSettingsEngine.ProcessExclusionList.Contains(SelectedProcess);
            }
        }

        public bool IsOptimizationRunning
        {
            get => _isOptimizationRunning;
            set { _isOptimizationRunning = value; OnPropertyChanged(); }
        }

        public bool CanOptimize
        {
            get { return MemoryAreas != Enums.Memory.Areas.None && !IsOptimizationRunning; }
        }

        public byte OptimizationProgressPercentage
        {
            get { return _optimizationProgressPercentage; }
            set
            {
                _optimizationProgressPercentage = value;
                OnPropertyChanged();
            }
        }

        public Action<Enums.Memory.Optimization.Reason, string>? OnOptimizeCommandCompleted;

        public string OptimizationProgressStep
        {
            get { return _optimizationProgressStep; }
            set
            {
                _optimizationProgressStep = value;
                OnPropertyChanged();
            }
        }

        public byte OptimizationProgressTotal
        {
            get { return _optimizationProgressTotal; }
            set
            {
                _optimizationProgressTotal = value;
                OnPropertyChanged();
            }
        }

        public byte OptimizationProgressValue
        {
            get { return _optimizationProgressValue; }
            set
            {
                _optimizationProgressValue = value;
                OnPropertyChanged();
            }
        }

        public List<VirtualKey> KeyboardKeys
        {
            get
            {
                return _hotKeyService.Keys;
            }
        }

        public Dictionary<VirtualKeyModifiers, string> KeyboardModifiers
        {
            get
            {
                return _hotKeyService.Modifiers;
            }
        }

        public VirtualKey OptimizationKey
        {
            get => LocalMachineSettingsEngine.OptimizationKey;
            set
            {
                if (value == VirtualKey.None || (int)value == 0) return;

                if (value != LocalMachineSettingsEngine.OptimizationKey)
                {
                    LocalMachineSettingsEngine.OptimizationKey = value;
                    OnPropertyChanged();
                    IsOptimizationKeyValid = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public VirtualKeyModifiers OptimizationModifiers
        {
            get => LocalMachineSettingsEngine.OptimizationModifiers;
            set
            {
                if (value == VirtualKeyModifiers.None || (int)value == 0) return;

                if (value != LocalMachineSettingsEngine.OptimizationModifiers)
                {
                    LocalMachineSettingsEngine.OptimizationModifiers = value;
                    OnPropertyChanged();
                    IsOptimizationKeyValid = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public bool IsOptimizationKeyValid
        {
            get => _isOptimizationKeyValid;
            set
            {
                if (_isReiniziliating) return;

                if (_isOptimizationKeyValid != value)
                {
                    _isOptimizationKeyValid = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseHotkey
        {
            get => LocalMachineSettingsEngine.UseHotkey;
            set
            {
                if (value != LocalMachineSettingsEngine.UseHotkey)
                {
                    LocalMachineSettingsEngine.UseHotkey = value;
                    OnPropertyChanged();
                    IsOptimizationKeyValid = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public bool ShowDiskSpace
        {
            get { return LocalMachineSettingsEngine.ShowDiskSpace; }
            set
            {
                try
                {
                    IsBusy = true;
                    LocalMachineSettingsEngine.ShowDiskSpace = value;
                    OnPropertyChanged();
                }
                finally { IsBusy = false; }
            }
        }

        public string? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                // 1. THE GUARD: Break the infinite loop!
                if (_selectedProcess == value) return;

                _selectedProcess = value;
                OnPropertyChanged();

                // Update the Add button state
                OnPropertyChanged(nameof(CanAddProcessToExclusionList));
            }
        }

        public bool RestartExplorerAfterOptimization
        {
            get { return LocalMachineSettingsEngine.RestartExplorerAfterOptimization; }
            set
            {
                try
                {
                    IsBusy = true;
                    LocalMachineSettingsEngine.RestartExplorerAfterOptimization = value;
                    OnPropertyChanged();
                }
                finally { IsBusy = false; }
            }
        }

        public bool ShowVirtualMemory
        {
            get { return LocalMachineSettingsEngine.ShowVirtualMemory; }
            set
            {
                try
                {
                    IsBusy = true;
                    LocalMachineSettingsEngine.ShowVirtualMemory = value;
                    OnPropertyChanged();
                }
                finally { IsBusy = false; }
            }
        }

        public bool RunOnLowPriority
        {
            get { return LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low; }
            set
            {
                try
                {
                    IsBusy = true;
                    var priority = value ? Enums.Priority.Low : Enums.Priority.Normal;
                    App.SetPriority(priority);
                    LocalMachineSettingsEngine.RunOnPriority = priority;
                    OnPropertyChanged();
                }
                finally { IsBusy = false; }
            }
        }

        public bool ShowOptimizationNotifications
        {
            get { return LocalMachineSettingsEngine.ShowOptimizationNotifications; }
            set
            {
                try
                {
                    IsBusy = true;
                    LocalMachineSettingsEngine.ShowOptimizationNotifications = value;
                    OnPropertyChanged();
                }
                finally { IsBusy = false; }
            }
        }

        public bool DisableAllOptimizationResults
        {
            get { return LocalMachineSettingsEngine.DisableAllOptimizationResults; }
            set
            {
                try
                {
                    IsBusy = true;
                    LocalMachineSettingsEngine.DisableAllOptimizationResults = value;
                    OnPropertyChanged();
                }
                finally { IsBusy = false; }
            }
        }

        private string _totalSpaceToFree = "0 MB";
        public string TotalSpaceToFree
        {
            get => _totalSpaceToFree;
            set { _totalSpaceToFree = value; OnPropertyChanged(); }
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set { _isScanning = value; OnPropertyChanged(); }
        }

        public System.Windows.Input.ICommand RefreshCleanupSpaceCommand => new RelayCommand(async (_) =>
        {
            TotalSpaceToFree = ResourceString.GetString("txt_scanning");
            await CalculateCleanupSpaceAsync();
        });

        private CancellationTokenSource? _cleanupCts;

        private async Task CalculateCleanupSpaceAsync()
        {
            _cleanupCts?.Cancel();
            _cleanupCts = new CancellationTokenSource();
            var token = _cleanupCts.Token;

            IsScanning = true;

            try
            {
                await Task.Delay(300, token);

                long totalBytes = 0;
                var areas = LocalMachineSettingsEngine.MemoryAreas;

                await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return;

                    if ((areas & Enums.Memory.Areas.WindowsOld) != 0)
                    {
                        string? root = Path.GetPathRoot(Environment.SystemDirectory);
                        if (root != null)
                        {
                            string winOldPath = Path.Combine(root, "Windows.old");
                            totalBytes += GetDirectorySize(winOldPath);
                        }
                    }

                    if (token.IsCancellationRequested) return;

                    if ((areas & Enums.Memory.Areas.DiskCleanup) != 0)
                    {
                        totalBytes += GetDirectorySize(Path.GetTempPath());
                        totalBytes += GetDirectorySize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
                        totalBytes += GetDirectorySize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution\\Download"));
                    }
                }, token);

                if (token.IsCancellationRequested) return;

                var unitPair = totalBytes.ToMemoryUnit();
                TotalSpaceToFree = totalBytes == 0 ? "0 MB" : string.Format("{0:0.##} {1}", unitPair.Key, unitPair.Value);
            }
            catch (TaskCanceledException) { }
            catch (Exception e) { ErrorLogging.LogDebug(e); }
            finally { IsScanning = false; }
        }

        private long GetDirectorySize(string path, int currentDepth = 0, int maxDepth = 5)
        {
            if (string.IsNullOrEmpty(path) || currentDepth > maxDepth || !Directory.Exists(path)) return 0;
            long size = 0;
            try
            {
                string[] files = Directory.GetFiles(path);
                foreach (string file in files)
                {
                    try { size += new FileInfo(file).Length; } catch { }
                }

                if (currentDepth < maxDepth)
                {
                    string[] dirs = Directory.GetDirectories(path);
                    foreach (string dir in dirs)
                    {
                        try
                        {
                            FileAttributes attributes = File.GetAttributes(dir);
                            if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;
                            size += GetDirectorySize(dir, currentDepth + 1, maxDepth);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error scanning {path}: {ex.Message}"); }
            return size;
        }

        public string VirtualMemoryHeader
        {
            get
            {
                if (Computer?.Memory?.Virtual?.Total == null) return ResourceString.GetString("txt_header_virtual_memory");
                return string.Format("{0} ({1:0.#} {2})", ResourceString.GetString("txt_header_virtual_memory"), Computer.Memory.Virtual.Total.Value, Computer.Memory.Virtual.Total.Unit);
            }
        }

        public List<byte> MemoryUsageThresholds { get; private set; }

        public ObservableCollection<ObservableItem<bool>> MemoryAreaItems
        {
            get
            {
                var items = new List<ObservableItem<bool>>();
                if (Computer == null) return new ObservableCollection<ObservableItem<bool>>();

                Action<string, string, Enums.Memory.Areas, bool> add = (name, tooltip, area, isEnabled) =>
                {
                    items.Add(new ObservableItem<bool>(name, () => (MemoryAreas & area) == area, (value) => { MemoryAreas = area; }, isEnabled, tooltip));
                };

                add(ResourceString.GetString("title_memory_areas_combined_page_list"), ResourceString.GetString("description_memory_areas_combined_page_list"), Enums.Memory.Areas.CombinedPageList, Computer.OperatingSystem.HasCombinedPageList);
                add(ResourceString.GetString("title_memory_areas_modified_file_cache"), ResourceString.GetString("description_memory_system_file_cache"), Enums.Memory.Areas.ModifiedFileCache, Computer.OperatingSystem.HasModifiedFileCache);
                add(ResourceString.GetString("title_memory_areas_modified_page_list"), ResourceString.GetString("description_memory_areas_modified_page_list"), Enums.Memory.Areas.ModifiedPageList, Computer.OperatingSystem.HasModifiedPageList);
                add(ResourceString.GetString("title_memory_areas_registry_cache"), ResourceString.GetString("description_memory_areas_registry_cache"), Enums.Memory.Areas.RegistryCache, Computer.OperatingSystem.HasRegistryHive);
                add(ResourceString.GetString("title_memory_areas_standby_list"), ResourceString.GetString("description_memory_areas_standby_list"), Enums.Memory.Areas.StandbyList, Computer.OperatingSystem.HasStandbyList);
                add(ResourceString.GetString("title_memory_areas_standby_list_low_priority"), ResourceString.GetString("description_memory_areas_standby_list_low_priority"), Enums.Memory.Areas.StandbyListLowPriority, Computer.OperatingSystem.HasStandbyList);
                add(ResourceString.GetString("title_memory_areas_system_file_cache"), ResourceString.GetString("description_memory_areas_system_file_cache"), Enums.Memory.Areas.SystemFileCache, Computer.OperatingSystem.HasSystemFileCache);
                add(ResourceString.GetString("title_memory_areas_working_set"), ResourceString.GetString("description_memory_areas_working_set"), Enums.Memory.Areas.WorkingSet, Computer.OperatingSystem.HasWorkingSet);

                return new ObservableCollection<ObservableItem<bool>>(items.OrderBy(item => item.Name));
            }
        }

        public ObservableCollection<ObservableItem<bool>> SystemCleanupAreaItems
        {
            get
            {
                var items = new List<ObservableItem<bool>>();
                Action<string, string, Enums.Memory.Areas, bool> add = (name, tooltip, area, isEnabled) =>
                {
                    items.Add(new ObservableItem<bool>(name, () => (MemoryAreas & area) == area, (value) => { MemoryAreas = area; }, isEnabled, tooltip));
                };

                string? root = Path.GetPathRoot(Environment.SystemDirectory);
                bool hasWindowsOld = root != null && Directory.Exists(Path.Combine(root, "Windows.old"));

                add(ResourceString.GetString("title_memory_areas_disk_cleanup"), ResourceString.GetString("description_memory_areas_disk_cleanup"), Enums.Memory.Areas.DiskCleanup, true);
                add(ResourceString.GetString("title_memory_areas_flush_dns"), ResourceString.GetString("description_memory_areas_flush_dns"), Enums.Memory.Areas.FlushDns, true);
                add(ResourceString.GetString("title_memory_areas_windows_old"), ResourceString.GetString("description_memory_areas_windows_old"), Enums.Memory.Areas.WindowsOld, hasWindowsOld);

                items.Add(new ObservableItem<bool>(ResourceString.GetString("title_settings_items_restart_explorer"), () => RestartExplorerAfterOptimization, value => RestartExplorerAfterOptimization = value, true, ResourceString.GetString("description_settings_items_restart_explorer")));

                return new ObservableCollection<ObservableItem<bool>>(items.OrderBy(item => item.Name));
            }
        }

        public Enums.Memory.Areas MemoryAreas
        {
            get
            {
                if (Computer == null) return LocalMachineSettingsEngine.MemoryAreas;

                var currentAreas = LocalMachineSettingsEngine.MemoryAreas;
                var originalAreas = currentAreas;

                if (!Computer.OperatingSystem.HasCombinedPageList) currentAreas &= ~Enums.Memory.Areas.CombinedPageList;
                if (!Computer.OperatingSystem.HasModifiedPageList) currentAreas &= ~Enums.Memory.Areas.ModifiedPageList;
                if (!Computer.OperatingSystem.HasRegistryHive) currentAreas &= ~Enums.Memory.Areas.RegistryCache;
                if (!Computer.OperatingSystem.HasStandbyList) { currentAreas &= ~Enums.Memory.Areas.StandbyList; currentAreas &= ~Enums.Memory.Areas.StandbyListLowPriority; }
                if (!Computer.OperatingSystem.HasSystemFileCache) currentAreas &= ~Enums.Memory.Areas.SystemFileCache;
                if (!Computer.OperatingSystem.HasWorkingSet) currentAreas &= ~Enums.Memory.Areas.WorkingSet;

                string? root = Path.GetPathRoot(Environment.SystemDirectory);
                if (root == null || !Directory.Exists(Path.Combine(root, "Windows.old"))) currentAreas &= ~Enums.Memory.Areas.WindowsOld;

                if (currentAreas != originalAreas)
                {
                    LocalMachineSettingsEngine.MemoryAreas = currentAreas;
                }

                return currentAreas;
            }
            set
            {
                try
                {
                    IsBusy = true;
                    var currentAreas = LocalMachineSettingsEngine.MemoryAreas;
                    if ((currentAreas & value) != 0) currentAreas &= ~value;
                    else currentAreas |= value;

                    if (value == Enums.Memory.Areas.StandbyList) currentAreas &= ~Enums.Memory.Areas.StandbyListLowPriority;
                    else if (value == Enums.Memory.Areas.StandbyListLowPriority) currentAreas &= ~Enums.Memory.Areas.StandbyList;
                    else if (value == Enums.Memory.Areas.WindowsOld && (currentAreas & Enums.Memory.Areas.WindowsOld) != 0) currentAreas |= Enums.Memory.Areas.DiskCleanup;

                    LocalMachineSettingsEngine.MemoryAreas = currentAreas;
                    _ = CalculateCleanupSpaceAsync();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanOptimize));
                    OnPropertyChanged(nameof(MemoryAreaItems));
                    OnPropertyChanged(nameof(SystemCleanupAreaItems));
                }
                finally { IsBusy = false; }
            }
        }

        public ObservableCollection<string> Processes
        {
            get
            {
                return new ObservableCollection<string>(Process.GetProcesses()
                    .Where(p => p != null && !p.ProcessName.Equals(App.Name) && !LocalMachineSettingsEngine.ProcessExclusionList.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase))
                    .Select(p => p.ProcessName.ToLower())
                    .Distinct()
                    .OrderBy(name => name));
            }
        }

        public ObservableCollection<string> ProcessExclusionList
        {
            get { return new ObservableCollection<string>(LocalMachineSettingsEngine.ProcessExclusionList); }
        }

        public int AutoOptimizationInterval
        {
            get => LocalMachineSettingsEngine.AutoOptimizationInterval;
            set
            {
                if (LocalMachineSettingsEngine.AutoOptimizationInterval != value)
                {
                    LocalMachineSettingsEngine.AutoOptimizationInterval = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AutoOptimizationMemoryIntervalDescription));
                }
            }
        }

        public int AutoOptimizationMemoryUsage
        {
            get => LocalMachineSettingsEngine.AutoOptimizationMemoryUsage;
            set
            {
                LocalMachineSettingsEngine.AutoOptimizationMemoryUsage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoOptimizationMemoryUsageDescription));
            }
        }

        public string AutoOptimizationMemoryIntervalDescription => ResourceHelper.GetPluralizedString("txt_auto_opt_interval", AutoOptimizationInterval);
        public string AutoOptimizationMemoryUsageDescription => string.Format(ResourceString.GetString("txt_auto_opt_usage_limit"), AutoOptimizationMemoryUsage);
        public string AutoOptimizationMemoryUsageWarning => ResourceString.GetString("txt_auto_opt_usage_warning");

        public System.Windows.Input.ICommand OptimizeCommand { get; }
        public System.Windows.Input.ICommand AddProcessToExclusionListCommand { get; }
        public System.Windows.Input.ICommand RemoveProcessFromExclusionListCommand { get; }

        #endregion

        public ObservableCollection<ObservableItem<bool>> SettingItems
        {
            get
            {
                return new ObservableCollection<ObservableItem<bool>>(new List<ObservableItem<bool>>
                {
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_show_disk_space"), () => ShowDiskSpace, v => ShowDiskSpace = v, true, ResourceString.GetString("description_settings_items_show_disk_space")),
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_show_notification"), () => ShowOptimizationNotifications, v => ShowOptimizationNotifications = v, !DisableAllOptimizationResults, ResourceString.GetString("description_settings_items_show_notification")),
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_show_no_result"), () => DisableAllOptimizationResults, v => { DisableAllOptimizationResults = v; OnPropertyChanged(nameof(SettingItems)); }, true, ResourceString.GetString("description_settings_items_show_no_result")),
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_low_priority"), () => RunOnLowPriority, v => RunOnLowPriority = v, true, ResourceString.GetString("description_settings_items_low_priority")),
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_show_virtual_memory"), () => ShowVirtualMemory, v => ShowVirtualMemory = v, true, ResourceString.GetString("description_settings_items_show_virtual_memory"))
                }.OrderBy(i => i.Name));
            }
        }

        public event Action? OnAddProcessToExclusionListCommandCompleted;
        public event Action? OnRemoveProcessFromExclusionListCommandCompleted;

        private void AddProcessToExclusionList(string? process)
        {
            if (string.IsNullOrWhiteSpace(process)) return;
            try
            {
                IsBusy = true;
                if (!LocalMachineSettingsEngine.ProcessExclusionList.Contains(process, StringComparer.OrdinalIgnoreCase))
                {
                    if (LocalMachineSettingsEngine.ProcessExclusionList.Add(process))
                    {
                        LocalMachineSettingsEngine.SaveExclusionList();

                        SelectedProcess = null;

                        OnPropertyChanged(nameof(Processes));
                        OnPropertyChanged(nameof(ProcessExclusionList));
                        _dispatcherQueue?.TryEnqueue(() => OnAddProcessToExclusionListCommandCompleted?.Invoke());
                    }
                }
            }
            finally { IsBusy = false; }
        }

        private void RemoveProcessFromExclusionList(string? process)
        {
            if (process == null) return;
            try
            {
                IsBusy = true;
                if (LocalMachineSettingsEngine.ProcessExclusionList.Remove(process))
                {
                    LocalMachineSettingsEngine.SaveExclusionList();
                    OnPropertyChanged(nameof(Processes));
                    OnPropertyChanged(nameof(ProcessExclusionList));
                    _dispatcherQueue?.TryEnqueue(() => OnRemoveProcessFromExclusionListCommandCompleted?.Invoke());
                }
            }
            finally { IsBusy = false; }
        }

        private void MonitorLoop()
        {
            var cts = _cancellationTokenSource;
            if (cts == null) return;

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    _computerService.RefreshMemory();
                    var currentMemory = _computerService.Memory;

                    _dispatcherQueue?.TryEnqueue(() =>
                    {
                        if (Computer != null && _isUiActive)
                        {
                            Computer.Memory = currentMemory;
                            OnPropertyChanged(nameof(Computer));
                        }
                    });

                    if (cts.Token.WaitHandle.WaitOne(5000)) break;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e) { ErrorLogging.LogDebug(e); }
            }
        }

        private void MonitorApp()
        {
            var cts = _cancellationTokenSource;
            if (cts == null) return;

            App.SetPriority(LocalMachineSettingsEngine.RunOnPriority);

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (IsBusy)
                    {
                        if (cts.Token.WaitHandle.WaitOne(1000)) break;
                        continue;
                    }

                    if (cts.Token.WaitHandle.WaitOne(60000)) break;

                    lock (_lockObject)
                    {
                        if (CanOptimize && Computer?.Memory?.Physical?.Free != null)
                        {
                            if (LocalMachineSettingsEngine.AutoOptimizationInterval > 0 &&
                                DateTimeOffset.Now.Subtract(_lastAutoOptimizationByInterval).TotalHours >= LocalMachineSettingsEngine.AutoOptimizationInterval)
                            {
                                _ = OptimizeAsync(Enums.Memory.Optimization.Reason.Schedule);
                                _lastAutoOptimizationByInterval = DateTimeOffset.Now;
                            }
                            else if (LocalMachineSettingsEngine.AutoOptimizationMemoryUsage > 0 &&
                                     Computer.Memory.Physical.Free.Percentage < LocalMachineSettingsEngine.AutoOptimizationMemoryUsage &&
                                     DateTimeOffset.Now.Subtract(_lastAutoOptimizationByMemoryUsage).TotalMinutes >= Win32Helper.AutoOptimizationMemoryUsageInterval)
                            {
                                _ = OptimizeAsync(Enums.Memory.Optimization.Reason.LowMemory);
                                _lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e) { ErrorLogging.LogDebug(e); }
            }
        }

        private void MonitorAsync()
        {
            ThreadPool.QueueUserWorkItem(_ => MonitorApp());
            ThreadPool.QueueUserWorkItem(_ => MonitorComputer());
        }

        private void MonitorComputer()
        {
            var cts = _cancellationTokenSource;
            if (cts == null) return;

            CancellationToken token = cts.Token;

            App.SetPriority(LocalMachineSettingsEngine.RunOnPriority);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (IsBusy)
                    {
                        if (token.WaitHandle.WaitOne(1000)) break;
                        continue;
                    }

                    var mem = _computerService.Memory;

                    _dispatcherQueue?.TryEnqueue(() =>
                    {
                        if (Computer != null && _isUiActive)
                        {
                            Computer.Memory = mem;
                            OnPropertyChanged(nameof(Computer));
                            OnPropertyChanged(nameof(VirtualMemoryHeader));
                        }
                    });

                    if (token.WaitHandle.WaitOne(5000)) break;
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { ErrorLogging.LogDebug(e); }
            }
        }

        private void OnOptimizeProgressUpdate(byte value, string step)
        {
            if (_dispatcherQueue == null) return;

            _dispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                int calcPercentage = (value * 100) / OptimizationProgressTotal;

                OptimizationProgressStep = step;

                OptimizationProgressPercentage = (byte)calcPercentage;
                OptimizationProgressValue = value;
            });
        }

        private async Task Optimize(Enums.Memory.Optimization.Reason reason)
        {
            if ((LocalMachineSettingsEngine.MemoryAreas & Enums.Memory.Areas.WindowsOld) != 0)
            {
                var tcs = new TaskCompletionSource<bool>();

                _dispatcherQueue?.TryEnqueue(async () =>
                {
                    try
                    {
                        var xamlRoot = Window.Current?.Content?.XamlRoot;

                        if (xamlRoot == null)
                        {
                            xamlRoot = VisualTreeHelper.GetOpenPopups(null).FirstOrDefault()?.XamlRoot;
                        }

                        ContentDialog warningDialog = new ContentDialog
                        {
                            XamlRoot = xamlRoot,
                            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                            Title = ResourceString.GetString("title_warning_priority"),
                            Content = ResourceString.GetString("msg_warning_windows_old_deletion"),
                            PrimaryButtonText = ResourceString.GetString("txt_yes") ?? "Yes",
                            CloseButtonText = ResourceString.GetString("txt_no") ?? "No",
                            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                            DefaultButton = ContentDialogButton.Primary
                        };

                        ContentDialogResult result = await warningDialog.ShowAsync();
                        tcs.SetResult(result == ContentDialogResult.Primary);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogging.LogDebug(ex);
                        tcs.SetResult(false);
                    }
                });

                if (!await tcs.Task) return;
            }

            string resultMessage = string.Empty;
            byte currentStep = 0;

            try
            {
                IsBusy = true;
                IsOptimizationRunning = true;

                App.SetPriority(LocalMachineSettingsEngine.RunOnPriority);

                OnOptimizeProgressUpdate(++currentStep, ResourceString.GetString("txt_progress_preparing"));
                await Task.Delay(500);

                long startPhysical, startVirtual, startDisk = 0;
                bool isDiskCleanupSelected = (LocalMachineSettingsEngine.MemoryAreas & (Enums.Memory.Areas.DiskCleanup | Enums.Memory.Areas.WindowsOld)) != 0;

                _computerService.RefreshMemory();
                startPhysical = _computerService.Memory.Physical.Free.Bytes;
                startVirtual = _computerService.Memory.Virtual.Free.Bytes;

                if (isDiskCleanupSelected)
                {
                    string? root = Path.GetPathRoot(Environment.SystemDirectory);
                    if (root != null) startDisk = new DriveInfo(root).AvailableFreeSpace;
                }

                OnOptimizeProgressUpdate(++currentStep, ResourceString.GetString("txt_progress_optimizing"));

                await _computerService.Optimize(reason, LocalMachineSettingsEngine.MemoryAreas);

                _computerService.RefreshMemory();
                OnOptimizeProgressUpdate(++currentStep, ResourceString.GetString("txt_progress_finalizing"));
                await Task.Delay(500);

                _dispatcherQueue?.TryEnqueue(() =>
                {
                    if (Computer != null) Computer.Memory = _computerService.Memory;
                    OnPropertyChanged(nameof(Computer));
                });

                var physicalDiff = Math.Max(0, _computerService.Memory.Physical.Free.Bytes - startPhysical);
                var virtualDiff = Math.Max(0, _computerService.Memory.Virtual.Free.Bytes - startVirtual);
                long diskDiff = 0;

                if (isDiskCleanupSelected)
                {
                    string? root = Path.GetPathRoot(Environment.SystemDirectory);
                    if (root != null) diskDiff = Math.Max(0, new DriveInfo(root).AvailableFreeSpace - startDisk);
                }

                var tcsMsg = new TaskCompletionSource<string>();
                _dispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        string msg = ResourceHelper.GetOptimizationResultMessage(
                            reason.ToString(),
                            physicalDiff.ToMemoryUnit(),
                            virtualDiff.ToMemoryUnit(),
                            diskDiff.ToMemoryUnit(),
                            LocalMachineSettingsEngine.ShowVirtualMemory,
                            isDiskCleanupSelected);
                        tcsMsg.SetResult(msg);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogging.LogDebug(ex);
                        tcsMsg.SetResult("Optimization completed.");
                    }
                });
                resultMessage = await tcsMsg.Task;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                resultMessage = "Error occurred.";
            }
            finally
            {
                OnOptimizeProgressUpdate(OptimizationProgressTotal, ResourceString.GetString("txt_optimization_completed"));
                IsOptimizationRunning = false;
                IsBusy = false;
                ResetProgressAfterDelay(10000);

                _dispatcherQueue?.TryEnqueue(() => OnOptimizeCommandCompleted?.Invoke(reason, resultMessage));
            }
        }

        private async Task OptimizeAsync(Enums.Memory.Optimization.Reason reason)
        {
            if (IsOptimizationRunning) return;
            try
            {
                OptimizationProgressStep = ResourceString.GetString("txt_progress_step");
                OptimizationProgressValue = 0;
                OptimizationProgressTotal = 4;
                await Task.Run(() => Optimize(reason));
            }
            catch (Exception e) { ErrorLogging.LogDebug(e); }
        }

        private async void ResetProgressAfterDelay(int milliseconds)
        {
            await Task.Delay(milliseconds);
            _dispatcherQueue?.TryEnqueue(() =>
            {
                OptimizationProgressPercentage = 0;
                OptimizationProgressValue = 0;
                OptimizationProgressStep = ResourceString.GetString("txt_progress_step");
            });
        }

        private void OnHotkeySettingsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(OptimizationKey));
            OnPropertyChanged(nameof(OptimizationModifiers));
            OnPropertyChanged(nameof(UseHotkey));
        }

        public void ReinitializeAfterHibernation()
        {
            try
            {
                lock (_lockObject)
                {
                    _isReiniziliating = true;

                    if (UseHotkey)
                    {
                        IsOptimizationKeyValid = App.NotifyHotkeySettingsChanged();
                    }

                    if (Computer != null)
                    {
                        Computer.Memory = _computerService.Memory;
                    }

                    OnPropertyChanged(string.Empty);
                    App.ReleaseMemory();
                }
            }
            catch { }
            finally { _isReiniziliating = false; }
        }

        public void PauseUiUpdates()
        {
            _isUiActive = false;
        }

        public void ResumeUiUpdates()
        {
            _isUiActive = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                App.HotkeySettingsChanged -= OnHotkeySettingsChanged;

                _cancellationTokenSource?.Cancel();

                _cancellationTokenSource?.Dispose();

                _cancellationTokenSource = null;

                _computerService.OnOptimizeProgressUpdate -= OnOptimizeProgressUpdate;
            }
            base.Dispose(disposing);
        }

        #region ObservableItem<T> class
        public class ObservableItem<T> : ObservableObject
        {
            private bool _isEnabled;
            private string _tooltip;

            public ObservableItem(string name, Func<T> getter, Action<T> setter, bool isEnabled = true, string tooltip = "")
            {
                Getter = getter;
                _isEnabled = isEnabled;
                Name = name;
                Setter = setter;
                _tooltip = tooltip;
            }

            public Func<T> Getter { get; private set; }
            public string Name { get; private set; }
            public Action<T> Setter { get; private set; }
            public string Tooltip { get => _tooltip; set { _tooltip = value; OnPropertyChanged(); } }
            public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }
            public T Value
            {
                get { return Getter != null ? Getter() : default(T)!; }
                set { if (Setter != null) { Setter(value); OnPropertyChanged(); } }
            }
        }
        #endregion
    }
}