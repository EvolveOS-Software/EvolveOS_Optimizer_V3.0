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

        internal enum DeviceType { All, Storage, Audio, Network }

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

        private void SafeWmiAction(Action action, string context)
        {
            try
            {
                action();
            }
            catch (COMException)
            {
                Debug.WriteLine($"[MonitoringService] {context}: COM Busy (Handled Silently)");
            }
            catch (ManagementException ex)
            {
                Debug.WriteLine($"[MonitoringService] {context}: WMI Error {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MonitoringService] {context}: Unexpected error: {ex.Message}");
            }
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
            catch { }
        }

        internal double GetDownloadSpeed()
        {
            try { return _downloadCounter != null ? Math.Max(0.0, (double)_downloadCounter.NextValue() / 1048576.0) : 0.0; }
            catch { return 0.0; }
        }

        internal double GetUploadSpeed()
        {
            try { return _uploadCounter != null ? Math.Max(0.0, (double)_uploadCounter.NextValue() / 1048576.0) : 0.0; }
            catch { return 0.0; }
        }

        internal async Task<string> GetProcessCount()
        {
            return await Task.Run(() =>
            {
                uint[] buffer = new uint[1024];
                if (EnumProcesses(buffer, (uint)(buffer.Length * sizeof(uint)), out uint bytesNeeded))
                {
                    return (bytesNeeded / sizeof(uint)).ToString();
                }
                return Process.GetProcesses().Length.ToString();
            });
        }

        internal async Task<string> GetServicesCount()
        {
            return await Task.Run(() =>
            {
                int runningCount = 0;
                try
                {
                    ServiceController[] allServices = ServiceController.GetServices();
                    foreach (var svc in allServices)
                    {
                        using (svc)
                        {
                            if (svc.Status == ServiceControllerStatus.Running &&
                               (svc.ServiceType.HasFlag(ServiceType.Win32OwnProcess) ||
                                svc.ServiceType.HasFlag(ServiceType.Win32ShareProcess)))
                            {
                                runningCount++;
                            }
                        }
                    }
                }
                catch { }
                return runningCount.ToString();
            });
        }

        internal async Task GetPhysicalAvailableMemory()
        {
            await Task.Run(() =>
            {
                MemoryStatus memStatus = new();
                if (!GlobalMemoryStatusEx(memStatus)) return;
                ulong total = memStatus.ullTotalPhys / 1048576;
                ulong avail = memStatus.ullAvailPhys / 1048576;
                Memory.Usage = (int)((float)(total - avail) / total * 100);
            });
        }

        internal async Task GetTotalProcessorUsage()
        {
            try
            {
                await Task.Run(async () =>
                {
                    static ulong ToTicks(SystemTime st) => ((ulong)st.dwHighDateTime << 32) | st.dwLowDateTime;
                    if (!GetSystemTimes(out var i1, out var k1, out var u1)) return;
                    await Task.Delay(1000);
                    if (!GetSystemTimes(out var i2, out var k2, out var u2)) return;

                    ulong idleDiff = ToTicks(i2) - ToTicks(i1);
                    ulong totalDiff = (ToTicks(k2) + ToTicks(u2)) - (ToTicks(k1) + ToTicks(u1));

                    if (totalDiff > 0)
                        Processor.Usage = Math.Min(100, Math.Max(0, (int)(100.0 * (totalDiff - idleDiff) / totalDiff)));
                });
            }
            catch { Processor.Usage = 0; }
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
            SafeWmiAction(() =>
            {
                WqlEventQuery query = new("__InstanceOperationEvent", TimeSpan.FromSeconds(1), filter);
                ManagementEventWatcher watcher = new(new ManagementScope(scope ?? @"root\cimv2"), query);

                void handler(object s, EventArrivedEventArgs e) => HandleDevicesEvents?.Invoke(type);

                watcher.EventArrived += handler;
                watcher.Start();
                lock (_watcherHandler) { _watcherHandler.Add((watcher, handler)); }
            }, $"DeviceWatcher ({type})");
        }

        internal void StopDeviceMonitoring()
        {
            lock (_watcherHandler)
            {
                foreach (var item in _watcherHandler)
                {
                    SafeWmiAction(() =>
                    {
                        item.watcher.EventArrived -= item.handler;
                        item.watcher.Stop();
                        item.watcher.Dispose();
                    }, "StopWatcher");
                }
                _watcherHandler.Clear();
            }
        }

        public void Dispose()
        {
            StopDeviceMonitoring();

            _downloadCounter?.Dispose();
            _uploadCounter?.Dispose();
            _downloadCounter = null;
            _uploadCounter = null;

            HandleDevicesEvents = null;
            GC.SuppressFinalize(this);
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
                string path = GetWallpaperPath();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return new BitmapImage(new Uri(path)) { CreateOptions = BitmapCreateOptions.IgnoreImageCache };
                }
            }
            catch { }
            return null;
        }
    }
}