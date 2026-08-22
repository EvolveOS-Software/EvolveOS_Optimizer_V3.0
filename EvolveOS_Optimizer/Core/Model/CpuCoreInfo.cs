// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class CpuCoreInfo : ObservableObject
    {
        private int _historyDuration;

        private readonly List<(DateTime Time, ObservablePoint Point)> _activePoints = new();
        private readonly LineSeries<ObservablePoint> _lineSeries;

        [ObservableProperty]
        public partial string CoreName { get; set; } = string.Empty;

        public string SensorHardwareName { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayValue))]
        public partial bool IsShowingLoad { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayValue))]
        public partial float SensorValue { get; set; }

        public string DisplayValue => IsShowingLoad ? $"{SensorValue:0}%" : $"{SensorValue:0.0}°C";

        public ObservableCollection<ObservablePoint> SensorHistory { get; } = new();
        public ISeries[] Series { get; }

        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        public CpuCoreInfo(bool isLoad, int historyDurationSeconds)
        {
            IsShowingLoad = isLoad;
            _historyDuration = historyDurationSeconds;

            SKColor chartColor = GetActiveColor(isLoad);

            TimeSpan animSpeed = TimeSpan.FromMilliseconds(isLoad ? 220 : 1000);

            XAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = true,
                    TextSize = 10,
                    MinLimit = 0,
                    MaxLimit = 400,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50)),
                    Labeler = value =>
                    {
                        double progress = Math.Clamp(value / 400.0, 0.0, 1.0);
                        double secondsAgo = (1.0 - progress) * _historyDuration;

                        if (secondsAgo < 1) return "0s";
                        if (_historyDuration >= 300 && secondsAgo >= 60)
                            return $"-{(int)(secondsAgo / 60)}m";

                        return $"-{(int)secondsAgo}s";
                    }
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = true,
                    TextSize = 10,
                    MinStep = 20,
                    MinLimit = isLoad ? -5 : 20,
                    MaxLimit = isLoad ? 100 : 115,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50)),
                    Labeler = value => isLoad ? $"{value}%" : $"{value}°C"
                }
            };

            _lineSeries = new LineSeries<ObservablePoint>
            {
                Values = SensorHistory,
                Fill = new LinearGradientPaint(new[] { chartColor.WithAlpha(100), chartColor.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                Stroke = new SolidColorPaint(chartColor) { StrokeThickness = 2 },

                GeometrySize = 0,
                LineSmoothness = 0,
                AnimationsSpeed = animSpeed,
                EasingFunction = LiveChartsCore.EasingFunctions.Lineal,
                DataPadding = new LiveChartsCore.Drawing.LvcPoint(0, 0)
            };

            Series = new ISeries[] { _lineSeries };
            UpdateHistoryDuration(historyDurationSeconds);
        }

        private SKColor GetActiveColor(bool isLoad)
        {
            if (!isLoad) return SKColor.Parse("#FF9900");

            SKColor accentColor = SKColor.Parse("#00FFFF");
            try
            {
                if (Application.Current.Resources.TryGetValue("MyDynamicAccentBrush", out object resource) &&
                    resource is SolidColorBrush customBrush)
                {
                    var winColor = customBrush.Color;
                    accentColor = new SKColor(winColor.R, winColor.G, winColor.B, winColor.A);
                }
            }
            catch { }
            return accentColor;
        }

        public void UpdateHistoryDuration(int seconds)
        {
            _historyDuration = seconds;

            if (seconds <= 60) XAxes[0].MinStep = 400.0 / (seconds / 15.0);
            else if (seconds <= 300) XAxes[0].MinStep = 400.0 / (seconds / 60.0);
            else if (seconds <= 600) XAxes[0].MinStep = 400.0 / (seconds / 120.0);
            else XAxes[0].MinStep = 400.0 / (seconds / 180.0);
        }

        public void SwitchMode(bool isLoad, string newHardwareName, float initialValue)
        {
            SensorHardwareName = newHardwareName;
            IsShowingLoad = isLoad;

            YAxes[0].MinLimit = isLoad ? -5 : 20;
            YAxes[0].MaxLimit = isLoad ? 100 : 115;
            YAxes[0].Labeler = value => isLoad ? $"{value}%" : $"{value}°C";

            SKColor newColor = GetActiveColor(isLoad);
            TimeSpan newAnimSpeed = TimeSpan.FromMilliseconds(isLoad ? 220 : 1000);

            _lineSeries.Stroke = new SolidColorPaint(newColor) { StrokeThickness = 2 };
            _lineSeries.Fill = new LinearGradientPaint(new[] { newColor.WithAlpha(100), newColor.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1));
            _lineSeries.AnimationsSpeed = newAnimSpeed;

            ClearHistory();
            AddSensorRecord(initialValue);
        }

        public void AddSensorRecord(float val)
        {
            SensorValue = val;
            DateTime now = DateTime.UtcNow;

            if (_activePoints.Count == 0)
            {
                var newPoint = new ObservablePoint(400.0, val);
                _activePoints.Add((now, newPoint));
                SensorHistory.Add(newPoint);
            }
            else
            {
                double thresholdSeconds = _historyDuration / 100.0;
                var last = _activePoints.Last();

                if ((now - last.Time).TotalSeconds >= thresholdSeconds)
                {
                    var newPoint = new ObservablePoint(400.0, val);
                    _activePoints.Add((now, newPoint));
                    SensorHistory.Add(newPoint);
                }
                else
                {
                    last.Point.Y = val;
                }
            }

            for (int i = 0; i < _activePoints.Count; i++)
            {
                double secondsBehind = (now - _activePoints[i].Time).TotalSeconds;
                double newX = 400.0 - ((secondsBehind / (double)_historyDuration) * 400.0);

                _activePoints[i].Point.X = newX;
            }

            DateTime cutoff = now.AddSeconds(-_historyDuration - 10.0);
            while (_activePoints.Count > 0 && _activePoints[0].Time < cutoff)
            {
                SensorHistory.Remove(_activePoints[0].Point);
                _activePoints.RemoveAt(0);
            }
        }

        public void ClearHistory()
        {
            _activePoints.Clear();
            SensorHistory.Clear();
        }
    }
}