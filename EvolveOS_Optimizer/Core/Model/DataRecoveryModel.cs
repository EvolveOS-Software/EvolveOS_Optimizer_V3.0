// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public enum RecoveryIntegrityStatus
    {
        Recoverable,
        Degraded,
        Unrecoverable
    }

    public enum FileCategory
    {
        Document,
        Picture,
        Video,
        Audio,
        Archive,
        Other
    }

    public class RecoveredItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string FormattedSize => FormatBytes(SizeBytes);
        public long SectorOffset { get; set; }
        public FileCategory Category { get; set; }
        public RecoveryIntegrityStatus Status { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public string StatusColor => Status switch
        {
            RecoveryIntegrityStatus.Recoverable => "#FF0F7B0F",    // Success Green
            RecoveryIntegrityStatus.Degraded => "#FFD83B01",       // Warning Amber
            RecoveryIntegrityStatus.Unrecoverable => "#FFD13438",  // Error Red
            _ => "#FF8A8886"
        };

        public string StatusDisplayText => Status switch
        {
            RecoveryIntegrityStatus.Recoverable => "Recoverable (100%)",
            RecoveryIntegrityStatus.Degraded => "Degraded / Fragmented",
            RecoveryIntegrityStatus.Unrecoverable => "Unrecoverable (TRIMmed/Zeroed)",
            _ => "Unknown"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class DriveTarget
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public bool IsSsd { get; set; }
        public bool IsTrimEnabled { get; set; }

        public string DisplayName => $"{VolumeLabel} ({DriveLetter}) [{(IsSsd ? "SSD/NVMe" : "HDD/USB")}]";
    }
}