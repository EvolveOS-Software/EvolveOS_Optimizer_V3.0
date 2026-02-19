using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using static EvolveOS_Optimizer.Core.Model.WeatherApiModels;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    public class WeatherService : IDisposable
    {
        private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private const string API_KEY = "6aa62b54867341f3b3925740250511";
        private const int FORECAST_DAYS = 5;
        private const string BASE_URL = "https://api.weatherapi.com/v1/forecast.json";

        private string _location = string.Empty;

        public string Location => _location;

        public async Task<WeatherData> GetWeatherAsync(string? locationOverride = null, CancellationToken token = default)
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
                using var response = await _client.GetAsync(url, token);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(token);
                if (token.IsCancellationRequested) return null!;

                var apiResponse = JsonSerializer.Deserialize<ApiWeatherResponse>(content);

                if (apiResponse == null) return GetMockWeatherData();

                if (apiResponse.Location?.Name != null)
                {
                    _location = apiResponse.Location.Name;
                }

                return MapApiToUiModel(apiResponse);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[WeatherService] Operation was cancelled during network request.");
                return null!;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Weather API Error: {ex.Message}");
                return GetMockWeatherData();
            }
        }

        private string GetLocalIconPath(string? conditionText)
        {
            if (string.IsNullOrEmpty(conditionText))
                return "ms-appx:///Assets/ImagePackages/Sunny.png";

            string lower = conditionText.ToLowerInvariant();

            if (lower.Contains("rain") || lower.Contains("drizzle") || lower.Contains("shower") || lower.Contains("snow") || lower.Contains("sleet"))
                return "ms-appx:///Assets/ImagePackages/Rain.png";

            if (lower.Contains("cloud") || lower.Contains("overcast") || lower.Contains("fog"))
                return "ms-appx:///Assets/ImagePackages/Cloudy.png";

            if (lower.Contains("wind") || lower.Contains("storm") || lower.Contains("blizzard"))
                return "ms-appx:///Assets/ImagePackages/Wind.png";

            return "ms-appx:///Assets/ImagePackages/Sunny.png";
        }

        private WeatherData MapApiToUiModel(ApiWeatherResponse apiResponse)
        {
            var uiModel = new WeatherData
            {
                TempC = apiResponse.Current?.TempC ?? 0,
                Description = apiResponse.Current?.Condition?.Text ?? "Unknown",

                CurrentIconUrl = GetLocalIconPath(apiResponse.Current?.Condition?.Text)
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
                            IconSource = GetLocalIconPath(apiDay.Day?.Condition?.Text),
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
                CurrentIconUrl = BasePath + "Cloudy.png",
                Forecast = new List<DailyForecast>
                {
                    new DailyForecast { Day = "MON", IconSource = BasePath + "Sunny.png", MaxTemp = "25°", MinTemp = "18°" },
                    new DailyForecast { Day = "TUE", IconSource = BasePath + "Cloudy.png", MaxTemp = "22°", MinTemp = "16°" },
                    new DailyForecast { Day = "WED", IconSource = BasePath + "Rain.png", MaxTemp = "19°", MinTemp = "14°" },
                    new DailyForecast { Day = "THU", IconSource = BasePath + "Wind.png", MaxTemp = "21°", MinTemp = "15°" },
                    new DailyForecast { Day = "FRI", IconSource = BasePath + "Sunny.png", MaxTemp = "24°", MinTemp = "17°" }
                }
            };
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}