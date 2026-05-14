// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Utilities.Extensions;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class StorageNode : ObservableObject
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsFolder { get; set; }

        public int Depth { get; set; }
        public Thickness DepthPadding => new Thickness(Depth * 16, 0, 0, 0);

        public long SizeBytes { get; set; }
        public string FormattedSize => SizeBytes.FormatBytes();

        public long AllocatedSizeBytes { get; set; }
        public string AllocatedSizeFormatted => AllocatedSizeBytes.FormatBytes();

        public int FilesCount { get; set; }
        public int FoldersCount { get; set; }

        public bool IsHidden { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PercentageString))]
        public partial double Percentage { get; set; }

        public string PercentageString => $"{Percentage:0.0}%";

        public DateTime LastModified { get; set; }
        public string LastModifiedString => LastModified.ToString("g");

        [ObservableProperty]
        public partial bool IsExpanded { get; set; }

        public ObservableCollection<StorageNode> Children { get; } = new();
    }
}