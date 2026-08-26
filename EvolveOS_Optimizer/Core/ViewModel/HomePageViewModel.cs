// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;

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
        private int _sensorTickCounter = 0;
        private int _isUpdatingGpuSensors = 0;
        private double _displayCpuUsage = 0;
        private double _displayGpuUsage = 0;
        private double _displayDownMbps = 0;
        private double _displayUpMbps = 0;
        private double _lastRamPercentage = 0;
        private double _cachedRawGpu = 0;
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

        private bool _isRefreshingNetworkInterfaces = false;
        private bool _isRefreshingGpuInstances = false;

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

        #region LiveCharts2 Engine Variables
        private readonly List<(long Time, double Value)> _cpuHistory = new();
        private readonly List<(long Time, double Value)> _ramHistory = new();
        private readonly List<(long Time, double Value)> _netDownHistory = new();
        private readonly List<(long Time, double Value)> _netUpHistory = new();
        private readonly List<(long Time, double Value)> _gpuHistory = new();

        private int _maxCpuDataPoints = 300;
        private int _maxGraphSeconds = 60;
        private double _peakNetworkSpeedMbps = 10.0;
        private const int MaxHistoryPoints = 4500; // Store up to 15 mins at 5Hz in background
        private const int MaxVisualPoints = 120;
        private long _currentTick = 0;
        #endregion

        #region LiveCharts2 Graphing Properties

        public ObservableCollection<ObservablePoint> CpuGraphValues { get; } = new();
        public ObservableCollection<ObservablePoint> CpuGraphDot { get; } = new();

        public ObservableCollection<ObservablePoint> RamGraphValues { get; } = new();
        public ObservableCollection<ObservablePoint> RamGraphDot { get; } = new();

        public ObservableCollection<ObservablePoint> GpuGraphValues { get; } = new();
        public ObservableCollection<ObservablePoint> GpuGraphDot { get; } = new();

        public ObservableCollection<ObservablePoint> NetDownGraphValues { get; } = new();
        public ObservableCollection<ObservablePoint> NetDownGraphDot { get; } = new();

        public ObservableCollection<ObservablePoint> NetUpGraphValues { get; } = new();
        public ObservableCollection<ObservablePoint> NetUpGraphDot { get; } = new();

        public ISeries[] CpuGraphSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] RamGraphSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] GpuGraphSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] NetGraphSeries { get; set; } = Array.Empty<ISeries>();

        public ObservableCollection<ICartesianAxis> HiddenXAxes { get; } = new();
        public ObservableCollection<ICartesianAxis> HiddenYAxes { get; } = new();
        public ObservableCollection<ICartesianAxis> DynamicNetYAxes { get; } = new();

        private string _xAxisLabelStart = "-60 SEC";
        public string XAxisLabelStart { get => _xAxisLabelStart; set => SetProperty(ref _xAxisLabelStart, value); }

        private string _xAxisLabelQ1 = "-45 SEC";
        public string XAxisLabelQ1 { get => _xAxisLabelQ1; set => SetProperty(ref _xAxisLabelQ1, value); }

        private string _xAxisLabelMid = "-30 SEC";
        public string XAxisLabelMid { get => _xAxisLabelMid; set => SetProperty(ref _xAxisLabelMid, value); }

        private string _xAxisLabelQ3 = "-15 SEC";
        public string XAxisLabelQ3 { get => _xAxisLabelQ3; set => SetProperty(ref _xAxisLabelQ3, value); }

        private string _netYAxis100 = "10";
        public string NetYAxis100 { get => _netYAxis100; set => SetProperty(ref _netYAxis100, value); }

        private string _netYAxis75 = "7.5";
        public string NetYAxis75 { get => _netYAxis75; set => SetProperty(ref _netYAxis75, value); }

        private string _netYAxis50 = "5";
        public string NetYAxis50 { get => _netYAxis50; set => SetProperty(ref _netYAxis50, value); }

        private string _netYAxis25 = "2.5";
        public string NetYAxis25 { get => _netYAxis25; set => SetProperty(ref _netYAxis25, value); }

        public int SelectedGraphTimeframeIndex
        {
            get => SettingsEngine.Dashboard_GraphTimeframe;
            set
            {
                if (SettingsEngine.Dashboard_GraphTimeframe != value)
                {
                    SettingsEngine.Dashboard_GraphTimeframe = value;
                    OnPropertyChanged();

                    MaxGraphSeconds = value switch
                    {
                        1 => 300,
                        2 => 900,
                        _ => 60
                    };
                }
            }
        }

        public int MaxGraphSeconds
        {
            get => _maxGraphSeconds;
            set
            {
                if (_maxGraphSeconds != value)
                {
                    _maxGraphSeconds = value;
                    _maxCpuDataPoints = value * 5;

                    UpdateAxisLabels(value);

                    App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        RebuildGraphsFromHistory();
                    });
                }
            }
        }

        #endregion

        #region Dashboard Settings

        public bool SaveCardStates
        {
            get => SettingsEngine.SaveCardExpandedStates;
            set
            {
                if (SettingsEngine.SaveCardExpandedStates != value)
                {
                    SettingsEngine.SaveCardExpandedStates = value;
                    OnPropertyChanged();
                }
            }
        }

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
        public string LastBoostFreedText
        {
            get => _lastBoostFreedText; set { _lastBoostFreedText = value; OnPropertyChanged(); }
        }
        #endregion

        #region GPU Card Properties

        private int _gpuUsageDisplay;
        public int GpuUsageDisplay { get => _gpuUsageDisplay; set { _gpuUsageDisplay = value; OnPropertyChanged(); } }

        private string _gpuTemperatureStr = "--°C";
        public string GpuTemperatureStr { get => _gpuTemperatureStr; set { _gpuTemperatureStr = value; OnPropertyChanged(); } }

        private string _gpuVramUsedStr = "-- GB";
        public string GpuVramUsedStr { get => _gpuVramUsedStr; set { _gpuVramUsedStr = value; OnPropertyChanged(); } }

        private string _gpuPowerDrawStr = "-- W";
        public string GpuPowerDrawStr { get => _gpuPowerDrawStr; set { _gpuPowerDrawStr = value; OnPropertyChanged(); } }

        #endregion

        #region CPU Card Properties

        private int _cpuUsageDisplay;
        public int CpuUsageDisplay { get => _cpuUsageDisplay; set { _cpuUsageDisplay = value; OnPropertyChanged(); } }

        private string _cpuTempStr = "--°C";
        public string CpuTempStr { get => _cpuTempStr; set { _cpuTempStr = value; OnPropertyChanged(); } }

        private string _cpuClockStr = "-- MHz";
        public string CpuClockStr { get => _cpuClockStr; set { _cpuClockStr = value; OnPropertyChanged(); } }

        private string _cpuPowerDrawStr = "-- W";
        public string CpuPowerDrawStr { get => _cpuPowerDrawStr; set { _cpuPowerDrawStr = value; OnPropertyChanged(); } }

        #endregion

        #region CPU Card Power Plan Properties

        private ObservableCollection<ComboBoxDisplayOption> _availablePowerPlans = new();
        public ObservableCollection<ComboBoxDisplayOption> AvailablePowerPlans
        {
            get => _availablePowerPlans;
            set { _availablePowerPlans = value; OnPropertyChanged(); }
        }

        private object? _selectedPowerPlan;
        public object? SelectedPowerPlan
        {
            get => _selectedPowerPlan;
            set
            {
                if (_selectedPowerPlan != value)
                {
                    _selectedPowerPlan = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Properties
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
            Task.Run(() =>
            {
                _monitoringService.GetHardwareData();

                HardwareTemperatureService.Instance.Initialize();

                InitializePowerPlans();
            });

            LocalIP = new IPWrapper { Data = _monitoringService.GetDefaultLocalIP() };

            LoadDisplayData();
            LoadDiskData();

            InitializeLiveCharts();

            _ = FetchWeatherAsync(_weatherLocation, _cts.Token);

            InitHardwareState();

            MaxGraphSeconds = SettingsEngine.Dashboard_GraphTimeframe switch
            {
                1 => 300,
                2 => 900,
                _ => 60
            };

            SetupWeatherTimer();
        }

        private void InitializeLiveCharts()
        {
            var cpuColor = SKColor.Parse("#0078D7");
            var ramColor = SKColor.Parse("#881798");
            var gpuColor = SKColor.Parse("#107C10");
            var netDownColor = SKColor.Parse("#0078D7");
            var netUpColor = SKColor.Parse("#881798");

            HiddenXAxes.Clear();
            HiddenYAxes.Clear();
            DynamicNetYAxes.Clear();

            HiddenXAxes.Add(new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 300, AnimationsSpeed = TimeSpan.FromMilliseconds(220) });
            HiddenYAxes.Add(new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 });
            DynamicNetYAxes.Add(new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 10 });

            CpuGraphSeries = new ISeries[] {
                new LineSeries<ObservablePoint> { Values = CpuGraphValues, Fill = new LinearGradientPaint(new[] { cpuColor.WithAlpha(100), cpuColor.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)), Stroke = new SolidColorPaint(cpuColor) { StrokeThickness = 2.5f }, GeometrySize = 0, LineSmoothness = 0, AnimationsSpeed = TimeSpan.FromMilliseconds(220) },
                new ScatterSeries<ObservablePoint> { Values = CpuGraphDot, Fill = new SolidColorPaint(cpuColor), GeometrySize = 10, AnimationsSpeed = TimeSpan.FromMilliseconds(220) }
            };

            RamGraphSeries = new ISeries[] {
                new LineSeries<ObservablePoint> { Values = RamGraphValues, Fill = new LinearGradientPaint(new[] { ramColor.WithAlpha(100), ramColor.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)), Stroke = new SolidColorPaint(ramColor) { StrokeThickness = 2.5f }, GeometrySize = 0, LineSmoothness = 0, AnimationsSpeed = TimeSpan.FromMilliseconds(220) },
                new ScatterSeries<ObservablePoint> { Values = RamGraphDot, Fill = new SolidColorPaint(ramColor), GeometrySize = 10, AnimationsSpeed = TimeSpan.FromMilliseconds(220) }
            };

            GpuGraphSeries = new ISeries[] {
                new LineSeries<ObservablePoint> { Values = GpuGraphValues, Fill = new LinearGradientPaint(new[] { gpuColor.WithAlpha(100), gpuColor.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)), Stroke = new SolidColorPaint(gpuColor) { StrokeThickness = 2.5f }, GeometrySize = 0, LineSmoothness = 0, AnimationsSpeed = TimeSpan.FromMilliseconds(220) },
                new ScatterSeries<ObservablePoint> { Values = GpuGraphDot, Fill = new SolidColorPaint(gpuColor), GeometrySize = 10, AnimationsSpeed = TimeSpan.FromMilliseconds(220) }
            };

            NetGraphSeries = new ISeries[] {
                new LineSeries<ObservablePoint> { Values = NetDownGraphValues, Fill = new LinearGradientPaint(new[] { netDownColor.WithAlpha(100), netDownColor.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)), Stroke = new SolidColorPaint(netDownColor) { StrokeThickness = 2.5f }, GeometrySize = 0, LineSmoothness = 0, AnimationsSpeed = TimeSpan.FromMilliseconds(220) },
                new ScatterSeries<ObservablePoint> { Values = NetDownGraphDot, Fill = new SolidColorPaint(netDownColor), GeometrySize = 10, AnimationsSpeed = TimeSpan.FromMilliseconds(220) },
                new LineSeries<ObservablePoint> { Values = NetUpGraphValues, Fill = new LinearGradientPaint(new[] { netUpColor.WithAlpha(100), netUpColor.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)), Stroke = new SolidColorPaint(netUpColor) { StrokeThickness = 2f }, GeometrySize = 0, LineSmoothness = 0, AnimationsSpeed = TimeSpan.FromMilliseconds(220) },
                new ScatterSeries<ObservablePoint> { Values = NetUpGraphDot, Fill = new SolidColorPaint(netUpColor), GeometrySize = 8, AnimationsSpeed = TimeSpan.FromMilliseconds(220) }
            };
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

        #region Powerplan

        private void InitializePowerPlans()
        {
            Task.Run(async () =>
            {
                try
                {
                    var predefinedPlans = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "381b4222-f694-41f0-9685-ff5bb260df2e", "Balanced" },
                        { "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "High performance" },
                        { "a1841308-3541-4fab-bc81-f71556f20b4a", "Power saver" },
                        { "e9a42b02-d5df-448d-aa00-03f14749eb61", "Ultimate Performance" }
                    };

                    string output = await CommandExecutor.StartTask("powercfg /l");
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    var options = new List<ComboBoxDisplayOption>();
                    ComboBoxDisplayOption? activeOption = null;
                    var installedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var line in lines)
                    {
                        if (line.Contains("Power Scheme GUID:"))
                        {
                            int guidStart = line.IndexOf(":") + 1;
                            int guidEnd = line.IndexOf("(");
                            string guid = line.Substring(guidStart, guidEnd - guidStart).Trim();
                            installedGuids.Add(guid);

                            int nameStart = line.IndexOf("(") + 1;
                            int nameEnd = line.IndexOf(")");
                            string name = line.Substring(nameStart, nameEnd - nameStart).Trim();

                            bool isActive = line.Contains("*");

                            var uiState = new PowerPlanComboBoxOption
                            {
                                ExistsOnSystem = true,
                                IsActive = isActive
                            };

                            var displayOption = new ComboBoxDisplayOption(name, guid, null, uiState);
                            options.Add(displayOption);

                            if (isActive)
                            {
                                activeOption = displayOption;
                            }
                        }
                    }

                    foreach (var kvp in predefinedPlans)
                    {
                        if (!installedGuids.Contains(kvp.Key))
                        {
                            var uiState = new PowerPlanComboBoxOption
                            {
                                ExistsOnSystem = false,
                                IsActive = false
                            };
                            options.Add(new ComboBoxDisplayOption(kvp.Value, kvp.Key, null, uiState));
                        }
                    }

                    App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        AvailablePowerPlans.Clear();
                        foreach (var opt in options)
                        {
                            AvailablePowerPlans.Add(opt);
                        }

                        SelectedPowerPlan = activeOption?.Value;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Power Plan Init Error] {ex.Message}");
                }
            });
        }

        public void ApplySelectedPowerPlan(object selectedItem)
        {
            string? guid = null;

            if (selectedItem is string s)
            {
                guid = s;
            }
            else if (selectedItem is ComboBoxDisplayOption option)
            {
                guid = option.Value?.ToString();
            }

            if (!string.IsNullOrEmpty(guid))
            {
                var match = AvailablePowerPlans.FirstOrDefault(p => p.Value?.ToString() == guid);
                bool needsInstall = false;

                if (match != null && match.Tag is PowerPlanComboBoxOption state)
                {
                    needsInstall = !state.ExistsOnSystem;
                }

                Task.Run(async () =>
                {
                    try
                    {
                        if (needsInstall)
                        {
                            await CommandExecutor.StartTask($"powercfg -duplicatescheme {guid}");
                        }

                        await CommandExecutor.StartTask($"powercfg /setactive {guid}");

                        await Task.Delay(200);
                        InitializePowerPlans();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Power Plan Apply Error] {ex.Message}");
                    }
                });
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
            System.Diagnostics.Debug.WriteLine("[HomePageVM] Background timers RESUMED.");
        }

        public void PauseUpdates()
        {
            _telemetryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _weatherTimer?.Stop();
            System.Diagnostics.Debug.WriteLine("[HomePageVM] Background timers PAUSED.");
        }

        private void TelemetryTimer_Tick(object? state)
        {
            if (Interlocked.CompareExchange(ref _isUpdatingTelemetry, 1, 0) != 0) return;

            try
            {
                _monitoringTick++;
                _currentTick++;
                bool isFullSecond = _monitoringTick >= 5;
                if (isFullSecond) _monitoringTick = 0;

                _sensorTickCounter++;
                bool isSensorTick = _sensorTickCounter >= 15;
                if (isSensorTick) _sensorTickCounter = 0;

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

                if (isFullSecond)
                {
                    _cachedRawGpu = GetGpuUsage();
                }
                double rawGpu = _cachedRawGpu;

                _displayCpuUsage = (_displayCpuUsage * 0.8) + (rawCpu * 0.2);
                _displayGpuUsage = (_displayGpuUsage * 0.8) + (rawGpu * 0.2);
                _displayDownMbps = (_displayDownMbps * 0.8) + (rawDlMbps * 0.2);
                _displayUpMbps = (_displayUpMbps * 0.8) + (rawUlMbps * 0.2);
                _lastRamPercentage = rawRam;

                _cpuHistory.Add((_currentTick, _displayCpuUsage));
                _ramHistory.Add((_currentTick, _lastRamPercentage));
                _gpuHistory.Add((_currentTick, _displayGpuUsage));
                _netDownHistory.Add((_currentTick, _displayDownMbps));
                _netUpHistory.Add((_currentTick, _displayUpMbps));

                while (_cpuHistory.Count > MaxHistoryPoints) _cpuHistory.RemoveAt(0);
                while (_ramHistory.Count > MaxHistoryPoints) _ramHistory.RemoveAt(0);
                while (_gpuHistory.Count > MaxHistoryPoints) _gpuHistory.RemoveAt(0);
                while (_netDownHistory.Count > MaxHistoryPoints) _netDownHistory.RemoveAt(0);
                while (_netUpHistory.Count > MaxHistoryPoints) _netUpHistory.RemoveAt(0);

                if (isSensorTick)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var pCount = await _monitoringService.GetProcessCountAsync();
                            var sCount = await _monitoringService.GetServicesCount();

                            float gpuTemp = 0, gpuPower = 0, gpuVram = 0;
                            float cpuTemp = 0, cpuPower = 0, cpuClock = 0;

                            if (Interlocked.CompareExchange(ref _isUpdatingGpuSensors, 1, 0) == 0)
                            {
                                try
                                {
                                    HardwareTemperatureService.Instance.UpdateSensors();
                                    gpuTemp = HardwareTemperatureService.Instance.GetGpuTemperature();
                                    gpuPower = HardwareTemperatureService.Instance.GetGpuPower();
                                    gpuVram = HardwareTemperatureService.Instance.GetGpuVramUsedGb();

                                    cpuTemp = HardwareTemperatureService.Instance.GetCpuTemperature();
                                    cpuPower = HardwareTemperatureService.Instance.GetCpuPower();
                                    cpuClock = HardwareTemperatureService.Instance.GetCpuClock();
                                }
                                catch { }
                                finally
                                {
                                    Volatile.Write(ref _isUpdatingGpuSensors, 0);
                                }
                            }

                            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                            {
                                _lastPCount = pCount;
                                _lastSCount = sCount;
                                RefreshStats(pCount, sCount);

                                if (gpuTemp > 0) GpuTemperatureStr = $"{(int)gpuTemp}°C";
                                if (gpuPower > 0) GpuPowerDrawStr = $"{(int)gpuPower} W";
                                if (gpuVram > 0) GpuVramUsedStr = $"{gpuVram:F1} GB";

                                if (cpuTemp > 0) CpuTempStr = $"{(int)cpuTemp}°C";
                                if (cpuPower > 0) CpuPowerDrawStr = $"{(int)cpuPower} W";
                                if (cpuClock > 0) CpuClockStr = $"{(int)cpuClock} MHz";
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

                if (isFullSecond)
                {
                    int takeCount = _maxCpuDataPoints;

                    var cpuSlice = _cpuHistory.Skip(Math.Max(0, _cpuHistory.Count - takeCount)).ToList();
                    var ramSlice = _ramHistory.Skip(Math.Max(0, _ramHistory.Count - takeCount)).ToList();
                    var gpuSlice = _gpuHistory.Skip(Math.Max(0, _gpuHistory.Count - takeCount)).ToList();
                    var netDownSlice = _netDownHistory.Skip(Math.Max(0, _netDownHistory.Count - takeCount)).ToList();
                    var netUpSlice = _netUpHistory.Skip(Math.Max(0, _netUpHistory.Count - takeCount)).ToList();

                    var cpuProc = DownsampleLTTB(cpuSlice, MaxVisualPoints);
                    var ramProc = DownsampleLTTB(ramSlice, MaxVisualPoints);
                    var gpuProc = DownsampleLTTB(gpuSlice, MaxVisualPoints);
                    var netDownProc = DownsampleLTTB(netDownSlice, MaxVisualPoints);
                    var netUpProc = DownsampleLTTB(netUpSlice, MaxVisualPoints);

                    double maxDown = netDownProc.Count > 0 ? netDownProc.Max(p => p.Value) : 0;
                    double maxUp = netUpProc.Count > 0 ? netUpProc.Max(p => p.Value) : 0;
                    double absoluteMax = Math.Max(maxDown, maxUp);

                    if (absoluteMax > _peakNetworkSpeedMbps) _peakNetworkSpeedMbps = absoluteMax * 1.1;
                    else if (_peakNetworkSpeedMbps > 10.0 && absoluteMax < (_peakNetworkSpeedMbps * 0.1))
                        _peakNetworkSpeedMbps = Math.Max(10.0, _peakNetworkSpeedMbps * 0.995);

                    double netScale = Math.Max(10.0, Math.Ceiling(_peakNetworkSpeedMbps));

                    App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (HiddenXAxes.Count > 0)
                        {
                            HiddenXAxes[0].MaxLimit = _currentTick;
                            HiddenXAxes[0].MinLimit = _currentTick - _maxCpuDataPoints;
                        }

                        UpdateDateTime();
                        AverageRamLoad = _ramHistory.Count > 0 ? _ramHistory.Average(x => x.Value) : 0;

                        if (DynamicNetYAxes.FirstOrDefault() is Axis dynamicAxis)
                            dynamicAxis.MaxLimit = netScale;

                        NetYAxis100 = Math.Round(netScale).ToString();
                        NetYAxis75 = Math.Round(netScale * 0.75).ToString();
                        NetYAxis50 = Math.Round(netScale * 0.50).ToString();
                        NetYAxis25 = Math.Round(netScale * 0.25).ToString();

                        UpdateSeriesData(cpuProc, CpuGraphValues, CpuGraphDot, 100.0);
                        UpdateSeriesData(ramProc, RamGraphValues, RamGraphDot, 100.0);
                        UpdateSeriesData(gpuProc, GpuGraphValues, GpuGraphDot, 100.0);
                        UpdateSeriesData(netDownProc, NetDownGraphValues, NetDownGraphDot, netScale);
                        UpdateSeriesData(netUpProc, NetUpGraphValues, NetUpGraphDot, netScale);
                    });
                }

                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    OnTelemetryTicked?.Invoke(payload);

                    GpuUsageDisplay = (int)Math.Round(_displayGpuUsage);
                    CpuUsageDisplay = (int)Math.Round(_displayCpuUsage);
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
                if (_cachedNetworkInterfaces == null || ((DateTime.Now - _lastNetworkInterfaceRefresh).TotalSeconds >= 60 && !_isRefreshingNetworkInterfaces))
                {
                    _isRefreshingNetworkInterfaces = true;
                    Task.Run(() =>
                    {
                        try
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
                        catch { }
                        finally { _isRefreshingNetworkInterfaces = false; }
                    });
                }

                long d = 0, u = 0;
                if (_cachedNetworkInterfaces != null)
                {
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
                }
                return (d, u);
            }
            catch { return (0, 0); }
        }

        private float GetGpuUsage()
        {
            try
            {
                if (_gpuCategory == null || ((DateTime.Now - _lastGpuInstanceRefresh).TotalSeconds >= 60 && !_isRefreshingGpuInstances))
                {
                    _isRefreshingGpuInstances = true;
                    Task.Run(() =>
                    {
                        try
                        {
                            if (_gpuCategory == null) _gpuCategory = new PerformanceCounterCategory("GPU Engine");

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
                        catch { }
                        finally { _isRefreshingGpuInstances = false; }
                    });
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

        #region LiveCharts2 Continuous Scroll Logic

        private void UpdateSeriesData(List<(long Time, double Value)> rawData, ObservableCollection<ObservablePoint> seriesValues, ObservableCollection<ObservablePoint> dotValue, double maxScale)
        {
            if (rawData.Count < 2) return;

            while (seriesValues.Count > rawData.Count) seriesValues.RemoveAt(seriesValues.Count - 1);
            while (seriesValues.Count < rawData.Count) seriesValues.Add(new ObservablePoint(0, 0));

            for (int i = 0; i < rawData.Count; i++)
            {
                var pt = seriesValues[i];
                double targetX = rawData[i].Time;
                double targetY = (rawData[i].Value / maxScale) * 100.0;

                if (pt.X != targetX) pt.X = targetX;
                if (pt.Y != targetY) pt.Y = targetY;
            }

            var lastPoint = seriesValues.Last();
            if (dotValue.Count == 0) dotValue.Add(new ObservablePoint(lastPoint.X, lastPoint.Y));
            else
            {
                if (dotValue[0].X != lastPoint.X) dotValue[0].X = lastPoint.X;
                if (dotValue[0].Y != lastPoint.Y) dotValue[0].Y = lastPoint.Y;
            }
        }

        private List<(long Time, double Value)> DownsampleLTTB(List<(long Time, double Value)> data, int threshold)
        {
            int dataCount = data.Count;
            if (dataCount <= threshold || threshold <= 2) return data;

            var sampled = new List<(long Time, double Value)>(threshold);
            double every = (double)(dataCount - 2) / (threshold - 2);

            int a = 0;
            sampled.Add(data[a]);

            for (int i = 0; i < threshold - 2; i++)
            {
                int avgRangeStart = (int)(Math.Floor((i + 1) * every) + 1);
                int avgRangeEnd = (int)(Math.Floor((i + 2) * every) + 1);
                avgRangeEnd = Math.Min(avgRangeEnd, dataCount);

                double avgY = 0, avgX = 0;
                for (int j = avgRangeStart; j < avgRangeEnd; j++)
                {
                    avgY += data[j].Value;
                    avgX += data[j].Time;
                }
                int rangeLen = Math.Max(1, avgRangeEnd - avgRangeStart);
                avgY /= rangeLen;
                avgX /= rangeLen;

                int rangeOffs = (int)(Math.Floor(i * every) + 1);
                int rangeTo = (int)(Math.Floor((i + 1) * every) + 1);

                double maxArea = -1;
                int nextA = rangeOffs;

                double pointAX = data[a].Time;
                double pointAY = data[a].Value;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    double curX = data[j].Time;
                    double curY = data[j].Value;
                    double area = Math.Abs((pointAX - avgX) * (curY - pointAY) - (pointAX - curX) * (avgY - pointAY)) * 0.5;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        nextA = j;
                    }
                }

                sampled.Add(data[nextA]);
                a = nextA;
            }

            sampled.Add(data[dataCount - 1]);
            return sampled;
        }

        private void RebuildGraphsFromHistory()
        {
            if (HiddenXAxes.Count > 0)
            {
                HiddenXAxes[0].MaxLimit = _currentTick;
                HiddenXAxes[0].MinLimit = _currentTick - _maxCpuDataPoints;
            }

            int takeCount = _maxCpuDataPoints;

            var cpuSlice = _cpuHistory.Skip(Math.Max(0, _cpuHistory.Count - takeCount)).ToList();
            var ramSlice = _ramHistory.Skip(Math.Max(0, _ramHistory.Count - takeCount)).ToList();
            var gpuSlice = _gpuHistory.Skip(Math.Max(0, _gpuHistory.Count - takeCount)).ToList();

            double netScale = Math.Max(10.0, Math.Ceiling(_peakNetworkSpeedMbps));
            var netDownSlice = _netDownHistory.Skip(Math.Max(0, _netDownHistory.Count - takeCount)).ToList();
            var netUpSlice = _netUpHistory.Skip(Math.Max(0, _netUpHistory.Count - takeCount)).ToList();

            var cpuProc = DownsampleLTTB(cpuSlice, MaxVisualPoints);
            var ramProc = DownsampleLTTB(ramSlice, MaxVisualPoints);
            var gpuProc = DownsampleLTTB(gpuSlice, MaxVisualPoints);
            var netDownProc = DownsampleLTTB(netDownSlice, MaxVisualPoints);
            var netUpProc = DownsampleLTTB(netUpSlice, MaxVisualPoints);

            UpdateSeriesData(cpuProc, CpuGraphValues, CpuGraphDot, 100.0);
            UpdateSeriesData(ramProc, RamGraphValues, RamGraphDot, 100.0);
            UpdateSeriesData(gpuProc, GpuGraphValues, GpuGraphDot, 100.0);
            UpdateSeriesData(netDownProc, NetDownGraphValues, NetDownGraphDot, netScale);
            UpdateSeriesData(netUpProc, NetUpGraphValues, NetUpGraphDot, netScale);
        }

        private void UpdateAxisLabels(int totalSeconds)
        {
            double step = totalSeconds / 4.0;
            XAxisLabelStart = FormatTime(totalSeconds);
            XAxisLabelQ1 = FormatTime(totalSeconds - step);
            XAxisLabelMid = FormatTime(totalSeconds - (step * 2));
            XAxisLabelQ3 = FormatTime(totalSeconds - (step * 3));
        }

        private string FormatTime(double seconds)
        {
            if (seconds < 60) return $"-{Math.Round(seconds)} SEC";
            return $"-{TimeSpan.FromSeconds(seconds).ToString(@"m\:ss")} MIN";
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

            LocalIP = new IPWrapper { Data = _monitoringService.GetDefaultLocalIP() };
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
            Task.Run(() =>
            {
                try
                {
                    var driveData = DiskInfoService.GetDrivesData();
                    App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        DiskDrives = new ObservableCollection<DriveSpaceInfo>(driveData);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Disk Data Error] Failed to load disk drives: {ex.Message}");
                }
            });
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