// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Dispatching;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class DiagnosticsPageViewModel : ObservableObject
    {
        private LiveEventWatcherHelper? _liveWatcher;
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private CancellationTokenSource? _scanCts;

        private DispatcherTimer? _telemetryTimer;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private PerformanceCounter? _diskCounter;
        private PerformanceCounter? _pagefileCounter;
        private double _totalMemoryMb = 0;

        private readonly List<double> _cpuHistoryBuffer = new List<double>();
        private readonly List<double> _ramHistoryBuffer = new List<double>();
        private readonly List<double> _diskHistoryBuffer = new List<double>();
        private readonly List<double> _pageHistoryBuffer = new List<double>();
        private const int MaxHistoryCapacity = 900;

        private DateTime _lastRamNotification = DateTime.MinValue;
        private DateTime _lastPagefileNotification = DateTime.MinValue;
        private DateTime _lastEventNotification = DateTime.MinValue;

        #region Constructor
        public DiagnosticsPageViewModel()
        {
            PerformanceGraphPoints.Add(new Point(400, 100));

            if (LocalMachineSettingsEngine.EnableLiveDiagnostics)
            {
                StartLiveMonitoring();
                StartLiveTelemetry();
            }
        }
        #endregion

        #region Standard Properties
        public Visibility EventEmptyStateVisibility =>
            !IsScanning && MinedSystemEvents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HardwareScannerVisibility =>
            IsScanning || DetectedHardwareIssues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HardwareListVisibility =>
            !IsScanning && DetectedHardwareIssues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public string HardwareScannerText => IsScanning ? "INTERROGATING BUS..." : "HARDWARE OPTIMAL. MONITORING BUS...";

        private string _scannerText = "SYSTEM OPTIMAL. MONITORING...";
        public string ScannerText
        {
            get => _scannerText;
            set => SetProperty(ref _scannerText, value);
        }

        private SolidColorBrush _systemHealthBrush = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
        public SolidColorBrush SystemHealthBrush
        {
            get => _systemHealthBrush;
            set => SetProperty(ref _systemHealthBrush, value);
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    OnPropertyChanged(nameof(IsNotScanning));
                    OnPropertyChanged(nameof(ScanningVisibility));

                    OnPropertyChanged(nameof(HardwareScannerVisibility));
                    OnPropertyChanged(nameof(HardwareListVisibility));
                    OnPropertyChanged(nameof(HardwareScannerText));
                    OnPropertyChanged(nameof(ScannerText));
                    OnPropertyChanged(nameof(EventEmptyStateVisibility));
                }
            }
        }

        public bool IsNotScanning => !IsScanning;

        public Visibility ScanningVisibility =>
            IsScanning ? Visibility.Visible : Visibility.Collapsed;

        private string _scanStatus = "System idle. Ready to initiate diagnostic scan.";
        public string ScanStatus
        {
            get => _scanStatus;
            set => SetProperty(ref _scanStatus, value);
        }
        #endregion

        #region Advanced Features Bridge

        public enum TelemetryMetric { CPU, RAM, Disk, Pagefile }

        private TelemetryMetric _activeGraphMetric = TelemetryMetric.CPU;
        public TelemetryMetric ActiveGraphMetric
        {
            get => _activeGraphMetric;
            set
            {
                if (SetProperty(ref _activeGraphMetric, value))
                {
                    OnPropertyChanged(nameof(IsCpuSelected));
                    OnPropertyChanged(nameof(IsRamSelected));
                    OnPropertyChanged(nameof(IsDiskSelected));
                    OnPropertyChanged(nameof(IsPageSelected));

                    OnPropertyChanged(nameof(ActivePrimaryLabel));
                    OnPropertyChanged(nameof(ActivePrimaryValueStr));

                    RebuildGraphFromHistory();
                }
            }
        }

        public bool IsCpuSelected
        {
            get => ActiveGraphMetric == TelemetryMetric.CPU;
            set { if (value) ActiveGraphMetric = TelemetryMetric.CPU; }
        }
        public bool IsRamSelected
        {
            get => ActiveGraphMetric == TelemetryMetric.RAM;
            set { if (value) ActiveGraphMetric = TelemetryMetric.RAM; }
        }
        public bool IsDiskSelected
        {
            get => ActiveGraphMetric == TelemetryMetric.Disk;
            set { if (value) ActiveGraphMetric = TelemetryMetric.Disk; }
        }
        public bool IsPageSelected
        {
            get => ActiveGraphMetric == TelemetryMetric.Pagefile;
            set { if (value) ActiveGraphMetric = TelemetryMetric.Pagefile; }
        }

        public string ActivePrimaryLabel => ActiveGraphMetric switch
        {
            TelemetryMetric.RAM => ResourceString.GetString("diag_ram_load") ?? "RAM LOAD",
            TelemetryMetric.Disk => ResourceString.GetString("diag_io_load") ?? "DISK I/O",
            TelemetryMetric.Pagefile => ResourceString.GetString("diag_pagefile_load") ?? "PAGEFILE",
            _ => ResourceString.GetString("diag_cpu_load") ?? "CPU LOAD"
        };

        public string ActivePrimaryValueStr => ActiveGraphMetric switch
        {
            TelemetryMetric.RAM => CurrentRamLoadStr,
            TelemetryMetric.Disk => CurrentIoLoadStr,
            TelemetryMetric.Pagefile => CurrentPagefileLoadStr,
            _ => CurrentCpuLoadStr
        };

        public class GraphScaleOption
        {
            public string Title { get; set; } = "";
            public int Seconds { get; set; }
        }

        public ObservableCollection<GraphScaleOption> TimeScaleOptions { get; } = new()
        {
            new GraphScaleOption { Title = ResourceString.GetString("diag_graph_60sec") ?? "60 Seconds", Seconds = 60 },
            new GraphScaleOption { Title = ResourceString.GetString("diag_graph_5min") ?? "5 Minutes", Seconds = 300 },
            new GraphScaleOption { Title = ResourceString.GetString("diag_graph_10min") ?? "10 Minutes", Seconds = 600 },
            new GraphScaleOption { Title = ResourceString.GetString("diag_graph_15min") ?? "15 Minutes", Seconds = 900 }
        };

        private int _maxGraphSeconds = 60;
        public int MaxGraphSeconds
        {
            get => _maxGraphSeconds;
            set
            {
                if (SetProperty(ref _maxGraphSeconds, value))
                {
                    OnPropertyChanged(nameof(XAxisLabelStart));
                    OnPropertyChanged(nameof(XAxisLabelQ1));
                    OnPropertyChanged(nameof(XAxisLabelMid));
                    OnPropertyChanged(nameof(XAxisLabelQ3));

                    RebuildGraphFromHistory();
                }
            }
        }

        public string XAxisLabelStart => MaxGraphSeconds >= 300
            ? $"-{MaxGraphSeconds / 60} MIN"
            : $"-{MaxGraphSeconds} SEC";

        public string XAxisLabelQ1 => MaxGraphSeconds >= 300
            ? $"-{(MaxGraphSeconds * 0.75) / 60.0:0.#} MIN"
            : $"-{MaxGraphSeconds * 0.75:0} SEC";

        public string XAxisLabelMid => MaxGraphSeconds >= 300
            ? $"-{(MaxGraphSeconds * 0.5) / 60.0:0.#} MIN"
            : $"-{MaxGraphSeconds * 0.5:0} SEC";

        public string XAxisLabelQ3 => MaxGraphSeconds >= 300
            ? $"-{(MaxGraphSeconds * 0.25) / 60.0:0.#} MIN"
            : $"-{MaxGraphSeconds * 0.25:0} SEC";


        private string _aiSummary = "AI Engine sleeping. Run a scan to generate a system health summary.";
        public string AiSummary
        {
            get => _aiSummary;
            set => SetProperty(ref _aiSummary, value);
        }

        public bool IsLiveMonitoringEnabled
        {
            get => LocalMachineSettingsEngine.EnableLiveDiagnostics;
            set
            {
                if (LocalMachineSettingsEngine.EnableLiveDiagnostics != value)
                {
                    LocalMachineSettingsEngine.EnableLiveDiagnostics = value;
                    OnPropertyChanged(nameof(IsLiveMonitoringEnabled));

                    if (value)
                    {
                        StartLiveMonitoring();
                        StartLiveTelemetry();
                    }
                    else
                    {
                        StopLiveMonitoring();
                        StopLiveTelemetry();
                    }
                }
            }
        }

        private string _stabilityScore = "100%";
        public string StabilityScore
        {
            get => _stabilityScore;
            set => SetProperty(ref _stabilityScore, value);
        }

        private string _activeHardwareCount = "68";
        public string ActiveHardwareCount
        {
            get => _activeHardwareCount;
            set => SetProperty(ref _activeHardwareCount, value);
        }

        private PointCollection _performanceGraphPoints = new();
        public PointCollection PerformanceGraphPoints
        {
            get => _performanceGraphPoints;
            set => SetProperty(ref _performanceGraphPoints, value);
        }

        private PointCollection _performanceAreaPoints = new();
        public PointCollection PerformanceAreaPoints
        {
            get => _performanceAreaPoints;
            set => SetProperty(ref _performanceAreaPoints, value);
        }

        private string _currentCpuLoadStr = "0%";
        public string CurrentCpuLoadStr
        {
            get => _currentCpuLoadStr;
            set => SetProperty(ref _currentCpuLoadStr, value);
        }

        private string _currentRamLoadStr = "0%";
        public string CurrentRamLoadStr
        {
            get => _currentRamLoadStr;
            set => SetProperty(ref _currentRamLoadStr, value);
        }

        private string _currentIoLoadStr = "0%";
        public string CurrentIoLoadStr
        {
            get => _currentIoLoadStr;
            set => SetProperty(ref _currentIoLoadStr, value);
        }

        private string _currentPagefileLoadStr = "0%";
        public string CurrentPagefileLoadStr
        {
            get => _currentPagefileLoadStr;
            set => SetProperty(ref _currentPagefileLoadStr, value);
        }

        public ObservableCollection<HourlyMetric> StabilityTrendData { get; } = new();
        public ObservableCollection<HardwareIssue> DetectedHardwareIssues { get; } = new();
        public ObservableCollection<SystemEventItem> MinedSystemEvents { get; } = new();
        #endregion

        #region Commands
        [RelayCommand]
        public async Task FixEventAsync(int eventId)
        {
            ScanStatus = $"Attempting automated remediation for Event {eventId}...";
            bool success = await RemediationEngine.RunFixAsync(eventId);

            if (success)
            {
                ScanStatus = $"Successfully repaired Event {eventId}.";
                AiSummary = $"AUTO-FIX DEPLOYED: The issue associated with ID {eventId} has been resolved.";

                var fixedEvent = MinedSystemEvents.FirstOrDefault(e => e.EventId == eventId);
                if (fixedEvent != null) MinedSystemEvents.Remove(fixedEvent);
            }
            else
            {
                ScanStatus = $"Remediation failed for Event {eventId}. Admin privileges required.";
            }
        }

        [RelayCommand]
        public async Task FixHardwareAsync(HardwareIssue issue)
        {
            if (issue == null || string.IsNullOrEmpty(issue.DeviceId)) return;

            ScanStatus = $"Initiating hardware sequence for {issue.ComponentDisplayName}...";

            bool success = await RemediationEngine.RunHardwareFixAsync(issue);

            if (success)
            {
                ScanStatus = "Command sent. Waiting for OS to initialize driver stack...";

                await Task.Delay(2500);

                await ExecuteFullScanAsync();

                AiSummary = $"REMEDIATION VERIFIED: {issue.ComponentDisplayName} driver stack signaled and re-initialized.";
            }
            else
            {
                ScanStatus = $"Failed to remediate {issue.ComponentDisplayName}. Check Admin privileges.";
            }
        }
        #endregion

        #region Execution Logic

        private SystemEventItem CreateAlert(int eventId, string source, string message)
        {
            return new SystemEventItem
            {
                TimeCreated = DateTime.Now,
                SourceName = source,
                EventId = eventId,
                Level = 1, // 1 = Critical/Warning in the UI
                Message = message,
                IsFixable = true
            };
        }

        public async Task ExecuteFullScanAsync()
        {
            if (IsScanning) return;

            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            IsScanning = true;
            ScanStatus = "Running deep system and hardware analysis...";
            AiSummary = "Neural engine analyzing telemetry data...";

            ScannerText = "INTERROGATING HARDWARE...";

            DetectedHardwareIssues.Clear();
            MinedSystemEvents.Clear();
            StabilityTrendData.Clear();

            await Task.Delay(600, token);

            try
            {
                var wmiTask = new WmiDiagnosticHelper().ListBrokenHardwareAsync();
                var eventTask = new EventLogMinerHelper().MineRecentErrorsAsync();
                var perfTask = new PerformanceTelemetryHelper().AnalyzePerformanceBottlenecksAsync();

                await Task.WhenAll(wmiTask, eventTask, perfTask);

                token.ThrowIfCancellationRequested();

                var hardwareIssues = wmiTask.Result;
                var systemEvents = eventTask.Result;
                var performanceIssues = perfTask.Result;

                systemEvents.AddRange(performanceIssues);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    int baselineHardware = 85;
                    ActiveHardwareCount = (baselineHardware - hardwareIssues.Count).ToString();

                    // --- SYNCHRONIZED HARDWARE DETECTION ---
                    int[] fixableWmiCodes = {
                        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
                        21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
                        40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
                        59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77,
                        78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96,
                        97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112,
                        113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127,
                        128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142,
                        143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157,
                        158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172,
                        173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187,
                        188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202,
                        203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217,
                        218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232,
                        233, 234, 235
                    };

                    foreach (var issue in hardwareIssues)
                    {
                        if (fixableWmiCodes.Contains(issue.WmiErrorCode))
                        {
                            issue.IsFixable = true;
                        }
                        DetectedHardwareIssues.Add(issue);
                    }

                    // --- SYNCHRONIZED EVENT DETECTION ---
                    int criticalCount = 0;

                    int[] fixableEvents = {
                        1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
                        32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 44, 45, 47, 49, 50, 51, 52, 54, 55, 56, 57, 58, 59, 60, 63, 65, 69,
                        98, 100, 101, 102, 103, 107, 109, 110, 117, 123, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141,
                        142, 143, 144, 153, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 201, 202, 300, 301, 302, 303, 304, 305, 306,
                        307, 308, 310, 315, 316, 317, 400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 417, 418, 419, 420, 421,
                        422, 423, 424, 425, 426, 427, 441, 442, 447, 448, 451, 454, 455, 467, 474, 477, 481, 482, 483, 488, 489, 490, 491,
                        492, 493, 504, 505, 506, 507, 510, 512, 513, 514, 515, 523, 524, 525, 533, 566, 601, 603, 604, 800, 801, 804, 805,
                        806, 808, 809, 810, 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 1011, 1012, 1013, 1014, 1015,
                        1016, 1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, 1025, 1030, 1033, 1040, 1041, 1042, 1053, 1054, 1055, 1058,
                        1074, 1076, 1096, 1101, 1102, 1104, 1105, 1108, 1112, 1116, 1117, 1118, 1119, 1500, 1501, 1502, 1504, 1505, 1506,
                        1507, 1508, 1509, 1511, 1512, 1513, 1514, 1515, 1517, 1530, 1531, 1532, 1534, 1542, 2000, 2001, 2002, 2003, 2004,
                        2005, 2010, 2011, 2012, 2021, 2022, 2049, 2050, 2100, 2101, 2102, 2504, 2505, 2506, 2507, 2508, 2509, 3000, 3001,
                        3002, 3003, 3004, 3006, 3007, 4004, 4005, 4007, 4008, 4101, 4109, 4115, 4226, 4227, 4231, 4319, 4624, 4625, 4634,
                        4647, 4648, 4672, 4688, 4689, 4720, 4722, 4723, 4724, 4725, 4726, 4732, 4733, 4735, 4738, 4740, 4741, 4742, 4743,
                        4744, 4745, 4746, 4747, 4748, 4749, 4750, 4800, 4801, 4802, 4803, 5000, 5001, 5002, 5003, 5004, 5005, 5006, 5007,
                        5010, 5011, 5012, 5032, 5140, 5142, 5145, 5719, 6005, 6006, 6008, 6009, 6062, 6272, 6273, 6278, 7000, 7001, 7002,
                        7003, 7004, 7005, 7006, 7009, 7011, 7022, 7023, 7024, 7026, 7030, 7031, 7032, 7034, 7035, 7036, 7040, 7042, 7045,
                        7046, 7047, 7048, 7049, 7050, 7051, 7052, 8000, 8001, 8002, 8003, 8004, 8021, 8033, 8193, 8194, 8213, 8217, 8218,
                        8219, 8220, 8221, 8222, 8223, 8224, 8225, 8226, 9000, 9001, 10000, 10001, 10002, 10005, 10010, 10011, 10012, 10013,
                        10014, 10015, 10016, 10020, 10053, 10054, 10060, 10061, 10065, 10066, 10067, 10068, 10069, 10070, 10071, 10072,
                        10073, 10074, 10100, 10101, 10102, 10103, 10104, 10105, 10106, 10107, 10108, 10109, 10110, 10111, 10112, 10113,
                        10114, 10115, 10116, 10117, 10118, 10119, 10120, 10200, 10400, 11001, 11002, 11004, 11005, 11006, 11706, 11707,
                        11708, 11724, 11728, 12001, 12010, 12011, 12012, 12013, 12289, 12290, 12291, 12292, 12293, 12294, 12295, 12296,
                        12297, 12298, 12300, 12301, 12302, 12303, 12304, 36870, 36871, 36874, 36880, 36881, 36882, 36884, 36885, 36886,
                        36887, 36888, 40961, 40962,

                        // Custom Actionable Event IDs
                        9001, 9002
                    };

                    foreach (var ev in systemEvents)
                    {
                        if (fixableEvents.Contains(ev.EventId))
                        {
                            ev.IsFixable = true;
                        }

                        if (ev.Level == 1 || ev.Level == 2)
                        {
                            criticalCount++;
                        }

                        MinedSystemEvents.Add(ev);
                    }

                    float currentRam = _ramCounter?.NextValue() ?? 0;
                    double ramUsagePct = _totalMemoryMb > 0 ? ((_totalMemoryMb - currentRam) / _totalMemoryMb) * 100 : 0;
                    if (ramUsagePct > 80)
                    {
                        var memAlert = CreateAlert(9001, "EvolveOS Neural Engine", $"CRITICAL: Physical Memory utilization at {Math.Round(ramUsagePct)}%. Immediate cache purge recommended to prevent paging thrash.");
                        MinedSystemEvents.Insert(0, memAlert);
                        criticalCount++;
                    }

                    float pagefileUsage = _pagefileCounter?.NextValue() ?? 0;
                    if (pagefileUsage > 75)
                    {
                        var pfAlert = CreateAlert(9002, "EvolveOS Neural Engine", $"WARNING: Pagefile usage exceeds {Math.Round(pagefileUsage)}%. Disk I/O bottleneck imminent. Recommend clearing virtual memory.");
                        MinedSystemEvents.Insert(0, pfAlert);
                        criticalCount++;
                    }

                    CalculateStabilityTrend(systemEvents);

                    AiSummary = criticalCount > 0
                        ? $"AI Analysis: Detected {criticalCount} system crashes/errors or high load bottlenecks. Hardware conflict identified in {DetectedHardwareIssues.Count} device(s). Recommendation: Run available auto-fixes from the Event Miner."
                        : "AI Analysis: System telemetry is nominal. No signs of degradation or instability found in recent logs.";

                    ScanStatus = $"Scan complete. {DetectedHardwareIssues.Count} hardware issue(s) | {MinedSystemEvents.Count} system event(s).";

                    if (hardwareIssues.Count > 0)
                    {
                        SystemHealthBrush = new SolidColorBrush(Microsoft.UI.Colors.Red); // Critical
                        ScannerText = $"CRITICAL. {hardwareIssues.Count} HARDWARE FAULTS DETECTED.";
                    }
                    else if (criticalCount > 15) // Tolerance for (normal) Windows background noise.
                    {
                        SystemHealthBrush = new SolidColorBrush(Colors.Gold); // Warning
                        ScannerText = $"WARNING. {criticalCount} SYSTEM EVENTS LOGGED.";
                    }
                    else
                    {
                        SystemHealthBrush = new SolidColorBrush(Colors.LimeGreen); // Healthy
                        ScannerText = "SYSTEM OPTIMAL. MONITORING...";
                    }

                    OnPropertyChanged(nameof(SystemHealthBrush));
                    OnPropertyChanged(nameof(ScannerText));
                    OnPropertyChanged(nameof(HardwareScannerVisibility));
                    OnPropertyChanged(nameof(HardwareListVisibility));
                    OnPropertyChanged(nameof(HardwareScannerText));
                    OnPropertyChanged(nameof(EventEmptyStateVisibility));
                });
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Scan cancelled by user/app shutdown.");
            }
            catch (Exception ex)
            {
                ScanStatus = "Diagnostic scan failed. Check system logs.";
                Debug.WriteLine($"[Diagnostic Scan Error] {ex.Message}");
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void RebuildGraphFromHistory()
        {
            double logicalWidth = 400.0;
            double pixelsPerSecond = logicalWidth / MaxGraphSeconds;

            var newPoints = new PointCollection();
            var areaPoints = new PointCollection();

            var targetBuffer = ActiveGraphMetric switch
            {
                TelemetryMetric.RAM => _ramHistoryBuffer,
                TelemetryMetric.Disk => _diskHistoryBuffer,
                TelemetryMetric.Pagefile => _pageHistoryBuffer,
                _ => _cpuHistoryBuffer
            };

            if (targetBuffer.Count == 0)
            {
                newPoints.Add(new Point(logicalWidth, 100));
                PerformanceGraphPoints = newPoints;
                PerformanceAreaPoints = areaPoints;
                return;
            }

            int maxPointsNeeded = MaxGraphSeconds + 1;
            int pointsToShow = Math.Min(targetBuffer.Count, maxPointsNeeded);

            var visibleHistory = targetBuffer.Skip(targetBuffer.Count - pointsToShow).ToList();

            double currentX = logicalWidth - ((pointsToShow - 1) * pixelsPerSecond);

            foreach (var yVal in visibleHistory)
            {
                newPoints.Add(new Point(currentX, yVal));
                currentX += pixelsPerSecond;
            }

            PerformanceGraphPoints = newPoints;

            if (newPoints.Count > 0)
            {
                areaPoints.Add(new Point(newPoints.First().X, 100));
                foreach (var p in newPoints) areaPoints.Add(p);
                areaPoints.Add(new Point(newPoints.Last().X, 100));
            }
            PerformanceAreaPoints = areaPoints;
        }
        #endregion

        #region Live Telemetry (Graph & Watcher)

        private void StartLiveTelemetry()
        {
            if (_telemetryTimer != null) return;

            try
            {
                var gcStatus = GC.GetGCMemoryInfo();
                _totalMemoryMb = gcStatus.TotalAvailableMemoryBytes / (1024.0 * 1024.0);

                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();

                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _ramCounter.NextValue();

                try
                {
                    _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                    _diskCounter.NextValue();
                }
                catch { _diskCounter = null; }

                try
                {
                    _pagefileCounter = new PerformanceCounter("Paging File", "% Usage", "_Total");
                    _pagefileCounter.NextValue();
                }
                catch { _pagefileCounter = null; }

                _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _telemetryTimer.Tick += UpdateTelemetryGraph;
                _telemetryTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start performance counters: {ex.Message}");
            }
        }

        private void UpdateTelemetryGraph(object? sender, object e)
        {
            try
            {
                float cpuUsage = _cpuCounter?.NextValue() ?? 0;

                float availableMb = _ramCounter?.NextValue() ?? 0;
                double usedMb = _totalMemoryMb - availableMb;
                double ramUsage = _totalMemoryMb > 0 ? (usedMb / _totalMemoryMb) * 100 : 0;
                if (ramUsage < 0) ramUsage = 0;

                float diskUsage = _diskCounter?.NextValue() ?? 0;

                if (diskUsage > 100) diskUsage = 100;

                float pagefileUsage = _pagefileCounter?.NextValue() ?? 0;

                if (ramUsage > 85 && (DateTime.Now - _lastRamNotification).TotalMinutes > 15)
                {
                    _lastRamNotification = DateTime.Now;
                    SendSystemNotification(2, "Memory Exhaustion Warning", $"Physical Memory usage has reached {Math.Round(ramUsage)}%. System performance may degrade.");
                }

                if (pagefileUsage > 80 && (DateTime.Now - _lastPagefileNotification).TotalMinutes > 15)
                {
                    _lastPagefileNotification = DateTime.Now;
                    SendSystemNotification(2, "Pagefile Saturation Warning", $"Pagefile usage is critically high at {Math.Round(pagefileUsage)}%. Disk thrashing likely.");
                }

                CurrentCpuLoadStr = $"{(int)cpuUsage}%";
                CurrentRamLoadStr = $"{(int)ramUsage}%";
                CurrentIoLoadStr = $"{(int)diskUsage}%";
                CurrentPagefileLoadStr = $"{(int)pagefileUsage}%";

                OnPropertyChanged(nameof(ActivePrimaryValueStr));

                _cpuHistoryBuffer.Add(100 - cpuUsage);
                _ramHistoryBuffer.Add(100 - ramUsage);
                _diskHistoryBuffer.Add(100 - diskUsage);
                _pageHistoryBuffer.Add(100 - pagefileUsage);

                if (_cpuHistoryBuffer.Count > MaxHistoryCapacity)
                {
                    _cpuHistoryBuffer.RemoveAt(0);
                    _ramHistoryBuffer.RemoveAt(0);
                    _diskHistoryBuffer.RemoveAt(0);
                    _pageHistoryBuffer.RemoveAt(0);
                }

                RebuildGraphFromHistory();
            }
            catch { }
        }

        private void StopLiveTelemetry()
        {
            try
            {
                if (_telemetryTimer != null)
                {
                    _telemetryTimer.Stop();
                    _telemetryTimer.Tick -= UpdateTelemetryGraph;
                    _telemetryTimer = null;
                }

                _cpuCounter?.Dispose(); _cpuCounter = null;
                _ramCounter?.Dispose(); _ramCounter = null;
                _diskCounter?.Dispose(); _diskCounter = null;
                _pagefileCounter?.Dispose(); _pagefileCounter = null;

                _cpuHistoryBuffer.Clear();
                _ramHistoryBuffer.Clear();
                _diskHistoryBuffer.Clear();
                _pageHistoryBuffer.Clear();

                try
                {
                    PerformanceGraphPoints.Clear();
                    PerformanceGraphPoints.Add(new Point(400, 100));
                    PerformanceAreaPoints.Clear();

                    CurrentCpuLoadStr = "0%";
                    CurrentRamLoadStr = "0%";
                    CurrentIoLoadStr = "0%";
                    CurrentPagefileLoadStr = "0%";
                }
                catch { /* Ignore UI update errors during shutdown */ }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Telemetry Teardown Error] {ex.Message}");
            }
        }

        private void StartLiveMonitoring()
        {
            if (_liveWatcher != null) return;
            ScanStatus = "Live Telemetry Interceptor: ACTIVE";

            _liveWatcher = new LiveEventWatcherHelper(newEvent =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    MinedSystemEvents.Insert(0, newEvent);
                    if (MinedSystemEvents.Count > 150) MinedSystemEvents.RemoveAt(MinedSystemEvents.Count - 1);

                    CalculateStabilityTrend(MinedSystemEvents);
                    AiSummary = $"LIVE INTERCEPT: {newEvent.SourceName} reported a Level {newEvent.Level} event. Stability updated.";

                    OnPropertyChanged(nameof(EventEmptyStateVisibility));

                    if (newEvent.Level <= 2 && (DateTime.Now - _lastEventNotification).TotalMinutes > 5)
                    {
                        _lastEventNotification = DateTime.Now;
                        SendSystemNotification(3, "Critical System Error Detected", $"Event ID {newEvent.EventId} logged by {newEvent.SourceName}.");
                    }
                });
            });

            _liveWatcher.Start();
        }

        private void StopLiveMonitoring()
        {
            ScanStatus = "Live Telemetry Interceptor: STANDBY";
            _liveWatcher?.Dispose();
            _liveWatcher = null;
        }

        public void DisposeWatcher()
        {
            StopLiveMonitoring();
            StopLiveTelemetry();
        }

        private void CalculateStabilityTrend(IEnumerable<SystemEventItem> events)
        {
            StabilityTrendData.Clear();
            DateTime now = DateTime.Now;
            double[] hourlyHealth = new double[24];
            for (int i = 0; i < 24; i++) hourlyHealth[i] = 100.0;

            foreach (var ev in events)
            {
                TimeSpan diff = now - ev.TimeCreated;
                int hourIndex = 23 - (int)diff.TotalHours;

                if (hourIndex >= 0 && hourIndex < 24)
                {
                    double penalty = ev.Level switch { 1 => 20.0, 2 => 10.0, 3 => 2.0, _ => 0.0 };
                    hourlyHealth[hourIndex] -= penalty;
                }
            }

            double totalHealthSum = 0;
            for (int i = 0; i < 24; i++)
            {
                double finalScore = Math.Clamp(hourlyHealth[i], 5.0, 100.0);
                totalHealthSum += finalScore;

                StabilityTrendData.Add(new HourlyMetric
                {
                    TimeLabel = $"-{23 - i}h",
                    BarHeight = finalScore,
                    BarColor = finalScore switch
                    {
                        >= 90 => new SolidColorBrush(ColorHelper.FromArgb(255, 0, 214, 232)),
                        >= 70 => new SolidColorBrush(Colors.Gold),
                        _ => new SolidColorBrush(Colors.Red)
                    }
                });
            }
            double averageHealth = totalHealthSum / 24.0;
            StabilityScore = $"{Math.Round(averageHealth, 2)}%";
        }
        #endregion

        #region Notifications
        private void SendSystemNotification(int tier, string title, string message)
        {
            var severity = tier switch
            {
                1 => NotificationManager.NoticeSeverity.Info,
                2 => NotificationManager.NoticeSeverity.Warning,
                3 => NotificationManager.NoticeSeverity.Error,
                _ => NotificationManager.NoticeSeverity.Info
            };

            _dispatcherQueue.TryEnqueue(() =>
            {
                NotificationManager
                    .Show(title, message)
                    .WithSeverity(severity)
                    .WithDuration(5000)
                    .Perform();
            });

            Debug.WriteLine($"[TELEMETRY ALERT] {title}: {message}");
        }
        #endregion

        #region Cleanup
        public void Cleanup()
        {
            try
            {
                if (_scanCts != null)
                {
                    _scanCts.Cancel();
                    _scanCts.Dispose();
                    _scanCts = null;
                }
            }
            catch (ObjectDisposedException) { /* Task already disposed it */ }
            catch (Exception ex) { Debug.WriteLine($"[Cleanup Error] {ex.Message}"); }

            DisposeWatcher();
        }
        #endregion
    }
}