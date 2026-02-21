using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using EvolveOS_Optimizer.Utilities.Controls;
using static EvolveOS_Optimizer.Core.Model.WeatherApiModels;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    public class WeatherService
    {
        private static readonly HttpClient _client = new HttpClient();

        private const string API_KEY = "6aa62b54867341f3b3925740250511";
        private const int FORECAST_DAYS = 5;
        private const string BASE_URL = "https://api.weatherapi.com/v1/forecast.json";

        private string _location = string.Empty;
        public string Location => _location;

        public async Task<WeatherData> GetWeatherAsync(string? locationOverride = null, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(API_KEY) || API_KEY.Contains("CHANGE_ME")) return GetMockWeatherData();

            string effectiveLocation = locationOverride ?? _location;
            if (string.IsNullOrEmpty(effectiveLocation)) effectiveLocation = SettingsEngine.LastLocation;
            if (string.IsNullOrEmpty(effectiveLocation)) effectiveLocation = "Paris";

            var url = $"{BASE_URL}?key={API_KEY}&q={Uri.EscapeDataString(effectiveLocation)}&days={FORECAST_DAYS}";

            int maxRetries = 3;
            int currentAttempt = 0;

            while (currentAttempt < maxRetries)
            {
                currentAttempt++;
                try
                {
                    int timeoutSeconds = currentAttempt == 1 ? 4 : 7;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    var apiResponse = JsonSerializer.Deserialize<ApiWeatherResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Current != null)
                    {
                        if (apiResponse.Location?.Name != null)
                        {
                            _location = apiResponse.Location.Name;
                            SettingsEngine.LastLocation = _location;
                        }
                        return MapApiToUiModel(apiResponse);
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
                {
                    Debug.WriteLine($"[Weather] Attempt {currentAttempt} timed out.");

                    if (currentAttempt < maxRetries)
                    {
                        await Task.Delay(500, token);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Weather] Fetch Error on attempt {currentAttempt}: {ex.Message}");
                    break;
                }
            }

            return GetMockWeatherData();
        }

        #region Helpers & Mapping

        private string GetLocalIconPath(string? conditionText)
        {
            if (string.IsNullOrEmpty(conditionText))
                return "ms-appx:///Assets/ImagePackages/Sunny.png";

            string lower = conditionText.ToLowerInvariant();

            if (lower.Contains("rain") || lower.Contains("drizzle") || lower.Contains("shower") ||
                lower.Contains("snow") || lower.Contains("sleet") || lower.Contains("thunder"))
                return "ms-appx:///Assets/ImagePackages/Rain.png";

            if (lower.Contains("cloud") || lower.Contains("overcast") || lower.Contains("fog") || lower.Contains("mist"))
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
                TempC = 0,
                Description = "Offline",
                CurrentIconUrl = BasePath + "Cloudy.png",
                Forecast = new List<DailyForecast>()
            };
        }

        #endregion
    }
}