// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class SystemEventItem
    {
        public DateTime TimeCreated { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public int EventId { get; set; }
        public byte Level { get; set; } // 1=Critical, 2=Error, 3=Warning
        public string Message { get; set; } = string.Empty;
        public string? FullMessage { get; set; }

        public string? AiAnalysis { get; set; }
        public string FormattedTime => TimeCreated.ToString("HH:mm:ss");

        public bool IsFixable { get; set; } = false;

        public Microsoft.UI.Xaml.Visibility FixButtonVisibility =>
            IsFixable ? Visibility.Visible : Visibility.Collapsed;

        public string StatusGlyph => Level switch
        {
            1 => "\uEA39", // Critical (Cross/Shield)
            2 => "\uE783", // Error (Warning Icon)
            3 => "\uE7BA", // Warning (Triangle)
            _ => "\uE9CE"  // Info
        };

        public SolidColorBrush StatusBrush => Level switch
        {
            1 => new SolidColorBrush(Colors.Red),
            2 => new SolidColorBrush(Colors.OrangeRed),
            3 => new SolidColorBrush(Colors.Gold),
            _ => new SolidColorBrush(Colors.LightGray)
        };
    }

    public class HourlyMetric
    {
        public string? TimeLabel { get; set; }
        public double BarHeight { get; set; }
        public SolidColorBrush? BarColor { get; set; }
    }
}