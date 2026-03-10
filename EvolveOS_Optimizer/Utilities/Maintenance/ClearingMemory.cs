// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using static EvolveOS_Optimizer.Core.Structs.Windows;

namespace EvolveOS_Optimizer.Utilities.Maintenance
{
    public sealed class ClearingMemory
    {
        public struct CleanupStatus
        {
            public bool WinOldRemovedAttempted { get; set; }
            public bool DnsFlushSuccessful { get; set; }
            public bool ExplorerRestartInitiated { get; set; }
            public bool MemoryCleanupAttempted { get; set; }
        }

        internal static bool IsWinOldExists => Directory.Exists(PathLocator.Folders.WindowsOld);

        private static Core.Model.OperatingSystem? _operatingSystem;
        public static Core.Model.OperatingSystem OperatingSystem
        {
            get
            {
                if (_operatingSystem == null)
                {
                    var operatingSystem = Environment.OSVersion;

                    _operatingSystem = new Core.Model.OperatingSystem
                    {
                        Is64Bit = Environment.Is64BitOperatingSystem,
                        IsWindows7OrGreater = (operatingSystem.Version.Major > 6) || (operatingSystem.Version.Major == 6 && operatingSystem.Version.Minor >= 1),
                        IsWindows8OrGreater = operatingSystem.Version.Major >= 6.2,
                        IsWindows81OrGreater = operatingSystem.Version.Major >= 6.3,
                        IsWindowsVistaOrGreater = operatingSystem.Version.Major >= 6,
                        IsWindowsXpOrGreater = operatingSystem.Version.Major >= 5.1
                    };
                }

                return _operatingSystem;
            }
        }

        private static SafeFileHandle? OpenVolumeHandle(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
            {
                return null;
            }

            string volumePath = @"\\.\" + driveLetter.TrimEnd(':', '\\') + ":";

            return Win32Helper.CreateFile(
                volumePath,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                IntPtr.Zero,
                FileMode.Open,
                Win32Helper.File.FlagsNoBuffering,
                IntPtr.Zero
            );
        }

        internal static void EmptyWorkingSetFunction()
        {
            string currentProcessName = Process.GetCurrentProcess().ProcessName;
            Process[] allProcesses = Process.GetProcesses();

            HashSet<string> skipSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "services", "csrss", "wininit", "Registry", "Secure System", "smss",
        "MsMpEng", "System", "Idle", "NisSrv", "SecurityHealthService", "sppsvc"
    };

            foreach (Process proc in allProcesses)
            {
                string processName = "Unknown";
                try
                {
                    processName = proc.ProcessName;

                    if (skipSet.Contains(processName) ||
                        processName.Equals(currentProcessName, StringComparison.OrdinalIgnoreCase) ||
                        (LocalMachineSettingsEngine.ProcessExclusionList?.Contains(processName) ?? false))
                    {
                        continue;
                    }

                    IntPtr hProcess = proc.Handle;
                    bool success = Win32Helper.EmptyWorkingSet(hProcess);

                    if (!success)
                    {
                        int error = Marshal.GetLastWin32Error();

                        if (error == 5 || error == 6) continue;

                        Debug.WriteLine($"{processName}: Win32 Error {error}");
                    }
                }
                catch (Exception ex) when (ex is Win32Exception || ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    // Catches the "Access Denied" thrown by proc.Handle
                    // Stay silent here to keep the debug logs clean.
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(new Exception($"{processName}: General Error: {ex.Message}", ex));
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }

        internal static bool Is64BitMode() => Environment.Is64BitProcess;

        internal static void ClearFileSystemCache(bool ClearStandbyCache, bool lowPriority = false)
        {
            if (SetIncreasePrivilege(Win32Helper.Privilege.SeIncreaseQuotaName))
            {
                uint status = 0;
                int systemInfoLength = 0;
                IntPtr pNativeStruct = IntPtr.Zero;

                try
                {
                    if (!Is64BitMode())
                    {
                        var cacheInfo = new SystemFileCacheInformation32 { MinimumWorkingSet = -1, MaximumWorkingSet = -1 };
                        systemInfoLength = Marshal.SizeOf(typeof(SystemFileCacheInformation32));

                        pNativeStruct = Marshal.AllocHGlobal(systemInfoLength);
                        Marshal.StructureToPtr(cacheInfo, pNativeStruct, false);
                    }
                    else
                    {
                        var cacheInfo64 = new SystemFileCacheInformation64 { MinimumWorkingSet = -1L, MaximumWorkingSet = -1L };
                        systemInfoLength = Marshal.SizeOf(typeof(SystemFileCacheInformation64));

                        pNativeStruct = Marshal.AllocHGlobal(systemInfoLength);
                        Marshal.StructureToPtr(cacheInfo64, pNativeStruct, false);
                    }

                    status = Win32Helper.NtSetSystemInformation(Win32Helper.SystemInformationClass.SystemFileCacheInformation, pNativeStruct, systemInfoLength);

                    if (status != Win32Helper.SystemErrorCode.ErrorSuccess)
                    {
                        ErrorLogging.LogDebug(new Exception($"Cache Clear failed: 0x{status:X}"));
                    }
                }
                catch (Exception ex) when (ex is SEHException || ex is AccessViolationException)
                {
                    ErrorLogging.LogDebug(new Exception("NtSetSystemInformation (Cache) SEH Crash Suppressed", ex));
                }
                finally
                {
                    if (pNativeStruct != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(pNativeStruct);
                    }
                }
            }

            if (ClearStandbyCache && SetIncreasePrivilege(Win32Helper.Privilege.SeProfSingleProcessName))
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

                    uint status2 = Win32Helper.NtSetSystemInformation(Win32Helper.SystemInformationClass.SystemMemoryListInformation, pMemoryPurge, size);

                    if (status2 != Win32Helper.SystemErrorCode.ErrorSuccess)
                    {
                        ErrorLogging.LogDebug(new Exception($"Standby Clear failed: 0x{status2:X}"));
                    }
                }
                catch (Exception ex) when (ex is SEHException || ex is AccessViolationException)
                {
                    ErrorLogging.LogDebug(new Exception("NtSetSystemInformation (Standby) SEH Crash Suppressed", ex));
                }
                finally
                {
                    if (pMemoryPurge != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(pMemoryPurge);
                    }
                }
            }
        }

        public static void OptimizeCombinedPageList()
        {
            if (!OperatingSystem.HasCombinedPageList)
            {
                ErrorLogging.LogDebug(new Exception("Combined Page List optimization not supported on this OS version."));
                return;
            }

            if (!SetIncreasePrivilege(Win32Helper.Privilege.SeProfSingleProcessName))
            {
                ErrorLogging.LogDebug(new Exception("Failed to set SeProfileSingleProcessPrivilege for Combined Page List."));
                return;
            }

            GCHandle handle = default;
            try
            {
                var memoryCombineInfo = new Structs.Windows.MemoryCombineInformationEx();

                handle = GCHandle.Alloc(memoryCombineInfo, GCHandleType.Pinned);

                int result = Win32Helper.NtSetSystemInformation(
                    Win32Helper.SystemInformationClass.SystemCombinePhysicalMemoryInformation,
                    handle.AddrOfPinnedObject(),
                    (uint)Marshal.SizeOf(memoryCombineInfo)
                );

                if (result != Win32Helper.SystemErrorCode.ErrorSuccess)
                {
                    ErrorLogging.LogDebug(new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        public static void OptimizeModifiedFileCache()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive == null || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name))
                {
                    continue;
                }

                using (SafeFileHandle? handle = OpenVolumeHandle(drive.Name))
                {
                    if (handle == null || handle.IsInvalid)
                    {
                        continue;
                    }

                    int bytesReturned;

                    try
                    {
                        IntPtr buffer = Marshal.AllocHGlobal(1);
                        try
                        {
                            Win32Helper.DeviceIoControl(handle, (int)Win32Helper.Drive.IoControlResetWriteOrder, buffer, 1, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                        }
                        finally { Marshal.FreeHGlobal(buffer); }
                    }
                    catch { }

                    try
                    {
                        Win32Helper.DeviceIoControl(handle, (int)Win32Helper.Drive.FsctlDiscardVolumeCache, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                    }
                    catch { }

                    try
                    {
                        Win32Helper.FlushFileBuffers(handle);
                    }
                    catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                }
            }
        }

        public static void OptimizeModifiedPageList()
        {
            if (!OperatingSystem.IsWindows7OrGreater)
            {
                ErrorLogging.LogDebug(new Exception("Modified Page List optimization is not supported on this OS."));
                return;
            }

            if (!SetIncreasePrivilege(Win32Helper.Privilege.SeProfSingleProcessName))
            {
                ErrorLogging.LogDebug(new Exception("Failed to set SeProfileSingleProcessPrivilege for Modified Page List."));
                return;
            }

            int memoryFlushModifiedList = Win32Helper.SystemMemoryListCommand.MemoryFlushModifiedList;
            GCHandle handle = default;

            try
            {
                handle = GCHandle.Alloc(memoryFlushModifiedList, GCHandleType.Pinned);

                uint status = Win32Helper.NtSetSystemInformation(
                    Win32Helper.SystemInformationClass.SystemMemoryListInformation,
                    handle.AddrOfPinnedObject(),
                    Marshal.SizeOf(typeof(int))
                );

                if (status != Win32Helper.SystemErrorCode.ErrorSuccess)
                {
                    ErrorLogging.LogDebug(new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        public static void OptimizeRegistryCache()
        {
            if (!OperatingSystem.IsWindows7OrGreater)
            {
                ErrorLogging.LogDebug(new Exception("Registry Cache optimization is not supported on this OS version."));
                return;
            }

            try
            {
                uint status = Win32Helper.NtSetSystemInformation(
                    Win32Helper.SystemInformationClass.SystemRegistryReconciliationInformation,
                    IntPtr.Zero,
                    0
                );

                if (status != Win32Helper.SystemErrorCode.ErrorSuccess)
                {
                    ErrorLogging.LogDebug(new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        public static bool SetIncreasePrivilege(string privilegeName)
        {
            IntPtr hToken = IntPtr.Zero;
            try
            {
                if (!Win32Helper.OpenProcessToken(Process.GetCurrentProcess().Handle, Win32Helper.Token.AdjustPrivileges | Win32Helper.Token.Query, out hToken))
                {
                    ErrorLogging.LogDebug(new Exception($"OpenProcessToken failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}"));
                    return false;
                }

                TokenPrivileges newState = new TokenPrivileges
                {
                    Count = 1,
                    Attr = Win32Helper.PrivilegeAttribute.Enabled
                };

                if (!Win32Helper.LookupPrivilegeValue(null, privilegeName, ref newState.Luid))
                {
                    ErrorLogging.LogDebug(new Exception($"LookupPrivilegeValue failed for {privilegeName}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}"));
                    return false;
                }

                bool result = Win32Helper.AdjustTokenPrivileges(hToken, false, ref newState, 0, IntPtr.Zero, IntPtr.Zero);
                int lastError = Marshal.GetLastWin32Error();

                if (!result)
                {
                    ErrorLogging.LogDebug(new Exception($"AdjustTokenPrivileges native failure for {privilegeName}: {new Win32Exception(lastError).Message}"));
                    return false;
                }

                if (lastError == 1300)
                {
                    ErrorLogging.LogDebug(new Exception($"Privilege {privilegeName} not held by caller."));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception($"General error during privilege adjustment for {privilegeName}", ex));
                return false;
            }
            finally
            {
                if (hToken != IntPtr.Zero)
                {
                    Win32Helper.CloseHandle(hToken);
                }
            }
        }

        public static async Task<CleanupStatus> CleanWindowsOld()
        {
            if (IsWinOldExists)
            {
                return await StartMemoryCleanup(
                    clearRamCache: false,
                    optimizeWorkingSet: false,
                    shouldRemoveWinOld: true,
                    shouldFlushDns: false
                );
            }

            return new CleanupStatus { WinOldRemovedAttempted = false };
        }

        public static bool FlushDnsCache()
        {
            try
            {
                bool isSuccess = true;

                ProcessStartInfo dnsInfo = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                ProcessStartInfo arpInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "interface ip delete arpcache",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process? dnsProc = Process.Start(dnsInfo))
                {
                    dnsProc?.WaitForExit(2000);
                    if (dnsProc == null || dnsProc.ExitCode != 0) isSuccess = false;
                }

                using (Process? arpProc = Process.Start(arpInfo))
                {
                    arpProc?.WaitForExit(2000);
                    if (arpProc == null || arpProc.ExitCode != 0) isSuccess = false;
                }

                if (isSuccess)
                {
                    Debug.WriteLine("[Network] DNS and ARP flushed via System Tools.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception("Process-based network flush failed.", ex));
            }

            try
            {
                bool nativeDns = Win32Helper.DnsFlushResolverCache();
                uint nativeArp = Win32Helper.FlushIpNetTable(0);

                return nativeDns && nativeArp == 0;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<CleanupStatus> StartMemoryCleanup(bool clearRamCache = false, bool optimizeWorkingSet = false, bool shouldRemoveWinOld = false, bool shouldFlushDns = false)
        {
            CleanupStatus status = new CleanupStatus();
            status.MemoryCleanupAttempted = true;

            if (clearRamCache)
            {
                try
                {
                    ClearFileSystemCache(true);
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }

            if (optimizeWorkingSet)
            {
                try
                {
                    EmptyWorkingSetFunction();
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }

            try
            {
                if (shouldFlushDns)
                {
                    status.DnsFlushSuccessful = FlushDnsCache();
                }

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string chrome = Path.Combine(localApp, @"Google\Chrome\User Data\Default\Cache");
                string edge = Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Cache");
                string brave = Path.Combine(localApp, @"BraveSoftware\Brave-Browser\User Data\Default\Cache");

                UnlockHandleHelper.UnlockDirectory(chrome);
                UnlockHandleHelper.UnlockDirectory(edge);
                UnlockHandleHelper.UnlockDirectory(brave);

                StringBuilder adminCmd = new StringBuilder("/c ");
                adminCmd.Append($@"rd /s /q ""{chrome}"" & ");
                adminCmd.Append($@"rd /s /q ""{edge}"" & ");
                adminCmd.Append($@"rd /s /q ""{brave}"" & ");
                adminCmd.Append($@"rd /s /q ""{PathLocator.Folders.SystemDrive}Windows\CbsTemp\*""");

                await CommandExecutor.RunCommand(adminCmd.ToString(), isPowerShell: false);

                await Task.Run(() => SafeCleanTempFolders());
                {
                    status.WinOldRemovedAttempted = true;
                    string winOldCmd = $@"/c rd /s /q ""{PathLocator.Folders.WindowsOld}""";
                    await CommandExecutor.RunCommandAsTrustedInstaller(winOldCmd, isPowerShell: false);
                }

                if (LocalMachineSettingsEngine.RestartExplorerAfterOptimization)
                {
                    //status.ExplorerRestartInitiated = await RestartExplorer();
                    status.ExplorerRestartInitiated = await RestartExplorerAsync();
                }
                else
                {
                    await RefreshSystemTrayAsync();
                    status.ExplorerRestartInitiated = false;
                }

                await Task.Delay(1000);
                MemoryHelper.TrimWorkingSet();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }

            return status;
        }

        public static async Task CleanSoftwareDistribution()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution\\Download");

            try
            {
                UnlockHandleHelper.UnlockDirectory(path);
                await CommandExecutor.RunCommandAsTrustedInstaller($@"/c rd /s /q ""{path}""");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SoftwareDistribution cleanup failed: {ex.Message}");
            }
        }

        private static void SafeCleanTempFolders()
        {
            string localTemp = Path.GetTempPath();
            string winTemp = Path.Combine(PathLocator.Folders.SystemDrive, @"Windows\Temp");

            CleanDirectorySafely(winTemp);
            CleanDirectorySafely(localTemp);
        }

        private static void CleanDirectorySafely(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;

            string currentAppBaseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            string targetDir = directoryPath.TrimEnd('\\', '/');

            if (targetDir.Equals(currentAppBaseDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DirectoryInfo dir = new DirectoryInfo(directoryPath);

            bool isParentOfApp = currentAppBaseDir.StartsWith(targetDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (!isParentOfApp)
            {
                foreach (FileInfo file in dir.GetFiles())
                {
                    try { file.Delete(); } catch { }
                }
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string subDirFullName = subDir.FullName.TrimEnd('\\', '/');

                if (subDirFullName.Equals(currentAppBaseDir, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (currentAppBaseDir.StartsWith(subDirFullName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    CleanDirectorySafely(subDir.FullName);
                }
                else
                {
                    try
                    {
                        subDir.Delete(true);
                    }
                    catch
                    {
                        CleanDirectorySafely(subDir.FullName);
                    }
                }
            }
        }

        #region Restart Explorer
        public static async Task<bool> RestartExplorer()
        {
            CleanOldBackups(daysToKeep: 7);

            BackupRegistryKey(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", "TaskbarSettings");
            BackupRegistryKey(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ExplorerAdvancedSettings");
            BackupRegistryKey(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", "TaskbarLayout");
            BackupTaskbarPins();

            try
            {
                IntPtr hWnd = Win32Helper.FindWindow("Shell_TrayWnd", null);
                if (hWnd != IntPtr.Zero)
                {
                    Win32Helper.PostMessage(hWnd, Win32Helper.WM_COMMAND, (IntPtr)0x5B4, IntPtr.Zero);
                    await Task.Delay(1000);
                }

                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                    }
                    catch { }
                }

                await Task.Delay(1000);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                    UseShellExecute = true
                };
                Process.Start(startInfo);

                await Task.Delay(3000);

                await EnsureLanguageBarVisibilityAsync();
                await RefreshSystemTrayAsync();

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                return false;
            }
        }

        internal static async Task<bool> RestartExplorerAsync()
        {
            try
            {
                ExitExplorerGracefully();

                int retryCount = 0;
                while (Process.GetProcessesByName("explorer").Any() && retryCount < 10)
                {
                    await Task.Delay(200);
                    retryCount++;
                }

                var remainingExplorers = Process.GetProcessesByName("explorer");
                foreach (var proc in remainingExplorers)
                {
                    try { proc.Kill(); } catch { }
                }

                await EnsureLanguageBarVisibilityAsync();

                string explorerPath = Path.Combine(Environment.GetEnvironmentVariable("windir") ?? @"C:\Windows", "explorer.exe");
                Process.Start(new ProcessStartInfo(explorerPath)
                {
                    UseShellExecute = true
                });

                await RefreshSystemTrayAsync();

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                return false;
            }
        }

        public static void ExitExplorerGracefully()
        {
            int result = Win32Helper.RegFlushKey(Win32Helper.HKEY_CURRENT_USER);

            if (result != 0)
            {
                ErrorLogging.LogDebug(new Exception($"RegFlushKey failed with error code: {result}"));
            }

            IntPtr taskbarPtr = Win32Helper.FindWindow("Shell_TrayWnd", null);
            if (taskbarPtr != IntPtr.Zero)
            {
                Win32Helper.PostMessage(taskbarPtr, Win32Helper.WM_COMMAND, (IntPtr)0x5B4, IntPtr.Zero);
            }
        }

        #region More agressive method
        /*public static async System.Threading.Tasks.Task<bool> RestartExplorer()
        {
            CleanOldBackups(daysToKeep: 7);

            BackupRegistryKey(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", "TaskbarSettings");
            BackupRegistryKey(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ExplorerAdvancedSettings");
            BackupRegistryKey(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", "TaskbarLayout");
            BackupTaskbarPins();

            try
            {
                var explorers = Process.GetProcessesByName("explorer");
                foreach (var p in explorers)
                {
                    try { if (!p.CloseMainWindow()) { p.Kill(); } p.WaitForExit(3000); }
                    catch { }
                    finally { p.Dispose(); }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                    UseShellExecute = true
                });

                EnsureLanguageBarVisibility();

                await System.Threading.Tasks.Task.Delay(2000);

                RefreshSystemTray();

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                return false;
            }
        }*/
        #endregion

        internal static async Task RefreshSystemTrayAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    IntPtr systemTrayHandle = Win32Helper.FindWindow("Shell_TrayWnd", null);
                    IntPtr trayNotifyHandle = Win32Helper.FindWindowEx(systemTrayHandle, IntPtr.Zero, "TrayNotifyWnd", null);
                    IntPtr sysPagerHandle = Win32Helper.FindWindowEx(trayNotifyHandle, IntPtr.Zero, "SysPager", null);

                    IntPtr userIconsHandle = Win32Helper.FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", null);

                    IntPtr hiddenIconsHandle = Win32Helper.FindWindow("NotifyIconOverflowWindow", null);
                    hiddenIconsHandle = Win32Helper.FindWindowEx(hiddenIconsHandle, IntPtr.Zero, "ToolbarWindow32", null);

                    RefreshHandle(userIconsHandle);
                    RefreshHandle(hiddenIconsHandle);
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            });
        }

        private static void RefreshHandle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            if (Win32Helper.GetClientRect(windowHandle, out Structs.Windows.Rect rect))
            {
                for (int x = 0; x < rect.Right; x += 5)
                {
                    for (int y = 0; y < rect.Bottom; y += 5)
                    {
                        Win32Helper.PostMessage(windowHandle, Win32Helper.WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)((y << 16) | x));
                    }
                }
            }
        }

        public static async Task EnsureLanguageBarVisibilityAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    using (RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null && runKey.GetValue("ctfmon") == null)
                        {
                            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                            runKey.SetValue("ctfmon", $"\"{Path.Combine(system32, "ctfmon.exe")}\"");
                        }
                    }

                    using (RegistryKey? langBarKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\CTF\LangBar", true))
                    {
                        if (langBarKey != null)
                        {
                            langBarKey.SetValue("ShowStatus", 3, RegistryValueKind.DWord);
                        }
                    }

                    Win32Helper.RegFlushKey(Win32Helper.HKEY_CURRENT_USER);

                    if (Process.GetProcessesByName("ctfmon").Length == 0)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "ctfmon.exe",
                            UseShellExecute = true,
                            CreateNoWindow = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(new Exception("Failed to ensure Language Bar visibility", ex));
                }
            });
        }

        public static bool BackupRegistryKey(string registryPath, string backupFileName)
        {
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer", "Backups");

                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string fullPath = Path.Combine(backupDir, $"{backupFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"export \"{registryPath}\" \"{fullPath}\" /y",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using (Process? process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(5000);
                        return process.ExitCode == 0;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception($"Failed to backup registry key: {registryPath}", ex));
                return false;
            }
        }

        public static void RestoreRegistryBackup(string backupFilePath)
        {
            if (!File.Exists(backupFilePath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{backupFilePath}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        public static void BackupTaskbarPins()
        {
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer", "Backups", "TaskbarPins");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                BackupRegistryKey(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband", "TaskbarPins_Registry");

                string pinnedAppsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

                if (Directory.Exists(pinnedAppsPath))
                {
                    foreach (var file in Directory.GetFiles(pinnedAppsPath))
                    {
                        string destFile = Path.Combine(backupDir, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception("Failed to backup Taskbar Pins", ex));
            }
        }

        public static async Task<bool> UndoLastBackup()
        {
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer", "Backups");
                string pinsDir = Path.Combine(backupDir, "TaskbarPins");

                if (!Directory.Exists(backupDir))
                {
                    return false;
                }

                var latestReg = new DirectoryInfo(backupDir)
                    .GetFiles("TaskbarPins_Registry_*.reg")
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault();

                if (latestReg == null)
                {
                    return false;
                }

                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
                }

                ProcessStartInfo regImport = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{latestReg.FullName}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                Process.Start(regImport)?.WaitForExit();

                string pinnedAppsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

                if (Directory.Exists(pinsDir) && Directory.Exists(pinnedAppsPath))
                {
                    foreach (var file in Directory.GetFiles(pinnedAppsPath)) { try { File.Delete(file); } catch { } }

                    foreach (var file in Directory.GetFiles(pinsDir))
                    {
                        File.Copy(file, Path.Combine(pinnedAppsPath, Path.GetFileName(file)), true);
                    }
                }

                return await RestartExplorer();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception("Undo Failed", ex));
                return false;
            }
        }

        public static void CleanOldBackups(int daysToKeep = 7)
        {
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer", "Backups");

                if (!Directory.Exists(backupDir))
                {
                    return;
                }

                DirectoryInfo dirInfo = new DirectoryInfo(backupDir);
                DateTime threshold = DateTime.Now.AddDays(-daysToKeep);

                foreach (FileInfo file in dirInfo.GetFiles("*.reg"))
                {
                    if (file.LastWriteTime < threshold)
                    {
                        file.Delete();
                    }
                }

                string pinsDir = Path.Combine(backupDir, "TaskbarPins");
                if (Directory.Exists(pinsDir))
                {
                    DirectoryInfo pinsDirInfo = new DirectoryInfo(pinsDir);
                    if (pinsDirInfo.LastWriteTime < threshold)
                    {
                        foreach (FileInfo file in pinsDirInfo.GetFiles())
                        {
                            if (file.LastWriteTime < threshold)
                            {
                                file.Delete();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception("Backup cleanup failed", ex));
            }
        }
        #endregion
    }
}