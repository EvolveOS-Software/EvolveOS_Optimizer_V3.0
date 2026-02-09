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
    }
}
