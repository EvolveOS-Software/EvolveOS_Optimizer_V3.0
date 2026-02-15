using Microsoft.Win32;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    internal class MonitoringService : HardwareData, IDisposable
    {
        private PerformanceCounter? _downloadCounter;
        private PerformanceCounter? _uploadCounter;

        internal event Action<DeviceType>? HandleDevicesEvents;
        private readonly List<(ManagementEventWatcher watcher, EventArrivedEventHandler handler)> _watcherHandler = new();

        internal enum DeviceType
        {
            All,
            Storage,
            Audio,
            Network
        }

        #region P/Invoke Structures
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemTime
        {
            internal uint dwLowDateTime;
            internal uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class MemoryStatus
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
            internal MemoryStatus() => dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatus));
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcesses([Out] uint[] lpidProcess, uint cb, out uint lpcbNeeded);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out SystemTime lpIdleTime, out SystemTime lpKernelTime, out SystemTime lpUserTime);
        #endregion

        public MonitoringService()
        {
            Task.Run(InitializeNetworkCounters);
        }

        internal void InitializeNetworkCounters()
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                NetworkInterface? targetInterface = interfaces
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .FirstOrDefault(nic =>
                        nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

                if (targetInterface != null)
                {
                    string instanceName = targetInterface.Description;

                    instanceName = instanceName.Replace('(', '[').Replace(')', ']')
                                               .Replace('/', '_').Replace('#', '_');

                    if (!PerformanceCounterCategory.InstanceExists(instanceName, "Network Interface"))
                    {
                        var category = new PerformanceCounterCategory("Network Interface");
                        string[] allInstances = category.GetInstanceNames();
                        string sanitizedNameLower = instanceName.ToLower();

                        string? fallbackInstance = allInstances.FirstOrDefault(n => n.ToLower().Contains(sanitizedNameLower));
                        instanceName = fallbackInstance ?? targetInterface.Name;
                    }

                    if (PerformanceCounterCategory.InstanceExists(instanceName, "Network Interface"))
                    {
                        _downloadCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instanceName, true);
                        _uploadCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instanceName, true);

                        _downloadCounter.NextValue();
                        _uploadCounter.NextValue();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkMonitor] Init Error: {ex.Message}");
                _downloadCounter = null;
                _uploadCounter = null;
            }
        }

        internal double GetDownloadSpeed()
        {
            try
            {
                if (_downloadCounter == null) return 0.0;
                float bytesPerSec = _downloadCounter.NextValue();
                return Math.Max(0.0, (double)bytesPerSec / 1024.0 / 1024.0);
            }
            catch { return 0.0; }
        }

        internal double GetUploadSpeed()
        {
            try
            {
                if (_uploadCounter == null) return 0.0;
                float bytesPerSec = _uploadCounter.NextValue();
                return Math.Max(0.0, (double)bytesPerSec / 1024.0 / 1024.0);
            }
            catch { return 0.0; }
        }

        internal async Task<string> GetProcessCount()
        {
            return await Task.Run(() =>
            {
                uint capacity = 1024;
                uint[] buffer = new uint[capacity];

                if (EnumProcesses(buffer, (uint)(buffer.Length * sizeof(uint)), out uint bytesNeeded))
                {
                    uint count = bytesNeeded / sizeof(uint);
                    if (count < capacity) return count.ToString();
                }

                return Process.GetProcesses().Length.ToString();
            });
        }

        internal async Task<string> GetServicesCount()
        {
            return await Task.Run(() =>
            {
                ServiceController[]? allServices = null;
                try
                {
                    allServices = ServiceController.GetServices();
                    int runningCount = 0;

                    foreach (var svc in allServices)
                    {
                        if (svc.Status == ServiceControllerStatus.Running &&
                           (svc.ServiceType.HasFlag(ServiceType.Win32OwnProcess) ||
                            svc.ServiceType.HasFlag(ServiceType.Win32ShareProcess)))
                        {
                            runningCount++;
                        }

                        svc.Dispose();
                    }

                    return runningCount.ToString();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Service Monitor] Error: {ex.Message}");
                    return "0";
                }
                finally
                {
                    if (allServices != null)
                    {
                        foreach (var svc in allServices)
                        {
                            try { svc.Dispose(); } catch { }
                        }
                    }
                }
            });
        }

        internal async Task GetPhysicalAvailableMemory()
        {
            await Task.Run(() =>
            {
                MemoryStatus memStatus = new();
                if (!GlobalMemoryStatusEx(memStatus)) return;

                ulong totalMemoryMb = memStatus.ullTotalPhys / 1048576;
                ulong availMemoryMb = memStatus.ullAvailPhys / 1048576;

                Memory.Usage = (int)((float)(totalMemoryMb - availMemoryMb) / totalMemoryMb * 100);
            });
        }

        internal async Task GetTotalProcessorUsage()
        {
            try
            {
                await Task.Run(async () =>
                {
                    static ulong ConvertTimeToTicks(SystemTime st) => ((ulong)st.dwHighDateTime << 32) | st.dwLowDateTime;

                    if (!GetSystemTimes(out SystemTime idleTime, out SystemTime kernelTime, out SystemTime userTime)) return;

                    ulong idleTicks = ConvertTimeToTicks(idleTime);
                    ulong totalTicks = ConvertTimeToTicks(kernelTime) + ConvertTimeToTicks(userTime);

                    await Task.Delay(1000);

                    if (!GetSystemTimes(out idleTime, out kernelTime, out userTime)) return;

                    ulong newIdleTicks = ConvertTimeToTicks(idleTime);
                    ulong newTotalTicks = ConvertTimeToTicks(kernelTime) + ConvertTimeToTicks(userTime);

                    ulong totalTicksDiff = newTotalTicks - totalTicks;
                    ulong idleTicksDiff = newIdleTicks - idleTicks;

                    if (totalTicksDiff > 0)
                    {
                        Processor.Usage = Math.Min(100, Math.Max(0, (int)(100.0 * (totalTicksDiff - idleTicksDiff) / totalTicksDiff)));
                    }
                });
            }
            catch { Processor.Usage = 1; }
        }

        internal void StartDeviceMonitoring()
        {
            Task.Run(() =>
            {
                var monitorTasks = new List<(string filter, DeviceType type, string? scope)>
                {
                    ($"TargetInstance ISA {(SystemDiagnostics.isMsftAvailable ? "'MSFT_PhysicalDisk'" : "'Win32_DiskDrive'")}",
                      DeviceType.Storage, SystemDiagnostics.isMsftAvailable ? @"root\microsoft\windows\storage" : null),
                    ("TargetInstance ISA 'Win32_SoundDevice'", DeviceType.Audio, null),
                    ("TargetInstance ISA 'Win32_NetworkAdapter' AND TargetInstance.NetConnectionStatus IS NOT NULL",
                      DeviceType.Network, null)
                };

                foreach (var param in monitorTasks)
                {
                    SubscribeToDeviceEvents(param.filter, param.type, param.scope);
                }
            });
        }

        private void SubscribeToDeviceEvents(string filter, DeviceType type, string? scope)
        {
            try
            {
                WqlEventQuery query = new("__InstanceOperationEvent", TimeSpan.FromSeconds(1), filter);
                ManagementEventWatcher watcher = new(new ManagementScope(scope ?? @"root\cimv2"), query);

                void handler(object s, EventArrivedEventArgs e) => HandleDevicesEvents?.Invoke(type);

                watcher.EventArrived += handler;
                watcher.Start();
                lock (_watcherHandler) { _watcherHandler.Add((watcher, handler)); }
            }
            catch (Exception ex) { Debug.WriteLine($"[WMI Watcher] Error: {ex.Message}"); }
        }

        internal void StopDeviceMonitoring()
        {
            lock (_watcherHandler)
            {
                foreach (var item in _watcherHandler)
                {
                    try
                    {
                        item.watcher.EventArrived -= item.handler;
                        item.watcher.Stop();
                        item.watcher.Dispose();
                    }
                    catch { }
                }
                _watcherHandler.Clear();
            }
        }

        public void Dispose()
        {
            StopDeviceMonitoring();

            try
            {
                _downloadCounter?.Close();
                _downloadCounter?.Dispose();
                _downloadCounter = null;

                _uploadCounter?.Close();
                _uploadCounter?.Dispose();
                _uploadCounter = null;
            }
            catch { }

            HandleDevicesEvents = null;

            GC.SuppressFinalize(this);
            Debug.WriteLine("[MonitoringService] Cleanly disposed system handles.");
        }

        internal string GetWallpaperPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                return key?.GetValue("Wallpaper")?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        public ImageSource? GetWallpaperSource()
        {
            try
            {
                string wallpaperPath = GetWallpaperPath();

                if (!string.IsNullOrWhiteSpace(wallpaperPath) && File.Exists(wallpaperPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.UriSource = new Uri(wallpaperPath);
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MonitoringService] Wallpaper Refresh Error: {ex.Message}");
            }
            return null;
        }
    }
}