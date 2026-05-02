// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.Model
{
    public class ProcessManagerModel
    {
        public string Priority { get; set; } = "Normal";
        public string Category { get; set; } = "Background Processes";
        public string Name { get; set; } = string.Empty;
        public int Id
        {
            get; set;
        }
        public double MemoryMB
        {
            get; set;
        }
        public int ThreadCount
        {
            get; set;
        }
        public string MemoryDisplay => $"{MemoryMB:F1} MB";
        public double MemoryPercent => Math.Min(MemoryMB / 500.0 * 100, 100);
        public double HeatmapOpacity => (MemoryMB >= 2048) ? 0.15 : 0.0;

        public string? HeatmapTooltip => (MemoryMB >= 2048)
            ? ResourceString.GetString("process_manager_page_high_memory")
            : null;

        public void UpdateFrom(ProcessManagerModel other)
        {
            Name = other.Name;
            MemoryMB = other.MemoryMB;
            ThreadCount = other.ThreadCount;
        }

        public byte[]? IconBytes { get; set; }
        public ImageSource? ProcessIcon { get; set; }
        public Visibility FallbackIconVisibility => ProcessIcon == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ImageIconVisibility => ProcessIcon != null ? Visibility.Visible : Visibility.Collapsed;
    }
}
