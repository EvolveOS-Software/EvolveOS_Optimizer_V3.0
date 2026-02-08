using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using EvolveOS_Optimizer.Utilities.Helpers; // Port your ErrorLogging here
using EvolveOS_Optimizer.Utilities.Services; // Ensure HardwareData is accessible

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    internal class MonitoringService : HardwareData, IDisposable
    {
        private PerformanceCounter? _downloadCounter;
        private PerformanceCounter? _uploadCounter;

        internal event Action<DeviceType>? HandleDevicesEvents;
        private readonly List<(ManagementEventWatcher watcher, EventArrivedEventHandler handler)> _watcherHandler = new();

        // Note: GetServices requires the System.ServiceProcess.ServiceController NuGet package
        private readonly ServiceController[] _servicesList = ServiceController.GetServices();

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
            // Initializing network counters on a separate thread to prevent splash screen freeze
            Task.Run(InitializeNetworkCounters);
        }

        internal void InitializeNetworkCounters()
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                // Select primary active interface
                NetworkInterface? targetInterface = interfaces
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .FirstOrDefault(nic =>
                        nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

                if (targetInterface != null)
                {
                    string instanceName = targetInterface.Description;

                    // Performance counters require specific character sanitization
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

                        // Initial sample
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
            try { return _downloadCounter != null ? ((_downloadCounter.NextValue() / 1024f) / 1024f) * 8.0 : 0.0; }
            catch { return 0.0; }
        }

        internal double GetUploadSpeed()
        {
            try { return _uploadCounter != null ? ((_uploadCounter.NextValue() / 1024f) / 1024f) * 8.0 : 0.0; }
            catch { return 0.0; }
        }

        internal async Task<string> GetProcessCount()
        {
            return await Task.Run(() =>
            {
                uint capacity = 1024;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    uint[] buffer = new uint[capacity];
                    if (!EnumProcesses(buffer, capacity * sizeof(uint), out uint bytesNeeded))
                    {
                        capacity = (bytesNeeded / sizeof(uint)) + 1;
                        continue;
                    }
                    return (bytesNeeded / sizeof(uint)).ToString();
                }
                return Process.GetProcesses().Length.ToString();
            });
        }

        internal async Task<string> GetServicesCount()
        {
            return await Task.Run(() =>
            {
                int running = 0;
                Parallel.ForEach(_servicesList, svc =>
                {
                    try
                    {
                        svc.Refresh();
                        if (svc.Status == ServiceControllerStatus.Running) running++;
                    }
                    catch { /* Service access denied or busy */ }
                });
                return running.ToString();
            });
        }

        internal async Task GetPhysicalAvailableMemory()
        {
            await Task.Run(() =>
            {
                MemoryStatus memStatus = new();
                if (!GlobalMemoryStatusEx(memStatus)) return;

                // Converting bytes to MB
                ulong totalMemoryMb = memStatus.ullTotalPhys / 1048576;
                ulong availMemoryMb = memStatus.ullAvailPhys / 1048576;

                // HardwareData is the base class, Usage is an integer percentage
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

                    await Task.Delay(1000); // Sample window

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
            // Run WMI watchers in the background
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
            _downloadCounter?.Dispose();
            _uploadCounter?.Dispose();
            foreach (var svc in _servicesList) svc.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}