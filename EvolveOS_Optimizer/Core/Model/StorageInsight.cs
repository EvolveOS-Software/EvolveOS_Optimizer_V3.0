// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class StorageInsight : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DynamicWidth))]
        [NotifyPropertyChangedFor(nameof(TooltipText))]
        public partial string CategoryName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ColorHex { get; set; } = "#808080";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DynamicWidth))]
        [NotifyPropertyChangedFor(nameof(TooltipText))]
        public partial double Percentage { get; set; }

        public double DynamicWidth => Math.Max(2, (Percentage / 100) * 370);

        public string TooltipText => $"{CategoryName}: {Percentage:F1}%";
    }
}