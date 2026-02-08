using System.Collections.Generic;
using System.Text.Json.Serialization;
using EvolveOS_Optimizer.Core.Base;
using Microsoft.UI.Xaml.Media;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class WeatherApiModels
    {
        public class DailyForecast
        {
            public string? Day { get; set; }
            public ImageSource? IconSource { get; set; }
            public string? MaxTemp { get; set; }
            public string? MinTemp { get; set; }
        }

        public class WeatherData : ViewModelBase
        {
            private string _description = "N/A";
            public string Description
            {
                get => _description;
                set { _description = value; OnPropertyChanged(); }
            }

            private double _tempC;
            public double TempC
            {
                get => _tempC;
                set { _tempC = value; OnPropertyChanged(); }
            }

            public ImageSource? CurrentIconUrl { get; set; }
            public List<DailyForecast> Forecast { get; set; } = new List<DailyForecast>();
        }

        public class LocationModel
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        public class ApiCondition
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("icon")]
            public string? Icon { get; set; }
        }

        public class ApiCurrent
        {
            [JsonPropertyName("temp_c")]
            public double TempC { get; set; }

            [JsonPropertyName("condition")]
            public ApiCondition? Condition { get; set; }
        }

        public class ApiDay
        {
            [JsonPropertyName("maxtemp_c")]
            public double MaxTempC { get; set; }

            [JsonPropertyName("mintemp_c")]
            public double MinTempC { get; set; }

            [JsonPropertyName("condition")]
            public ApiCondition? Condition { get; set; }
        }

        public class ApiForecastDay
        {
            [JsonPropertyName("date")]
            public string? Date { get; set; }

            [JsonPropertyName("day")]
            public ApiDay? Day { get; set; }
        }

        public class ApiForecast
        {
            [JsonPropertyName("forecastday")]
            public List<ApiForecastDay> ForecastDay { get; set; } = new List<ApiForecastDay>();
        }

        public class ApiWeatherResponse
        {
            [JsonPropertyName("location")]
            public LocationModel? Location { get; set; }

            [JsonPropertyName("current")]
            public ApiCurrent? Current { get; set; }

            [JsonPropertyName("forecast")]
            public ApiForecast? Forecast { get; set; }
        }
    }
}
