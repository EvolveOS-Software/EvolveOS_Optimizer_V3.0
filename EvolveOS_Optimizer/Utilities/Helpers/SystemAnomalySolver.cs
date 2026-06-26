// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class SystemAnomalySolver
    {
        #region CPU Anomaly Engine

        // Critical system processes (NEVER kill, throttle, or trim.)
        private readonly HashSet<string> _systemWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "idle", "system", "registry", "smss", "csrss", "wininit", "services",
            "lsass", "winlogon", "explorer", "dwm", "fontdrvhost", "svchost",
            "spoolsv", "taskmgr", "systemsettings", "sihost", "taskhostw",
            "audiodg", "securityhealthservice", "msmpeng", "nissrv", "wmiapsrv",
            "sppsvc", "secure system"
        };

        public async Task<List<string>> DetectAndResolveAllAnomaliesAsync(CancellationToken token)
        {
            var resolvedAnomalies = new List<string>();
            string currentProcessName = Process.GetCurrentProcess().ProcessName.ToLower();

            EnableDebugPrivilege();
            SetIncreasePrivilege(Win32Helper.Privilege.SeProfSingleProcessName);
            SetIncreasePrivilege(Win32Helper.Privilege.SeIncreaseQuotaName);

            try
            {
                var snapshot1 = Process.GetProcesses().ToDictionary(p => p.Id, p =>
                {
                    try { return p.TotalProcessorTime; } catch { return TimeSpan.Zero; }
                });

                await Task.Delay(1000, token);

                var currentProcesses = Process.GetProcesses();

                foreach (var process in currentProcesses)
                {
                    try
                    {
                        string pName = process.ProcessName.ToLower();

                        if (_systemWhitelist.Contains(pName) || pName == currentProcessName ||
                           (LocalMachineSettingsEngine.ProcessExclusionList?.Contains(pName) ?? false))
                            continue;

                        if (snapshot1.TryGetValue(process.Id, out TimeSpan previousTime))
                        {
                            TimeSpan currentTime = process.TotalProcessorTime;
                            double cpuUsage = (currentTime - previousTime).TotalMilliseconds / (Environment.ProcessorCount * 1000.0) * 100;

                            if (cpuUsage > 15.0 && process.MainWindowHandle == IntPtr.Zero)
                            {
                                string nameForLog = process.ProcessName;
                                int pid = process.Id;

                                if (TerminateProcessSafely(process, pid))
                                {
                                    resolvedAnomalies.Add($"Terminated orphaned background process '{nameForLog}' consuming {cpuUsage:0.#}% CPU.");
                                    continue;
                                }
                            }
                            else if (cpuUsage > 40.0)
                            {
                                try
                                {
                                    if (process.PriorityClass != ProcessPriorityClass.Idle)
                                    {
                                        process.PriorityClass = ProcessPriorityClass.Idle;
                                        resolvedAnomalies.Add($"Throttled priority of resource-heavy process '{process.ProcessName}' ({cpuUsage:0.#}% CPU).");
                                    }
                                }
                                catch (Win32Exception) { }
                            }
                        }

                        IntPtr hProcess = process.Handle;
                        if (!Win32Helper.EmptyWorkingSet(hProcess))
                        {
                            int error = Marshal.GetLastWin32Error();
                            if (error != 5 && error != 6)
                            {
                                Debug.WriteLine($"{pName}: Win32 Error {error} during working set trim.");
                            }
                        }
                    }
                    catch { /* Safely ignore inaccessible processes */ }
                    finally { process.Dispose(); }
                }

                //resolvedAnomalies.Add("Successfully trimmed dormant memory from active processes.");

                ClearFileSystemCache(true, lowPriority: false);
                //resolvedAnomalies.Add("Flushed System File Cache and Standby Memory Lists.");

                OptimizeCombinedPageList();
                OptimizeModifiedPageList();
                OptimizeModifiedFileCache();
                OptimizeRegistryCache();
                //resolvedAnomalies.Add("Optimized OS Page Lists, Modified File Caches, and Registry Reconciliation.");

                if (FlushDnsCache())
                {
                    //resolvedAnomalies.Add("Flushed DNS Resolver Cache and cleared ARP network tables.");
                }

            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }

            return resolvedAnomalies;
        }

        #endregion

        #region Memory & Cache Anomaly Engine

        private static bool Is64BitMode() => Environment.Is64BitProcess;

        private void ClearFileSystemCache(bool clearStandbyCache, bool lowPriority = false)
        {
            uint status = 0;
            int systemInfoLength = 0;
            IntPtr pNativeStruct = IntPtr.Zero;

            try
            {
                if (!Is64BitMode())
                {
                    var cacheInfo = new Structs.Windows.SystemFileCacheInformation32 { MinimumWorkingSet = -1, MaximumWorkingSet = -1 };
                    systemInfoLength = Marshal.SizeOf(typeof(Structs.Windows.SystemFileCacheInformation32));
                    pNativeStruct = Marshal.AllocHGlobal(systemInfoLength);
                    Marshal.StructureToPtr(cacheInfo, pNativeStruct, false);
                }
                else
                {
                    var cacheInfo64 = new Structs.Windows.SystemFileCacheInformation64 { MinimumWorkingSet = -1L, MaximumWorkingSet = -1L };
                    systemInfoLength = Marshal.SizeOf(typeof(Structs.Windows.SystemFileCacheInformation64));
                    pNativeStruct = Marshal.AllocHGlobal(systemInfoLength);
                    Marshal.StructureToPtr(cacheInfo64, pNativeStruct, false);
                }

                status = Win32Helper.NtSetSystemInformation(Win32Helper.SystemInformationClass.SystemFileCacheInformation, pNativeStruct, systemInfoLength);
            }
            catch (Exception ex) when (ex is SEHException || ex is AccessViolationException) { }
            finally { if (pNativeStruct != IntPtr.Zero) Marshal.FreeHGlobal(pNativeStruct); }

            if (clearStandbyCache)
            {
                IntPtr pMemoryPurge = IntPtr.Zero;
                try
                {
                    int purgeCommand = lowPriority
                        ? Win32Helper.SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList
                        : Win32Helper.SystemMemoryListCommand.MemoryPurgeStandbyList;

                    int size = sizeof(int);
                    pMemoryPurge = Marshal.AllocHGlobal(size);
                    Marshal.WriteInt32(pMemoryPurge, purgeCommand);

                    Win32Helper.NtSetSystemInformation(Win32Helper.SystemInformationClass.SystemMemoryListInformation, pMemoryPurge, size);
                }
                catch (Exception ex) when (ex is SEHException || ex is AccessViolationException) { }
                finally { if (pMemoryPurge != IntPtr.Zero) Marshal.FreeHGlobal(pMemoryPurge); }
            }
        }

        private void OptimizeCombinedPageList()
        {
            GCHandle handle = default;
            try
            {
                var memoryCombineInfo = new Structs.Windows.MemoryCombineInformationEx();
                handle = GCHandle.Alloc(memoryCombineInfo, GCHandleType.Pinned);
                Win32Helper.NtSetSystemInformation(Win32Helper.SystemInformationClass.SystemCombinePhysicalMemoryInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(memoryCombineInfo));
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            finally { if (handle.IsAllocated) handle.Free(); }
        }

        private void OptimizeModifiedPageList()
        {
            int memoryFlushModifiedList = Win32Helper.SystemMemoryListCommand.MemoryFlushModifiedList;
            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(memoryFlushModifiedList, GCHandleType.Pinned);
                Win32Helper.NtSetSystemInformation(Win32Helper.SystemInformationClass.SystemMemoryListInformation, handle.AddrOfPinnedObject(), Marshal.SizeOf(typeof(int)));
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            finally { if (handle.IsAllocated) handle.Free(); }
        }

        private void OptimizeModifiedFileCache()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive == null || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name)) continue;

                string volumePath = @"\\.\" + drive.Name.TrimEnd(':', '\\') + ":";
                using (SafeFileHandle handle = Win32Helper.CreateFile(volumePath, FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, Win32Helper.File.FlagsNoBuffering, IntPtr.Zero))
                {
                    if (handle.IsInvalid) continue;

                    try
                    {
                        IntPtr buffer = Marshal.AllocHGlobal(1);
                        try { Win32Helper.DeviceIoControl(handle, (int)Win32Helper.Drive.IoControlResetWriteOrder, buffer, 1, IntPtr.Zero, 0, out _, IntPtr.Zero); }
                        finally { Marshal.FreeHGlobal(buffer); }

                        Win32Helper.DeviceIoControl(handle, (int)Win32Helper.Drive.FsctlDiscardVolumeCache, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                        Win32Helper.FlushFileBuffers(handle);
                    }
                    catch { /* Drive locked or unsupported */ }
                }
            }
        }

        private void OptimizeRegistryCache()
        {
            try
            {
                Win32Helper.NtSetSystemInformation(Win32Helper.SystemInformationClass.SystemRegistryReconciliationInformation, IntPtr.Zero, 0);
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }

        #endregion

        #region Service Anomaly Engine

        // Dictionary of critical services that should NEVER be disabled
        // Key = Service Name, Value = Friendly Name for the UI
        private readonly Dictionary<string, string> _criticalServices = new(StringComparer.OrdinalIgnoreCase)
        {
            { "BFE", "Base Filtering Engine" },
            { "ProfSvc", "User Profile Service" },
            { "EventLog", "Windows Event Log" },
            { "wscsvc", "Windows Security Center" },
            { "Winmgmt", "Windows Management Instrumentation" },
            { "RpcSs", "Remote Procedure Call (RPC)" },
            { "Audiosrv", "Windows Audio" }
        };

        public List<ServiceAnomaly> DetectAdvancedServiceAnomalies()
        {
            var anomalies = new List<ServiceAnomaly>();

            string[] protectedServices = { "RpcSs", "DcomLaunch", "SamSs", "LSM" };

            foreach (var svc in _criticalServices)
            {
                string serviceName = svc.Key;
                string friendlyName = svc.Value;

                if (protectedServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                    if (key == null) continue;

                    int startValue = (int)(key.GetValue("Start", -1));

                    if (startValue == 4)
                    {
                        anomalies.Add(new ServiceAnomaly
                        {
                            ServiceName = serviceName,
                            FriendlyName = friendlyName,
                            AnomalyType = "Disabled",
                            RecommendedEventId = 7000 + Math.Abs(serviceName.GetHashCode() % 99), // 7000 block
                            AlertMessage = $"CRITICAL: {friendlyName} ({serviceName}) is disabled."
                        });
                        continue;
                    }

                    if (startValue == 2)
                    {
                        try
                        {
                            using var controller = new ServiceController(serviceName);
                            if (controller.Status == ServiceControllerStatus.Stopped)
                            {
                                anomalies.Add(new ServiceAnomaly
                                {
                                    ServiceName = serviceName,
                                    FriendlyName = friendlyName,
                                    AnomalyType = "Ghosted",
                                    RecommendedEventId = 7100 + Math.Abs(serviceName.GetHashCode() % 99), // 7100 block
                                    AlertMessage = $"SERVICE FAILURE: {friendlyName} is set to Automatic but has crashed or stopped."
                                });
                            }
                        }
                        catch { /* Service missing from SCM despite registry entry */ }
                    }

                    string imagePath = key.GetValue("ImagePath") as string ?? "";
                    if (!IsPathTrusted(imagePath))
                    {
                        anomalies.Add(new ServiceAnomaly
                        {
                            ServiceName = serviceName,
                            FriendlyName = friendlyName,
                            AnomalyType = "Tampered",
                            RecommendedEventId = 7200 + Math.Abs(serviceName.GetHashCode() % 99), // 7200 block
                            AlertMessage = $"INTEGRITY COMPROMISED: {friendlyName} execution path is suspicious or hijacked."
                        });
                    }

                    byte[]? failureActions = key.GetValue("FailureActions") as byte[];
                    if (IsRecoveryWiped(failureActions))
                    {
                        anomalies.Add(new ServiceAnomaly
                        {
                            ServiceName = serviceName,
                            FriendlyName = friendlyName,
                            AnomalyType = "NoRecovery",
                            RecommendedEventId = 7300 + Math.Abs(serviceName.GetHashCode() % 99), // 7300 block
                            AlertMessage = $"RECOVERY DISABLED: The crash-recovery protocols for {friendlyName} have been wiped."
                        });
                    }

                    string[]? dependencies = key.GetValue("DependOnService") as string[];
                    if (dependencies != null)
                    {
                        foreach (var dep in dependencies)
                        {

                            string cleanDep = dep.TrimStart('+');

                            using var depKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{cleanDep}");
                            if (depKey != null)
                            {
                                int depStart = (int)(depKey.GetValue("Start", -1));
                                if (depStart == 4)
                                {
                                    anomalies.Add(new ServiceAnomaly
                                    {
                                        ServiceName = serviceName,
                                        FriendlyName = friendlyName,
                                        AnomalyType = "Dependency",
                                        RecommendedEventId = 7400 + Math.Abs(serviceName.GetHashCode() % 99), // 7400 block
                                        AlertMessage = $"DEPENDENCY BROKEN: {friendlyName} requires '{cleanDep}', which is disabled."
                                    });
                                }
                            }
                        }
                    }
                }
                catch { /* Ignore access violations */ }
            }

            return anomalies;
        }

        public string GetServiceFriendlyName(string serviceKey)
        {
            return _criticalServices.TryGetValue(serviceKey, out string? friendlyName) ? friendlyName : serviceKey;
        }

        #endregion

        #region Network & OS Anomaly Engine

        private bool FlushDnsCache()
        {
            try
            {
                bool nativeDns = Win32Helper.DnsFlushResolverCache();
                uint nativeArp = Win32Helper.FlushIpNetTable(0);
                return nativeDns && nativeArp == 0;
            }
            catch { return false; }
        }

        public async Task CleanSoftwareDistributionAsync()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution\\Download");
            try
            {
                UnlockHandleHelper.UnlockDirectory(path);
                await CommandExecutor.RunCommandAsTrustedInstaller($@"/c rd /s /q ""{path}""");
            }
            catch (Exception ex) { Debug.WriteLine($"SoftwareDistribution cleanup failed: {ex.Message}"); }
        }

        #endregion

        #region Nuclear TrustedInstaller Strike

        private bool TerminateProcessSafely(Process process, int pid)
        {
            try
            {
                if (process.HasExited) return true;

                try { process.CloseMainWindow(); process.WaitForExit(1000); } catch { }

                if (!process.HasExited)
                {
                    try { process.Kill(); process.WaitForExit(2000); }
                    catch (Win32Exception) { /* Access Denied! */ }
                }

                if (!process.HasExited)
                {
                    ExecuteNuclearKill(pid);
                    Thread.Sleep(1500);
                    try { process.Refresh(); } catch { return true; }
                }

                return process.HasExited;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                return false;
            }
        }

        private async void ExecuteNuclearKill(int targetPid)
        {
            try
            {
                await TrustedInstaller.StartTrustedInstallerServiceAsync();

                int tiPid = CommandExecutor.PID;
                if (tiPid > 0)
                {
                    string taskkillCmd = $"cmd.exe /c taskkill.exe /F /PID {targetPid}";
                    TrustedInstaller.CreateProcessAsTrustedInstaller(tiPid, taskkillCmd, false);
                }
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }

        #endregion

        #region Advanced Heuristic Helpers

        private bool IsPathTrusted(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            string lowerPath = path.ToLowerInvariant();

            bool isSystemRoot = lowerPath.Contains("system32") || lowerPath.Contains("syswow64");
            bool isSvcHost = lowerPath.Contains("svchost.exe");
            bool isSystemDriver = lowerPath.StartsWith(@"\systemroot\") || lowerPath.StartsWith(@"system32\drivers\");

            return isSystemRoot || isSvcHost || isSystemDriver;
        }

        private bool IsRecoveryWiped(byte[]? failureActions)
        {
            if (failureActions == null) return false;

            if (failureActions.Length >= 20)
            {
                int actionCount = BitConverter.ToInt32(failureActions, 16);
                return actionCount == 0;
            }

            return false;
        }

        #endregion

        #region Win32 Privilege Escalation

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)] public LUID_AND_ATTRIBUTES[] Privileges; }

        private static void EnableDebugPrivilege()
        {
            try
            {
                if (OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out IntPtr hToken))
                {
                    if (LookupPrivilegeValue(null, "SeDebugPrivilege", out LUID luid))
                    {
                        TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privileges = new LUID_AND_ATTRIBUTES[1] };
                        tp.Privileges[0].Luid = luid;
                        tp.Privileges[0].Attributes = 0x00000002;
                        AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                    }
                    CloseHandle(hToken);
                }
            }
            catch { /* Fails silently if app isn't running as Admin */ }
        }

        private static bool SetIncreasePrivilege(string privilegeName)
        {
            IntPtr hToken = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out hToken)) return false;

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privileges = new LUID_AND_ATTRIBUTES[1] };
                if (!LookupPrivilegeValue(null, privilegeName, out tp.Privileges[0].Luid)) return false;

                tp.Privileges[0].Attributes = 0x00000002;
                return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { return false; }
            finally { if (hToken != IntPtr.Zero) CloseHandle(hToken); }
        }

        #endregion
    }
}