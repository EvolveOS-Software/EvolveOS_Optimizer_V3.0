using System.Collections.ObjectModel;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using static EvolveOS_Optimizer.Core.Model.WeatherApiModels;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class HomePageViewModel : ViewModelBase
    {
        private readonly HomePageModel _model = new HomePageModel();
        private readonly SystemDiagnostics _monitoringService = new SystemDiagnostics();
        private readonly WeatherService _weatherService = new WeatherService();

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

        public ImageSource? DisplayWallpaper => _monitoringService.GetWallpaperSource();

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

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _ = FetchWeatherAsync(_weatherLocation);

            while (true)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    UpdateDateTime();
                    UpdateNetworkSpeed();
                    RefreshStats();
                });

                await Task.Delay(1000);
            }
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
            _dispatcherQueue.TryEnqueue(() =>
            {
                var osName = _displayData.FirstOrDefault(x => x.Name == "OSName");
                if (osName != null) osName.Data = HardwareData.OS.Name;

                var osVer = _displayData.FirstOrDefault(x => x.Name == "OSVersion");
                if (osVer != null) osVer.Data = HardwareData.OS.Version;

                var proc = _displayData.FirstOrDefault(x => x.Name == "Processes");
                if (proc != null) proc.Data = HardwareData.RunningProcessesCount;

                var svc = _displayData.FirstOrDefault(x => x.Name == "Services");
                if (svc != null) svc.Data = HardwareData.RunningServicesCount;
            });
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

        public async Task FetchWeatherAsync(string? locationOverride = null)
        {
            try
            {
                string loc = locationOverride ?? WeatherLocation;
                WeatherData data = await _weatherService.GetWeatherAsync(loc);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    WeatherDescription = data.Description;
                    WeatherTemperature = data.TempC.ToString("F0") + "°";
                    WeatherLocation = loc;

                    if (data.Forecast != null)
                    {
                        FiveDayForecast.Clear();
                        foreach (var day in data.Forecast) FiveDayForecast.Add(day);
                    }
                });
            }
            catch { /* Log error */ }
        }

        private void LoadDiskData()
        {
            // Assuming DiskInfoService is accessible
            // var driveData = DiskInfoService.GetDrivesData();
            // DiskDrives = new ObservableCollection<DriveSpaceInfo>(driveData);
        }
    }
}