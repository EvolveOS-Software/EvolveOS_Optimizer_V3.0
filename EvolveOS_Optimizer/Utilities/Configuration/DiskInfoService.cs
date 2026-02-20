using System.IO;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    public static class DiskInfoService
    {
        private const long BytesInGB = 1024L * 1024L * 1024L;
        private const int MaxDrivesToShow = 10;

        public static List<DriveSpaceInfo> GetDrivesData()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Take(MaxDrivesToShow)
                .ToList();

            var driveList = new List<DriveSpaceInfo>();

            foreach (var drive in drives)
            {
                double totalSizeGB = (double)drive.TotalSize / BytesInGB;
                double freeSpaceGB = (double)drive.AvailableFreeSpace / BytesInGB;
                double usedSpaceGB = totalSizeGB - freeSpaceGB;
                double usedPercentage = (usedSpaceGB / totalSizeGB) * 100;

                driveList.Add(new DriveSpaceInfo
                {
                    Name = drive.Name,
                    VolumeLabel = drive.VolumeLabel,
                    TotalSizeGB = totalSizeGB,
                    FreeSpaceGB = freeSpaceGB,
                    UsedSpaceGB = usedSpaceGB,
                    UsedPercentage = usedPercentage
                });
            }
            return driveList;
        }
    }
}
