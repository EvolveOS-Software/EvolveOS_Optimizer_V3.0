// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class ProcessManagerModel : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Background Processes";
        public byte[]? IconBytes { get; set; }
        public ImageSource? ProcessIcon { get; set; }

        public Visibility FallbackIconVisibility => ProcessIcon == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ImageIconVisibility => ProcessIcon != null ? Visibility.Visible : Visibility.Collapsed;

        private string _priority = "Normal";
        public string Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        private double _memoryMB;
        public double MemoryMB
        {
            get => _memoryMB;
            set => SetProperty(ref _memoryMB, value);
        }

        private int _threadCount;
        public int ThreadCount
        {
            get => _threadCount;
            set => SetProperty(ref _threadCount, value);
        }

        public string MemoryDisplay => $"{MemoryMB:F1} MB";
        public double MemoryPercent => Math.Min(MemoryMB / 500.0 * 100, 100);
        public double HeatmapOpacity => (MemoryMB >= 2048) ? 0.15 : 0.0;

        public string? HeatmapTooltip => (MemoryMB >= 2048)
            ? ResourceString.GetString("process_manager_page_high_memory")
            : null;

        public void UpdateFrom(ProcessManagerModel other)
        {
            this.Priority = other.Priority;
            this.MemoryMB = other.MemoryMB;
            this.ThreadCount = other.ThreadCount;
            this.Name = other.Name;

            OnPropertyChanged(nameof(MemoryDisplay));
            OnPropertyChanged(nameof(MemoryPercent));
            OnPropertyChanged(nameof(HeatmapOpacity));
            OnPropertyChanged(nameof(HeatmapTooltip));
        }
    }
}