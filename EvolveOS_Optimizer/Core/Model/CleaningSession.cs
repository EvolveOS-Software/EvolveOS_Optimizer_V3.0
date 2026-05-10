// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public record CleaningSession(DateTime Timestamp, long BytesRecovered);

    public partial class HistoryBarItem : ObservableObject
    {
        [ObservableProperty]
        public partial double BarHeight { get; set; }

        [ObservableProperty]
        public partial string DayLabel { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string TooltipText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ColorHex { get; set; } = "#0078D4";

        [ObservableProperty]
        public partial double Opacity { get; set; } = 1.0;
    }
}