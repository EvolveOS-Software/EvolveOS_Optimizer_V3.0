using System.Collections.ObjectModel;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using static EvolveOS_Optimizer.Core.Model.WeatherApiModels;
using System.Threading;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class HomePageViewModel : ViewModelBase, IDisposable
    {
        private readonly HomePageModel _model = new HomePageModel();
        private readonly SystemDiagnostics _monitoringService = new SystemDiagnostics();
        private readonly WeatherService _weatherService = new WeatherService();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        #region Properties

        private ObservableCollection<HomePageModel> _displayData = new();
        public ObservableCollection<HomePageModel> DisplayData
        {
            get => _displayData;
            set { _displayData = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DriveSpaceInfo> _diskDrives = new();
        public ObservableCollection<DriveSpaceInfo> DiskDrives
        {
            get => _diskDrives;
            set { _diskDrives = value; OnPropertyChanged(); }
        }

        private ImageSource _currentWeatherIcon = new BitmapImage(
            new Uri("ms-appx:///Assets/ImagePackages/Sunny.png")
        );
        public ImageSource CurrentWeatherIcon
        {
            get => _currentWeatherIcon;
            set { _currentWeatherIcon = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DailyForecast> _fiveDayForecast = new ObservableCollection<DailyForecast>();
        public ObservableCollection<DailyForecast> FiveDayForecast
        {
            get => _fiveDayForecast;
            set { _fiveDayForecast = value; OnPropertyChanged(); }
        }

        private string _weatherDescription = "Loading...";
        public string WeatherDescription
        {
            get => _weatherDescription;
            set { _weatherDescription = value; OnPropertyChanged(); }
        }

        private string _weatherTemperature = "--";
        public string WeatherTemperature
        {
            get => _weatherTemperature;
            set { _weatherTemperature = value; OnPropertyChanged(); }
        }

        private string _weatherLocation;
        public string WeatherLocation
        {
            get => _weatherLocation;
            set { _weatherLocation = value; OnPropertyChanged(); }
        }

        private string _currentTime = "--:--";
        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        private string _currentDate = "Loading...";
        public string CurrentDate
        {
            get => _currentDate;
            set { _currentDate = value; OnPropertyChanged(); }
        }

        private double _downloadSpeed;
        public double DownloadSpeed
        {
            get => _downloadSpeed;
            set { _downloadSpeed = value; OnPropertyChanged(); }
        }

        private double _uploadSpeed;
        public double UploadSpeed
        {
            get => _uploadSpeed;
            set { _uploadSpeed = value; OnPropertyChanged(); }
        }

        private ImageSource? _displayWallpaper;
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

        public Visibility IpVisibility => SystemDiagnostics.isIPAddressFormatValid ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        public HomePageModel? this[string name]
        {
            get => DisplayData.FirstOrDefault(x => x.Name == name);
        }

        public HomePageViewModel OSInfo => this;
        public HomePageViewModel SystemStats => this;

        public HomePageViewModel()
        {
            _weatherLocation = "Paris";
            _monitoringService.GetHardwareData();

            LoadDisplayData();
            LoadDiskData();

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
            // No finally block needed
        }

        public void LoadDisplayData()
        {
            _displayData.Clear();
            _displayData.Add(new HomePageModel { Name = "OSName", Data = HardwareData.OS.Name });
            _displayData.Add(new HomePageModel { Name = "OSVersion", Data = HardwareData.OS.Version });
            _displayData.Add(new HomePageModel { Name = "Processes", Data = HardwareData.RunningProcessesCount });
            _displayData.Add(new HomePageModel { Name = "Services", Data = HardwareData.RunningServicesCount });
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
                System.Diagnostics.Debug.WriteLine($"[Weather Error] {ex.Message}");
            }
        }

        private void LoadDiskData()
        {

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

        public override void Dispose()
        {
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
    }
}