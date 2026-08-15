// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class HomePageViewModel : ViewModelBase, IDisposable
    {
        #region Native Methods & Structs
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        #endregion

        #region Fields
        private readonly HomePageModel _model = new HomePageModel();
        private readonly SystemDiagnostics _monitoringService = new SystemDiagnostics();
        private readonly WeatherService _weatherService = new WeatherService();

        private System.Threading.Timer? _telemetryTimer;
        private DispatcherTimer? _weatherTimer;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public event Action<TelemetryDataPayload>? OnTelemetryTicked;

        private int _isUpdatingTelemetry = 0;
        private int _monitoringTick = 0;
        private double _displayCpuUsage = 0;
        private double _displayGpuUsage = 0;
        private double _displayDownMbps = 0;
        private double _displayUpMbps = 0;
        private double _lastRamPercentage = 0;
        private string _lastPCount = "0";
        private string _lastSCount = "0";

        private ulong _prevIdleTime;
        private ulong _prevKernelTime;
        private ulong _prevUserTime;
        private long _prevNetworkDownBytes;
        private long _prevNetworkUpBytes;
        private DateTime _lastNetworkCheckTime = DateTime.MinValue;
        private bool _isFirstTick = true;

        private PerformanceCounterCategory? _gpuCategory;
        private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new();
        private DateTime _lastGpuInstanceRefresh = DateTime.MinValue;
        private NetworkInterface[]? _cachedNetworkInterfaces;
        private DateTime _lastNetworkInterfaceRefresh = DateTime.MinValue;

        private ObservableCollection<HomePageModel> _displayData = new();
        private ObservableCollection<DriveSpaceInfo> _diskDrives = new();
        private ObservableCollection<DailyForecast> _fiveDayForecast = new ObservableCollection<DailyForecast>();
        private ObservableCollection<string> _availableCities = new ObservableCollection<string>();

        private string? _currentWeatherIcon = "ms-appx:///Assets/ImagePackages/Sunny.png";
        private string _weatherDescription = "Loading...";
        private string _weatherTemperature = "--°";
        private string _weatherLocation;
        private string _currentTime = DateTime.Now.ToString("HH:mm");
        private string _currentDate = DateTime.Now.ToString("dddd, MMMM d");
        private double _downloadSpeed;
        private double _uploadSpeed;

        private readonly object _gpuLock = new object();
        #endregion

        #region Memory Boost Card Properties
        private string _ramUsageGbText = "0.0 / 0.0 GB";
        public string RamUsageGbText { get => _ramUsageGbText; set { _ramUsageGbText = value; OnPropertyChanged(); } }

        private string _totalRamGb = "0.0 GB";
        public string TotalRamGb { get => _totalRamGb; set { _totalRamGb = value; OnPropertyChanged(); } }

        private string _usedRamGb = "0.0 GB";
        public string UsedRamGb { get => _usedRamGb; set { _usedRamGb = value; OnPropertyChanged(); } }

        private string _availableRamGb = "0.0 GB";
        public string AvailableRamGb { get => _availableRamGb; set { _availableRamGb = value; OnPropertyChanged(); } }

        private string _systemCacheGb = "0.0 GB";
        public string SystemCacheGb { get => _systemCacheGb; set { _systemCacheGb = value; OnPropertyChanged(); } }

        private string _ramUsagePercentageText = "0%";
        public string RamUsagePercentageText { get => _ramUsagePercentageText; set { _ramUsagePercentageText = value; OnPropertyChanged(); } }

        private string _availableRamPercentageText = "0%";
        public string AvailableRamPercentageText { get => _availableRamPercentageText; set { _availableRamPercentageText = value; OnPropertyChanged(); } }

        private double _averageRamLoad = 0;
        public double AverageRamLoad { get => _averageRamLoad; set { _averageRamLoad = value; OnPropertyChanged(); } }

        private string _lastBoostFreedText = "Last run: -- MB";
        public string LastBoostFreedText { get => _lastBoostFreedText; set { _lastBoostFreedText = value; OnPropertyChanged(); }
        }
        #endregion

        #region Properties
        public int GpuUsageDisplay => HardwareData.Gpu.Usage;
        public int GpuUsagePercentage => HardwareData.Gpu.Usage;

        public double CpuUsage => HardwareData.Processor.Usage;
        public string CpuUsageText => CpuUsage.ToString("F0");

        public double RamUsage => HardwareData.Memory.Usage;
        public string RamUsageText => RamUsage.ToString("F0");

        public string? OSName
        {
            get
            {
                var info = this["OSName"];
                return info != null ? info.Data : "Windows";
            }
        }

        public string? OSVersion
        {
            get
            {
                var info = this["OSVersion"];
                return info != null ? info.Data : string.Empty;
            }
        }

        public string NetworkName => HardwareData.NetworkAdapter ?? "Disconnected";
        public string PublicIP => HardwareData.UserIPAddress ?? "0.0.0.0";
        public string LocalIPValue => HardwareData.LocalIPAddress ?? "127.0.0.1";

        public double DownloadSpeedMbps { get; set; }
        public double UploadSpeedMbps { get; set; }

        public Visibility VisionVisibility => SetVisibility;
        public bool IsVisionChecked
        {
            get => StateButtonVision;
            set { StateButtonVision = value; OnPropertyChanged(); }
        }

        public ObservableCollection<HomePageModel> DisplayData
        {
            get => _displayData;
            set { _displayData = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DriveSpaceInfo> DiskDrives
        {
            get => _diskDrives;
            set { _diskDrives = value; OnPropertyChanged(); }
        }

        public string? CurrentWeatherIcon
        {
            get => _currentWeatherIcon;
            set { _currentWeatherIcon = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DailyForecast> FiveDayForecast
        {
            get => _fiveDayForecast;
            set { _fiveDayForecast = value; OnPropertyChanged(); }
        }

        public string WeatherDescription
        {
            get => _weatherDescription;
            set { _weatherDescription = value; OnPropertyChanged(); }
        }

        public string WeatherTemperature
        {
            get => _weatherTemperature;
            set { _weatherTemperature = value; OnPropertyChanged(); }
        }

        public string WeatherLocation
        {
            get => _weatherLocation;
            set
            {
                if (_weatherLocation != value)
                {
                    _weatherLocation = value;
                    OnPropertyChanged();
                    _ = FetchWeatherAsync(value, _cts.Token);
                }
            }
        }

        public ObservableCollection<string> AvailableCities
        {
            get => _availableCities;
            set { _availableCities = value; OnPropertyChanged(); }
        }

        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        public string CurrentDate
        {
            get => _currentDate;
            set { _currentDate = value; OnPropertyChanged(); }
        }

        public double DownloadSpeed
        {
            get => _downloadSpeed;
            set { _downloadSpeed = value; OnPropertyChanged(); }
        }

        public double UploadSpeed
        {
            get => _uploadSpeed;
            set { _uploadSpeed = value; OnPropertyChanged(); }
        }

        public class IPWrapper
        {
            public string Data { get; set; } = "0.0.0.0";
        }

        private IPWrapper _localIP = new IPWrapper();
        public IPWrapper LocalIP
        {
            get => _localIP;
            set
            {
                if (_localIP == value) return;
                _localIP = value;
                OnPropertyChanged(nameof(LocalIP));
            }
        }

        public Visibility SetVisibility
        {
            get => _model.IpVisibility;
            set
            {
                if (_model.IpVisibility != value)
                {
                    _model.IpVisibility = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SetBlurValue
        {
            get => _model.BlurValue;
            set
            {
                if (_model.BlurValue != value)
                {
                    _model.BlurValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool StateButtonVision
        {
            get => SettingsEngine.IsHiddenIpAddress;
            set
            {
                if (SettingsEngine.IsHiddenIpAddress != value)
                {
                    SettingsEngine.IsHiddenIpAddress = value;
                    SetBlurValue = value ? 0 : 20;
                    OnPropertyChanged();
                }
            }
        }

        public Visibility IpVisibility => SystemDiagnostics.isIPAddressFormatValid ? Visibility.Visible : Visibility.Collapsed;

        public HomePageModel? this[string name]
        {
            get => DisplayData.FirstOrDefault(x => x.Name == name);
        }

        public HomePageViewModel OSInfo => this;
        public HomePageViewModel SystemStats => this;
        #endregion

        #region Constructor & Initialization
        public HomePageViewModel()
        {
            AvailableCities = new ObservableCollection<string>
            {
                "New York", "Los Angeles", "Chicago", "Toronto", "Mexico City", "Vancouver", "Miami", "Houston",
                "London", "Paris", "Berlin", "Amsterdam", "Rome", "Madrid", "Barcelona", "Moscow", "Istanbul",
                "Vienna", "Saint Petersburg", "Dublin", "Zurich", "Lisbon",
                "Tokyo", "Beijing", "Shanghai", "Seoul", "Delhi", "Mumbai", "Singapore", "Hong Kong", "Bangkok",
                "Jakarta", "Manila", "Taipei", "Kuala Lumpur", "Riyadh", "Dubai", "Tel Aviv",
                "Rio de Janeiro", "Sao Paulo", "Buenos Aires", "Lima", "Santiago", "Bogotá",
                "Cairo", "Lagos", "Johannesburg", "Cape Town", "Nairobi", "Casablanca",
                "Sydney", "Melbourne", "Auckland", "Perth", "Brisbane"
            };

            SystemDiagnostics.InitCpuBaseline();

            _weatherLocation = LoadLocationFromRegistry();
            Task.Run(() => _monitoringService.GetHardwareData());

            LocalIP = new IPWrapper { Data = _monitoringService.GetDefaultLocalIP() };

            LoadDisplayData();
            LoadDiskData();

            _ = FetchWeatherAsync(_weatherLocation, _cts.Token);

            InitHardwareState();
            SetupWeatherTimer();
        }

        private void InitHardwareState()
        {
            if (GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
            {
                _prevIdleTime = ((ulong)idleTime.dwHighDateTime << 32) | idleTime.dwLowDateTime;
                _prevKernelTime = ((ulong)kernelTime.dwHighDateTime << 32) | kernelTime.dwLowDateTime;
                _prevUserTime = ((ulong)userTime.dwHighDateTime << 32) | userTime.dwLowDateTime;
            }
        }
        #endregion

        #region Background Engine (High-Performance Telemetry)
        public void ResumeUpdates()
        {
            if (_telemetryTimer == null)
            {
                _telemetryTimer = new System.Threading.Timer(TelemetryTimer_Tick, null, 0, 200);
            }
            else
            {
                _telemetryTimer.Change(0, 200);
            }

            _weatherTimer?.Start();
            Debug.WriteLine("[HomePageVM] Background timers RESUMED.");
        }

        public void PauseUpdates()
        {
            _telemetryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _weatherTimer?.Stop();
            Debug.WriteLine("[HomePageVM] Background timers PAUSED.");
        }

        private void TelemetryTimer_Tick(object? state)
        {
            if (Interlocked.CompareExchange(ref _isUpdatingTelemetry, 1, 0) != 0) return;

            try
            {
                _monitoringTick++;
                bool isFullSecond = _monitoringTick >= 5;
                if (isFullSecond) _monitoringTick = 0;

                double rawCpu = 0;
                if (GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
                {
                    ulong curIdleTime = ((ulong)idleTime.dwHighDateTime << 32) | idleTime.dwLowDateTime;
                    ulong curKernelTime = ((ulong)kernelTime.dwHighDateTime << 32) | kernelTime.dwLowDateTime;
                    ulong curUserTime = ((ulong)userTime.dwHighDateTime << 32) | userTime.dwLowDateTime;

                    ulong idleDiff = curIdleTime - _prevIdleTime;
                    ulong kernelDiff = curKernelTime - _prevKernelTime;
                    ulong userDiff = curUserTime - _prevUserTime;
                    ulong totalSystemTime = kernelDiff + userDiff;

                    _prevIdleTime = curIdleTime;
                    _prevKernelTime = curKernelTime;
                    _prevUserTime = curUserTime;

                    if (totalSystemTime > 0)
                    {
                        rawCpu = (double)((totalSystemTime - idleDiff) * 100.0 / totalSystemTime);
                        rawCpu = Math.Clamp(rawCpu, 0, 100);
                    }
                }

                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                double rawRam = 0;
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    rawRam = memStatus.dwMemoryLoad;

                    if (isFullSecond)
                    {
                        double totalGb = memStatus.ullTotalPhys / 1073741824.0;
                        double availGb = memStatus.ullAvailPhys / 1073741824.0;
                        double usedGb = totalGb - availGb;

                        double cacheGb = (memStatus.ullTotalPageFile - memStatus.ullAvailPageFile) / 1073741824.0 * 0.4;

                        App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            TotalRamGb = $"{totalGb:F1} GB";
                            UsedRamGb = $"{usedGb:F1} GB";
                            AvailableRamGb = $"{availGb:F1} GB";
                            SystemCacheGb = $"{Math.Max(0.1, cacheGb):F1} GB";

                            RamUsageGbText = $"{usedGb:F1} / {totalGb:F1} GB";
                            RamUsagePercentageText = $"{rawRam}%";
                            AvailableRamPercentageText = $"{100 - rawRam}%";
                        });
                    }
                }

                var netUsage = GetNetworkUsage();
                DateTime now = DateTime.Now;
                double timeDiff = (now - _lastNetworkCheckTime).TotalSeconds;

                double rawDlMbps = 0, rawUlMbps = 0;
                if (timeDiff > 0 && !_isFirstTick)
                {
                    if (netUsage.Down >= _prevNetworkDownBytes)
                        rawDlMbps = ((netUsage.Down - _prevNetworkDownBytes) * 8.0) / timeDiff / 1_000_000.0;

                    if (netUsage.Up >= _prevNetworkUpBytes)
                        rawUlMbps = ((netUsage.Up - _prevNetworkUpBytes) * 8.0) / timeDiff / 1_000_000.0;
                }

                _prevNetworkDownBytes = netUsage.Down;
                _prevNetworkUpBytes = netUsage.Up;
                _lastNetworkCheckTime = now;
                _isFirstTick = false;

                double rawGpu = GetGpuUsage();

                _displayCpuUsage = (_displayCpuUsage * 0.8) + (rawCpu * 0.2);
                _displayGpuUsage = (_displayGpuUsage * 0.8) + (rawGpu * 0.2);
                _displayDownMbps = (_displayDownMbps * 0.8) + (rawDlMbps * 0.2);
                _displayUpMbps = (_displayUpMbps * 0.8) + (rawUlMbps * 0.2);
                _lastRamPercentage = rawRam;

                if (isFullSecond)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var pCount = await _monitoringService.GetProcessCountAsync();
                            var sCount = await _monitoringService.GetServicesCount();

                            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                            {
                                _lastPCount = pCount;
                                _lastSCount = sCount;
                                RefreshStats(pCount, sCount);
                            });
                        }
                        catch { }
                    });
                }

                var payload = new TelemetryDataPayload
                {
                    Cpu = _displayCpuUsage,
                    Ram = _lastRamPercentage,
                    Gpu = _displayGpuUsage,
                    NetDown = _displayDownMbps,
                    NetUp = _displayUpMbps,
                    ProcCount = _lastPCount,
                    SvcCount = _lastSCount,
                    IsFullSecond = isFullSecond
                };

                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    if (isFullSecond) UpdateDateTime();
                    OnTelemetryTicked?.Invoke(payload);
                });
            }
            catch { }
            finally
            {
                _isUpdatingTelemetry = 0;
            }
        }

        private (long Down, long Up) GetNetworkUsage()
        {
            try
            {
                long d = 0, u = 0;
                if (_cachedNetworkInterfaces == null || (DateTime.Now - _lastNetworkInterfaceRefresh).TotalSeconds >= 60)
                {
                    var allInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                    var mainInterface = allInterfaces.FirstOrDefault(ni =>
                        ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        ni.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));

                    if (mainInterface == null)
                    {
                        mainInterface = allInterfaces.FirstOrDefault(ni =>
                            ni.OperationalStatus == OperationalStatus.Up &&
                            (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                            !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                            !ni.Description.Contains("Pseudo", StringComparison.OrdinalIgnoreCase));
                    }

                    _cachedNetworkInterfaces = mainInterface != null ? new[] { mainInterface } : Array.Empty<NetworkInterface>();
                    _lastNetworkInterfaceRefresh = DateTime.Now;
                }

                foreach (var ni in _cachedNetworkInterfaces)
                {
                    try
                    {
                        var stats = ni.GetIPStatistics();
                        d += stats.BytesReceived;
                        u += stats.BytesSent;
                    }
                    catch { }
                }
                return (d, u);
            }
            catch { return (0, 0); }
        }

        private float GetGpuUsage()
        {
            try
            {
                if (_gpuCategory == null)
                {
                    _gpuCategory = new PerformanceCounterCategory("GPU Engine");
                }

                if ((DateTime.Now - _lastGpuInstanceRefresh).TotalSeconds >= 10)
                {
                    var currentInstances = _gpuCategory.GetInstanceNames()
                        .Where(i => i.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                        .ToHashSet();

                    lock (_gpuLock)
                    {
                        var toRemove = _gpuCounters.Keys.Where(k => !currentInstances.Contains(k)).ToList();
                        foreach (var key in toRemove)
                        {
                            _gpuCounters[key].Dispose();
                            _gpuCounters.Remove(key);
                        }

                        foreach (var instance in currentInstances)
                        {
                            if (!_gpuCounters.ContainsKey(instance))
                            {
                                var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                                counter.NextValue();
                                _gpuCounters[instance] = counter;
                            }
                        }
                    }

                    _lastGpuInstanceRefresh = DateTime.Now;
                }

                float totalUsage = 0;

                lock (_gpuLock)
                {
                    foreach (var counter in _gpuCounters.Values)
                    {
                        totalUsage += counter.NextValue();
                    }
                }

                return Math.Clamp(totalUsage, 0f, 100f);
            }
            catch { return 0f; }
        }
        #endregion

        #region System Data Management
        public void LoadDisplayData()
        {
            _displayData.Clear();
            _displayData.Add(new HomePageModel { Name = "OSName", Data = HardwareData.OS.Name });
            _displayData.Add(new HomePageModel { Name = "OSVersion", Data = HardwareData.OS.Version });
            _displayData.Add(new HomePageModel { Name = "Processes", Data = HardwareData.RunningProcessesCount });
            _displayData.Add(new HomePageModel { Name = "Services", Data = HardwareData.RunningServicesCount });
            _displayData.Add(new HomePageModel { Name = "Network", Data = HardwareData.NetworkAdapter });
            _displayData.Add(new HomePageModel { Name = "IpAddress", Data = HardwareData.UserIPAddress });
            _displayData.Add(new HomePageModel { Name = "Memory", Data = HardwareData.Memory.Data });
            _displayData.Add(new HomePageModel { Name = "Type", Data = HardwareData.Memory.Type });
            _displayData.Add(new HomePageModel { Name = "CPU", Data = HardwareData.Processor.DetailedData });
            _displayData.Add(new HomePageModel { Name = "GPU", Data = HardwareData.Gpu.Data });
            _displayData.Add(new HomePageModel { Name = "Storage", Data = HardwareData.Storage });

            LocalIP = new IPWrapper { Data = HardwareData.LocalIPAddress };
        }

        public void RefreshStats(string processCount, string servicesCount)
        {
            if (_displayData == null) return;

            var proc = _displayData.FirstOrDefault(x => x.Name == "Processes");
            if (proc != null) proc.Data = processCount;

            var svc = _displayData.FirstOrDefault(x => x.Name == "Services");
            if (svc != null) svc.Data = servicesCount;

            if (LocalIP.Data != HardwareData.LocalIPAddress)
            {
                LocalIP = new IPWrapper { Data = HardwareData.LocalIPAddress };
            }

            OnPropertyChanged(nameof(IpVisibility));
            OnPropertyChanged(nameof(GpuUsageDisplay));
            OnPropertyChanged(nameof(GpuUsagePercentage));

            OnPropertyChanged("Item[]");
        }

        public void UpdateDateTime()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm");
            CurrentDate = now.ToString("dddd, MMMM d");
        }

        private void LoadDiskData()
        {
            try
            {
                var driveData = DiskInfoService.GetDrivesData();
                DiskDrives = new ObservableCollection<DriveSpaceInfo>(driveData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Disk Data Error] Failed to load disk drives: {ex.Message}");
            }
        }
        #endregion

        #region Weather Service
        private void SetupWeatherTimer()
        {
            _weatherTimer?.Stop();
            _weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _weatherTimer.Tick += (s, e) => { _ = FetchWeatherAsync(_weatherLocation, _cts.Token); };
            _weatherTimer.Start();
        }

        public async Task FetchWeatherAsync(string? locationOverride = null, CancellationToken token = default, bool forceRefresh = false)
        {
            try
            {
                string loc = locationOverride ?? WeatherLocation;
                if (string.IsNullOrWhiteSpace(loc)) loc = "Paris";

                WeatherData data = await _weatherService.GetWeatherAsync(loc, token, forceRefresh);

                if (data == null || token.IsCancellationRequested) return;

                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    if (_isDisposed || token.IsCancellationRequested || _fiveDayForecast == null) return;

                    WeatherDescription = data.Description;
                    WeatherTemperature = data.TempC.ToString("F0") + "°";

                    _weatherLocation = loc;
                    OnPropertyChanged(nameof(WeatherLocation));

                    CurrentWeatherIcon = data.CurrentIconUrl;

                    if (data.Forecast != null)
                    {
                        FiveDayForecast.Clear();
                        foreach (var day in data.Forecast)
                        {
                            if (_fiveDayForecast == null) return;
                            _fiveDayForecast.Add(day);
                        }
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[Weather UI Error] {ex.Message}"); }
        }

        private static string LoadLocationFromRegistry()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer");
                if (key != null)
                {
                    var saved = key.GetValue("LastLocation") as string;
                    if (!string.IsNullOrWhiteSpace(saved)) return saved;
                }
            }
            catch { }
            return "Paris";
        }

        public void UpdateWeatherData(WeatherData data)
        {
            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                this.WeatherTemperature = $"{data.TempC:F0}°";
                this.WeatherDescription = data.Description;
                this.CurrentWeatherIcon = data.CurrentIconUrl;

                if (data.Forecast != null)
                {
                    this.FiveDayForecast.Clear();
                    foreach (var item in data.Forecast)
                    {
                        this.FiveDayForecast.Add(item);
                    }
                }
            });
        }
        #endregion

        #region Disposal
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isDisposed = true;

                if (_telemetryTimer != null)
                {
                    _telemetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _telemetryTimer.Dispose();
                    _telemetryTimer = null;
                }

                if (_weatherTimer != null)
                {
                    _weatherTimer.Stop();
                    _weatherTimer = null;
                }

                try
                {
                    lock (_gpuLock)
                    {
                        foreach (var counter in _gpuCounters.Values)
                        {
                            counter.Dispose();
                        }
                        _gpuCounters.Clear();
                    }
                }
                catch { }

                try
                {
                    if (!_cts.IsCancellationRequested) _cts.Cancel();
                    _cts.Dispose();
                }
                catch (ObjectDisposedException) { }

                if (_displayData != null) { _displayData.Clear(); _displayData = null!; }
                if (_fiveDayForecast != null) { _fiveDayForecast.Clear(); _fiveDayForecast = null!; }
                if (_diskDrives != null) { _diskDrives.Clear(); _diskDrives = null!; }
                if (_availableCities != null) { _availableCities.Clear(); _availableCities = null!; }

                (_weatherService as IDisposable)?.Dispose();
                (_monitoringService as IDisposable)?.Dispose();

                ClearPropertyChangedListeners();

                Debug.WriteLine("[HomePageVM] Purge: All models and delegates unrooted.");
            }

            base.Dispose(disposing);
        }
        #endregion
    }
}