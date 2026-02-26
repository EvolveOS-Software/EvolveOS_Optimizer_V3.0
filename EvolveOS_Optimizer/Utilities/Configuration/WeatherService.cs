using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using static EvolveOS_Optimizer.Core.Model.WeatherApiModels;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    public class WeatherService : IDisposable
    {
        #region Fields & HTTP Client Setup

        private static readonly HttpClient _client;
        private readonly CancellationTokenSource _internalCts = new();

        private const string API_KEY = "6aa62b54867341f3b3925740250511";
        private const int FORECAST_DAYS = 5;
        private const string BASE_URL = "https://api.weatherapi.com/v1/forecast.json";

        private string _location = string.Empty;
        public string Location => _location;

        private static WeatherData? _cachedWeather;
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static string _lastRequestedLocation = string.Empty;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        static WeatherService()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            _client.DefaultRequestHeaders.Add("User-Agent", "EvolveOS_Optimizer/1.0 (Windows)");
            _client.DefaultRequestHeaders.ConnectionClose = false;
        }

        #endregion

        #region API Fetch Logic

        public async Task<WeatherData> GetWeatherAsync(string? locationOverride = null, CancellationToken token = default, bool forceRefresh = false)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _internalCts.Token);
            var activeToken = linkedCts.Token;

            if (string.IsNullOrEmpty(API_KEY) || API_KEY.Contains("CHANGE_ME"))
                return GetMockWeatherData();

            string effectiveLocation = locationOverride ?? _location;
            if (string.IsNullOrEmpty(effectiveLocation)) effectiveLocation = SettingsEngine.LastLocation;
            if (string.IsNullOrEmpty(effectiveLocation)) effectiveLocation = "Paris";

            if (!forceRefresh &&
                _cachedWeather != null &&
                string.Equals(_lastRequestedLocation, effectiveLocation, StringComparison.OrdinalIgnoreCase) &&
                (DateTime.Now - _lastFetchTime) < CacheDuration)
            {
                Debug.WriteLine("[WeatherService] Serving weather from Memory Cache (Fast).");
                return _cachedWeather;
            }

            if (forceRefresh)
            {
                Debug.WriteLine("[WeatherService] Force Refresh requested. Bypassing cache...");
            }

            var url = $"{BASE_URL}?key={API_KEY}&q={Uri.EscapeDataString(effectiveLocation)}&days={FORECAST_DAYS}";

            int maxRetries = 3;
            int currentAttempt = 0;

            while (currentAttempt < maxRetries)
            {
                currentAttempt++;
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);

                    request.Version = new Version(2, 0);

                    var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, activeToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized ||
                            response.StatusCode == HttpStatusCode.BadRequest ||
                            response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            Debug.WriteLine($"[Weather] Fatal API Error: {response.StatusCode}");
                            break;
                        }

                        throw new HttpRequestException($"HTTP Error {response.StatusCode}");
                    }

                    var content = await response.Content.ReadAsStringAsync(activeToken);
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

                        var finalModel = MapApiToUiModel(apiResponse);
                        _cachedWeather = finalModel;
                        _lastRequestedLocation = effectiveLocation;
                        _lastFetchTime = DateTime.Now;

                        return finalModel;
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException)
                {
                    Debug.WriteLine($"[Weather] Attempt {currentAttempt} failed: {ex.Message}");

                    if (currentAttempt < maxRetries)
                    {
                        await Task.Delay(currentAttempt * 1000, activeToken);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Weather] Fatal Fetch Error on attempt {currentAttempt}: {ex.Message}");
                    break;
                }
            }

            if (_cachedWeather != null)
            {
                Debug.WriteLine("[WeatherService] Fetch failed, serving expired cache instead of breaking UI.");
                return _cachedWeather;
            }

            return GetMockWeatherData();
        }

        #endregion

        #region Helpers & Mapping

        private string GetLocalIconPath(string? conditionText, bool isDay = true)
        {
            string lower = conditionText?.ToLowerInvariant() ?? "";

            string nightPrefix = DateTime.Now.Day % 2 == 0 ? "night_full_moon_" : "night_half_moon_";
            string prefix = isDay ? "day_" : nightPrefix;

            // 1. EXTREME WEATHER (Neutral)
            if (lower.Contains("tornado") || lower.Contains("hurricane") || lower.Contains("cyclone"))
                return "ms-appx:///Assets/ImagePackages/tornado.png";

            // 2. WIND (Neutral)
            if (lower.Contains("wind") || lower.Contains("gale") || lower.Equals("breezy"))
                return "ms-appx:///Assets/ImagePackages/wind.png";

            // 3. FOG & MIST (Neutral - completely blocks the sky)
            if (lower.Contains("mist")) return "ms-appx:///Assets/ImagePackages/mist.png";
            if (lower.Contains("fog")) return "ms-appx:///Assets/ImagePackages/fog.png";

            // 4. OVERCAST & CLOUDS (Neutral - completely blocks the sky)
            if (lower.Contains("overcast")) return "ms-appx:///Assets/ImagePackages/overcast.png";
            if (lower.Equals("cloudy") || lower.Equals("clouds")) return "ms-appx:///Assets/ImagePackages/cloudy.png";
            if (lower.Contains("angry") || lower.Contains("squall")) return "ms-appx:///Assets/ImagePackages/angry_clouds.png";

            // 5. DRY THUNDERSTORMS (Neutral - Lightning without rain)
            if (lower.Contains("thunder") && !lower.Contains("rain") && !lower.Contains("snow") && !lower.Contains("storm"))
                return "ms-appx:///Assets/ImagePackages/thunder.png";

            // 6. HEAVY PRECIPITATION (Neutral - Storms so thick they block the sun/moon)
            if (lower.Contains("heavy") || lower.Contains("torrential") || lower.Contains("moderate"))
            {
                if (lower.Contains("thunder") && lower.Contains("snow")) return "ms-appx:///Assets/ImagePackages/snow_thunder.png";
                if (lower.Contains("thunder") || lower.Contains("storm")) return "ms-appx:///Assets/ImagePackages/rain_thunder.png";
                if (lower.Contains("sleet") || lower.Contains("ice") || lower.Contains("pellets")) return "ms-appx:///Assets/ImagePackages/sleet.png";
                if (lower.Contains("snow") || lower.Contains("blizzard")) return "ms-appx:///Assets/ImagePackages/snow.png";
                if (lower.Contains("rain") || lower.Contains("shower")) return "ms-appx:///Assets/ImagePackages/rain.png";
            }

            // 7. LIGHT / PATCHY PRECIPITATION (Prefix - Sun or Moon is visible)
            if (lower.Contains("thunder") && lower.Contains("snow")) return $"ms-appx:///Assets/ImagePackages/{prefix}snow_thunder.png";
            if (lower.Contains("thunder") || lower.Contains("storm")) return $"ms-appx:///Assets/ImagePackages/{prefix}rain_thunder.png";
            if (lower.Contains("sleet") || lower.Contains("freezing") || lower.Contains("ice") || lower.Contains("pellets")) return $"ms-appx:///Assets/ImagePackages/{prefix}sleet.png";
            if (lower.Contains("snow") || lower.Contains("blizzard") || lower.Contains("flurries")) return $"ms-appx:///Assets/ImagePackages/{prefix}snow.png";
            if (lower.Contains("rain") || lower.Contains("drizzle") || lower.Contains("shower")) return $"ms-appx:///Assets/ImagePackages/{prefix}rain.png";

            // 8. PARTIAL CLOUDS (Prefix - Sun/Moon peeking through)
            if (lower.Contains("partly") || lower.Contains("cloud")) return $"ms-appx:///Assets/ImagePackages/{prefix}partial_cloud.png";

            // 9. CLEAR SKIES (Fallback)
            return $"ms-appx:///Assets/ImagePackages/{prefix}clear.png";
        }

        private WeatherData MapApiToUiModel(ApiWeatherResponse apiResponse)
        {
            bool isDaytime = (apiResponse.Current?.Is_Day ?? 1) == 1;

            var uiModel = new WeatherData
            {
                TempC = apiResponse.Current?.TempC ?? 0,
                Description = apiResponse.Current?.Condition?.Text ?? "Unknown",
                CurrentIconUrl = GetLocalIconPath(apiResponse.Current?.Condition?.Text, isDaytime)
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
                            IconSource = GetLocalIconPath(apiDay.Day?.Condition?.Text, true),
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

        #region Disposal

        public void Dispose()
        {
            try
            {
                if (!_internalCts.IsCancellationRequested)
                {
                    _internalCts.Cancel();
                }
                _internalCts.Dispose();
            }
            catch { }

            Debug.WriteLine("[WeatherService] Disposed - Pending requests cancelled.");
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}