using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using EvolveOS_Optimizer.Core;
using Microsoft.Win32.SafeHandles;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class Win32Helper
    {
        #region Constants

        public const int AutoOptimizationMemoryUsageInterval = 5;

        public const uint WM_MOUSEMOVE = 0x0200;

        internal const uint WM_COMMAND = 0x0111;
        internal const uint WM_USER = 0x0400;

        internal const int MIN_ALL = 419;
        internal const int MIN_ALL_UNDO = 416;

        internal static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(-2147483647);

        public static class Privilege
        {
            public const string SeDebugName = "SeDebugPrivilege"; // Required to debug and adjust the memory of a process owned by another account. User Right: Debug programs.
            public const string SeIncreaseQuotaName = "SeIncreaseQuotaPrivilege"; // Required to increase the quota assigned to a process. User Right: Adjust memory quotas for a process.
            public const string SeProfSingleProcessName = "SeProfileSingleProcessPrivilege"; // Required to gather profiling information for a single process. User Right: Profile single process.
        }

        public static class PrivilegeAttribute
        {
            public const int Enabled = 2;
        }

        public static class Token
        {
            public const uint Query = 0x0008;
            public const uint AdjustPrivileges = 0x0020;
        }

        /*public static class Drive
        {
            public const int FsctlDiscardVolumeCache = 589828; // 0x00090054 - FSCTL_DISCARD_VOLUME_CACHE
            public const int IoControlResetWriteOrder = 589832; // 0x000900F8 - FSCTL_RESET_WRITE_ORDER
        }*/

        public static class Drive
        {
            public const uint FsctlDiscardVolumeCache = 0x00090000 | (0x0002 << 14) | (0x0053 << 2) | 0;
            public const uint IoControlResetWriteOrder = 0x00070000 | (0x0002 << 14) | (0x0024 << 2) | 0;
        }

        public static class File
        {
            public const int FlagsNoBuffering = 536870912; // 0x20000000 - FILE_FLAG_NO_BUFFERING
        }

        public static class Registry
        {
            public static class Key
            {
                public const string ProcessExclusionList = @"SOFTWARE\EvolveOS_Optimizer\ProcessExclusionList";
                public const string Settings = @"SOFTWARE\EvolveOS_Optimizer";
            }
        }

        public static class SystemInformationClass
        {
            public const int SystemCombinePhysicalMemoryInformation = 130; // 0x82
            public const int SystemFileCacheInformation = 21; // 0x15
            public const int SystemMemoryListInformation = 80; // 0x50
            public const int SystemRegistryReconciliationInformation = 155; // 0x9B
        }

        public static class SystemMemoryListCommand
        {
            public const int MemoryEmptyWorkingSets = 2;
            public const int MemoryFlushModifiedList = 3;
            public const int MemoryPurgeLowPriorityStandbyList = 5;
            public const int MemoryPurgeStandbyList = 4;
        }

        public static class SystemErrorCode
        {
            public const int ErrorAccessDenied = 5; // (ERROR_ACCESS_DENIED) Access is denied
            public const int ErrorSuccess = 0; // (ERROR_SUCCESS) The operation completed successfully
        }

        public static class Keyboard
        {
            public const int WmHotkey = 786; // 0x312
        }


        public const int GWL_STYLE = -16;
        public const int WS_BORDER = 0x00800000;
        public const int WS_THICKFRAME = 0x00040000;

        #endregion

        #region Native Methods

        [DllImport("winmm.dll")]
        internal static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("user32.dll")]
        internal static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges, ref Structs.Windows.TokenPrivileges newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachConsole(int dwProcessId);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern SafeFileHandle CreateFile([MarshalAs(UnmanagedType.LPWStr)] string lpFileName, FileAccess dwDesiredAccess, FileShare dwShareMode, IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("dwmapi.dll", SetLastError = true)]
        internal static extern void DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("user32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GlobalMemoryStatusEx([In, Out] Structs.Windows.MemoryStatusEx lpBuffer);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref long lpLuid);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("ntdll.dll", SetLastError = true)]
        internal static extern int NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, uint SystemInformationLength);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSystemFileCacheSize(IntPtr minimumFileCacheSize, IntPtr maximumFileCacheSize, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("ntdll.dll")]
        internal static extern uint NtSetSystemInformation(int InfoClass, IntPtr Info, int Length);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool FlushFileBuffers(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out Structs.Windows.Rect lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int RegFlushKey(IntPtr hKey);

        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
        internal static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

        #region Backup
        /*[DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);*/

        /*[SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, int dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);*/

        /*[DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);*/

        //[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        //internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

        /*[DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokenPrivileges newst, int len, IntPtr prev, IntPtr relen);*/

        /*[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);*/
        #endregion

    }
}