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

            XAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = true,
                    TextSize = 10,
                    MinStep = 15,
                    MinLimit = -_historyDuration,
                    MaxLimit = 0,

                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50)),
                    Labeler = value =>
                    {
                        double secondsAgo = Math.Abs(value);

                        if (_historyDuration >= 300 && secondsAgo >= 60)
                            return $"{(int)secondsAgo / 60}m";

                        return $"{(int)secondsAgo}s";
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

            Series = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = SensorHistory,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.OrangeRed) { StrokeThickness = 2 },
                    GeometrySize = 0,
                    LineSmoothness = 0.5,
                    AnimationsSpeed = TimeSpan.Zero
                }
            };
        }

        public void UpdateHistoryDuration(int seconds)
        {
            _historyDuration = seconds;

            if (seconds <= 60) XAxes[0].MinStep = 15;
            else if (seconds <= 300) XAxes[0].MinStep = 60;
            else if (seconds <= 600) XAxes[0].MinStep = 120;
            else XAxes[0].MinStep = 180;

            XAxes[0].MinLimit = -_historyDuration;
        }

        public void SwitchMode(bool isLoad, string newHardwareName, float initialValue)
        {
            SensorHardwareName = newHardwareName;
            IsShowingLoad = isLoad;

            YAxes[0].MinLimit = isLoad ? -5 : 20;
            YAxes[0].MaxLimit = isLoad ? 100 : 115;
            YAxes[0].Labeler = value => isLoad ? $"{value}%" : $"{value}°C";

            ClearHistory();
            AddSensorRecord(initialValue);
        }

        public void AddSensorRecord(float val)
        {
            SensorValue = val;

            foreach (var point in SensorHistory)
            {
                if (point.X.HasValue) point.X = point.X.Value - 1;
            }

            SensorHistory.Add(new ObservablePoint(0, val));

            while (SensorHistory.Count > 0 && SensorHistory[0].X < -900)
            {
                SensorHistory.RemoveAt(0);
            }
        }

        public void ClearHistory()
        {
            SensorHistory.Clear();
        }
    }
}