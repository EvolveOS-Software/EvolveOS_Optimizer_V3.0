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
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    internal sealed class SystemDiagnostics : MonitoringService
    {
        private static readonly object _wmiLock = new object();
        private static readonly HttpClient _updateClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private static readonly PerformanceCounter _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        internal static bool IsElevated => IsRunningAsAdmin();
        internal static bool IsNeedUpdate { get; private set; } = false;
        internal static string DownloadVersion { get; private set; } = string.Empty;
        internal static bool isIPAddressFormatValid = false, isMsftAvailable = false;

        internal string? WallpaperPath { get; private set; }
        internal string? AvatarPath { get; private set; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
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
            public MEMORYSTATUSEX() { this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

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
                string regPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}";
                string? avatarPath = Registry.GetValue(regPath, "Image1080", string.Empty)?.ToString();

                if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath) && new FileInfo(avatarPath).Length != 0)
                {
                    return new BitmapImage(new Uri(avatarPath));
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Diagnostics] Profile Image Error: {ex.Message}"); }

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
                        nameProfile = managementObj["FullName"] as string ?? string.Empty;
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
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Caption, OSArchitecture, BuildNumber, Version from Win32_OperatingSystem", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    foreach (ManagementObject managementObj in results)
                    {
                        string data = managementObj["Caption"] as string ?? "Windows";
                        HardwareData.OS.Name = $"{data} {Regex.Replace((string)managementObj["OSArchitecture"], @"\-.+", "-bit")} {(!string.IsNullOrWhiteSpace(release) ? $"({release})" : string.Empty)}";
                        HardwareData.OS.Version = $"{(string)managementObj["Version"]}.{revisionNumber}";

                        string buildRaw = $"{Convert.ToString(managementObj["BuildNumber"])}.{revisionNumber}";
                        if (decimal.TryParse(buildRaw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal result))
                        {
                            HardwareData.OS.Build = result;
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
                        string data = managementObj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown BIOS";
                        string dataSN = managementObj["SerialNumber"]?.ToString()?.Trim() ?? "";
                        bool isValidSN = !string.IsNullOrWhiteSpace(dataSN) && !dataSN.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase);
                        biosEntries.Add(isValidSN ? $"{data}, S/N-{dataSN}" : data);
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
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Manufacturer, Product, Version from Win32_BaseBoard", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    var entries = new List<string>();
                    foreach (ManagementObject managementObj in results)
                    {
                        string data = $"{managementObj["Manufacturer"]} {managementObj["Product"]}";
                        string? dataVersion = managementObj["Version"]?.ToString();
                        entries.Add(!string.IsNullOrWhiteSpace(dataVersion) ? $"{data}, V{dataVersion}" : data);
                    }
                    Motherboard = string.Join(Environment.NewLine, entries);
                }
                catch { Motherboard = "Unavailable"; }
            }
        }

        private void GetProcessorInfo()
        {
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Name, NumberOfCores, NumberOfLogicalProcessors from Win32_Processor", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    foreach (ManagementObject managementObj in results)
                    {
                        Processor.Data = (string)managementObj["Name"];
                        Processor.Cores = Convert.ToString(managementObj["NumberOfCores"]) ?? "0";
                        Processor.Threads = Convert.ToString(managementObj["NumberOfLogicalProcessors"]) ?? "0";
                    }
                }
                catch { }
            }
        }

        private void GetGraphicsInfo()
        {
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Name, AdapterRAM, PNPDeviceID from Win32_VideoController", new System.Management.EnumerationOptions { ReturnImmediately = true });
                    using var results = searcher.Get();
                    var entries = new List<string>();

                    foreach (ManagementObject managementObj in results)
                    {
                        string data = managementObj["Name"] as string ?? "Unknown GPU";
                        string pnp = managementObj["PNPDeviceID"]?.ToString() ?? "";

                        var (isFound, dataMemoryReg, driverDesc) = GetMemorySizeFromRegistry(data);

                        string displayName = (!string.IsNullOrEmpty(driverDesc)) ? driverDesc : data;
                        string displayRAM = isFound ? dataMemoryReg : (managementObj["AdapterRAM"] != null ? SizeCalculationHelper(Convert.ToUInt64(managementObj["AdapterRAM"])) : "N/A");

                        entries.Add($"{displayName}, {displayRAM}");
                        VendorDetection.Nvidia |= pnp.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    Graphics = string.Join(Environment.NewLine, entries);
                }
                catch { Graphics = "Unavailable"; }
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

                    foreach (ManagementObject managementObj in results)
                    {
                        ulong cap = Convert.ToUInt64(managementObj["Capacity"]);
                        totalCapacity += cap;
                        string data = managementObj["Manufacturer"]?.ToString() ?? "Unknown RAM";
                        string capacity = SizeCalculationHelper(cap);
                        string speed = managementObj["Speed"]?.ToString() ?? "";
                        entries.Add($"{data}, {capacity} @ {speed}MHz");
                    }
                    Memory.Data = string.Join(Environment.NewLine, entries);

                    HardwareData.Memory.Total = totalCapacity / (1024.0 * 1024.0);
                }
                catch { Memory.Data = "Unavailable"; }
            }
        }

        private string GetStorageDevices()
        {
            StringBuilder result = new StringBuilder();
            lock (_wmiLock)
            {
                try
                {
                    string scope = isMsftAvailable ? @"root\microsoft\windows\storage" : @"root\cimv2";
                    string query = isMsftAvailable ? "select FriendlyName, Size from MSFT_PhysicalDisk" : "select Model, Size from Win32_DiskDrive";

                    using var searcher = new ManagementObjectSearcher(scope, query);
                    foreach (ManagementObject managementObj in searcher.Get())
                    {
                        string name = managementObj[isMsftAvailable ? "FriendlyName" : "Model"]?.ToString() ?? "Disk";
                        string size = SizeCalculationHelper(Convert.ToUInt64(managementObj["Size"]));
                        result.AppendLine($"{size} [{name}]");
                    }
                }
                catch { }
            }
            return result.ToString().Trim();
        }

        private string GetAudioDevices()
        {
            StringBuilder result = new StringBuilder();
            lock (_wmiLock)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "select Name, PNPDeviceID from Win32_SoundDevice where Status = 'OK'");
                    foreach (ManagementObject managementObj in searcher.Get())
                    {
                        result.AppendLine(managementObj["Name"]?.ToString() ?? "Audio Device");
                        string pnpId = managementObj["PNPDeviceID"]?.ToString() ?? string.Empty;
                        VendorDetection.Realtek |= pnpId.IndexOf("VEN_10EC", StringComparison.OrdinalIgnoreCase) >= 0;
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
                    foreach (ManagementObject managementObj in searcher.Get())
                    {
                        result.AppendLine(managementObj["Name"]?.ToString() ?? "Network Adapter");
                    }
                }
                catch { }
            }
            return result.ToString().Trim();
        }

        internal bool IsNetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();

        internal string GetDefaultLocalIP()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
            }
            catch { return "0.0.0.0"; }
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

        internal async Task ValidateVersionUpdatesAsync(CancellationToken token = default)
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

                        NotificationManager.Show("info", "msg_update_available").WithDuration(0).Perform();
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

        internal new async Task<string> GetProcessCount()
        {
            return await Task.Run(() => Process.GetProcesses().Length.ToString());
        }

        internal new async Task<string> GetServicesCount()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var allServices = System.ServiceProcess.ServiceController.GetServices();

                    int runningCount = allServices
                        .Where(s => s.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                        .Where(s => s.ServiceType.HasFlag(System.ServiceProcess.ServiceType.Win32OwnProcess) ||
                                    s.ServiceType.HasFlag(System.ServiceProcess.ServiceType.Win32ShareProcess))
                        .Count();

                    foreach (var svc in allServices)
                    {
                        svc.Dispose();
                    }

                    return runningCount.ToString();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Service Monitor] Error: {ex.Message}");
                    return "0";
                }
            });
        }

        internal new async Task<double> GetTotalProcessorUsage()
        {
            return await Task.Run(() =>
            {
                try { return Math.Round((double)_cpuCounter.NextValue(), 1); }
                catch { return 0.0; }
            });
        }

        internal new async Task<double> GetPhysicalAvailableMemory()
        {
            return await Task.Run(() =>
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return (double)memStatus.ullAvailPhys;
                }
                return 0.0;
            });
        }

        internal new string GetWallpaperPath() => WallpaperPath ?? string.Empty;
    }

    public sealed class GitMetadata
    {
        [JsonProperty("tag_name")]
        internal string? СurrentVersion { get; set; }
    }
}