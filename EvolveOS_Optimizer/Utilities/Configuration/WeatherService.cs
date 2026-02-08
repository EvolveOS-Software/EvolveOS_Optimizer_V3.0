using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

using static EvolveOS_Optimizer.Core.Model.WeatherApiModels;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    public class WeatherService : IDisposable
    {
        private readonly HttpClient _client = new HttpClient();

        private const string API_KEY = "6aa62b54867341f3b3925740250511";
        private const int FORECAST_DAYS = 5;
        private const string BASE_URL = "https://api.weatherapi.com/v1/forecast.json";

        private string _location = string.Empty;

        public string Location => _location;

        public async Task<WeatherData> GetWeatherAsync(string? locationOverride = null)
        {
            if (string.IsNullOrEmpty(API_KEY) || API_KEY == "YOUR_WEATHERAPI_KEY")
            {
                return GetMockWeatherData();
            }

            string effectiveLocation = locationOverride ?? _location;

            if (string.IsNullOrEmpty(effectiveLocation)) effectiveLocation = "London";

            var url = $"{BASE_URL}?key={API_KEY}&q={effectiveLocation}&days={FORECAST_DAYS}";

            try
            {
                var response = await _client.GetStringAsync(url);
                var apiResponse = JsonSerializer.Deserialize<ApiWeatherResponse>(response);

                if (apiResponse == null)
                {
                    return GetMockWeatherData();
                }

                if (apiResponse.Location?.Name != null)
                {
                    _location = apiResponse.Location.Name;
                }

                return MapApiToUiModel(apiResponse);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Weather API Error: {ex.Message}");
                return GetMockWeatherData();
            }
        }

        private ImageSource? GetImageSourceFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            if (!url.StartsWith("https:"))
            {
                url = url.Replace("http:", "https:").Replace("//", "https://");
            }

            try
            {
                return new BitmapImage(new Uri(url));
            }
            catch
            {
                return null;
            }
        }

        private ImageSource? GetLocalImage(string path)
        {
            try
            {
                return new BitmapImage(new Uri(path));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Local Image Load Failed: {ex.Message}");
                return null;
            }
        }

        private WeatherData MapApiToUiModel(ApiWeatherResponse apiResponse)
        {
            var uiModel = new WeatherData
            {
                TempC = apiResponse.Current?.TempC ?? 0,
                Description = apiResponse.Current?.Condition?.Text ?? "Unknown",
                CurrentIconUrl = GetImageSourceFromUrl(apiResponse.Current?.Condition?.Icon)
            };

            if (apiResponse.Forecast?.ForecastDay != null)
            {
                foreach (var apiDay in apiResponse.Forecast.ForecastDay)
                {
                    if (apiDay?.Date != null && DateTime.TryParseExact(apiDay.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        uiModel.Forecast.Add(new DailyForecast
                        {
                            Day = date.DayOfWeek.ToString().Substring(0, 3).ToUpper(),
                            IconSource = GetImageSourceFromUrl(apiDay.Day?.Condition?.Icon),
                            MaxTemp = $"{apiDay.Day?.MaxTempC ?? 0:F0}°",
                            MinTemp = $"{apiDay.Day?.MinTempC ?? 0:F0}°"
                        });
                    }
                }
            }

            return uiModel;
        }

        private WeatherData GetMockWeatherData()
        {
            const string BasePath = "ms-appx:///Assets/ImagePackages/";

            return new WeatherData
            {
                TempC = 25,
                Description = "Partly Cloudy",
                CurrentIconUrl = GetLocalImage(BasePath + "Cloudy.png"),
                Forecast = new List<DailyForecast>
                {
                    new DailyForecast { Day = "MON", IconSource = GetLocalImage(BasePath + "Sunny.png"), MaxTemp = "25°", MinTemp = "18°" },
                    new DailyForecast { Day = "TUE", IconSource = GetLocalImage(BasePath + "Cloudy.png"), MaxTemp = "22°", MinTemp = "16°" },
                    new DailyForecast { Day = "WED", IconSource = GetLocalImage(BasePath + "Rain.png"), MaxTemp = "19°", MinTemp = "14°" },
                    new DailyForecast { Day = "THU", IconSource = GetLocalImage(BasePath + "Wind.png"), MaxTemp = "21°", MinTemp = "15°" },
                    new DailyForecast { Day = "FRI", IconSource = GetLocalImage(BasePath + "Sunny.png"), MaxTemp = "24°", MinTemp = "17°" }
                }
            };
        }

        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}