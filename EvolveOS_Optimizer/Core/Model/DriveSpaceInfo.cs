namespace EvolveOS_Optimizer.Core.Model
{
    public class DriveSpaceInfo
    {
        public string? Name { get; set; }
        public string? VolumeLabel { get; set; }
        public double TotalSizeGB { get; set; }
        public double FreeSpaceGB { get; set; }
        public double UsedSpaceGB { get; set; }
        public double UsedPercentage { get; set; }

        public string DisplayName => string.IsNullOrEmpty(VolumeLabel) ? Name ?? "Local Disk" : $"{VolumeLabel} ({Name})";
        public string UsedPercentageStr => $"{UsedPercentage:0}%";
        public string UsedSpaceStr => $"{UsedSpaceGB:0.0} GB";
        public string FreeSpaceStr => $"{FreeSpaceGB:0.0} GB";
    }
}
