using System.Collections.ObjectModel;
using System.Threading;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using static EvolveOS_Optimizer.Core.Model.WeatherApiModels;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class HomePageViewModel : ViewModelBase, IDisposable
    {
        #region Fields
        private readonly HomePageModel _model = new HomePageModel();
        private readonly SystemDiagnostics _monitoringService = new SystemDiagnostics();
        private readonly WeatherService _weatherService = new WeatherService();

        private DispatcherTimer? _statsTimer;
        public int GpuUsageDisplay => HardwareData.Gpu.Usage;
        public int GpuUsagePercentage => HardwareData.Gpu.Usage;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        private ObservableCollection<HomePageModel> _displayData = new();
        private ObservableCollection<DriveSpaceInfo> _diskDrives = new();
        private ObservableCollection<DailyForecast> _fiveDayForecast = new ObservableCollection<DailyForecast>();
        private ObservableCollection<string> _availableCities = new ObservableCollection<string>();

        private string? _currentWeatherIcon = "ms-appx:///Assets/ImagePackages/Sunny.png";
        private string _weatherDescription = "Loading...";
        private string _weatherTemperature = "--°";
        private string _weatherLocation;
        private string _currentTime = "--:--";
        private string _currentDate = "Loading...";
        private double _downloadSpeed;
        private double _uploadSpeed;
        private ImageSource? _displayWallpaper;
        #endregion

        #region Properties
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

        public ImageSource? DisplayWallpaper
        {
            get
            {
                if (_displayWallpaper == null)
                    _displayWallpaper = _monitoringService.GetWallpaperSource();
                return _displayWallpaper;
            }
            set
            {
                _displayWallpaper = value;
                OnPropertyChanged();
            }
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

            SetupTimer();

            _ = InitializeAsync(_cts.Token);
        }

        private async Task InitializeAsync(CancellationToken token)
        {
            try
            {
                var weatherTask = Task.Run(async () =>
                {
                    try { await FetchWeatherAsync(_weatherLocation, token); }
                    catch (OperationCanceledException) { }
                }, token);

                while (!token.IsCancellationRequested)
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        UpdateDateTime();
                        UpdateNetworkSpeed();
                        RefreshStats();
                    });

                    await Task.Delay(1000, token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[HomePageVM] Background loop stopped safely.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HomePageVM] Unexpected error: {ex.Message}");
            }
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

            LocalIP = new IPWrapper { Data = HardwareData.LocalIPAddress };
        }

        public void RefreshStats()
        {
            if (_displayData == null) return;

            var osName = _displayData.FirstOrDefault(x => x.Name == "OSName");
            if (osName != null) osName.Data = HardwareData.OS.Name;

            var osVer = _displayData.FirstOrDefault(x => x.Name == "OSVersion");
            if (osVer != null) osVer.Data = HardwareData.OS.Version;

            var proc = _displayData.FirstOrDefault(x => x.Name == "Processes");
            if (proc != null) proc.Data = HardwareData.RunningProcessesCount;

            var svc = _displayData.FirstOrDefault(x => x.Name == "Services");
            if (svc != null) svc.Data = HardwareData.RunningServicesCount;

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
            if (gpuItem != null)
            {
                gpuItem.Data = HardwareData.Gpu.Data;
            }

            if (LocalIP.Data != HardwareData.LocalIPAddress)
            {
                LocalIP = new IPWrapper { Data = HardwareData.LocalIPAddress };
            }

            OnPropertyChanged(nameof(IpVisibility));
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
            // Populate disk data
        }

        public void RefreshWallpaper()
        {
            var wallpaperPath = _monitoringService.GetWallpaperPath();
            if (string.IsNullOrEmpty(wallpaperPath)) return;

            _dispatcherQueue.TryEnqueue(() =>
            {
                var bitmap = new BitmapImage();
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.UriSource = new Uri(wallpaperPath);

                DisplayWallpaper = bitmap;
            });
        }
        #endregion

        #region Weather Service
        public async Task FetchWeatherAsync(string? locationOverride = null, CancellationToken token = default)
        {
            try
            {
                string loc = locationOverride ?? WeatherLocation;

                Task<WeatherData> weatherTask = _weatherService.GetWeatherAsync(loc, token);
                Task timeoutTask = Task.Delay(5000, token);
                Task completedTask = await Task.WhenAny(weatherTask, timeoutTask);

                if (completedTask == timeoutTask || token.IsCancellationRequested)
                {
                    return;
                }

                WeatherData data = await weatherTask;

                if (data == null) return;

                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;

                    WeatherDescription = data.Description;
                    WeatherTemperature = data.TempC.ToString("F0") + "°";
                    WeatherLocation = loc;

                    CurrentWeatherIcon = data.CurrentIconUrl;

                    if (data.Forecast != null)
                    {
                        FiveDayForecast.Clear();
                        foreach (var day in data.Forecast)
                        {
                            FiveDayForecast.Add(day);
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Silent exit
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Weather Error] {ex.Message}");
            }
        }

        private string LoadLocationFromRegistry()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer");
                string? saved = key?.GetValue("LastLocation") as string;
                return !string.IsNullOrEmpty(saved) ? saved : "Paris";
            }
            catch { return "Paris"; }
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
        }

        private async void StatsTimer_Tick(object? sender, object? e)
        {
            await SystemDiagnostics.GetGpuUsage();

            RefreshStats();

            OnPropertyChanged(nameof(GpuUsageDisplay));
        }

        #endregion

        #region Disposal
        public override void Dispose()
        {
            if (_statsTimer != null)
            {
                _statsTimer.Stop();
                _statsTimer.Tick -= StatsTimer_Tick;
                _statsTimer = null;
            }

            try
            {
                _cts.Cancel();
            }
            catch { }

            DisplayWallpaper = null;
            _displayWallpaper = null;

            _displayData?.Clear();
            _fiveDayForecast?.Clear();
            _diskDrives?.Clear();

            OnPropertyChanged(string.Empty);

            base.Dispose();

            Debug.WriteLine("[HomePageVM] ViewModel Disposed and Tasks Canceled.");
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