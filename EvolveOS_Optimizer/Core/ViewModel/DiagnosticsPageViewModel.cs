// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Windows.Foundation;
using Windows.System;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class DiagnosticsPageViewModel : ObservableObject
    {
        #region Singleton Instance
        private static DiagnosticsPageViewModel? _instance;

        public static DiagnosticsPageViewModel Current => _instance ??= new DiagnosticsPageViewModel();
        #endregion

        #region Fields (Diagnostics)
        private LiveEventWatcherHelper? _liveWatcher;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        private CancellationTokenSource? _scanCts;

        private readonly DiagnosticScannerEngine _scannerEngine;

        private DispatcherTimer? _telemetryTimer;
        internal PerformanceCounter? _cpuCounter;
        internal PerformanceCounter? _ramCounter;
        internal PerformanceCounter? _diskCounter;
        internal PerformanceCounter? _pagefileCounter;
        internal double _totalMemoryMb = 0;

        private float _peakNetworkSpeedMbps = 10f;

        private readonly List<double> _cpuHistoryBuffer = new List<double>();
        private readonly List<double> _ramHistoryBuffer = new List<double>();
        private readonly List<double> _diskHistoryBuffer = new List<double>();
        private readonly List<double> _pageHistoryBuffer = new List<double>();
        private readonly List<double> _gpuHistoryBuffer = new List<double>();
        private readonly List<double> _networkUpHistoryBuffer = new List<double>();
        private readonly List<double> _networkDownHistoryBuffer = new List<double>();
        private const int MaxHistoryCapacity = 900;
        private readonly HashSet<string> _dismissedEventHashes = new();

        private Dictionary<string, PerformanceCounter> _gpuCounters = new Dictionary<string, PerformanceCounter>();
        private Dictionary<string, PerformanceCounter> _networkUpCounters = new Dictionary<string, PerformanceCounter>();
        private Dictionary<string, PerformanceCounter> _networkDownCounters = new Dictionary<string, PerformanceCounter>();

        private DateTime _lastRamNotification = DateTime.MinValue;
        private DateTime _lastPagefileNotification = DateTime.MinValue;
        private DateTime _lastEventNotification = DateTime.MinValue;
        #endregion

        #region Fields (Maintenance)
        private CancellationTokenSource? _cancellationTokenSource;
        private CancellationTokenSource? _cleanupCts;
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
        private string _optimizationProgressStep = ResourceString.GetString("txt_progress_step") ?? "Waiting...";
        private byte _optimizationProgressTotal = byte.MaxValue;
        private byte _optimizationProgressValue = byte.MinValue;
        private string? _selectedProcess;
        private bool _isBusy;
        private bool _isUiActive = true;
        private string _totalSpaceToFree = "0 MB";
        #endregion

        #region Security Properties (Security)
        private DispatcherTimer? _securityRefreshTimer;
        private bool _isSecurityCheckInProgress;
        private bool _isUacSliderUpdating = false;
        private bool _isSmartAppControlUpdating = false;
        private bool _isPowerShellPolicyUpdating = false;
        private bool _isRdpToggleUpdating = false;
        private bool _isRaToggleUpdating = false;
        private bool _isDevModeToggleUpdating = false;
        private bool _hasSecurityInitialized = false;
        private List<string> _currentSecurityIssues = new();

        private string _securityStatusText = "Scanning...";
        public string SecurityStatusText { get => _securityStatusText; set => SetProperty(ref _securityStatusText, value); }

        private string _securityStatusImageUri = "ms-appx:///Assets/PngImages/Secure.png";
        public string SecurityStatusImageUri { get => _securityStatusImageUri; set => SetProperty(ref _securityStatusImageUri, value); }

        private Visibility _securityStatusImageVisibility = Visibility.Visible;
        public Visibility SecurityStatusImageVisibility { get => _securityStatusImageVisibility; set => SetProperty(ref _securityStatusImageVisibility, value); }

        private Visibility _securityStatusLoadingRingVisibility = Visibility.Visible;
        public Visibility SecurityStatusLoadingRingVisibility { get => _securityStatusLoadingRingVisibility; set => SetProperty(ref _securityStatusLoadingRingVisibility, value); }

        private bool _isSecurityStatusLoadingRingActive = true;
        public bool IsSecurityStatusLoadingRingActive { get => _isSecurityStatusLoadingRingActive; set => SetProperty(ref _isSecurityStatusLoadingRingActive, value); }

        private string _securityLastRefreshedText = "";
        public string SecurityLastRefreshedText { get => _securityLastRefreshedText; set => SetProperty(ref _securityLastRefreshedText, value); }

        private Visibility _btnViewIssuesVisibility = Visibility.Collapsed;
        public Visibility BtnViewIssuesVisibility { get => _btnViewIssuesVisibility; set => SetProperty(ref _btnViewIssuesVisibility, value); }

        // Feature Cards
        private string _virusThreatProtectionStatus = "";
        public string VirusThreatProtectionStatus { get => _virusThreatProtectionStatus; set => SetProperty(ref _virusThreatProtectionStatus, value); }
        private Visibility _virusThreatProtectionLinkVisibility = Visibility.Collapsed;
        public Visibility VirusThreatProtectionLinkVisibility { get => _virusThreatProtectionLinkVisibility; set => SetProperty(ref _virusThreatProtectionLinkVisibility, value); }

        private string _firewallStatus = "";
        public string FirewallStatus { get => _firewallStatus; set => SetProperty(ref _firewallStatus, value); }
        private Visibility _firewallLinkVisibility = Visibility.Collapsed;
        public Visibility FirewallLinkVisibility { get => _firewallLinkVisibility; set => SetProperty(ref _firewallLinkVisibility, value); }

        private string _windowsUpdateStatus = "";
        public string WindowsUpdateStatus { get => _windowsUpdateStatus; set => SetProperty(ref _windowsUpdateStatus, value); }
        private Visibility _windowsUpdateLinkVisibility = Visibility.Collapsed;
        public Visibility WindowsUpdateLinkVisibility { get => _windowsUpdateLinkVisibility; set => SetProperty(ref _windowsUpdateLinkVisibility, value); }

        private string _smartScreenStatus = "";
        public string SmartScreenStatus { get => _smartScreenStatus; set => SetProperty(ref _smartScreenStatus, value); }
        private Visibility _smartScreenLinkVisibility = Visibility.Collapsed;
        public Visibility SmartScreenLinkVisibility { get => _smartScreenLinkVisibility; set => SetProperty(ref _smartScreenLinkVisibility, value); }

        private string _coreIsolationStatus = "";
        public string CoreIsolationStatus { get => _coreIsolationStatus; set => SetProperty(ref _coreIsolationStatus, value); }
        private Visibility _coreIsolationLinkVisibility = Visibility.Collapsed;
        public Visibility CoreIsolationLinkVisibility { get => _coreIsolationLinkVisibility; set => SetProperty(ref _coreIsolationLinkVisibility, value); }

        private string _realTimeProtectionStatus = "";
        public string RealTimeProtectionStatus { get => _realTimeProtectionStatus; set => SetProperty(ref _realTimeProtectionStatus, value); }
        private Visibility _realTimeProtectionLinkVisibility = Visibility.Collapsed;
        public Visibility RealTimeProtectionLinkVisibility { get => _realTimeProtectionLinkVisibility; set => SetProperty(ref _realTimeProtectionLinkVisibility, value); }

        private string _accountProtectionStatus = "";
        public string AccountProtectionStatus { get => _accountProtectionStatus; set => SetProperty(ref _accountProtectionStatus, value); }
        private Visibility _accountProtectionLinkVisibility = Visibility.Collapsed;
        public Visibility AccountProtectionLinkVisibility { get => _accountProtectionLinkVisibility; set => SetProperty(ref _accountProtectionLinkVisibility, value); }

        private string _lsaProtectionStatus = "";
        public string LsaProtectionStatus { get => _lsaProtectionStatus; set => SetProperty(ref _lsaProtectionStatus, value); }
        private Visibility _lsaProtectionLinkVisibility = Visibility.Collapsed;
        public Visibility LsaProtectionLinkVisibility { get => _lsaProtectionLinkVisibility; set => SetProperty(ref _lsaProtectionLinkVisibility, value); }

        private string _tamperProtectionStatus = "";
        public string TamperProtectionStatus { get => _tamperProtectionStatus; set => SetProperty(ref _tamperProtectionStatus, value); }
        private Visibility _tamperProtectionLinkVisibility = Visibility.Collapsed;
        public Visibility TamperProtectionLinkVisibility { get => _tamperProtectionLinkVisibility; set => SetProperty(ref _tamperProtectionLinkVisibility, value); }

        private string _controlledFolderAccessStatus = "";
        public string ControlledFolderAccessStatus { get => _controlledFolderAccessStatus; set => SetProperty(ref _controlledFolderAccessStatus, value); }
        private Visibility _controlledFolderAccessLinkVisibility = Visibility.Collapsed;
        public Visibility ControlledFolderAccessLinkVisibility { get => _controlledFolderAccessLinkVisibility; set => SetProperty(ref _controlledFolderAccessLinkVisibility, value); }

        private string _bitLockerStatus = "";
        public string BitLockerStatus { get => _bitLockerStatus; set => SetProperty(ref _bitLockerStatus, value); }
        private Visibility _bitLockerLinkVisibility = Visibility.Collapsed;
        public Visibility BitLockerLinkVisibility { get => _bitLockerLinkVisibility; set => SetProperty(ref _bitLockerLinkVisibility, value); }

        private string _defenderServiceStatus = "";
        public string DefenderServiceStatus { get => _defenderServiceStatus; set => SetProperty(ref _defenderServiceStatus, value); }
        private Visibility _defenderServiceLinkVisibility = Visibility.Collapsed;
        public Visibility DefenderServiceLinkVisibility { get => _defenderServiceLinkVisibility; set => SetProperty(ref _defenderServiceLinkVisibility, value); }

        // Toggles and Selectors
        private string _remoteDesktopStatus = "";
        public string RemoteDesktopStatus { get => _remoteDesktopStatus; set => SetProperty(ref _remoteDesktopStatus, value); }
        private Visibility _remoteDesktopLinkVisibility = Visibility.Collapsed;
        public Visibility RemoteDesktopLinkVisibility { get => _remoteDesktopLinkVisibility; set => SetProperty(ref _remoteDesktopLinkVisibility, value); }
        private bool _isRdpEnabled;
        public bool IsRdpEnabled { get => _isRdpEnabled; set { if (SetProperty(ref _isRdpEnabled, value) && !_isRdpToggleUpdating) { _ = ToggleRdpAsync(value); } } }
        private bool _isRdpToggleEnabled = false;
        public bool IsRdpToggleEnabled { get => _isRdpToggleEnabled; set => SetProperty(ref _isRdpToggleEnabled, value); }

        private string _remoteAssistanceStatus = "";
        public string RemoteAssistanceStatus { get => _remoteAssistanceStatus; set => SetProperty(ref _remoteAssistanceStatus, value); }
        private Visibility _remoteAssistanceLinkVisibility = Visibility.Collapsed;
        public Visibility RemoteAssistanceLinkVisibility { get => _remoteAssistanceLinkVisibility; set => SetProperty(ref _remoteAssistanceLinkVisibility, value); }
        private bool _isRaEnabled;
        public bool IsRaEnabled { get => _isRaEnabled; set { if (SetProperty(ref _isRaEnabled, value) && !_isRaToggleUpdating) { _ = ToggleRaAsync(value); } } }
        private bool _isRaToggleEnabled = false;
        public bool IsRaToggleEnabled { get => _isRaToggleEnabled; set => SetProperty(ref _isRaToggleEnabled, value); }

        private string _developerModeStatus = "";
        public string DeveloperModeStatus { get => _developerModeStatus; set => SetProperty(ref _developerModeStatus, value); }
        private Visibility _developerModeLinkVisibility = Visibility.Collapsed;
        public Visibility DeveloperModeLinkVisibility { get => _developerModeLinkVisibility; set => SetProperty(ref _developerModeLinkVisibility, value); }
        private bool _isDevModeEnabled;
        public bool IsDevModeEnabled { get => _isDevModeEnabled; set { if (SetProperty(ref _isDevModeEnabled, value) && !_isDevModeToggleUpdating) { _ = ToggleDevModeAsync(value); } } }
        private bool _isDevModeToggleEnabled = false;
        public bool IsDevModeToggleEnabled { get => _isDevModeToggleEnabled; set => SetProperty(ref _isDevModeToggleEnabled, value); }

        private int _uacSliderValue;
        public int UacSliderValue { get => _uacSliderValue; set { if (SetProperty(ref _uacSliderValue, value) && !_isUacSliderUpdating) { UpdateUacLevel(value); } } }
        private string _uacLevelDescription = "";
        public string UacLevelDescription { get => _uacLevelDescription; set => SetProperty(ref _uacLevelDescription, value); }
        private bool _isUacSliderEnabled = false;
        public bool IsUacSliderEnabled { get => _isUacSliderEnabled; set => SetProperty(ref _isUacSliderEnabled, value); }

        private int _smartAppControlSelectedIndex;
        public int SmartAppControlSelectedIndex { get => _smartAppControlSelectedIndex; set { if (SetProperty(ref _smartAppControlSelectedIndex, value) && !_isSmartAppControlUpdating) { UpdateSmartAppControl(value); } } }
        private string _smartAppControlDescription = "";
        public string SmartAppControlDescription { get => _smartAppControlDescription; set => SetProperty(ref _smartAppControlDescription, value); }
        private bool _isSmartAppControlComboBoxEnabled = false;
        public bool IsSmartAppControlComboBoxEnabled { get => _isSmartAppControlComboBoxEnabled; set => SetProperty(ref _isSmartAppControlComboBoxEnabled, value); }

        private int _powerShellPolicySelectedIndex;
        public int PowerShellPolicySelectedIndex { get => _powerShellPolicySelectedIndex; set { if (SetProperty(ref _powerShellPolicySelectedIndex, value) && !_isPowerShellPolicyUpdating) { _ = UpdatePowerShellPolicyAsync(value); } } }
        private string _powerShellPolicyDescription = "";
        public string PowerShellPolicyDescription { get => _powerShellPolicyDescription; set => SetProperty(ref _powerShellPolicyDescription, value); }
        private bool _isPowerShellPolicyComboBoxEnabled = false;
        public bool IsPowerShellPolicyComboBoxEnabled { get => _isPowerShellPolicyComboBoxEnabled; set => SetProperty(ref _isPowerShellPolicyComboBoxEnabled, value); }
        private Microsoft.UI.Xaml.Media.Brush _powerShellPolicyDescriptionForeground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray);
        public Microsoft.UI.Xaml.Media.Brush PowerShellPolicyDescriptionForeground { get => _powerShellPolicyDescriptionForeground; set => SetProperty(ref _powerShellPolicyDescriptionForeground, value); }
        private double _powerShellPolicyDescriptionOpacity = 0.8;
        public double PowerShellPolicyDescriptionOpacity { get => _powerShellPolicyDescriptionOpacity; set => SetProperty(ref _powerShellPolicyDescriptionOpacity, value); }

        private string _signatureUpdateText = "";
        public string SignatureUpdateText { get => _signatureUpdateText; set => SetProperty(ref _signatureUpdateText, value); }
        private Visibility _signatureUpdateTextVisibility = Visibility.Collapsed;
        public Visibility SignatureUpdateTextVisibility { get => _signatureUpdateTextVisibility; set => SetProperty(ref _signatureUpdateTextVisibility, value); }

        private string _antivirusProductName = "";
        public string AntivirusProductName { get => _antivirusProductName; set => SetProperty(ref _antivirusProductName, value); }

        private bool _isQuickScanRunning;
        public bool IsQuickScanRunning { get => _isQuickScanRunning; set => SetProperty(ref _isQuickScanRunning, value); }
        #endregion

        #region Constructor
        public DiagnosticsPageViewModel()
        {
            _instance = this;

            _scannerEngine = new DiagnosticScannerEngine(this);

            LocalMachineSettingsEngine.LoadDismissedEventsList();

            PerformanceGraphPoints.Add(new Point(400, 100));

            if (LocalMachineSettingsEngine.EnableLiveDiagnostics)
            {
                StartLiveMonitoring();
                StartLiveTelemetry();
            }

            _computerService = new EvolveOS_Optimizer.Utilities.Services.ComputerService();
            _hotKeyService = App.GetService<IHotkeyService>()!;

            _isOptimizationKeyValid = true;
            _cancellationTokenSource = new CancellationTokenSource();
            Computer = new Computer();

            AddProcessToExclusionListCommand = new RelayCommand<string>(AddProcessToExclusionList, _ => CanAddProcessToExclusionList);
            OptimizeCommand = new EvolveOS_Optimizer.Core.Base.RelayCommand(_ => _ = OptimizeAsync(Enums.Memory.Optimization.Reason.Manual), _ => CanOptimize);
            RemoveProcessFromExclusionListCommand = new RelayCommand<string>(RemoveProcessFromExclusionList);

            MemoryUsageThresholds = Enumerable.Range(1, 99).Select(number => (byte)number).ToList();
            _computerService.OnOptimizeProgressUpdate += OnOptimizeProgressUpdate;
            Computer.OperatingSystem = _computerService.OperatingSystem;

            App.HotkeySettingsChanged += OnHotkeySettingsChanged;

            Thread monitorThread = new Thread(MonitorLoop) { IsBackground = true };
            monitorThread.Start();

            MonitorAsync();

            RefreshAllDrivesInfo();

            MinedSystemEvents.CollectionChanged += (s, e) =>
            {
                _dispatcherQueue.TryEnqueue(() => UpdateSystemStatus());
            };
        }
        #endregion

        #region Standard Properties (Diagnostics)
        public ObservableCollection<DismissedEventCard> HistoryCards { get; } = new();

        public Visibility EventEmptyStateVisibility =>
            !IsScanning && MinedSystemEvents.Count <= 5 && !ShowMinorEvents ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MinorEventsButtonVisibility =>
            !IsScanning && MinedSystemEvents.Count > 0 && MinedSystemEvents.Count <= 5 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MinorEventsTextVisibility =>
            !IsScanning && MinedSystemEvents.Count > 0 && MinedSystemEvents.Count <= 5 && !ShowMinorEvents ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EventListVisibility =>
            !IsScanning && (MinedSystemEvents.Count > 5 || (MinedSystemEvents.Count > 0 && ShowMinorEvents))
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility Dot1Visibility => !IsScanning && MinedSystemEvents.Count >= 1 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Dot2Visibility => !IsScanning && MinedSystemEvents.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Dot3Visibility => !IsScanning && MinedSystemEvents.Count >= 3 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Dot4Visibility => !IsScanning && MinedSystemEvents.Count >= 4 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Dot5Visibility => !IsScanning && MinedSystemEvents.Count >= 5 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HardwareScannerVisibility =>
            IsScanning || DetectedHardwareIssues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HardwareListVisibility =>
            !IsScanning && DetectedHardwareIssues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public string HardwareScannerText => IsScanning
            ? ResourceString.GetString("diag_hw_interrogating") ?? "INTERROGATING BUS..."
            : ResourceString.GetString("diag_hw_optimal") ?? "HARDWARE OPTIMAL. MONITORING BUS...";

        private string _scannerText = ResourceString.GetString("diag_sys_optimal") ?? "SYSTEM OPTIMAL. MONITORING...";
        public string ScannerText
        {
            get => _scannerText;
            set => SetProperty(ref _scannerText, value);
        }

        private SolidColorBrush _systemHealthBrush = new SolidColorBrush(Colors.LimeGreen);
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

                    RefreshHUD();
                }
            }
        }

        public bool IsNotScanning => !IsScanning;

        public Visibility ScanningVisibility =>
            IsScanning ? Visibility.Visible : Visibility.Collapsed;

        private string _scanStatus = ResourceString.GetString("diag_scan_idle") ?? "System idle. Ready to initiate diagnostic scan.";
        public string ScanStatus
        {
            get => _scanStatus;
            set => SetProperty(ref _scanStatus, value);
        }

        private Visibility _historyPanelVisibility = Visibility.Collapsed;
        public Visibility HistoryPanelVisibility
        {
            get => _historyPanelVisibility;
            set
            {
                SetProperty(ref _historyPanelVisibility, value);
                OnPropertyChanged(nameof(ActiveListVisibility));
            }
        }
        public Visibility ActiveListVisibility => HistoryPanelVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

        private Visibility _historyEmptyStateVisibility = Visibility.Visible;
        public Visibility HistoryEmptyStateVisibility
        {
            get => _historyEmptyStateVisibility;
            set => SetProperty(ref _historyEmptyStateVisibility, value);
        }

        private bool _showMinorEvents;
        public bool ShowMinorEvents
        {
            get => _showMinorEvents;
            set
            {
                if (SetProperty(ref _showMinorEvents, value))
                {
                    if (value && HistoryPanelVisibility == Visibility.Visible)
                    {
                        HistoryPanelVisibility = Visibility.Collapsed;
                    }

                    OnPropertyChanged(nameof(EventEmptyStateVisibility));
                    OnPropertyChanged(nameof(EventListVisibility));
                    OnPropertyChanged(nameof(MinorEventsTextVisibility));
                }
            }
        }

        private Microsoft.UI.Xaml.Media.PointCollection _cpuTrayPoints = new();
        public Microsoft.UI.Xaml.Media.PointCollection CpuTrayPoints
        {
            get => _cpuTrayPoints;
            set => SetProperty(ref _cpuTrayPoints, value);
        }

        private Microsoft.UI.Xaml.Media.PointCollection _ramTrayPoints = new();
        public Microsoft.UI.Xaml.Media.PointCollection RamTrayPoints
        {
            get => _ramTrayPoints;
            set => SetProperty(ref _ramTrayPoints, value);
        }

        private Microsoft.UI.Xaml.Media.PointCollection _gpuTrayPoints = new();
        public Microsoft.UI.Xaml.Media.PointCollection GpuTrayPoints
        {
            get => _gpuTrayPoints;
            set => SetProperty(ref _gpuTrayPoints, value);
        }

        private Microsoft.UI.Xaml.Media.PointCollection _diskTrayPoints = new();
        public Microsoft.UI.Xaml.Media.PointCollection DiskTrayPoints
        {
            get => _diskTrayPoints;
            set => SetProperty(ref _diskTrayPoints, value);
        }

        private ObservableCollection<double> _ramTrayHistory = new ObservableCollection<double>();
        public ObservableCollection<double> RamTrayHistory
        {
            get => _ramTrayHistory;
            set => SetProperty(ref _ramTrayHistory, value);
        }

        private ObservableCollection<double> _diskTrayHistory = new ObservableCollection<double>();
        public ObservableCollection<double> DiskTrayHistory
        {
            get => _diskTrayHistory;
            set => SetProperty(ref _diskTrayHistory, value);
        }

        private ObservableCollection<double> _gpuTrayHistory = new ObservableCollection<double>();
        public ObservableCollection<double> GpuTrayHistory
        {
            get => _gpuTrayHistory;
            set => SetProperty(ref _gpuTrayHistory, value);
        }
        #endregion

        #region Storage Monitoring Properties

        public ObservableCollection<DriveSpaceInfo> SystemDrives { get; } = new ObservableCollection<DriveSpaceInfo>();

        private bool _isStorageInfoSelected;
        public bool IsStorageInfoSelected
        {
            get => _isStorageInfoSelected;
            set => SetProperty(ref _isStorageInfoSelected, value);
        }

        #endregion

        #region Merged Core UI & State Properties (Maintenance)
        public Computer? Computer
        {
            get => _computer;
            set { SetProperty(ref _computer, value); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { SetProperty(ref _isBusy, value); }
        }

        public bool IsOptimizationRunning
        {
            get => _isOptimizationRunning;
            set { SetProperty(ref _isOptimizationRunning, value); OnPropertyChanged(nameof(CanOptimize)); }
        }

        public bool CanOptimize => MemoryAreas != Enums.Memory.Areas.None && !IsOptimizationRunning;

        public string TotalSpaceToFree
        {
            get => _totalSpaceToFree;
            set { SetProperty(ref _totalSpaceToFree, value); }
        }
        #endregion

        #region Advanced Features Bridge (Diagnostics)

        public enum TelemetryMetric { CPU, RAM, Disk, Pagefile, GPU, Network }

        private TelemetryMetric _activeGraphMetric = TelemetryMetric.CPU;
        public TelemetryMetric ActiveGraphMetric
        {
            get => _activeGraphMetric;
            set
            {
                if (SetProperty(ref _activeGraphMetric, value))
                {
                    if (value != TelemetryMetric.Disk)
                    {
                        IsStorageInfoSelected = false;
                    }

                    OnPropertyChanged(nameof(IsCpuSelected));
                    OnPropertyChanged(nameof(IsRamSelected));
                    OnPropertyChanged(nameof(IsDiskSelected));
                    OnPropertyChanged(nameof(IsPageSelected));
                    OnPropertyChanged(nameof(IsGpuSelected));
                    OnPropertyChanged(nameof(IsNetworkSelected));

                    OnPropertyChanged(nameof(CpuSecondaryVisibility));
                    OnPropertyChanged(nameof(RamSecondaryVisibility));
                    OnPropertyChanged(nameof(DiskSecondaryVisibility));
                    OnPropertyChanged(nameof(PageSecondaryVisibility));
                    OnPropertyChanged(nameof(GpuSecondaryVisibility));
                    OnPropertyChanged(nameof(HeroStandardVisibility));
                    OnPropertyChanged(nameof(NetworkSecondaryVisibility));

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

        public bool IsGpuSelected
        {
            get => ActiveGraphMetric == TelemetryMetric.GPU;
            set { if (value) ActiveGraphMetric = TelemetryMetric.GPU; }
        }

        public bool IsNetworkSelected
        {
            get => ActiveGraphMetric == TelemetryMetric.Network;
            set { if (value) ActiveGraphMetric = TelemetryMetric.Network; }
        }

        public Visibility CpuSecondaryVisibility => IsCpuSelected ? Visibility.Collapsed : Visibility.Visible;
        public Visibility RamSecondaryVisibility => IsRamSelected ? Visibility.Collapsed : Visibility.Visible;
        public Visibility DiskSecondaryVisibility => IsDiskSelected ? Visibility.Collapsed : Visibility.Visible;
        public Visibility PageSecondaryVisibility => IsPageSelected ? Visibility.Collapsed : Visibility.Visible;
        public Visibility GpuSecondaryVisibility => IsGpuSelected ? Visibility.Collapsed : Visibility.Visible;
        public Visibility NetworkSecondaryVisibility => IsNetworkSelected ? Visibility.Collapsed : Visibility.Visible;
        public Visibility HeroStandardVisibility => IsNetworkSelected ? Visibility.Collapsed : Visibility.Visible;

        public string ActivePrimaryLabel => ActiveGraphMetric switch
        {
            TelemetryMetric.RAM => ResourceString.GetString("diag_ram_load") ?? "RAM LOAD",
            TelemetryMetric.Disk => ResourceString.GetString("diag_io_load") ?? "DISK I/O",
            TelemetryMetric.Pagefile => ResourceString.GetString("diag_pagefile_load") ?? "PAGEFILE",
            TelemetryMetric.GPU => ResourceString.GetString("diag_gpu_load") ?? "GPU LOAD",
            TelemetryMetric.Network => ResourceString.GetString("diag_network_load") ?? "NETWORK SPEED",
            _ => ResourceString.GetString("diag_cpu_load") ?? "CPU LOAD"
        };

        public string ActivePrimaryValueStr => ActiveGraphMetric switch
        {
            TelemetryMetric.RAM => CurrentRamLoadStr,
            TelemetryMetric.Disk => CurrentIoLoadStr,
            TelemetryMetric.Pagefile => CurrentPagefileLoadStr,
            TelemetryMetric.GPU => CurrentGpuLoadStr,
            TelemetryMetric.Network => CurrentNetworkLoadStr,
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

        private int _maxGraphSeconds = LocalMachineSettingsEngine.DiagnosticsGraphTime;
        public int MaxGraphSeconds
        {
            get => _maxGraphSeconds;
            set
            {
                if (SetProperty(ref _maxGraphSeconds, value))
                {
                    LocalMachineSettingsEngine.DiagnosticsGraphTime = value;

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


        private string _aiSummary = ResourceString.GetString("diag_ai_sleeping") ?? "AI Engine sleeping. Run a scan to generate a system health summary.";
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

        private PointCollection _performanceGraphPointsAlt = new();
        public PointCollection PerformanceGraphPointsAlt
        {
            get => _performanceGraphPointsAlt;
            set => SetProperty(ref _performanceGraphPointsAlt, value);
        }

        private PointCollection _performanceAreaPointsAlt = new();
        public PointCollection PerformanceAreaPointsAlt
        {
            get => _performanceAreaPointsAlt;
            set => SetProperty(ref _performanceAreaPointsAlt, value);
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
        public string CurrentDiskLoadStr => CurrentIoLoadStr;

        private string _currentPagefileLoadStr = "0%";
        public string CurrentPagefileLoadStr
        {
            get => _currentPagefileLoadStr;
            set => SetProperty(ref _currentPagefileLoadStr, value);
        }

        private string _currentGpuLoadStr = "0%";
        public string CurrentGpuLoadStr
        {
            get => _currentGpuLoadStr;
            set => SetProperty(ref _currentGpuLoadStr, value);
        }

        private string _currentNetworkUpLoadStr = "0 Mbps";
        public string CurrentNetworkUpLoadStr
        {
            get => _currentNetworkUpLoadStr;
            set => SetProperty(ref _currentNetworkUpLoadStr, value);
        }

        private string _currentNetworkDownLoadStr = "0 Mbps";
        public string CurrentNetworkDownLoadStr
        {
            get => _currentNetworkDownLoadStr;
            set => SetProperty(ref _currentNetworkDownLoadStr, value);
        }

        private string _currentNetworkLoadStr = "0 / 0 Mbps";
        public string CurrentNetworkLoadStr
        {
            get => _currentNetworkLoadStr;
            set => SetProperty(ref _currentNetworkLoadStr, value);
        }

        private string _currentNetworkLoadSecondaryStr = "0 / 0";
        public string CurrentNetworkLoadSecondaryStr
        {
            get => _currentNetworkLoadSecondaryStr;
            set => SetProperty(ref _currentNetworkLoadSecondaryStr, value);
        }

        public ObservableCollection<HourlyMetric> StabilityTrendData { get; } = new();
        public ObservableCollection<HardwareIssue> DetectedHardwareIssues { get; } = new();
        public ObservableCollection<SystemEventItem> MinedSystemEvents { get; } = new();
        #endregion

        #region Progress Properties (Maintenance)
        public byte OptimizationProgressPercentage
        {
            get => _optimizationProgressPercentage;
            set { SetProperty(ref _optimizationProgressPercentage, value); }
        }

        public string OptimizationProgressStep
        {
            get => _optimizationProgressStep;
            set { SetProperty(ref _optimizationProgressStep, value); }
        }

        public byte OptimizationProgressTotal
        {
            get => _optimizationProgressTotal;
            set { SetProperty(ref _optimizationProgressTotal, value); }
        }

        public byte OptimizationProgressValue
        {
            get => _optimizationProgressValue;
            set { SetProperty(ref _optimizationProgressValue, value); }
        }
        #endregion

        #region Settings & Configuration Properties (Maintenance)
        public List<VirtualKey> KeyboardKeys => _hotKeyService.Keys;
        public Dictionary<VirtualKeyModifiers, string> KeyboardModifiers => _hotKeyService.Modifiers;

        public VirtualKey OptimizationKey
        {
            get => LocalMachineSettingsEngine.OptimizationKey;
            set
            {
                if (value == VirtualKey.None || (int)value == 0) return;
                if (value != LocalMachineSettingsEngine.OptimizationKey)
                {
                    LocalMachineSettingsEngine.OptimizationKey = value;
                    OnPropertyChanged(nameof(OptimizationKey));
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
                    OnPropertyChanged(nameof(OptimizationModifiers));
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
                    SetProperty(ref _isOptimizationKeyValid, value);
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
                    OnPropertyChanged(nameof(UseHotkey));
                    IsOptimizationKeyValid = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public bool RestartExplorerAfterOptimization
        {
            get => LocalMachineSettingsEngine.RestartExplorerAfterOptimization;
            set
            {
                try { IsBusy = true; LocalMachineSettingsEngine.RestartExplorerAfterOptimization = value; OnPropertyChanged(nameof(RestartExplorerAfterOptimization)); }
                finally { IsBusy = false; }
            }
        }

        public bool RunOnLowPriority
        {
            get => LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low;
            set
            {
                try
                {
                    IsBusy = true;
                    var priority = value ? Enums.Priority.Low : Enums.Priority.Normal;
                    App.SetPriority(priority);
                    LocalMachineSettingsEngine.RunOnPriority = priority;
                    OnPropertyChanged(nameof(RunOnLowPriority));
                }
                finally { IsBusy = false; }
            }
        }

        public bool ShowOptimizationNotifications
        {
            get => LocalMachineSettingsEngine.ShowOptimizationNotifications;
            set
            {
                try { IsBusy = true; LocalMachineSettingsEngine.ShowOptimizationNotifications = value; OnPropertyChanged(nameof(ShowOptimizationNotifications)); }
                finally { IsBusy = false; }
            }
        }

        public bool DisableAllOptimizationResults
        {
            get => LocalMachineSettingsEngine.DisableAllOptimizationResults;
            set
            {
                try { IsBusy = true; LocalMachineSettingsEngine.DisableAllOptimizationResults = value; OnPropertyChanged(nameof(DisableAllOptimizationResults)); OnPropertyChanged(nameof(SettingItems)); }
                finally { IsBusy = false; }
            }
        }

        public int AutoOptimizationInterval
        {
            get => LocalMachineSettingsEngine.AutoOptimizationInterval;
            set
            {
                if (LocalMachineSettingsEngine.AutoOptimizationInterval != value)
                {
                    LocalMachineSettingsEngine.AutoOptimizationInterval = value;
                    OnPropertyChanged(nameof(AutoOptimizationInterval));
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
                OnPropertyChanged(nameof(AutoOptimizationMemoryUsage));
                OnPropertyChanged(nameof(AutoOptimizationMemoryUsageDescription));
            }
        }

        public bool ShowHardwarePanelInTray
        {
            get => LocalMachineSettingsEngine.ShowHardwarePanelInTray;
            set
            {
                if (LocalMachineSettingsEngine.ShowHardwarePanelInTray != value)
                {
                    LocalMachineSettingsEngine.ShowHardwarePanelInTray = value;
                    OnPropertyChanged(nameof(ShowHardwarePanelInTray));

                    _dispatcherQueue?.TryEnqueue(() => {
                        OnPropertyChanged(nameof(ShowHardwarePanelInTray));
                    });
                }
            }
        }

        public bool ShowCpuInTray
        {
            get => LocalMachineSettingsEngine.ShowCpuInTray;
            set { LocalMachineSettingsEngine.ShowCpuInTray = value; OnPropertyChanged(nameof(ShowCpuInTray)); }
        }

        public bool ShowRamInTray
        {
            get => LocalMachineSettingsEngine.ShowRamInTray;
            set { LocalMachineSettingsEngine.ShowRamInTray = value; OnPropertyChanged(nameof(ShowRamInTray)); }
        }

        public bool ShowDiskInTray
        {
            get => LocalMachineSettingsEngine.ShowDiskInTray;
            set { LocalMachineSettingsEngine.ShowDiskInTray = value; OnPropertyChanged(nameof(ShowDiskInTray)); }
        }

        public bool ShowGpuInTray
        {
            get => LocalMachineSettingsEngine.ShowGpuInTray;
            set { LocalMachineSettingsEngine.ShowGpuInTray = value; OnPropertyChanged(nameof(ShowGpuInTray)); }
        }

        public string AutoOptimizationMemoryIntervalDescription => ResourceHelper.GetPluralizedString("txt_auto_opt_interval", AutoOptimizationInterval);
        public string AutoOptimizationMemoryUsageDescription => string.Format(ResourceString.GetString("txt_auto_opt_usage_limit") ?? "", AutoOptimizationMemoryUsage);
        public string AutoOptimizationMemoryUsageWarning => ResourceString.GetString("txt_auto_opt_usage_warning") ?? "";
        public List<byte> MemoryUsageThresholds { get; private set; }
        #endregion

        #region Observable Collections & Memory Areas (Maintenance)
        public ObservableCollection<ObservableItem<bool>> SettingItems
        {
            get
            {
                return new ObservableCollection<ObservableItem<bool>>(new List<ObservableItem<bool>>
                {
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_show_notification") ?? "Show Notifications", () => ShowOptimizationNotifications, v => ShowOptimizationNotifications = v, !DisableAllOptimizationResults, ResourceString.GetString("description_settings_items_show_notification")),
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_show_no_result") ?? "Disable Results", () => DisableAllOptimizationResults, v => { DisableAllOptimizationResults = v; OnPropertyChanged(nameof(SettingItems)); }, true, ResourceString.GetString("description_settings_items_show_no_result")),
                    new ObservableItem<bool>(ResourceString.GetString("title_settings_items_low_priority") ?? "Low Priority", () => RunOnLowPriority, v => RunOnLowPriority = v, true, ResourceString.GetString("description_settings_items_low_priority"))
                }.OrderBy(i => i.Name));
            }
        }

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

                add(ResourceString.GetString("title_memory_areas_combined_page_list") ?? "Combined Page List", "", Enums.Memory.Areas.CombinedPageList, Computer.OperatingSystem.HasCombinedPageList);
                add(ResourceString.GetString("title_memory_areas_modified_file_cache") ?? "Modified File Cache", "", Enums.Memory.Areas.ModifiedFileCache, Computer.OperatingSystem.HasModifiedFileCache);
                add(ResourceString.GetString("title_memory_areas_modified_page_list") ?? "Modified Page List", "", Enums.Memory.Areas.ModifiedPageList, Computer.OperatingSystem.HasModifiedPageList);
                add(ResourceString.GetString("title_memory_areas_registry_cache") ?? "Registry Cache", "", Enums.Memory.Areas.RegistryCache, Computer.OperatingSystem.HasRegistryHive);
                add(ResourceString.GetString("title_memory_areas_standby_list") ?? "Standby List", "", Enums.Memory.Areas.StandbyList, Computer.OperatingSystem.HasStandbyList);
                add(ResourceString.GetString("title_memory_areas_standby_list_low_priority") ?? "Standby List (Low)", "", Enums.Memory.Areas.StandbyListLowPriority, Computer.OperatingSystem.HasStandbyList);
                add(ResourceString.GetString("title_memory_areas_system_file_cache") ?? "System File Cache", "", Enums.Memory.Areas.SystemFileCache, Computer.OperatingSystem.HasSystemFileCache);
                add(ResourceString.GetString("title_memory_areas_working_set") ?? "Working Set", "", Enums.Memory.Areas.WorkingSet, Computer.OperatingSystem.HasWorkingSet);

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

                add(ResourceString.GetString("title_memory_areas_disk_cleanup") ?? "Disk Cleanup", "", Enums.Memory.Areas.DiskCleanup, true);
                add(ResourceString.GetString("title_memory_areas_flush_dns") ?? "Flush DNS", "", Enums.Memory.Areas.FlushDns, true);
                add(ResourceString.GetString("title_memory_areas_windows_old") ?? "Windows.old", "", Enums.Memory.Areas.WindowsOld, hasWindowsOld);

                items.Add(new ObservableItem<bool>(ResourceString.GetString("title_settings_items_restart_explorer") ?? "Restart Explorer", () => RestartExplorerAfterOptimization, value => RestartExplorerAfterOptimization = value, true, ""));

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
                    OnPropertyChanged(nameof(MemoryAreas));
                    OnPropertyChanged(nameof(CanOptimize));
                    OnPropertyChanged(nameof(MemoryAreaItems));
                    OnPropertyChanged(nameof(SystemCleanupAreaItems));
                }
                finally { IsBusy = false; }
            }
        }
        #endregion

        #region Process Exclusions (Maintenance)
        public string? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetProperty(ref _selectedProcess, value))
                    OnPropertyChanged(nameof(CanAddProcessToExclusionList));
            }
        }

        public bool CanAddProcessToExclusionList => !string.IsNullOrWhiteSpace(SelectedProcess) && !LocalMachineSettingsEngine.ProcessExclusionList.Contains(SelectedProcess);

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

        public ObservableCollection<string> ProcessExclusionList => new ObservableCollection<string>(LocalMachineSettingsEngine.ProcessExclusionList);

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
        #endregion

        #region Commands & Events
        public System.Windows.Input.ICommand OptimizeCommand { get; }
        public System.Windows.Input.ICommand AddProcessToExclusionListCommand { get; }
        public System.Windows.Input.ICommand RemoveProcessFromExclusionListCommand { get; }
        public System.Windows.Input.ICommand RefreshCleanupSpaceCommand => new EvolveOS_Optimizer.Core.Base.RelayCommand(async (_) =>
        {
            if (IsScanning) return;

            TotalSpaceToFree = ResourceString.GetString("txt_scanning") ?? "Scanning...";
            await CalculateCleanupSpaceAsync();
        });

        public Action<Enums.Memory.Optimization.Reason, string>? OnOptimizeCommandCompleted;
        public event Action? OnAddProcessToExclusionListCommandCompleted;
        public event Action? OnRemoveProcessFromExclusionListCommandCompleted;

        [RelayCommand]
        public async Task FixEventAsync(int eventId)
        {
            if (eventId >= 9101 && eventId <= 9117)
            {
                try
                {
                    string? targetUri = eventId switch
                    {
                        9101 => "windowsdefender://threatsettings/",       // Antivirus
                        9102 => "windowsdefender://network/",              // Firewall
                        9103 => "windowsdefender://threatsettings/",       // Real-Time
                        9104 => "windowsdefender://threatsettings/",       // Tamper
                        9106 => "ms-settings:windowsupdate",               // Windows Update
                        9107 => "windowsdefender://smartscreenpua/",       // SmartScreen
                        9108 => "windowsdefender://coreisolation/",        // Core Isolation
                        9109 => "windowsdefender://ransomwareprotection/", // Controlled Folder Access
                        9110 => "windowsdefender://account/",              // Account Protection
                        9111 => "windowsdefender://threatsettings/",       // Defender Service
                        9112 => "windowsdefender://smartapp/",             // Smart App Control
                        9113 => "windowsdefender://coreisolation/",        // LSA Protection
                        9114 => "ms-settings:remotedesktop",               // Remote Desktop
                        9116 => "ms-settings:developers",                  // Developer Mode
                        _ => null
                    };

                    if (targetUri != null)
                    {
                        Process.Start(new ProcessStartInfo { FileName = targetUri, UseShellExecute = true });
                    }
                    else if (eventId == 9105)
                    {
                        Process.Start(new ProcessStartInfo("UserAccountControlSettings.exe") { UseShellExecute = true });
                    }
                    else if (eventId == 9115)
                    {
                        Process.Start(new ProcessStartInfo("SystemPropertiesRemote.exe") { UseShellExecute = true });
                    }
                    else if (eventId == 9117)
                    {
                        Process.Start(new ProcessStartInfo("ms-settings:privacy") { UseShellExecute = true });
                    }

                    ScanStatus = ResourceString.GetString("diag_security_fix_launched") ?? "Opened system settings. Please adjust the required protection.";
                    return;
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                    ScanStatus = "Failed to launch security tool.";
                    return;
                }
            }

            if (eventId == 9003)
            {
                ScanStatus = ResourceString.GetString("diag_fix_dwm_attempt") ?? "Unlocking .NET native cache...";

                string netTempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", ".net");

                try
                {
                    UnlockHandleHelper.UnlockDirectory(netTempPath);
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }

                var currentXamlRoot = App.MainWindow?.Content?.XamlRoot;

                if (currentXamlRoot == null)
                {
                    ScanStatus = "UI context unavailable. Please restart the application manually.";
                    return;
                }

                ScanStatus = ResourceString.GetString("diag_fix_dwm_success") ?? "Cache unlocked. Pending restart...";

                ContentDialog restartDialog = new ContentDialog
                {
                    XamlRoot = currentXamlRoot,
                    Title = ResourceString.GetString("diag_reboot_required_title") ?? "Restart Required",
                    Content = ResourceString.GetString("diag_reboot_required_msg") ?? "The application must be restarted to purge the corrupted UI cache. Would you like to restart now?",
                    PrimaryButtonText = ResourceString.GetString("txt_restart_now") ?? "Restart Now",
                    CloseButtonText = ResourceString.GetString("txt_later") ?? "Later",
                    DefaultButton = ContentDialogButton.Primary
                };

                if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
                {
                    restartDialog.Style = (Style)style;
                }

                ContentDialogResult result = await restartDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    LocalMachineSettingsEngine.LastCachePurgeTime = DateTime.Now;

                    string deleteCmd = $"rd /s /q \"{netTempPath}\"";
                    SettingsEngine.SelfReboot(deleteCmd);
                }
                else
                {
                    ScanStatus = ResourceString.GetString("diag_fix_dwm_cancelled") ?? "Cache purge aborted. Restart required.";
                    AiSummary = ResourceString.GetString("diag_fix_dwm_pending") ?? "PENDING: The ui rendering cache is unlocked but requires an app restart to purge.";
                }

                return;
            }

            ScanStatus = string.Format(ResourceString.GetString("diag_fix_event_attempt") ?? "Attempting remediation for {0}...", eventId);

            bool success = await RemediationEngine.RunFixAsync(eventId);

            if (success)
            {
                ScanStatus = string.Format(ResourceString.GetString("diag_fix_event_success") ?? "Successfully repaired Event {0}.", eventId);

                if (eventId == 1801)
                {
                    AiSummary = ResourceString.GetString("diag_secureboot_fix_msg")
                        ?? "Secure Boot update staged. CRITICAL: You must RESTART your computer TWICE for the hardware to enroll the new keys.";
                }
                else if (eventId == 7026 || eventId == 7000)
                {
                    AiSummary = "LUAFV virtualization repaired. Note: This re-enabled UAC. A reboot is required.";
                }
                else
                {
                    AiSummary = string.Format(ResourceString.GetString("diag_fix_event_deploy") ?? "AUTO-FIX DEPLOYED: The issue associated with ID {0} has been resolved.", eventId);
                }

                var fixedEvent = MinedSystemEvents.FirstOrDefault(e => e.EventId == eventId);
                if (fixedEvent != null) MinedSystemEvents.Remove(fixedEvent);

                CalculateStabilityTrend(MinedSystemEvents);
            }
            else
            {
                ScanStatus = string.Format(ResourceString.GetString("diag_fix_event_fail") ?? "Remediation failed for Event {0}.", eventId);
            }
        }

        [RelayCommand]
        public async Task FixHardwareAsync(HardwareIssue issue)
        {
            if (issue == null || string.IsNullOrEmpty(issue.DeviceId)) return;

            ScanStatus = string.Format(ResourceString.GetString("diag_fix_hw_init") ?? "Initiating hardware sequence for {0} to initialize driver stack...", issue.ComponentDisplayName);

            bool success = await RemediationEngine.RunHardwareFixAsync(issue);

            if (success)
            {
                ScanStatus = ResourceString.GetString("diag_fix_hw_wait") ?? "Command sent. Waiting for OS...";
                await Task.Delay(2500);
                await ExecuteFullScanAsync();
                AiSummary = string.Format(ResourceString.GetString("diag_fix_hw_verified") ?? "REMEDIATION VERIFIED: {0} driver stack signaled and re-initialized.", issue.ComponentDisplayName);
            }
            else
            {
                ScanStatus = string.Format(ResourceString.GetString("diag_fix_hw_fail") ?? "Failed to remediate {0}.", issue.ComponentDisplayName);
            }
        }

        [RelayCommand]
        public void CopyEventMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(message);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                SendSystemNotification(1,
                    ResourceString.GetString("diag_copy_success_title") ?? "Copied to Clipboard",
                    ResourceString.GetString("diag_copy_success_msg") ?? "The event log message has been copied.");
            }
        }

        [RelayCommand]
        public void DismissEvent(SystemEventItem ev)
        {
            if (ev == null) return;

            MinedSystemEvents.Remove(ev);

            string eventFingerprint = ev.EventId >= 9101
                ? $"{ev.EventId}_{ev.SourceName}_SECURE"
                : $"{ev.EventId}_{ev.SourceName}_{ev.TimeCreated.Ticks}";

            LocalMachineSettingsEngine.DismissedEventsList.Add(eventFingerprint);
            LocalMachineSettingsEngine.SaveDismissedEventsList();

            CalculateStabilityTrend(MinedSystemEvents);
        }

        [RelayCommand]
        public void ToggleHistoryPanel()
        {
            if (HistoryPanelVisibility == Visibility.Visible)
            {
                HistoryPanelVisibility = Visibility.Collapsed;
            }
            else
            {
                if (ShowMinorEvents)
                {
                    ShowMinorEvents = false;
                }

                HistoryCards.Clear();
                foreach (var hash in LocalMachineSettingsEngine.DismissedEventsList)
                {
                    var parts = hash.Split('_');
                    if (parts.Length == 3)
                    {
                        string dateDisplay;

                        if (parts[2] == "SECURE")
                        {
                            dateDisplay = ResourceString.GetString("diag_history_system_state") ?? "Active Configuration";
                        }
                        else if (long.TryParse(parts[2], out long ticks))
                        {
                            dateDisplay = new DateTime(ticks).ToString("g");
                        }
                        else
                        {
                            continue;
                        }

                        HistoryCards.Add(new DismissedEventCard
                        {
                            EventId = parts[0],
                            SourceName = parts[1],
                            DateString = dateDisplay,
                            OriginalHash = hash
                        });
                    }
                }

                HistoryEmptyStateVisibility = HistoryCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                HistoryPanelVisibility = Visibility.Visible;
            }
        }

        [RelayCommand]
        public async Task RestoreEvent(DismissedEventCard card)
        {
            if (card == null) return;

            LocalMachineSettingsEngine.DismissedEventsList.Remove(card.OriginalHash);
            LocalMachineSettingsEngine.SaveDismissedEventsList();
            HistoryCards.Remove(card);

            HistoryEmptyStateVisibility = HistoryCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            SendSystemNotification(1,
                ResourceString.GetString("diag_notify_restore_title") ?? "Event Restored",
                string.Format(ResourceString.GetString("diag_notify_restore_msg") ?? "Event ID {0} will appear in your next scan.", card.EventId));

            //Debug.WriteLine($"[RESTORE] Attempting to find Event {card.EventId} from {card.SourceName}...");

            await ExecuteFullScanAsync();

            /*bool found = MinedSystemEvents.Any(e => e.EventId.ToString() == card.EventId);
            if (!found)
            {
                Debug.WriteLine($"[RESTORE WARNING] Event {card.EventId} was not found in the fresh scan. It may have been purged by Windows or filtered by severity.");
            }*/
        }
        #endregion

        #region Security Action Commands (Security)
        public event Action<List<string>>? ShowSecurityIssuesRequested;

        [RelayCommand]
        public void ViewSecurityIssues()
        {
            if (_currentSecurityIssues.Count > 0)
            {
                ShowSecurityIssuesRequested?.Invoke(_currentSecurityIssues);
            }
        }

        [RelayCommand]
        public void OpenWindowsSecurity(string uri)
        {
            string targetUri = string.IsNullOrEmpty(uri) ? "windowsdefender://" : uri;

            try { Process.Start(new ProcessStartInfo { FileName = targetUri, UseShellExecute = true }); }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                try { Process.Start(new ProcessStartInfo { FileName = "windowsdefender://", UseShellExecute = true }); }
                catch (Exception fallbackEx) { ErrorLogging.LogDebug(fallbackEx); }
            }
        }

        [RelayCommand]
        public async Task RunQuickScanAsync()
        {
            try
            {
                IsQuickScanRunning = true;

                await Task.Run(() =>
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Start-MpScan -ScanType QuickScan\"",
                            UseShellExecute = true,
                            CreateNoWindow = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }).ConfigureAwait(true);

                SendSystemNotification(1, ResourceString.GetString("SecurityPage_QuickScanTitle") ?? "Quick Scan", ResourceString.GetString("SecurityPage_QuickScanCompleted") ?? "Scan completed.");

                await Task.Delay(1000);
                await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                SendSystemNotification(3, ResourceString.GetString("SecurityPage_QuickScanTitle") ?? "Quick Scan", ResourceString.GetString("SecurityPage_QuickScanFailed") ?? "Scan failed.");
            }
            finally
            {
                IsQuickScanRunning = false;
            }
        }

        [RelayCommand]
        public async Task UpdateDefenderSignaturesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Update-MpSignature\"",
                            UseShellExecute = true,
                            CreateNoWindow = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }).ConfigureAwait(true);

                SendSystemNotification(1, ResourceString.GetString("SecurityPage_UpdateDefinitionsTitle") ?? "Security Intelligence", ResourceString.GetString("SecurityPage_DefinitionsUpdated") ?? "Definitions updated successfully.");

                await Task.Delay(2000);
                await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                SendSystemNotification(3, ResourceString.GetString("SecurityPage_UpdateDefinitionsTitle") ?? "Security Intelligence", ResourceString.GetString("SecurityPage_DefinitionsUpdateFailed") ?? "Update failed.");
            }
        }

        [RelayCommand]
        public async Task RefreshSecurityStatusAsync()
        {
            await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }
        #endregion

        #region Diagnostics & Hardware Deep Scan Logic

        private void UpdateSystemStatus()
        {
            double.TryParse(StabilityScore.Replace("%", ""), out double currentScore);

            int currentCriticalCount = MinedSystemEvents.Count(e => e.Level == 1 || e.Level == 2);
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            double errorsPerHour = currentCriticalCount / Math.Max(1, uptime.TotalHours);

            bool isUptimeReliable = uptime.TotalMinutes > 30;
            bool isStabilityCritical = (isUptimeReliable && errorsPerHour > 5.0) || currentScore < 70;
            bool isHardwareCritical = DetectedHardwareIssues.Count > 0;
            bool hasCriticalAppCrash = MinedSystemEvents.Any(e => e.EventId == 9003);

            int totalEventCount = MinedSystemEvents.Count;

            // 1. Evaluate Critical State
            if (isHardwareCritical || isStabilityCritical || hasCriticalAppCrash)
            {
                SystemHealthBrush = new SolidColorBrush(Colors.Red);

                if (hasCriticalAppCrash && !isHardwareCritical && !isStabilityCritical)
                {
                    ScannerText = ResourceString.GetString("diag_status_critical_app") ?? "CRITICAL. RENDERING ENGINE CORRUPTION DETECTED.";
                }
                else if (isHardwareCritical)
                {
                    ScannerText = string.Format(ResourceString.GetString("diag_status_critical_hw") ?? "CRITICAL. {0} HARDWARE FAULTS.", DetectedHardwareIssues.Count);
                }
                else
                {
                    ScannerText = ResourceString.GetString("diag_status_critical_sw") ?? "CRITICAL. SYSTEM STABILITY COMPROMISED.";
                }
            }
            // 2. Evaluate Warning State (Crashes)
            else if (MinedSystemEvents.Any(e => e.EventId == 42))
            {
                SystemHealthBrush = new SolidColorBrush(Colors.Gold);
                ScannerText = ResourceString.GetString("diag_status_warning_crash") ?? "WARNING. RECENT SYSTEM CRASH OR POWER LOSS DETECTED.";
            }
            // 3. Evaluate Warning State (High Event Count)
            else if (totalEventCount > 5 || currentScore < 90)
            {
                SystemHealthBrush = new SolidColorBrush(Colors.Gold);
                ScannerText = string.Format(ResourceString.GetString("diag_status_warning_events") ?? "WARNING. {0} SYSTEM EVENTS LOGGED.", totalEventCount);
            }
            // 4. Evaluate Optimal State
            else
            {
                SystemHealthBrush = new SolidColorBrush(Colors.LimeGreen);

                if (totalEventCount > 0)
                {
                    ScannerText = string.Format(ResourceString.GetString("diag_status_optimal_minor") ?? "SYSTEM OPTIMAL. {0} MINOR LOGS IGNORED.", totalEventCount);
                }
                else
                {
                    ScannerText = ResourceString.GetString("diag_sys_optimal") ?? "SYSTEM OPTIMAL. MONITORING...";
                }
            }

            if (totalEventCount == 0)
            {
                ShowMinorEvents = false;
            }

            OnPropertyChanged(nameof(SystemHealthBrush));
            OnPropertyChanged(nameof(ScannerText));

            RefreshHUD();
        }

        private void RefreshHUD()
        {
            OnPropertyChanged(nameof(EventEmptyStateVisibility));
            OnPropertyChanged(nameof(EventListVisibility));
            OnPropertyChanged(nameof(MinorEventsButtonVisibility));
            OnPropertyChanged(nameof(MinorEventsTextVisibility));
            OnPropertyChanged(nameof(Dot1Visibility));
            OnPropertyChanged(nameof(Dot2Visibility));
            OnPropertyChanged(nameof(Dot3Visibility));
            OnPropertyChanged(nameof(Dot4Visibility));
            OnPropertyChanged(nameof(Dot5Visibility));
        }

        internal SystemEventItem CreateAlert(int eventId, string source, string message)
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
            await _scannerEngine.ExecuteFullScanAsync();
        }

        private void RebuildGraphFromHistory()
        {
            double logicalWidth = 400.0;
            double pixelsPerSecond = logicalWidth / MaxGraphSeconds;

            var newPoints = new PointCollection();
            var areaPoints = new PointCollection();

            var newPointsAlt = new PointCollection();
            var areaPointsAlt = new PointCollection();

            var targetBuffer = ActiveGraphMetric switch
            {
                TelemetryMetric.RAM => _ramHistoryBuffer,
                TelemetryMetric.Disk => _diskHistoryBuffer,
                TelemetryMetric.Pagefile => _pageHistoryBuffer,
                TelemetryMetric.GPU => _gpuHistoryBuffer,
                TelemetryMetric.Network => _networkDownHistoryBuffer,
                _ => _cpuHistoryBuffer
            };

            if (targetBuffer.Count == 0)
            {
                newPoints.Add(new Point(logicalWidth, 100));
                PerformanceGraphPoints = newPoints;
                PerformanceAreaPoints = areaPoints;

                newPointsAlt.Add(new Point(logicalWidth, 100));
                PerformanceGraphPointsAlt = newPointsAlt;
                PerformanceAreaPointsAlt = areaPointsAlt;
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

            if (ActiveGraphMetric == TelemetryMetric.Network)
            {
                var visibleAltHistory = _networkUpHistoryBuffer.Skip(_networkUpHistoryBuffer.Count - pointsToShow).ToList();
                double currentAltX = logicalWidth - ((pointsToShow - 1) * pixelsPerSecond);

                foreach (var yVal in visibleAltHistory)
                {
                    newPointsAlt.Add(new Point(currentAltX, yVal));
                    currentAltX += pixelsPerSecond;
                }

                PerformanceGraphPointsAlt = newPointsAlt;

                if (newPointsAlt.Count > 0)
                {
                    areaPointsAlt.Add(new Point(newPointsAlt.First().X, 100));
                    foreach (var p in newPointsAlt) areaPointsAlt.Add(p);
                    areaPointsAlt.Add(new Point(newPointsAlt.Last().X, 100));
                }
                PerformanceAreaPointsAlt = areaPointsAlt;
            }
            else
            {
                newPointsAlt.Add(new Point(logicalWidth, 100));
                PerformanceGraphPointsAlt = newPointsAlt;
                PerformanceAreaPointsAlt = areaPointsAlt;
            }
        }
        #endregion

        #region Security Core Engine & Control Handlers (Security)
        public void InitializeSecurityScan()
        {
            // Only run this the very first time the pane is opened!
            if (_hasSecurityInitialized) return;
            _hasSecurityInitialized = true;

            _securityRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _securityRefreshTimer.Tick += async (s, e) =>
            {
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    await CheckSecurityStatusAsync(_cancellationTokenSource.Token);
                }
            };
            _securityRefreshTimer.Start();

            _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }

        private async Task CheckSecurityStatusAsync(CancellationToken cancellationToken = default)
        {
            if (_isSecurityCheckInProgress || cancellationToken.IsCancellationRequested) return;

            _isSecurityCheckInProgress = true;

            try
            {
                _dispatcherQueue?.TryEnqueue(() =>
                {
                    SecurityStatusText = ResourceString.GetString("text_scanning_system") ?? "Scanning...";
                    SecurityStatusImageVisibility = Visibility.Collapsed;
                    IsSecurityStatusLoadingRingActive = true;
                    SecurityStatusLoadingRingVisibility = Visibility.Visible;
                });

                var results = await Task.Run(async () =>
                {
                    var antivirusInfo = await SecurityDiagnostics.GetAntivirusInfoAsync(cancellationToken).ConfigureAwait(false);
                    var firewallProtection = await SecurityDiagnostics.IsFirewallEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var windowsUpdate = await SecurityDiagnostics.IsWindowsUpdateEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var smartscreen = await SecurityDiagnostics.IsSmartScreenEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var realTimeProtection = await SecurityDiagnostics.IsRealTimeProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var uac = await SecurityDiagnostics.IsUACEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var tamperProtection = await SecurityDiagnostics.IsTamperProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var controlledFolderAccess = await SecurityDiagnostics.IsControlledFolderAccessEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var bitLockerEnabled = await SecurityDiagnostics.IsBitLockerEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var coreIsolationEnabled = await SecurityDiagnostics.IsCoreIsolationEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var defenderServiceEnabled = await SecurityDiagnostics.IsDefenderServiceEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var accountProtectionEnabled = await SecurityDiagnostics.IsAccountProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var smartAppControlState = await SecurityDiagnostics.GetSmartAppControlStateAsync(cancellationToken).ConfigureAwait(false);
                    var psPolicy = await SecurityDiagnostics.GetPowerShellExecutionPolicyAsync(cancellationToken).ConfigureAwait(false);
                    var lsaProtection = await SecurityDiagnostics.IsLsaProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var rdpEnabled = await SecurityDiagnostics.IsRdpEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var raEnabled = await SecurityDiagnostics.IsRemoteAssistanceEnabledAsync(cancellationToken).ConfigureAwait(false);
                    var devModeEnabled = await SecurityDiagnostics.IsDeveloperModeEnabledAsync(cancellationToken).ConfigureAwait(false);

                    return (antivirusInfo, firewallProtection, windowsUpdate, smartscreen, realTimeProtection,
                            uac, tamperProtection, controlledFolderAccess, bitLockerEnabled, coreIsolationEnabled,
                            defenderServiceEnabled, accountProtectionEnabled, smartAppControlState, psPolicy, lsaProtection, rdpEnabled, raEnabled, devModeEnabled);
                }, cancellationToken).ConfigureAwait(true);

                if (cancellationToken.IsCancellationRequested) return;

                _dispatcherQueue?.TryEnqueue(() =>
                {
                    UpdateSecurityCardState(ref _virusThreatProtectionStatus, ref _virusThreatProtectionLinkVisibility, results.antivirusInfo.IsEnabled, nameof(VirusThreatProtectionStatus), nameof(VirusThreatProtectionLinkVisibility));
                    UpdateSecurityCardState(ref _firewallStatus, ref _firewallLinkVisibility, results.firewallProtection, nameof(FirewallStatus), nameof(FirewallLinkVisibility));
                    UpdateSecurityCardState(ref _windowsUpdateStatus, ref _windowsUpdateLinkVisibility, results.windowsUpdate, nameof(WindowsUpdateStatus), nameof(WindowsUpdateLinkVisibility));
                    UpdateSecurityCardState(ref _smartScreenStatus, ref _smartScreenLinkVisibility, results.smartscreen, nameof(SmartScreenStatus), nameof(SmartScreenLinkVisibility));
                    UpdateSecurityCardState(ref _coreIsolationStatus, ref _coreIsolationLinkVisibility, results.coreIsolationEnabled, nameof(CoreIsolationStatus), nameof(CoreIsolationLinkVisibility));
                    UpdateSecurityCardState(ref _realTimeProtectionStatus, ref _realTimeProtectionLinkVisibility, results.realTimeProtection, nameof(RealTimeProtectionStatus), nameof(RealTimeProtectionLinkVisibility));
                    UpdateSecurityCardState(ref _accountProtectionStatus, ref _accountProtectionLinkVisibility, results.accountProtectionEnabled, nameof(AccountProtectionStatus), nameof(AccountProtectionLinkVisibility));
                    UpdateSecurityCardState(ref _lsaProtectionStatus, ref _lsaProtectionLinkVisibility, results.lsaProtection, nameof(LsaProtectionStatus), nameof(LsaProtectionLinkVisibility));
                    UpdateSecurityCardState(ref _tamperProtectionStatus, ref _tamperProtectionLinkVisibility, results.tamperProtection, nameof(TamperProtectionStatus), nameof(TamperProtectionLinkVisibility));
                    UpdateSecurityCardState(ref _controlledFolderAccessStatus, ref _controlledFolderAccessLinkVisibility, results.controlledFolderAccess, nameof(ControlledFolderAccessStatus), nameof(ControlledFolderAccessLinkVisibility));
                    UpdateSecurityCardState(ref _bitLockerStatus, ref _bitLockerLinkVisibility, results.bitLockerEnabled, nameof(BitLockerStatus), nameof(BitLockerLinkVisibility));
                    UpdateSecurityCardState(ref _defenderServiceStatus, ref _defenderServiceLinkVisibility, results.defenderServiceEnabled, nameof(DefenderServiceStatus), nameof(DefenderServiceLinkVisibility));

                    RemoteDesktopStatus = results.rdpEnabled ? ResourceString.GetString("Enabled") ?? "Enabled" : ResourceString.GetString("Disabled") ?? "Disabled";
                    RemoteDesktopLinkVisibility = Visibility.Collapsed;
                    _isRdpToggleUpdating = true; IsRdpEnabled = results.rdpEnabled; IsRdpToggleEnabled = true; _isRdpToggleUpdating = false;

                    RemoteAssistanceStatus = results.raEnabled ? ResourceString.GetString("Enabled") ?? "Enabled" : ResourceString.GetString("Disabled") ?? "Disabled";
                    RemoteAssistanceLinkVisibility = Visibility.Collapsed;
                    _isRaToggleUpdating = true; IsRaEnabled = results.raEnabled; IsRaToggleEnabled = true; _isRaToggleUpdating = false;

                    DeveloperModeStatus = results.devModeEnabled ? ResourceString.GetString("Enabled") ?? "Enabled" : ResourceString.GetString("Disabled") ?? "Disabled";
                    DeveloperModeLinkVisibility = Visibility.Collapsed;
                    _isDevModeToggleUpdating = true; IsDevModeEnabled = results.devModeEnabled; IsDevModeToggleEnabled = true; _isDevModeToggleUpdating = false;

                    _isUacSliderUpdating = true;
                    IsUacSliderEnabled = true;
                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                        int consentBehavior = (int)(key?.GetValue("ConsentPromptBehaviorAdmin") ?? 5);
                        int secureDesktop = (int)(key?.GetValue("PromptOnSecureDesktop") ?? 1);

                        if (consentBehavior == 2 && secureDesktop == 1) { UacSliderValue = 3; UacLevelDescription = ResourceString.GetString("UAC_Level3") ?? "Always notify me"; }
                        else if (consentBehavior == 5 && secureDesktop == 1) { UacSliderValue = 2; UacLevelDescription = ResourceString.GetString("UAC_Level2") ?? "Notify me only when apps try to make changes (default)"; }
                        else if (consentBehavior == 5 && secureDesktop == 0) { UacSliderValue = 1; UacLevelDescription = ResourceString.GetString("UAC_Level1") ?? "Notify me only when apps try to make changes (do not dim desktop)"; }
                        else { UacSliderValue = 0; UacLevelDescription = ResourceString.GetString("UAC_Level0") ?? "Never notify me (Not recommended)"; }
                    }
                    catch { IsUacSliderEnabled = true; UacLevelDescription = "Access denied reading UAC status."; }
                    _isUacSliderUpdating = false;

                    _isSmartAppControlUpdating = true;
                    bool isSmartAppControlSecure = results.smartAppControlState != 0;
                    if (results.smartAppControlState == -1)
                    {
                        IsSmartAppControlComboBoxEnabled = true; SmartAppControlDescription = "Access denied reading Smart App Control status.";
                    }
                    else
                    {
                        IsSmartAppControlComboBoxEnabled = true;
                        if (results.smartAppControlState == 0) { SmartAppControlSelectedIndex = 0; SmartAppControlDescription = ResourceString.GetString("SmartAppControl_Level0") ?? "Smart App Control is off."; }
                        else if (results.smartAppControlState == 1) { SmartAppControlSelectedIndex = 2; SmartAppControlDescription = ResourceString.GetString("SmartAppControl_Level1") ?? "Smart App Control is on and enforcing protection."; }
                        else { SmartAppControlSelectedIndex = 1; SmartAppControlDescription = ResourceString.GetString("SmartAppControl_Level2") ?? "Evaluating if Smart App Control can protect you without getting in the way."; }
                    }
                    _isSmartAppControlUpdating = false;

                    _isPowerShellPolicyUpdating = true;
                    bool isPsWarning = false;
                    if (results.psPolicy == "Error")
                    {
                        IsPowerShellPolicyComboBoxEnabled = true; PowerShellPolicyDescription = "Access denied reading PowerShell Execution Policy.";
                    }
                    else
                    {
                        IsPowerShellPolicyComboBoxEnabled = true;
                        switch (results.psPolicy)
                        {
                            case "Restricted": PowerShellPolicySelectedIndex = 0; PowerShellPolicyDescription = ResourceString.GetString("text_ps_policy_restricted") ?? "Only individual commands are allowed."; break;
                            case "AllSigned": PowerShellPolicySelectedIndex = 1; PowerShellPolicyDescription = ResourceString.GetString("text_ps_policy_allsigned") ?? "Only scripts signed by a trusted publisher can run."; break;
                            case "RemoteSigned": PowerShellPolicySelectedIndex = 2; PowerShellPolicyDescription = ResourceString.GetString("text_ps_policy_remotesigned") ?? "Local scripts allowed; downloaded scripts must be signed."; break;
                            case "Unrestricted": PowerShellPolicySelectedIndex = 3; PowerShellPolicyDescription = $"⚠️ {ResourceString.GetString("text_ps_policy_unrestricted")}"; isPsWarning = true; break;
                            case "Bypass": PowerShellPolicySelectedIndex = 4; PowerShellPolicyDescription = $"⚠️ {ResourceString.GetString("text_ps_policy_bypass")}"; isPsWarning = true; break;
                            default: PowerShellPolicySelectedIndex = 0; PowerShellPolicyDescription = "Unknown policy. Defaulting to Restricted UI state."; break;
                        }

                        if (isPsWarning) { PowerShellPolicyDescriptionForeground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red); PowerShellPolicyDescriptionOpacity = 1.0; }
                        else { PowerShellPolicyDescriptionForeground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray); PowerShellPolicyDescriptionOpacity = 0.8; }
                    }
                    _isPowerShellPolicyUpdating = false;

                    AntivirusProductName = results.antivirusInfo.ProductName ?? ResourceString.GetString("None") ?? "None";
                    if (results.antivirusInfo.SignatureUpdated.HasValue)
                    {
                        SignatureUpdateText = $"{ResourceString.GetString("SecurityPage_LastUpdated")}: {results.antivirusInfo.SignatureUpdated.Value:g}";
                        SignatureUpdateTextVisibility = Visibility.Visible;
                    }
                    else { SignatureUpdateTextVisibility = Visibility.Collapsed; }

                    _currentSecurityIssues.Clear();
                    if (!results.antivirusInfo.IsEnabled) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_VirusThreatProtection") ?? "Virus & Threat Protection is disabled");
                    if (!results.firewallProtection) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_FirewallNetworkProtection") ?? "Firewall is disabled");
                    if (!results.realTimeProtection) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_RealTimeProtection") ?? "Real-Time Protection is disabled");
                    if (!results.uac) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_UAC") ?? "UAC is set to a low security level");
                    if (!results.windowsUpdate) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_WindowsUpdate") ?? "Windows Update is disabled");
                    if (!results.tamperProtection) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_TamperProtection") ?? "Tamper Protection is disabled");
                    if (!isSmartAppControlSecure) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_SmartAppControl") ?? "Smart App Control is not enforcing protection");
                    if (!results.lsaProtection) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_LSAProtection") ?? "Local Security Authority (LSA) protection is off");
                    if (results.rdpEnabled) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_RemoteDesktop") ?? "Remote Desktop is enabled");
                    if (results.raEnabled) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_RemoteAssistance") ?? "Remote Assistance is enabled");
                    if (results.devModeEnabled) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_DeveloperMode") ?? "Developer Mode is enabled");

                    bool isPsPolicySecure = results.psPolicy != "Unrestricted" && results.psPolicy != "Bypass" && results.psPolicy != "Error";
                    if (!isPsPolicySecure) _currentSecurityIssues.Add(ResourceString.GetString("SecurityPage_PSExecutionPolicy") ?? "PowerShell execution policy is insecure");

                    int issuesCount = _currentSecurityIssues.Count;
                    BtnViewIssuesVisibility = issuesCount > 0 ? Visibility.Visible : Visibility.Collapsed;

                    bool isCoreProtected = results.antivirusInfo.IsEnabled && results.firewallProtection && results.realTimeProtection;

                    if (!isCoreProtected)
                    {
                        SecurityStatusImageUri = "ms-appx:///Assets/PngImages/UnSecure.png";
                        SecurityStatusText = $"{issuesCount} {ResourceString.GetString("text_security_critical") ?? "Critical Issues"}";
                    }
                    else if (issuesCount > 0)
                    {
                        SecurityStatusImageUri = "ms-appx:///Assets/PngImages/Secure.png";
                        SecurityStatusText = $"{issuesCount} {ResourceString.GetString("text_security_warning") ?? "Warnings Found"}";
                    }
                    else
                    {
                        SecurityStatusImageUri = "ms-appx:///Assets/PngImages/Secure.png";
                        SecurityStatusText = ResourceString.GetString("text_security_good") ?? "System is Secure";
                    }

                    SecurityStatusImageVisibility = Visibility.Visible;
                    IsSecurityStatusLoadingRingActive = false;
                    SecurityStatusLoadingRingVisibility = Visibility.Collapsed;
                    SecurityLastRefreshedText = $"{ResourceString.GetString("SecurityPage_LastRefreshed")}: {DateTime.Now:T}";
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                _dispatcherQueue?.TryEnqueue(() =>
                {
                    SecurityStatusText = "Scan timed out or failed.";
                    IsSecurityStatusLoadingRingActive = false;
                    SecurityStatusLoadingRingVisibility = Visibility.Collapsed;

                    SecurityStatusImageUri = "ms-appx:///Assets/PngImages/Warning.png";
                    SecurityStatusImageVisibility = Visibility.Visible;

                    IsRdpToggleEnabled = true;
                    IsRaToggleEnabled = true;
                    IsDevModeToggleEnabled = true;
                    IsUacSliderEnabled = true;
                    IsSmartAppControlComboBoxEnabled = true;
                    IsPowerShellPolicyComboBoxEnabled = true;
                });
            }
            finally
            {
                _isSecurityCheckInProgress = false;
            }
        }

        private void UpdateSecurityCardState(ref string statusField, ref Visibility visField, bool isEnabled, string statusPropName, string visPropName)
        {
            statusField = isEnabled ? ResourceString.GetString("Enabled") : ResourceString.GetString("Disabled");
            visField = isEnabled ? Visibility.Collapsed : Visibility.Visible;
            OnPropertyChanged(statusPropName);
            OnPropertyChanged(visPropName);
        }

        private void UpdateUacLevel(int newValue)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                if (key != null)
                {
                    if (newValue == 3) { key.SetValue("ConsentPromptBehaviorAdmin", 2, Microsoft.Win32.RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 1, Microsoft.Win32.RegistryValueKind.DWord); UacLevelDescription = ResourceString.GetString("UAC_Level3") ?? "Always notify me"; }
                    else if (newValue == 2) { key.SetValue("ConsentPromptBehaviorAdmin", 5, Microsoft.Win32.RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 1, Microsoft.Win32.RegistryValueKind.DWord); UacLevelDescription = ResourceString.GetString("UAC_Level2") ?? "Notify me only when apps try to make changes (default)"; }
                    else if (newValue == 1) { key.SetValue("ConsentPromptBehaviorAdmin", 5, Microsoft.Win32.RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 0, Microsoft.Win32.RegistryValueKind.DWord); UacLevelDescription = ResourceString.GetString("UAC_Level1") ?? "Notify me only when apps try to make changes (do not dim desktop)"; }
                    else if (newValue == 0) { key.SetValue("ConsentPromptBehaviorAdmin", 0, Microsoft.Win32.RegistryValueKind.DWord); key.SetValue("PromptOnSecureDesktop", 0, Microsoft.Win32.RegistryValueKind.DWord); UacLevelDescription = ResourceString.GetString("UAC_Level0") ?? "Never notify me (Not recommended)"; }
                }
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default); }
        }

        private void UpdateSmartAppControl(int newValue)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CI\Policy", true);
                if (key != null)
                {
                    int regValue = newValue == 0 ? 0 : (newValue == 2 ? 1 : 2);
                    if (newValue == 0) SmartAppControlDescription = ResourceString.GetString("SmartAppControl_Level0") ?? "Smart App Control is off.";
                    else if (newValue == 1) SmartAppControlDescription = ResourceString.GetString("SmartAppControl_Level2") ?? "Evaluating if Smart App Control can protect you without getting in the way.";
                    else if (newValue == 2) SmartAppControlDescription = ResourceString.GetString("SmartAppControl_Level1") ?? "Smart App Control is on and enforcing protection.";
                    key.SetValue("VerifiedAndReputablePolicyState", regValue, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default); }
        }

        private async Task UpdatePowerShellPolicyAsync(int newValue)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", true);
                if (key != null)
                {
                    string policy = "Restricted"; string desc = ""; bool isWarning = false;
                    switch (newValue)
                    {
                        case 0: policy = "Restricted"; desc = ResourceString.GetString("text_ps_policy_restricted") ?? "Only individual commands are allowed."; break;
                        case 1: policy = "AllSigned"; desc = ResourceString.GetString("text_ps_policy_allsigned") ?? "Only scripts signed by a trusted publisher can run."; break;
                        case 2: policy = "RemoteSigned"; desc = ResourceString.GetString("text_ps_policy_remotesigned") ?? "Local scripts allowed; downloaded scripts must be signed."; break;
                        case 3: policy = "Unrestricted"; desc = $"⚠️ {ResourceString.GetString("text_ps_policy_unrestricted")}"; isWarning = true; break;
                        case 4: policy = "Bypass"; desc = $"⚠️ {ResourceString.GetString("text_ps_policy_bypass")}"; isWarning = true; break;
                    }
                    key.SetValue("ExecutionPolicy", policy, Microsoft.Win32.RegistryValueKind.String);
                    PowerShellPolicyDescription = desc;

                    if (isWarning) { PowerShellPolicyDescriptionForeground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red); PowerShellPolicyDescriptionOpacity = 1.0; }
                    else
                    {
                        PowerShellPolicyDescriptionForeground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray); PowerShellPolicyDescriptionOpacity = 0.8;
                        SendSystemNotification(1, ResourceString.GetString("SecurityPage_PSExecutionPolicy") ?? "PowerShell Policy", ResourceString.GetString("text_saved_successfully") ?? "Settings saved securely.");
                    }
                    _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
                }
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default); }
        }

        private async Task ToggleRdpAsync(bool enable)
        {
            RemoteDesktopStatus = enable ? ResourceString.GetString("Enabled") ?? "Enabled" : ResourceString.GetString("Disabled") ?? "Disabled";
            try
            {
                int fDenyVal = enable ? 0 : 1;
                string command = $@"
                $ts = Get-WmiObject -Class Win32_TerminalServiceSetting -Namespace root\cimv2\TerminalServices -ComputerName '.' -Authentication 6;
                if ($ts) {{ $ts.SetAllowTSConnections({(enable ? 1 : 0)}, 1); }}
                $tsPath = 'HKLM:\System\CurrentControlSet\Control\Terminal Server';
                Set-ItemProperty -Path $tsPath -Name 'fDenyTSConnections' -Value {fDenyVal};
                Set-ItemProperty -Path ""$tsPath\WinStations\RDP-Tcp"" -Name 'UserAuthentication' -Value {(enable ? 1 : 0)};
                if ({enable.ToString().ToLower()}) {{ Enable-NetFirewallRule -DisplayGroup '@{{Microsoft.Windows.RemoteDesktop.RemoteDesktop.Resources.dll,-28752}}'; }} 
                else {{ Disable-NetFirewallRule -DisplayGroup '@{{Microsoft.Windows.RemoteDesktop.RemoteDesktop.Resources.dll,-28752}}'; }}";
                await CommandExecutor.InvokeRunCommand(command, isPowerShell: true);
                SendSystemNotification(1, ResourceString.GetString("SecurityPage_RemoteDesktop") ?? "Remote Desktop", ResourceString.GetString("text_saved_successfully") ?? "Settings synchronized.");
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                _isRdpToggleUpdating = true; IsRdpEnabled = !enable; _isRdpToggleUpdating = false;
            }
            _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }

        private async Task ToggleRaAsync(bool enable)
        {
            RemoteAssistanceStatus = enable ? ResourceString.GetString("Enabled") ?? "Enabled" : ResourceString.GetString("Disabled") ?? "Disabled";
            try
            {
                int val = enable ? 1 : 0;
                string command = $@"
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance' -Name 'fAllowToGetHelp' -Value {val};
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -Name 'fAllowToGetHelp' -Value {val};
                if ({enable.ToString().ToLower()}) {{ Enable-NetFirewallRule -DisplayGroup '@{{FirewallAPI.dll,-28502}}' -ErrorAction SilentlyContinue; }} 
                else {{ Disable-NetFirewallRule -DisplayGroup '@{{FirewallAPI.dll,-28502}}' -ErrorAction SilentlyContinue; }}";
                await CommandExecutor.InvokeRunCommand(command, isPowerShell: true);
                SendSystemNotification(1, ResourceString.GetString("SecurityPage_RemoteAssistance") ?? "Remote Assistance", ResourceString.GetString("text_saved_successfully") ?? "Settings synchronized.");
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                _isRaToggleUpdating = true; IsRaEnabled = !enable; _isRaToggleUpdating = false;
            }
            _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }

        private async Task ToggleDevModeAsync(bool enable)
        {
            DeveloperModeStatus = enable ? ResourceString.GetString("Enabled") ?? "Enabled" : ResourceString.GetString("Disabled") ?? "Disabled";
            try
            {
                await Task.Run(() =>
                {
                    int val = enable ? 1 : 0;
                    using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
                    if (key != null)
                    {
                        key.SetValue("AllowAllTrustedApps", val, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("AllowDevelopmentWithoutDevLicense", val, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                });
                SendSystemNotification(1, ResourceString.GetString("SecurityPage_DeveloperMode") ?? "Developer Mode", ResourceString.GetString("text_saved_successfully") ?? "Settings synchronized.");
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                _isDevModeToggleUpdating = true; IsDevModeEnabled = !enable; _isDevModeToggleUpdating = false;
            }
            _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
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
                    _pagefileCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
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
                // 1. Data Collection (Keep this on the background thread)
                float cpuUsage = _cpuCounter?.NextValue() ?? 0;
                float gpuUsage = GetGpuUsage();
                float availableMb = _ramCounter?.NextValue() ?? 0;
                double usedMb = _totalMemoryMb - availableMb;

                double ramUsage = _totalMemoryMb > 0 ? (usedMb / _totalMemoryMb) * 100 : 0;
                if (ramUsage < 0) ramUsage = 0;

                float diskUsage = _diskCounter?.NextValue() ?? 0;
                if (diskUsage > 100) diskUsage = 100;

                float pagefileUsage = _pagefileCounter?.NextValue() ?? 0;
                var netUsage = GetNetworkUsage();
                float downMbps = netUsage.downMbps;
                float upMbps = netUsage.upMbps;

                // Peak Network Logic
                float currentMax = Math.Max(downMbps, upMbps);
                if (currentMax > _peakNetworkSpeedMbps)
                {
                    _peakNetworkSpeedMbps = currentMax * 1.1f;
                }
                else if (_peakNetworkSpeedMbps > 10f && currentMax < (_peakNetworkSpeedMbps * 0.1f))
                {
                    _peakNetworkSpeedMbps = Math.Max(10f, _peakNetworkSpeedMbps * 0.99f);
                }

                float downPct = (_peakNetworkSpeedMbps > 0) ? Math.Clamp((downMbps / _peakNetworkSpeedMbps) * 100f, 0f, 100f) : 0;
                float upPct = (_peakNetworkSpeedMbps > 0) ? Math.Clamp((upMbps / _peakNetworkSpeedMbps) * 100f, 0f, 100f) : 0;

                // Notifications
                if (!IsOptimizationRunning)
                {
                    if (ramUsage > 85 && (DateTime.Now - _lastRamNotification).TotalMinutes > 15)
                    {
                        _lastRamNotification = DateTime.Now;
                        SendSystemNotification(2,
                            ResourceString.GetString("diag_ram_exhaustion_title") ?? "Memory Warning",
                            string.Format(ResourceString.GetString("diag_ram_exhaustion_msg") ?? "Usage at {0}%.", Math.Round(ramUsage)));
                    }

                    if (pagefileUsage > 80 && (DateTime.Now - _lastPagefileNotification).TotalMinutes > 15)
                    {
                        _lastPagefileNotification = DateTime.Now;
                        SendSystemNotification(2,
                            ResourceString.GetString("diag_pf_saturation_title") ?? "Pagefile Warning",
                            string.Format(ResourceString.GetString("diag_pf_saturation_msg") ?? "Usage at {0}%.", Math.Round(pagefileUsage)));
                    }
                }

                // 2. Buffer Management
                _cpuHistoryBuffer.Add(100 - cpuUsage);
                _ramHistoryBuffer.Add(100 - ramUsage);
                _diskHistoryBuffer.Add(100 - diskUsage);
                _pageHistoryBuffer.Add(100 - pagefileUsage);
                _gpuHistoryBuffer.Add(100 - gpuUsage);
                _networkDownHistoryBuffer.Add(100 - downPct);
                _networkUpHistoryBuffer.Add(100 - upPct);

                if (_cpuHistoryBuffer.Count > MaxHistoryCapacity)
                {
                    _cpuHistoryBuffer.RemoveAt(0);
                    _ramHistoryBuffer.RemoveAt(0);
                    _diskHistoryBuffer.RemoveAt(0);
                    _pageHistoryBuffer.RemoveAt(0);
                    _gpuHistoryBuffer.RemoveAt(0);
                    _networkDownHistoryBuffer.RemoveAt(0);
                    _networkUpHistoryBuffer.RemoveAt(0);
                }

                // 3. Snapshot data for the UI (prevents "Collection Modified" crashes)
                var cpuSnapshot = _cpuHistoryBuffer.ToList();
                var ramSnapshot = _ramHistoryBuffer.ToList();
                var gpuSnapshot = _gpuHistoryBuffer.ToList();
                var diskSnapshot = _diskHistoryBuffer.ToList();

                // 4. UI Update (Everything touching properties goes here)
                var dispatcher = _dispatcherQueue ?? MainWindow.Instance?.DispatcherQueue;
                dispatcher?.TryEnqueue(() =>
                {
                    CurrentCpuLoadStr = $"{(int)cpuUsage}%";
                    CurrentRamLoadStr = $"{(int)ramUsage}%";
                    CurrentIoLoadStr = $"{(int)diskUsage}%";
                    CurrentPagefileLoadStr = $"{(int)pagefileUsage}%";
                    CurrentGpuLoadStr = $"{(int)gpuUsage}%";

                    CurrentNetworkDownLoadStr = $"{downMbps:0.#} ▼";
                    CurrentNetworkUpLoadStr = $"{upMbps:0.#} ▲";
                    CurrentNetworkLoadStr = $"{downMbps:0.#} ▼ / {upMbps:0.#} ▲ Mbps";
                    CurrentNetworkLoadSecondaryStr = $"{downMbps:0.#} ▼ / {upMbps:0.#} ▲";

                    OnPropertyChanged(nameof(ActivePrimaryValueStr));
                    OnPropertyChanged(nameof(HeroStandardVisibility));

                    // Tray Visibility Sync
                    OnPropertyChanged(nameof(ShowHardwarePanelInTray));
                    OnPropertyChanged(nameof(ShowCpuInTray));
                    OnPropertyChanged(nameof(ShowRamInTray));
                    OnPropertyChanged(nameof(ShowGpuInTray));
                    OnPropertyChanged(nameof(ShowDiskInTray));

                    // Sparklines (Using snapshots)
                    int sparklinePoints = 20;
                    double stepX = 3.0;
                    double chartHeight = 15.0;

                    CpuTrayPoints = GenerateTrayPoints(cpuSnapshot, sparklinePoints, stepX, chartHeight);
                    RamTrayPoints = GenerateTrayPoints(ramSnapshot, sparklinePoints, stepX, chartHeight);
                    GpuTrayPoints = GenerateTrayPoints(gpuSnapshot, sparklinePoints, stepX, chartHeight);
                    DiskTrayPoints = GenerateTrayPoints(diskSnapshot, sparklinePoints, stepX, chartHeight);

                    OnPropertyChanged(nameof(CpuTrayPoints));
                    OnPropertyChanged(nameof(RamTrayPoints));
                    OnPropertyChanged(nameof(GpuTrayPoints));
                    OnPropertyChanged(nameof(DiskTrayPoints));

                    RebuildGraphFromHistory();
                });
            }
            catch { }
        }

        private Microsoft.UI.Xaml.Media.PointCollection GenerateTrayPoints(
            System.Collections.Generic.IEnumerable<double> buffer,
            int pointsToTake,
            double stepX,
            double height)
        {
            var newPoints = new Microsoft.UI.Xaml.Media.PointCollection();
            var data = buffer.Reverse().Take(pointsToTake).Reverse().ToList();

            if (data.Count == 0) return newPoints;

            double startX = 60 - ((data.Count - 1) * stepX);

            for (int i = 0; i < data.Count; i++)
            {
                double y = (data[i] / 100.0) * height;
                newPoints.Add(new Windows.Foundation.Point(startX + (i * stepX), y));
            }

            return newPoints;
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

                foreach (var counter in _gpuCounters.Values)
                {
                    counter.Dispose();
                }
                _gpuCounters.Clear();

                foreach (var counter in _networkUpCounters.Values) counter.Dispose();
                foreach (var counter in _networkDownCounters.Values) counter.Dispose();
                _networkUpCounters.Clear();
                _networkDownCounters.Clear();

                _cpuHistoryBuffer.Clear();
                _ramHistoryBuffer.Clear();
                _diskHistoryBuffer.Clear();
                _pageHistoryBuffer.Clear();
                _gpuHistoryBuffer.Clear();

                _networkUpHistoryBuffer.Clear();
                _networkDownHistoryBuffer.Clear();

                try
                {
                    PerformanceGraphPoints.Clear();
                    PerformanceGraphPoints.Add(new Point(400, 100));
                    PerformanceAreaPoints.Clear();

                    PerformanceGraphPointsAlt.Clear();
                    PerformanceAreaPointsAlt.Clear();

                    CurrentCpuLoadStr = "0%";
                    CurrentRamLoadStr = "0%";
                    CurrentIoLoadStr = "0%";
                    CurrentPagefileLoadStr = "0%";
                    CurrentGpuLoadStr = "0%";

                    CurrentNetworkUpLoadStr = "0 Mbps";
                    CurrentNetworkDownLoadStr = "0 Mbps";
                    CurrentNetworkLoadStr = "0 / 0 Mbps";
                    CurrentNetworkLoadSecondaryStr = "0 / 0";
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
            ScanStatus = ResourceString.GetString("diag_live_active") ?? "Live Telemetry Interceptor: ACTIVE";

            _liveWatcher = new LiveEventWatcherHelper(async newEvent =>
            {
                if (newEvent.EventId == 1801)
                {
                    bool physicallyEnrolled = false;
                    try
                    {
                        physicallyEnrolled = await SecureBootHelper.IsCa2023EnrolledAsync();
                    }
                    catch { }

                    if (physicallyEnrolled) return;
                }

                _dispatcherQueue.TryEnqueue(() =>
                {
                    MinedSystemEvents.Insert(0, newEvent);
                    if (MinedSystemEvents.Count > 150) MinedSystemEvents.RemoveAt(MinedSystemEvents.Count - 1);

                    CalculateStabilityTrend(MinedSystemEvents);

                    AiSummary = string.Format(ResourceString.GetString("diag_live_intercept_msg") ?? "LIVE INTERCEPT: {0} reported a Level {1} event. Stability updated.", newEvent.SourceName, newEvent.Level);

                    if (newEvent.Level <= 2 && (DateTime.Now - _lastEventNotification).TotalMinutes > 5)
                    {
                        _lastEventNotification = DateTime.Now;
                        SendSystemNotification(3,
                            ResourceString.GetString("diag_critical_error_title") ?? "Critical System Error Detected",
                            string.Format(ResourceString.GetString("diag_critical_error_msg") ?? "Event ID {0} logged by {1}.", newEvent.EventId, newEvent.SourceName));
                    }
                });
            });

            _liveWatcher.Start();
        }

        private void StopLiveMonitoring()
        {
            ScanStatus = ResourceString.GetString("diag_live_standby") ?? "Live Telemetry Interceptor: STANDBY";
            _liveWatcher?.Dispose();
            _liveWatcher = null;
        }

        public void DisposeWatcher()
        {
            StopLiveMonitoring();
            StopLiveTelemetry();
        }

        internal void CalculateStabilityTrend(IEnumerable<SystemEventItem> events)
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
                    double penalty;

                    if (ev.EventId >= 9101 && ev.EventId <= 9117)
                    {
                        penalty = ev.Level switch
                        {
                            1 => 10.0, // Critical Vulnerability (e.g. Antivirus Off)
                            2 => 4.0,  // Security Warning (e.g. UAC low)
                            3 => 1.0,  // Minor Hardening (e.g. Developer Mode On)
                            _ => 0.0
                        };
                    }
                    else
                    {
                        // Standard System/Hardware Crash Penalties (High impact)
                        penalty = ev.Level switch
                        {
                            1 => 20.0, // Critical System Fault
                            2 => 10.0, // Stability Warning
                            3 => 2.0,  // Minor Telemetry Event
                            _ => 0.0
                        };
                    }

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
                        >= 80 => new SolidColorBrush(ColorHelper.FromArgb(255, 0, 214, 232)), // Evolve Blue
                        >= 60 => new SolidColorBrush(Colors.Gold),                            // Warning
                        _ => new SolidColorBrush(Colors.Red)                                  // Critical
                    }
                });
            }

            double averageHealth = totalHealthSum / 24.0;
            StabilityScore = $"{Math.Round(averageHealth, 2)}%";
        }

        private void RefreshAllDrivesInfo()
        {
            try
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed && d.TotalSize > 10737418240)
                    .ToList();

                var dispatcher = _dispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

                if (dispatcher == null) return;

                dispatcher.TryEnqueue(() =>
                {
                    SystemDrives.Clear();

                    foreach (var drive in drives)
                    {
                        double totalGB = drive.TotalSize / 1073741824.0;
                        double freeGB = drive.AvailableFreeSpace / 1073741824.0;
                        double usedGB = totalGB - freeGB;
                        double usedPct = totalGB > 0 ? (usedGB / totalGB) * 100 : 0;

                        string driveName = string.IsNullOrEmpty(drive.Name) ? "Disk" : drive.Name.TrimEnd('\\');

                        SystemDrives.Add(new DriveSpaceInfo
                        {
                            Name = driveName,
                            VolumeLabel = drive.VolumeLabel,
                            TotalSizeGB = totalGB,
                            FreeSpaceGB = freeGB,
                            UsedSpaceGB = usedGB,
                            UsedPercentage = usedPct
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Drive Monitor Error] {ex.Message}");
            }
        }

        private float GetGpuUsage()
        {
            try
            {
                var category = new System.Diagnostics.PerformanceCounterCategory("GPU Engine");

                var currentInstances = category.GetInstanceNames()
                    .Where(i => i.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var toRemove = _gpuCounters.Keys.Except(currentInstances).ToList();
                foreach (var key in toRemove)
                {
                    _gpuCounters[key].Dispose();
                    _gpuCounters.Remove(key);
                }

                foreach (var instance in currentInstances)
                {
                    if (!_gpuCounters.ContainsKey(instance))
                    {
                        var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                        counter.NextValue();
                        _gpuCounters.Add(instance, counter);
                    }
                }

                float totalGpu = 0;
                foreach (var counter in _gpuCounters.Values)
                {
                    totalGpu += counter.NextValue();
                }

                return Math.Clamp(totalGpu, 0f, 100f);
            }
            catch
            {
                return 0;
            }
        }

        private (float downMbps, float upMbps) GetNetworkUsage()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .ToList();

                var activeDescriptions = interfaces.Select(i => FormatPerformanceCounterInstanceName(i.Description)).ToList();

                var toRemove = _networkUpCounters.Keys.Except(activeDescriptions).ToList();
                foreach (var key in toRemove)
                {
                    _networkUpCounters[key].Dispose();
                    _networkUpCounters.Remove(key);
                    _networkDownCounters[key].Dispose();
                    _networkDownCounters.Remove(key);
                }

                foreach (var desc in activeDescriptions)
                {
                    if (!_networkUpCounters.ContainsKey(desc))
                    {
                        try
                        {
                            var upCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", desc);
                            upCounter.NextValue();
                            _networkUpCounters.Add(desc, upCounter);

                            var downCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", desc);
                            downCounter.NextValue();
                            _networkDownCounters.Add(desc, downCounter);
                        }
                        catch { /* Ignore if counter instance is missing/locked */ }
                    }
                }

                float totalUpBytes = 0;
                float totalDownBytes = 0;

                foreach (var key in _networkUpCounters.Keys.ToList())
                {
                    try
                    {
                        totalUpBytes += _networkUpCounters[key].NextValue();
                        totalDownBytes += _networkDownCounters[key].NextValue();
                    }
                    catch { }
                }

                float downMbps = (totalDownBytes * 8) / 1_000_000f;
                float upMbps = (totalUpBytes * 8) / 1_000_000f;

                return (downMbps, upMbps);
            }
            catch
            {
                return (0f, 0f);
            }
        }

        private string FormatPerformanceCounterInstanceName(string description)
        {
            return description.Replace('(', '[').Replace(')', ']').Replace('#', '_').Replace('/', '_').Replace('\\', '_');
        }

        #endregion

        #region Background Monitoring Loops (Maintenance)

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
                        }
                    });

                    if (token.WaitHandle.WaitOne(5000)) break;
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { ErrorLogging.LogDebug(e); }
            }
        }
        #endregion

        #region Optimization Operations (Maintenance)
        private async Task CalculateCleanupSpaceAsync()
        {
            _cleanupCts?.Cancel();
            _cleanupCts = new CancellationTokenSource();
            var token = _cleanupCts.Token;

            _dispatcherQueue?.TryEnqueue(() => IsScanning = true);

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
                string sizeStr = totalBytes == 0 ? "0 MB" : string.Format("{0:0.##} {1}", unitPair.Key, unitPair.Value);

                _dispatcherQueue?.TryEnqueue(() => TotalSpaceToFree = sizeStr);
            }
            catch (TaskCanceledException) { }
            catch (Exception e) { ErrorLogging.LogDebug(e); }
            finally
            {
                _dispatcherQueue?.TryEnqueue(() => IsScanning = false);
            }
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

        private void OnOptimizeProgressUpdate(byte value, string step)
        {
            if (_dispatcherQueue == null) return;

            _dispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                OptimizationProgressStep = step;
                OptimizationProgressPercentage = value;
                OptimizationProgressValue = value;
            });
        }

        private async Task OptimizeAsync(Enums.Memory.Optimization.Reason reason)
        {
            if (IsOptimizationRunning) return;
            try
            {
                OptimizationProgressStep = ResourceString.GetString("txt_progress_step") ?? "Waiting...";
                OptimizationProgressValue = 0;
                OptimizationProgressPercentage = 0;

                await Task.Run(() => Optimize(reason));
            }
            catch (Exception e) { ErrorLogging.LogDebug(e); }
        }

        public async Task Optimize(Enums.Memory.Optimization.Reason reason)
        {
            if ((LocalMachineSettingsEngine.MemoryAreas & Enums.Memory.Areas.WindowsOld) != 0)
            {
                var tcs = new TaskCompletionSource<bool>();

                _dispatcherQueue?.TryEnqueue(async () =>
                {
                    try
                    {
                        var xamlRoot = MainWindow.Instance?.Content?.XamlRoot;
                        if (xamlRoot == null)
                        {
                            tcs.TrySetResult(false);
                            return;
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
                        tcs.TrySetResult(result == ContentDialogResult.Primary);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogging.LogDebug(ex);
                        tcs.TrySetResult(false);
                    }
                });

                if (!await tcs.Task) return;
            }

            string resultMessage = string.Empty;
            byte currentStep = 0;

            try
            {
                _dispatcherQueue?.TryEnqueue(() =>
                {
                    IsBusy = true;
                    IsOptimizationRunning = true;
                });

                App.SetPriority(LocalMachineSettingsEngine.RunOnPriority);

                OnOptimizeProgressUpdate(++currentStep, ResourceString.GetString("txt_progress_preparing") ?? "Preparing...");
                await Task.Delay(500);

                long startPhysical = 0, startVirtual = 0, startDisk = 0;
                bool isDiskCleanupSelected = (LocalMachineSettingsEngine.MemoryAreas & (Enums.Memory.Areas.DiskCleanup | Enums.Memory.Areas.WindowsOld)) != 0;

                _computerService.RefreshMemory();
                startPhysical = _computerService.Memory.Physical.Free.Bytes;
                startVirtual = _computerService.Memory.Virtual.Free.Bytes;

                if (isDiskCleanupSelected)
                {
                    string? root = Path.GetPathRoot(Environment.SystemDirectory);
                    if (root != null) startDisk = new DriveInfo(root).AvailableFreeSpace;
                }

                OnOptimizeProgressUpdate(++currentStep, ResourceString.GetString("txt_progress_optimizing") ?? "Optimizing...");

                await _computerService.Optimize(reason, LocalMachineSettingsEngine.MemoryAreas);

                _computerService.RefreshMemory();

                OnOptimizeProgressUpdate(++currentStep, ResourceString.GetString("txt_progress_finalizing") ?? "Finalizing...");
                await Task.Delay(500);

                _dispatcherQueue?.TryEnqueue(() =>
                {
                    if (Computer != null) Computer.Memory = _computerService.Memory;
                    OnPropertyChanged(nameof(Computer));
                });

                _ = CalculateCleanupSpaceAsync();

                var physicalDiff = Math.Max(0, _computerService.Memory.Physical.Free.Bytes - startPhysical);
                var virtualDiff = Math.Max(0, _computerService.Memory.Virtual.Free.Bytes - startVirtual);
                long diskDiff = 0;

                if (isDiskCleanupSelected)
                {
                    string? root = Path.GetPathRoot(Environment.SystemDirectory);
                    if (root != null) diskDiff = Math.Max(0, new DriveInfo(root).AvailableFreeSpace - startDisk);
                }

                var tcsMsg = new TaskCompletionSource<string>();
                bool enqueued = _dispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        string msg = ResourceHelper.GetOptimizationResultMessage(
                            reason.ToString(),
                            physicalDiff.ToMemoryUnit(),
                            virtualDiff.ToMemoryUnit(),
                            diskDiff.ToMemoryUnit(),
                            true,
                            isDiskCleanupSelected);
                        tcsMsg.TrySetResult(msg);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogging.LogDebug(ex);
                        tcsMsg.TrySetResult("Optimization completed.");
                    }
                }) ?? false;

                if (!enqueued) tcsMsg.TrySetResult("Optimization completed.");

                resultMessage = await tcsMsg.Task;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                resultMessage = "Error occurred.";
            }
            finally
            {
                OnOptimizeProgressUpdate(OptimizationProgressTotal, ResourceString.GetString("txt_optimization_completed") ?? "Optimization Complete");

                await Task.Delay(2000);

                RefreshAllDrivesInfo();

                _dispatcherQueue?.TryEnqueue(() =>
                {
                    IsOptimizationRunning = false;
                    IsBusy = false;
                });

                ResetProgressAfterDelay(10000);

                _lastRamNotification = DateTime.Now;
                _lastPagefileNotification = DateTime.Now;

                _dispatcherQueue?.TryEnqueue(() =>
                {
                    OnOptimizeCommandCompleted?.Invoke(reason, resultMessage);

                    if (ShowOptimizationNotifications)
                    {
                        string summaryTitle = ResourceString.GetString("toast_optimization_complete_title") ?? "Optimization Complete";
                        string finalMsg = resultMessage ?? string.Empty;

                        SendSystemNotification(4, summaryTitle, finalMsg);
                    }
                });
            }
        }

        private async void ResetProgressAfterDelay(int milliseconds)
        {
            await Task.Delay(milliseconds);
            _dispatcherQueue?.TryEnqueue(() =>
            {
                OptimizationProgressPercentage = 0;
                OptimizationProgressValue = 0;
                OptimizationProgressStep = ResourceString.GetString("txt_progress_step") ?? "Waiting...";
            });
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
                4 => NotificationManager.NoticeSeverity.Success,
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

        #region Lifecycle & Cleanup
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

                    OnPropertyChanged(nameof(IsOptimizationKeyValid));
                    OnPropertyChanged(nameof(UseHotkey));

                    App.ReleaseMemory();
                }
            }
            catch { }
            finally { _isReiniziliating = false; }
        }

        public void PauseUiUpdates()
        {
            _isUiActive = false;

            StopLiveTelemetry();
            StopLiveMonitoring();
        }

        public void ResumeUiUpdates()
        {
            _isUiActive = true;

            if (LocalMachineSettingsEngine.EnableLiveDiagnostics)
            {
                StartLiveTelemetry();
                StartLiveMonitoring();
            }

            RebuildGraphFromHistory();

            OnPropertyChanged(string.Empty);
        }

        private void SetPropertySafe<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            if (_isUiActive) OnPropertyChanged(propertyName);
        }

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

                if (_cleanupCts != null)
                {
                    _cleanupCts.Cancel();
                    _cleanupCts.Dispose();
                    _cleanupCts = null;
                }

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                App.HotkeySettingsChanged -= OnHotkeySettingsChanged;
                _computerService.OnOptimizeProgressUpdate -= OnOptimizeProgressUpdate;

            }
            catch (ObjectDisposedException) { /* Task already disposed it */ }
            catch (Exception ex) { Debug.WriteLine($"[Cleanup Error] {ex.Message}"); }

            DisposeWatcher();
        }
        #endregion
    }
}