// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class HomePageViewModel : ViewModelBase, IDisposable
    {
        #region Fields
        private readonly HomePageModel _model = new HomePageModel();
        private readonly SystemDiagnostics _monitoringService = new SystemDiagnostics();
        private readonly WeatherService _weatherService = new WeatherService();

        private DispatcherTimer? _statsTimer;
        private DispatcherTimer? _weatherTimer;

        public int GpuUsageDisplay => HardwareData.Gpu.Usage;
        public int GpuUsagePercentage => HardwareData.Gpu.Usage;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

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

        public Microsoft.UI.Xaml.Visibility IpVisibility => SystemDiagnostics.isIPAddressFormatValid ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

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

            _weatherLocation = LoadLocationFromRegistry();
            _monitoringService.GetHardwareData();

            LocalIP = new IPWrapper { Data = _monitoringService.GetDefaultLocalIP() };

            LoadDisplayData();
            LoadDiskData();

            _ = FetchWeatherAsync(_weatherLocation, _cts.Token);

            SetupTimer();
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

            var osName = _displayData.FirstOrDefault(x => x.Name == "OSName");
            if (osName != null) osName.Data = HardwareData.OS.Name;

            var osVer = _displayData.FirstOrDefault(x => x.Name == "OSVersion");
            if (osVer != null) osVer.Data = HardwareData.OS.Version;

            var proc = _displayData.FirstOrDefault(x => x.Name == "Processes");
            if (proc != null) proc.Data = processCount;

            var svc = _displayData.FirstOrDefault(x => x.Name == "Services");
            if (svc != null) svc.Data = servicesCount;

            var netItem = _displayData.FirstOrDefault(x => x.Name == "Network");
            if (netItem != null) netItem.Data = HardwareData.NetworkAdapter;

            var ipItem = _displayData.FirstOrDefault(x => x.Name == "IpAddress");
            if (ipItem != null) ipItem.Data = HardwareData.UserIPAddress;

            var memItem = _displayData.FirstOrDefault(x => x.Name == "Memory");
            if (memItem != null) memItem.Data = HardwareData.Memory.Data;

            var typeItem = _displayData.FirstOrDefault(x => x.Name == "Type");
            if (typeItem != null) typeItem.Data = HardwareData.Memory.Type;

            var cpuItem = _displayData.FirstOrDefault(x => x.Name == "CPU");
            if (cpuItem != null) cpuItem.Data = HardwareData.Processor.DetailedData;

            var gpuItem = _displayData.FirstOrDefault(x => x.Name == "GPU");
            if (gpuItem != null) gpuItem.Data = HardwareData.Gpu.Data;

            var storageItem = _displayData.FirstOrDefault(x => x.Name == "Storage");
            if (storageItem != null) storageItem.Data = HardwareData.Storage;

            if (LocalIP.Data != HardwareData.LocalIPAddress)
            {
                LocalIP = new IPWrapper { Data = HardwareData.LocalIPAddress };
            }

            OnPropertyChanged(nameof(IpVisibility));
            OnPropertyChanged(nameof(GpuUsageDisplay));
            OnPropertyChanged(nameof(GpuUsagePercentage));

            OnPropertyChanged("Item[]");
        }

        private void UpdateNetworkSpeed()
        {
            DownloadSpeed = _monitoringService.GetDownloadSpeed();
            UploadSpeed = _monitoringService.GetUploadSpeed();
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
        public async Task FetchWeatherAsync(string? locationOverride = null, CancellationToken token = default, bool forceRefresh = false)
        {
            try
            {
                string loc = locationOverride ?? WeatherLocation;
                if (string.IsNullOrWhiteSpace(loc)) loc = "Paris";

                WeatherData data = await _weatherService.GetWeatherAsync(loc, token, forceRefresh);

                if (data == null || token.IsCancellationRequested) return;

                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (_isDisposed || token.IsCancellationRequested || _fiveDayForecast == null)
                        return;

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
            catch (Exception ex)
            {
                Debug.WriteLine($"[Weather UI Error] {ex.Message}");
            }
        }

        private static string LoadLocationFromRegistry()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer");
                if (key != null)
                {
                    var saved = key.GetValue("LastLocation") as string;
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        return saved;
                    }
                }
            }
            catch
            {
                // Log error if necessary, otherwise fall back
            }

            return "Paris";
        }

        public void UpdateWeatherData(WeatherData data)
        {
            _dispatcherQueue.TryEnqueue(() =>
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

        #region Background Statistics Timer

        private void SetupTimer()
        {
            _statsTimer?.Stop();

            _statsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _statsTimer.Tick += StatsTimer_Tick;
            _statsTimer.Start();

            _weatherTimer?.Stop();
            _weatherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(15)
            };
            _weatherTimer.Tick += (s, e) =>
            {
                _ = FetchWeatherAsync(_weatherLocation, _cts.Token);
            };
            _weatherTimer.Start();
        }

        private async void StatsTimer_Tick(object? sender, object? e)
        {
            try
            {
                await SystemDiagnostics.GetGpuUsage();

                RefreshStats(HardwareData.RunningProcessesCount, HardwareData.RunningServicesCount);

                UpdateDateTime();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatsTimer Error] {ex.Message}");
            }
        }

        #endregion

        #region Disposal
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isDisposed = true;

                if (_statsTimer != null)
                {
                    _statsTimer.Stop();
                    _statsTimer = null;
                }

                if (_weatherTimer != null)
                {
                    _weatherTimer.Stop();
                    _weatherTimer = null;
                }

                try
                {
                    if (!_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }
                    _cts.Dispose();
                }
                catch (ObjectDisposedException) { }

                if (_displayData != null)
                {
                    _displayData.Clear();
                    _displayData = null!;
                }

                if (_fiveDayForecast != null)
                {
                    _fiveDayForecast.Clear();
                    _fiveDayForecast = null!;
                }

                if (_diskDrives != null)
                {
                    _diskDrives.Clear();
                    _diskDrives = null!;
                }

                if (_availableCities != null)
                {
                    _availableCities.Clear();
                    _availableCities = null!;
                }

                (_weatherService as IDisposable)?.Dispose();
                (_monitoringService as IDisposable)?.Dispose();

                ClearPropertyChangedListeners();

                Debug.WriteLine("[HomePageVM] Purge: All models and delegates unrooted.");
            }

            base.Dispose(disposing);
        }
        #endregion

        private void UpdateModelData(string name, string newData)
        {
            var item = _displayData.FirstOrDefault(x => x.Name == name);
            if (item != null && item.Data != newData)
            {
                item.Data = newData;
                OnPropertyChanged("Item[]");
            }
        }
    }
}