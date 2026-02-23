namespace EvolveOS_Optimizer.Utilities.Configuration
{
    internal class HardwareData
    {
        #region Nested Info Classes

        internal sealed class OperatingSystemInfo
        {
            internal string Name { get; set; } = string.Empty;
            internal string Version { get; set; } = string.Empty;
            internal decimal Build { get; set; } = default;
            internal bool IsWin10 => Name.Contains("10");
            internal bool IsWin11 => Name.Contains("11");
        }

        internal sealed class BiosInfo
        {
            internal string Data { get; set; } = string.Empty;
            internal string Mode { get; set; } = string.Empty;
        }

        internal sealed class ProcessorInfo
        {
            internal string Data { get; set; } = string.Empty;
            internal double Usage { get; set; } = default;
            internal string Cores { get; set; } = string.Empty;
            internal string Threads { get; set; } = string.Empty;
            internal string DetailedData { get; set; } = string.Empty;
            internal string Manufacturer { get; set; } = string.Empty;
            internal string Architecture { get; set; } = string.Empty;
            internal string MaxClockSpeed { get; set; } = string.Empty;
            internal string SocketDesignation { get; set; } = string.Empty;
            internal string L2CacheSize { get; set; } = string.Empty;
            internal string L3CacheSize { get; set; } = string.Empty;
        }

        internal sealed class GpuInfo
        {
            internal string Data { get; set; } = string.Empty;
            internal int Usage { get; set; } = default;
        }

        internal sealed class MemoryInfo
        {
            internal string Data { get; set; } = string.Empty;
            internal double Usage { get; set; } = default;
            internal double Total { get; set; } = default;
            internal string Type { get; set; } = string.Empty;
        }

        #endregion

        #region Enums

        internal enum ConnectionStatus { Available, Lose, Block, Limited }

        #endregion

        #region Static Data Instances

        internal static OperatingSystemInfo OS { get; set; } = new OperatingSystemInfo();
        internal static BiosInfo Bios { get; set; } = new BiosInfo();
        internal static ProcessorInfo Processor { get; set; } = new ProcessorInfo();
        internal static GpuInfo Gpu { get; set; } = new GpuInfo();
        internal static MemoryInfo Memory { get; set; } = new MemoryInfo();

        #endregion

        #region Static State Properties

        internal static ImageSource? Wallpaper { get; set; } = default;

        internal static string RunningProcessesCount { get; set; } = string.Empty;
        internal static string RunningServicesCount { get; set; } = string.Empty;
        internal static string Motherboard { get; set; } = string.Empty;
        internal static string Graphics { get; set; } = string.Empty;
        internal static string Storage { get; set; } = string.Empty;
        internal static string AudioDevice { get; set; } = string.Empty;
        internal static string NetworkAdapter { get; set; } = string.Empty;
        internal static string UserIPAddress { get; set; } = string.Empty;
        internal static string LocalIPAddress { get; set; } = string.Empty;
        internal static ConnectionStatus CurrentConnection = ConnectionStatus.Lose;

        #endregion

        #region Vendor Detection

        internal static class VendorDetection
        {
            internal static bool Nvidia { get; set; } = default;
            internal static bool Realtek { get; set; } = default;
        }

        #endregion

        #region Disk Type Labels

        internal static class DiskTypeLabels
        {
            internal const string Unspecified = "(Unspecified)";
            internal const string SCM = "(SCM)";
            internal const string HDD = "(HDD)";
            internal const string SSD = "(SSD)";
            internal const string NVMe = "(NVMe SSD)";
            internal const string SCSI = "(SCSI)";
            internal const string USB = "(USB-Media)";
            internal const string SD = "(SD-Card)";
            internal const string CD = "(CD/DVD)";
            internal const string VHD = "(VHD)";
            internal const string VHDX = "(VHDX)";
        }

        #endregion

        #region Cleanup Logic

        internal static void ClearResources()
        {
            Wallpaper = null;
        }

        #endregion
    }
}