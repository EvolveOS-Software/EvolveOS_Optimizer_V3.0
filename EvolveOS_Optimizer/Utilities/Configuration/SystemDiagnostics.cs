// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    internal sealed class SystemDiagnostics : MonitoringService
    {
        private static readonly object _wmiLock = new object();
        private static readonly HttpClient _updateClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private ImageSource? _cachedAvatarSource;
        private string? _cachedAvatarPath;

        internal static bool IsElevated => IsRunningAsAdmin();
        internal static bool IsNeedUpdate { get; private set; } = false;
        internal static string DownloadVersion { get; private set; } = string.Empty;
        internal static bool isIPAddressFormatValid = false, isMsftAvailable = false;

        internal string? WallpaperPath { get; private set; }
        internal string? AvatarPath { get; private set; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        private static ulong _prevIdleTime;
        private static ulong _prevKernelTime;
        private static ulong _prevUserTime;
        private static DateTime _lastCpuCheck = DateTime.MinValue;
        private static double _lastCpuUsage = 0.0;
        private static readonly object _cpuLock = new object();

        private static readonly (object[] Keys, string Type)[] MediaTypeMap = new (object[] Keys, string Type)[]
        {
            (new object[] { (ushort)3, "Removable Media" }, "HDD"),
            (new object[] { (ushort)4, "Fixed hard disk media" }, "SSD"),
            (new object[] { (ushort)5, "Unspecified" }, "SCM")
        };

        private static readonly Dictionary<ushort, string> BusTypeMap = new Dictionary<ushort, string>()
        {
            { 7,  "USB" },
            { 12, "SD" },
            { 17, "NVMe" }
        };

        internal static void InitCpuBaseline()
        {
            if (GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
            {
                _prevIdleTime = ((ulong)idleTime.dwHighDateTime << 32) | idleTime.dwLowDateTime;
                _prevKernelTime = ((ulong)kernelTime.dwHighDateTime << 32) | kernelTime.dwLowDateTime;
                _prevUserTime = ((ulong)userTime.dwHighDateTime << 32) | userTime.dwLowDateTime;
            }
        }

        internal static bool IsRunningAsAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        internal static (string Code, string Region) GetCurrentSystemLang()
        {
            CultureInfo culture = CultureInfo.CurrentUICulture;
            string[] parts = culture.Name.Split('-');
            return (culture.TwoLetterISOLanguageName.ToLowerInvariant(), parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty);
        }

        internal ImageSource? GetProfileImage()
        {
            try
            {
                string sid = WindowsIdentity.GetCurrent().User?.Value ?? "";
                string regKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}";

                using var key = Registry.LocalMachine.OpenSubKey(regKey);
                string? avatarPath = key?.GetValue("Image1080")?.ToString();

                if (_cachedAvatarSource != null && avatarPath == _cachedAvatarPath)
                {
                    return _cachedAvatarSource;
                }

                if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath) && new FileInfo(avatarPath).Length > 0)
                {
                    var bitmap = new BitmapImage();
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.DecodePixelWidth = 200;
                    bitmap.UriSource = new Uri(avatarPath);

                    _cachedAvatarPath = avatarPath;
                    _cachedAvatarSource = bitmap;

                    Debug.WriteLine($"[Diagnostics] Avatar loaded and cached: {avatarPath}");
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Diagnostics] Profile Image Error: {ex.Message}");
            }

            if (Application.Current.Resources.ContainsKey("Icon_ProfileAvatar"))
            {
                return Application.Current.Resources["Icon_ProfileAvatar"] as ImageSource;
            }

            return null;
        }

        internal string GetProfileName()
        {
            string nameProfile = string.Empty;
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", $"select FullName from Win32_UserAccount where domain='{Environment.UserDomainName}' and name='{Environment.UserName.ToLowerInvariant()}'", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            nameProfile = managementObj["FullName"] as string ?? string.Empty;
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[Diagnostics] WMI User Error: {ex.Message}"); }
            }
            return !string.IsNullOrWhiteSpace(nameProfile) ? nameProfile : Environment.UserName.ToLowerInvariant();
        }

        internal void GetHardwareData()
        {
            Task.Run(() =>
            {
                lock (_wmiLock)
                {
                    try
                    {
                        using var managementObj = new ManagementObjectSearcher(@"root\microsoft\windows\storage", "select FriendlyName from MSFT_PhysicalDisk", new System.Management.EnumerationOptions { ReturnImmediately = true });
                        using var results = managementObj?.Get();
                        isMsftAvailable = results?.Count > 0;
                    }
                    catch { isMsftAvailable = false; }
                }

                Parallel.Invoke(
                    GetOperatingSystemInfo,
                    GetWallpaperImage,
                    GetBiosInfo,
                    GetMotherboardInfo,
                    GetProcessorInfo,
                    GetGraphicsInfo,
                    GetMemoryInfo,
                    () => GetUserIpAddress().GetAwaiter().GetResult(),
                    () => RefreshDevicesData(DeviceType.All)
                );
            });
        }

        internal void RefreshDevicesData(DeviceType deviceType = DeviceType.All)
        {
            if (deviceType == DeviceType.Storage || deviceType == DeviceType.All)
                Storage = GetStorageDevices();

            if (deviceType == DeviceType.Audio || deviceType == DeviceType.All)
                AudioDevice = GetAudioDevices();

            if (deviceType == DeviceType.Network || deviceType == DeviceType.All)
                NetworkAdapter = GetNetworkAdapters();
        }

        public void GetWallpaperImage()
        {
            try
            {
                string wallpaperPath = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", string.Empty)?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
                {
                    string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes");
                    if (Directory.Exists(cacheFolder))
                    {
                        wallpaperPath = Directory.GetFiles(cacheFolder, "TranscodedWallpaper*")
                            .Select(f => new FileInfo(f))
                            .Where(f => f.Exists)
                            .OrderByDescending(f => f.LastWriteTime)
                            .FirstOrDefault()?.FullName ?? string.Empty;
                    }
                }

                if (!string.IsNullOrWhiteSpace(wallpaperPath) && File.Exists(wallpaperPath))
                {
                    WallpaperPath = wallpaperPath;
                }
                else
                {
                    WallpaperPath = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Diagnostics] Wallpaper Error: {ex.Message}");
                WallpaperPath = null;
            }
        }

        internal string? GetProfileAvatarPath()
        {
            try
            {
                string sid = WindowsIdentity.GetCurrent().User?.Value ?? "";
                string regPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}";
                string? path = Registry.GetValue(regPath, "Image1080", string.Empty)?.ToString();

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return path;
                }
            }
            catch { }
            return null;
        }

        internal void GetOperatingSystemInfo()
        {
            string regPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            int revisionNumber = Convert.ToInt32(Registry.GetValue(regPath, "UBR", 0) ?? 0);
            string release = Registry.GetValue(regPath, "DisplayVersion", string.Empty)?.ToString() ?? string.Empty;

            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Caption, Description, OSArchitecture, BuildNumber, Version from Win32_OperatingSystem", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            string data = new string[] { "Caption", "Description" }.Select(p => managementObj[p] as string).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "Windows";

                            HardwareData.OS.Name = $"{(data.Contains('W') ? data.Substring(data.IndexOf('W')) : data)} {Regex.Replace((string)managementObj["OSArchitecture"], @"\-.+", "-bit")} {(!string.IsNullOrWhiteSpace(release) ? $"({release})" : string.Empty)}";
                            HardwareData.OS.Version = $"{(string)managementObj["Version"]}.{revisionNumber}";

                            string buildRaw = $"{Convert.ToString(managementObj["BuildNumber"])}.{revisionNumber}";

                            if (decimal.TryParse(buildRaw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal result))
                            {
                                HardwareData.OS.Build = result;
                            }
                            else if (decimal.TryParse(Registry.GetValue(regPath, "CurrentBuild", "0")?.ToString(), out result))
                            {
                                HardwareData.OS.Build = result;
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[Diagnostics] OS Info Error: {ex.Message}"); }
            }
        }

        private void GetBiosInfo()
        {
            Bios.Mode = Environment.GetEnvironmentVariable("firmware_type") ?? (Directory.Exists(@"C:\Windows\Boot\EFI") ? "UEFI" : "Legacy Boot");
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Name, Caption, SMBIOSBIOSVersion, SerialNumber from Win32_BIOS", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    var biosEntries = new List<string>();
                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            string data = managementObj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown BIOS";
                            string dataSN = managementObj["SerialNumber"]?.ToString()?.Trim() ?? "";
                            bool isValidSN = !string.IsNullOrWhiteSpace(dataSN) && !dataSN.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase);
                            biosEntries.Add(isValidSN ? $"{data}, S/N-{dataSN}" : data);
                        }
                    }
                    Bios.Data = string.Join(Environment.NewLine, biosEntries);
                }
                catch { Bios.Data = "Unavailable"; }
            }
        }

        private void GetMotherboardInfo()
        {
            lock (_wmiLock)
            {
                try
                {
                    string mbName = string.Empty;
                    string mbVersion = string.Empty;
                    string mbSerial = string.Empty;
                    string mbChipset = string.Empty;

                    using (var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Manufacturer, Product, Version, SerialNumber from Win32_BaseBoard", new System.Management.EnumerationOptions { ReturnImmediately = true }))
                    {
                        foreach (ManagementObject managementObj in searcher.Get().Cast<ManagementObject>())
                        {
                            using (managementObj)
                            {
                                mbName = $"{managementObj["Manufacturer"]?.ToString() ?? string.Empty} {managementObj["Product"]?.ToString() ?? string.Empty}".Trim();
                                mbVersion = managementObj["Version"]?.ToString()?.Trim() ?? string.Empty;
                                mbSerial = managementObj["SerialNumber"]?.ToString()?.Trim() ?? string.Empty;
                            }
                        }
                    }

                    string[] registryPaths = { @"SYSTEM\CurrentControlSet\Enum\PCI", @"SYSTEM\CurrentControlSet\Enum\ACPI" };

                    foreach (string rootPath in registryPaths)
                    {
                        bool isPci = rootPath.IndexOf("PCI", StringComparison.OrdinalIgnoreCase) >= 0;

                        List<string>? devices = RegistryHelp.GetSubKeyNames<List<string>>(Registry.LocalMachine, rootPath);
                        if (devices == null) continue;

                        foreach (string deviceId in devices)
                        {
                            string devicePath = rootPath + @"\" + deviceId;
                            List<string>? instances = RegistryHelp.GetSubKeyNames<List<string>>(Registry.LocalMachine, devicePath);
                            if (instances == null) continue;

                            foreach (string instanceId in instances)
                            {
                                string driverRef = RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\{devicePath}\{instanceId}", "Driver", string.Empty);
                                if (string.IsNullOrEmpty(driverRef)) continue;

                                string driverDesc = RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{driverRef}", "DriverDesc", string.Empty);
                                if (string.IsNullOrEmpty(driverDesc) || driverDesc.TrimStart().StartsWith("@", StringComparison.Ordinal))
                                {
                                    continue;
                                }

                                bool match = isPci
                                    ? (driverDesc.IndexOf("LPC", StringComparison.OrdinalIgnoreCase) >= 0 || driverDesc.IndexOf("eSPI", StringComparison.OrdinalIgnoreCase) >= 0)
                                    : (driverDesc.IndexOf("Qualcomm", StringComparison.OrdinalIgnoreCase) >= 0 || driverDesc.IndexOf("Snapdragon", StringComparison.OrdinalIgnoreCase) >= 0);

                                if (!match) continue;

                                string chipset = ParseChipset(driverDesc) ?? string.Empty;

                                if (!string.IsNullOrWhiteSpace(chipset))
                                {
                                    mbChipset = chipset;
                                    goto BuildString;
                                }
                            }
                        }
                    }

                BuildString:
                    List<string> details = new List<string>();
                    if (!string.IsNullOrWhiteSpace(mbVersion)) details.Add($"V{mbVersion}");
                    if (!string.IsNullOrWhiteSpace(mbChipset)) details.Add($"Chipset: {mbChipset}");
                    if (!string.IsNullOrWhiteSpace(mbSerial)) details.Add($"S/N: {mbSerial}");

                    if (details.Count > 0)
                    {
                        Motherboard = $"{mbName} ({string.Join(", ", details)})";
                    }
                    else
                    {
                        Motherboard = mbName;
                    }
                }
                catch
                {
                    Motherboard = "Unavailable";
                }
            }
        }

        private void GetProcessorInfo()
        {
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2",
                        "select Name, Manufacturer, Architecture, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, SocketDesignation, L2CacheSize, L3CacheSize from Win32_Processor",
                        new System.Management.EnumerationOptions { ReturnImmediately = true });

                    using var results = searcher.Get();
                    var sb = new StringBuilder(512);

                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            Processor.Data = (string)managementObj["Name"];
                            Processor.Cores = Convert.ToString(managementObj["NumberOfCores"]) ?? "0";
                            Processor.Threads = Convert.ToString(managementObj["NumberOfLogicalProcessors"]) ?? "0";

                            sb.AppendLine($"Name: {managementObj["Name"]}");
                            sb.AppendLine($"Manufacturer: {managementObj["Manufacturer"]}");
                            sb.AppendLine($"Architecture: {managementObj["Architecture"]}");
                            sb.AppendLine($"Cores: {managementObj["NumberOfCores"]}");
                            sb.AppendLine($"Logical Processors: {managementObj["NumberOfLogicalProcessors"]}");
                            sb.AppendLine($"Max Speed: {managementObj["MaxClockSpeed"]} MHz");
                            sb.AppendLine($"Socket Designation: {managementObj["SocketDesignation"]}");
                            sb.AppendLine($"L2 Cache: {managementObj["L2CacheSize"]} KB");
                            sb.Append($"L3 Cache: {managementObj["L3CacheSize"]} KB");
                        }
                    }

                    Processor.DetailedData = sb.ToString();
                }
                catch { }
            }
        }

        public static async Task<int> GetGpuUsage()
        {
            return await Task.Run(() =>
            {
                lock (_wmiLock)
                {
                    try
                    {
                        long totalUsage = 0;

                        var scope = new ManagementScope(@"root\cimv2");
                        var query = new ObjectQuery("SELECT UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%engtype_3D'");

                        var options = new System.Management.EnumerationOptions { ReturnImmediately = true };

                        using var searcher = new ManagementObjectSearcher(scope, query, options);

                        using var results = searcher.Get();
                        foreach (ManagementObject obj in results)
                        {
                            using (obj)
                            {
                                totalUsage += Convert.ToInt64(obj["UtilizationPercentage"]);
                            }
                        }

                        int finalUsage = (int)Math.Clamp(totalUsage, 0, 100);
                        HardwareData.Gpu.Usage = finalUsage;

                        return finalUsage;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            });
        }

        private void GetGraphicsInfo()
        {
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2",
                        "select Name, AdapterRAM, PNPDeviceID, DriverVersion, VideoArchitecture from Win32_VideoController",
                        new System.Management.EnumerationOptions { ReturnImmediately = true });

                    using var results = searcher.Get();
                    var entries = new List<string>();
                    int gpuNumber = 0;

                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            string data = managementObj["Name"] as string ?? "Unknown GPU";
                            string pnp = managementObj["PNPDeviceID"]?.ToString() ?? "";
                            string driverVersion = managementObj["DriverVersion"]?.ToString() ?? "Unknown";
                            string videoArch = managementObj["VideoArchitecture"]?.ToString() ?? "Unknown";

                            var (isFound, dataMemoryReg, driverDesc) = GetMemorySizeFromRegistry(data);

                            string displayName = (!string.IsNullOrEmpty(driverDesc)) ? driverDesc : data;
                            string displayRAM = isFound ? dataMemoryReg : (managementObj["AdapterRAM"] != null ? SizeCalculationHelper(Convert.ToUInt64(managementObj["AdapterRAM"])) : "N/A");

                            var sb = new StringBuilder();
                            if (gpuNumber > 0) sb.AppendLine();

                            sb.AppendLine($"GPU {gpuNumber}:");
                            sb.AppendLine($"   Name: {displayName}");
                            sb.AppendLine($"   Adapter RAM: {displayRAM}");
                            sb.AppendLine($"   Driver Version: {driverVersion}");
                            sb.Append($"   Video Architecture: {videoArch}");

                            entries.Add(sb.ToString());

                            VendorDetection.Nvidia |= pnp.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) >= 0;
                            gpuNumber++;
                        }
                    }

                    HardwareData.Gpu.Data = entries.Count > 0
                        ? string.Join(Environment.NewLine, entries)
                        : "No GPU detected";
                }
                catch
                {
                    HardwareData.Gpu.Data = "Unavailable";
                }
            }
        }

        private (bool Found, string Size, string Desc) GetMemorySizeFromRegistry(string name)
        {
            try
            {
                using RegistryKey? baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                if (baseKey == null) return (false, "", "");

                foreach (string subKeyName in baseKey.GetSubKeyNames())
                {
                    if (subKeyName == "Properties") continue;
                    using RegistryKey? regKey = baseKey.OpenSubKey(subKeyName);
                    string driverDesc = regKey?.GetValue("DriverDesc")?.ToString() ?? "";

                    if (driverDesc.Contains(name, StringComparison.OrdinalIgnoreCase))
                    {
                        object? memorySizeValue = regKey?.GetValue("HardwareInformation.qwMemorySize") ?? regKey?.GetValue("HardwareInformation.MemorySize");
                        if (memorySizeValue != null)
                        {
                            return (true, SizeCalculationHelper(Convert.ToUInt64(memorySizeValue)), driverDesc);
                        }
                    }
                }
            }
            catch { }
            return (false, "", "");
        }

        private void GetMemoryInfo()
        {
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Manufacturer, Capacity, Speed, SMBIOSMemoryType from Win32_PhysicalMemory", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    var entries = new List<string>();
                    ulong totalCapacity = 0;
                    int memoryTypeCode = 0;

                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            ulong cap = Convert.ToUInt64(managementObj["Capacity"]);
                            totalCapacity += cap;

                            string manufacturer = managementObj["Manufacturer"]?.ToString() ?? "Unknown";
                            string capacity = SizeCalculationHelper(cap);
                            string speed = managementObj["Speed"]?.ToString() ?? "0";

                            entries.Add($"{manufacturer}, {capacity} @ {speed} MHz");

                            if (memoryTypeCode == 0 && managementObj["SMBIOSMemoryType"] != null)
                            {
                                int.TryParse(managementObj["SMBIOSMemoryType"].ToString(), out memoryTypeCode);
                            }
                        }
                    }

                    HardwareData.Memory.Data = string.Join(Environment.NewLine, entries);
                    HardwareData.Memory.Total = totalCapacity / (1024.0 * 1024.0 * 1024.0);
                    HardwareData.Memory.Type = MapSmbiosMemoryType(memoryTypeCode);
                }
                catch
                {
                    HardwareData.Memory.Data = "Unavailable";
                    HardwareData.Memory.Type = "Unknown";
                }
            }
        }

        private string MapSmbiosMemoryType(int code)
        {
            return code switch
            {
                20 => "DDR",
                21 => "DDR2",
                24 => "DDR3",
                26 => "DDR4",
                30 => "LPDDR4",
                34 => "DDR5",
                35 => "LPDDR5",
                0 => "Unknown",
                _ => $"DDR ({code})"
            };
        }

        private string GetStorageDevices()
        {
            StringBuilder result = new StringBuilder();
            lock (_wmiLock)
            {
                try
                {
                    if (isMsftAvailable)
                    {
                        using var searcher = new ManagementObjectSearcher(@"root\microsoft\windows\storage", "select FriendlyName, Size, MediaType, BusType from MSFT_PhysicalDisk", new System.Management.EnumerationOptions { ReturnImmediately = true });
                        using var results = searcher.Get();
                        foreach (ManagementObject managementObj in results)
                        {
                            using (managementObj)
                            {
                                string name = managementObj["FriendlyName"]?.ToString() ?? "Disk";
                                string size = SizeCalculationHelper(Convert.ToUInt64(managementObj["Size"] ?? 0));

                                ushort mediaType = managementObj["MediaType"] != null ? Convert.ToUInt16(managementObj["MediaType"]) : (ushort)0;
                                ushort busType = managementObj["BusType"] != null ? Convert.ToUInt16(managementObj["BusType"]) : (ushort)0;

                                string typeLabel = busType == 17 ? "NVMe" : busType == 7 ? "USB" : mediaType == 4 ? "SSD" : mediaType == 3 ? "HDD" : "Drive";

                                result.AppendLine($"{size} [{name}] ({typeLabel})");
                            }
                        }
                    }
                    else
                    {
                        using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Model, Size, MediaType, InterfaceType from Win32_DiskDrive", new System.Management.EnumerationOptions { ReturnImmediately = true });
                        using var results = searcher.Get();
                        foreach (ManagementObject managementObj in results)
                        {
                            using (managementObj)
                            {
                                string name = managementObj["Model"]?.ToString() ?? "Disk";
                                string size = SizeCalculationHelper(Convert.ToUInt64(managementObj["Size"] ?? 0));

                                string interfaceType = managementObj["InterfaceType"]?.ToString() ?? string.Empty;
                                string typeLabel = interfaceType.Contains("USB") ? "USB" : name.Contains("NVMe") ? "NVMe" : "Drive";

                                result.AppendLine($"{size} [{name}] ({typeLabel})");
                            }
                        }
                    }
                }
                catch { }
            }
            return result.ToString().Trim();
        }

        internal static (string Health, string Type, string Temp) GetDriveSmartInfo(string driveLetter)
        {
            string healthStatus = "Good";
            string driveType = "Drive";
            string driveTemp = "--";

            driveLetter = driveLetter.Replace("\\", "").Replace(":", "").ToUpper().Trim();

            try
            {
                if (isMsftAvailable)
                {
                    string diskNumber = "-1";

                    try
                    {
                        using var partitionSearcher = new ManagementObjectSearcher(@"root\microsoft\windows\storage", $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{driveLetter}'");
                        foreach (ManagementObject part in partitionSearcher.Get())
                        {
                            diskNumber = part["DiskNumber"]?.ToString() ?? "-1";
                            break;
                        }
                    }
                    catch { }

                    if (diskNumber != "-1")
                    {
                        using var diskSearcher = new ManagementObjectSearcher(@"root\microsoft\windows\storage", $"SELECT HealthStatus, MediaType, BusType FROM MSFT_PhysicalDisk WHERE DeviceId='{diskNumber}'");
                        foreach (ManagementObject obj in diskSearcher.Get())
                        {
                            ushort health = obj["HealthStatus"] != null ? Convert.ToUInt16(obj["HealthStatus"]) : (ushort)0;
                            healthStatus = health == 0 ? "Good" : "Warning";

                            ushort mediaType = obj["MediaType"] != null ? Convert.ToUInt16(obj["MediaType"]) : (ushort)0;
                            ushort busType = obj["BusType"] != null ? Convert.ToUInt16(obj["BusType"]) : (ushort)0;

                            if (busType == 17) driveType = "NVMe SSD";
                            else if (busType == 7) driveType = "USB Drive";
                            else if (mediaType == 4) driveType = "SATA SSD";
                            else if (mediaType == 3) driveType = "HDD";
                            else driveType = "Drive";

                            break;
                        }

                        try
                        {
                            using var tempSearcher = new ManagementObjectSearcher(@"root\microsoft\windows\storage", $"SELECT Temperature FROM MSFT_StorageReliabilityCounter WHERE DeviceId='{diskNumber}'");
                            foreach (ManagementObject obj in tempSearcher.Get())
                            {
                                if (obj["Temperature"] != null)
                                {
                                    int t = Convert.ToInt32(obj["Temperature"]);
                                    if (t > 0) driveTemp = $"{t}°C";
                                }
                                break;
                            }
                        }
                        catch { }

                        return (healthStatus, driveType, driveTemp);
                    }
                }

                string win32Index = "0";
                try
                {
                    using var partSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}:'}} VIA Win32_LogicalDiskToPartition");
                    foreach (ManagementObject part in partSearcher.Get())
                    {
                        using var driveSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} VIA Win32_DiskDriveToDiskPartition");
                        foreach (ManagementObject drive in driveSearcher.Get())
                        {
                            win32Index = drive["Index"]?.ToString() ?? "0";
                            break;
                        }
                        break;
                    }
                }
                catch { }

                using var searcherFallback = new ManagementObjectSearcher($"SELECT Status, Model, InterfaceType FROM Win32_DiskDrive WHERE Index={win32Index}");
                foreach (ManagementObject obj in searcherFallback.Get())
                {
                    string status = obj["Status"]?.ToString() ?? "OK";
                    healthStatus = status.Equals("OK", StringComparison.OrdinalIgnoreCase) ? "Good" : "Warning";

                    string model = obj["Model"]?.ToString() ?? "";
                    string interfaceType = obj["InterfaceType"]?.ToString() ?? "";

                    if (model.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0) driveType = "NVMe SSD";
                    else if (model.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0) driveType = "SATA SSD";
                    else if (interfaceType.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0) driveType = "USB Drive";
                    else driveType = "HDD";

                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemDiagnostics] SMART Error: {ex.Message}");
                healthStatus = "Error";
            }

            return (healthStatus, driveType, driveTemp);
        }

        internal static List<string> GetSiblingDrives(string driveLetter)
        {
            var siblings = new List<string>();
            try
            {
                driveLetter = driveLetter.Replace("\\", "").Replace(":", "").ToUpper().Trim();
                if (string.IsNullOrEmpty(driveLetter)) return siblings;

                if (isMsftAvailable)
                {
                    string diskNumber = "-1";
                    using var partSearcher = new ManagementObjectSearcher(@"root\microsoft\windows\storage", $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{driveLetter}'");
                    foreach (ManagementObject part in partSearcher.Get())
                    {
                        diskNumber = part["DiskNumber"]?.ToString() ?? "-1";
                        break;
                    }

                    if (diskNumber != "-1")
                    {
                        using var siblingSearcher = new ManagementObjectSearcher(@"root\microsoft\windows\storage", $"SELECT DriveLetter FROM MSFT_Partition WHERE DiskNumber = {diskNumber}");
                        foreach (ManagementObject part in siblingSearcher.Get())
                        {
                            string l = part["DriveLetter"]?.ToString() ?? "";

                            if (!string.IsNullOrWhiteSpace(l) && !l.Contains('\0'))
                            {
                                siblings.Add(l.ToUpper() + ":");
                            }
                        }
                        if (siblings.Count > 0) return siblings.Distinct().ToList();
                    }
                }

                string physicalDeviceId = "";

                using var logSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}:'}} VIA Win32_LogicalDiskToPartition");
                foreach (ManagementObject part in logSearcher.Get())
                {
                    using var driveSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} VIA Win32_DiskDriveToDiskPartition");
                    foreach (ManagementObject drive in driveSearcher.Get())
                    {
                        physicalDeviceId = drive["DeviceID"]?.ToString() ?? "";
                        break;
                    }
                    if (!string.IsNullOrEmpty(physicalDeviceId)) break;
                }

                if (!string.IsNullOrEmpty(physicalDeviceId))
                {
                    string escapedDeviceId = physicalDeviceId.Replace("\\", "\\\\");

                    using var drivePartSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{escapedDeviceId}'}} VIA Win32_DiskDriveToDiskPartition");
                    foreach (ManagementObject part in drivePartSearcher.Get())
                    {
                        using var logDiskSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} VIA Win32_LogicalDiskToPartition");
                        foreach (ManagementObject logDisk in logDiskSearcher.Get())
                        {
                            string devId = logDisk["DeviceID"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(devId))
                            {
                                siblings.Add(devId.ToUpper());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetSiblingDrives Error] {ex.Message}");
            }

            return siblings.Distinct().ToList();
        }

        private string GetAudioDevices()
        {
            StringBuilder result = new StringBuilder();

            static (bool isUsb, string name) IsUsbAudioDevice(string deviceID)
            {
                foreach (string basePath in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render", @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture" })
                {
                    using RegistryKey? regKey = Registry.LocalMachine.OpenSubKey(basePath);
                    if (regKey != null)
                    {
                        foreach (string subKeyName in regKey.GetSubKeyNames())
                        {
                            string propsPath = $@"HKEY_LOCAL_MACHINE\{basePath}\{subKeyName}\Properties";
                            using RegistryKey? subKey = regKey.OpenSubKey(subKeyName + @"\Properties");
                            if (subKey != null)
                            {
                                string valueID = Registry.GetValue(propsPath, "{b3f8fa53-0004-438e-9003-51a46e139bfc},2", string.Empty)?.ToString() ?? string.Empty;

                                if (!string.IsNullOrEmpty(valueID) && valueID.IndexOf(deviceID, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    string nameValue6 = Registry.GetValue(propsPath, "{b3f8fa53-0004-438e-9003-51a46e139bfc},6", string.Empty)?.ToString()?.Trim() ?? string.Empty;
                                    string typeNameValue2 = Registry.GetValue(propsPath, "{a45c254e-df1c-4efd-8020-67d146a850e0},2", string.Empty)?.ToString()?.Trim() ?? string.Empty;

                                    string name = nameValue6.Length > 10 && !string.Equals(nameValue6, typeNameValue2, StringComparison.OrdinalIgnoreCase) ? nameValue6 : $"{typeNameValue2} {nameValue6}".Trim();
                                    return (true, name);
                                }
                            }
                        }
                    }
                }
                return (false, string.Empty);
            }

            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select DeviceID, Name, Caption, PNPDeviceID from Win32_SoundDevice where Status = 'OK'", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            (bool isUsbDevice, string usbName) = IsUsbAudioDevice(managementObj["DeviceID"]?.ToString() ?? string.Empty);

                            if (isUsbDevice && !string.IsNullOrEmpty(usbName))
                            {
                                result.AppendLine(usbName);
                            }
                            else
                            {
                                string wmiName = new[] { "Name", "Caption" }.Select(prop => managementObj[prop] as string).FirstOrDefault(info => !string.IsNullOrEmpty(info)) ?? "Audio Device";
                                result.AppendLine(wmiName);
                            }

                            string pnpId = managementObj["PNPDeviceID"]?.ToString() ?? string.Empty;
                            VendorDetection.Realtek |= pnpId.IndexOf("VEN_10EC", StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                    }
                }
                catch { }
            }
            return result.ToString().Trim();
        }

        private string GetNetworkAdapters()
        {
            StringBuilder result = new StringBuilder();
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Name from Win32_NetworkAdapter where NetConnectionStatus=2");
                    using var results = searcher.Get();
                    foreach (ManagementObject managementObj in results)
                    {
                        using (managementObj)
                        {
                            result.AppendLine(managementObj["Name"]?.ToString() ?? "Network Adapter");
                        }
                    }
                }
                catch { }
            }
            return result.ToString().Trim();
        }

        internal static bool IsNetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();

        internal string GetDefaultLocalIP()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                string ip = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";

                HardwareData.LocalIPAddress = ip;
                return ip;
            }
            catch
            {
                HardwareData.LocalIPAddress = "0.0.0.0";
                return "0.0.0.0";
            }
        }

        internal async Task GetUserIpAddress(CancellationToken token = default)
        {
            if (IsNetworkAvailable())
            {
                try
                {
                    UserIPAddress = await _updateClient.GetStringAsync("https://api.ipify.org", token);
                }
                catch { UserIPAddress = "Offline"; }
            }
            else { UserIPAddress = "No Network"; }
            isIPAddressFormatValid = UserIPAddress.Any(char.IsDigit);
        }

        internal static async Task ValidateVersionUpdatesAsync(CancellationToken token = default)
        {
            if (!SettingsEngine.IsUpdateCheckRequired || !IsNetworkAvailable())
            {
                return;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, PathLocator.Links.GitHubApi);
                request.Headers.Add("User-Agent", "EvolveOS-Optimizer-Updater");

                using var response = await _updateClient.SendAsync(request, token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var git = JsonConvert.DeserializeObject<GitMetadata>(json);

                    if (git?.СurrentVersion != null && git.СurrentVersion.CompareTo(SettingsEngine.currentRelease) > 0)
                    {
                        IsNeedUpdate = true;
                        DownloadVersion = git.СurrentVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update Check] Failed: {ex.Message}");
                IsNeedUpdate = false;
            }
        }

        private static string SizeCalculationHelper<T>(T sizeInBytes) where T : struct, IConvertible
        {
            decimal bytes = Convert.ToDecimal(sizeInBytes);
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unitIndex = 0;
            while (bytes >= 1024 && unitIndex < units.Length - 1) { bytes /= 1024; unitIndex++; }
            return $"{Math.Round(bytes, 2)} {units[unitIndex]}";
        }

        internal async Task<string> GetProcessCountAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return Process.GetProcesses().Length.ToString();
                }
                catch
                {
                    return "0";
                }
            });
        }

        internal new async Task<string> GetServicesCount()
        {
            return await Task.Run(() =>
            {
                int count = 0;
                var allServices = System.ServiceProcess.ServiceController.GetServices();

                foreach (var svc in allServices)
                {
                    try
                    {
                        if (svc.Status == System.ServiceProcess.ServiceControllerStatus.Running &&
                            (svc.ServiceType.HasFlag(System.ServiceProcess.ServiceType.Win32OwnProcess) ||
                             svc.ServiceType.HasFlag(System.ServiceProcess.ServiceType.Win32ShareProcess)))
                        {
                            count++;
                        }
                    }
                    catch { }
                    finally
                    {
                        svc.Dispose();
                    }
                }
                return count.ToString();
            });
        }

        internal new async Task<double> GetTotalProcessorUsage()
        {
            return await Task.Run(() =>
            {
                lock (_cpuLock)
                {
                    if ((DateTime.UtcNow - _lastCpuCheck).TotalMilliseconds < 500)
                    {
                        return _lastCpuUsage;
                    }

                    if (!GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
                    {
                        return 0.0;
                    }

                    ulong curIdleTime = ((ulong)idleTime.dwHighDateTime << 32) | idleTime.dwLowDateTime;
                    ulong curKernelTime = ((ulong)kernelTime.dwHighDateTime << 32) | kernelTime.dwLowDateTime;
                    ulong curUserTime = ((ulong)userTime.dwHighDateTime << 32) | userTime.dwLowDateTime;

                    if (_prevIdleTime == 0)
                    {
                        _prevIdleTime = curIdleTime;
                        _prevKernelTime = curKernelTime;
                        _prevUserTime = curUserTime;
                        _lastCpuCheck = DateTime.UtcNow;
                        return 0.0;
                    }

                    ulong idleDiff = curIdleTime - _prevIdleTime;
                    ulong kernelDiff = curKernelTime - _prevKernelTime;
                    ulong userDiff = curUserTime - _prevUserTime;
                    ulong totalSystemTime = kernelDiff + userDiff;

                    _prevIdleTime = curIdleTime;
                    _prevKernelTime = curKernelTime;
                    _prevUserTime = curUserTime;
                    _lastCpuCheck = DateTime.UtcNow;

                    if (totalSystemTime == 0) return 0.0;

                    double cpuUsage = (totalSystemTime - idleDiff) * 100.0 / totalSystemTime;
                    _lastCpuUsage = Math.Clamp(Math.Round(cpuUsage, 1), 0, 100);

                    return _lastCpuUsage;
                }
            });
        }

        internal new async Task<double> GetPhysicalAvailableMemory()
        {
            return await Task.Run(() =>
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    return (double)memStatus.ullAvailPhys;
                }
                return 0.0;
            });
        }

        internal static double GetMemoryUsagePercentage()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (double)memStatus.dwMemoryLoad;
            }
            return 0.0;
        }

        internal static async Task<double> GetQuickJunkSizeGigabytesAsync()
        {
            return await Task.Run(() =>
            {
                long totalBytes = 0;

                List<string> foldersToCheck = new List<string>
                {
                    Path.GetTempPath(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download")
                };

                string? root = Path.GetPathRoot(Environment.SystemDirectory);
                if (root != null)
                {
                    foldersToCheck.Add(Path.Combine(root, "Windows.old"));
                }

                foreach (string folder in foldersToCheck)
                {
                    totalBytes += GetDirectorySizeSafe(folder, 0, 5);
                }

                double gigabytes = totalBytes / 1024.0 / 1024.0 / 1024.0;
                return Math.Round(gigabytes, 2);
            });
        }

        private static long GetDirectorySizeSafe(string path, int currentDepth, int maxDepth)
        {
            if (string.IsNullOrEmpty(path) || currentDepth > maxDepth || !Directory.Exists(path))
                return 0;

            long size = 0;
            try
            {
                string[] files = Directory.GetFiles(path);
                foreach (string file in files)
                {
                    try { size += new FileInfo(file).Length; } catch { }
                }

                if (currentDepth < maxDepth)
                {
                    string[] dirs = Directory.GetDirectories(path);
                    foreach (string dir in dirs)
                    {
                        try
                        {
                            FileAttributes attributes = File.GetAttributes(dir);
                            if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;
                            size += GetDirectorySizeSafe(dir, currentDepth + 1, maxDepth);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return size;
        }

        internal static double GetVirtualMemoryUsagePercentage()
        {
            MEMORYSTATUSEX memex = new MEMORYSTATUSEX();
            memex.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (GlobalMemoryStatusEx(ref memex))
            {
                double totalVirtual = memex.ullTotalPageFile;
                double usedVirtual = totalVirtual - memex.ullAvailPageFile;

                return Math.Round((usedVirtual / totalVirtual) * 100.0, 1);
            }
            return 0;
        }

        public static double GetTotalPhysicalMemoryGigabytes()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
            }
            return 16.0;
        }

        public static double GetTotalVirtualMemoryGigabytes()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return memStatus.ullTotalPageFile / (1024.0 * 1024.0 * 1024.0);
            }
            return 16.0;
        }

        private string ParseChipset(string rawCaption)
        {
            if (!string.IsNullOrWhiteSpace(rawCaption))
            {
                Match match = Regex.Match(rawCaption, @"\((?<res>(?=[^)]*\d)[^)]{3,})\)|\b(?<res>[A-Z]{1,2}\d{2,4}[A-Z]?)\b");
                if (match.Success)
                {
                    return match.Groups["res"].Value;
                }

                string clean = Regex.Replace(rawCaption, @"\([RTMtm]+\)|(?i:\b(Intel|AMD|NVIDIA|VIA|Series|Chipset|Family|LPC|Controller|Interface|Bridge|Host|Standard)\b)", "");
                clean = Regex.Replace(clean, @"[()\[\]\-\s]+", " ").Trim();

                return clean.Length > 2 ? clean : rawCaption;
            }

            return string.Empty;
        }

        internal new string GetWallpaperPath() => WallpaperPath ?? string.Empty;

        #region Disposal

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cachedAvatarSource = null;
                _cachedAvatarPath = null;

                base.Dispose();
            }
        }

        #endregion
    }

    public sealed class GitMetadata
    {
        [JsonProperty("tag_name")]
        internal string? СurrentVersion { get; set; }
    }
}