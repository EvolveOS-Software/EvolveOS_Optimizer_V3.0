// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class StorageInsight : ObservableObject
    {
        [ObservableProperty]
        public partial string CategoryName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ColorHex { get; set; } = "#808080";

        [ObservableProperty]
        public partial double Percentage { get; set; }

        [ObservableProperty]
        public partial double DynamicWidth { get; set; }

        [ObservableProperty]
        public partial string TooltipText { get; set; } = string.Empty;

        public StorageNode? TargetNode { get; set; }
    }
}