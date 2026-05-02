using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using EvolveOS_Optimizer.Core;
using Microsoft.Win32.SafeHandles;
using static EvolveOS_Optimizer.Core.Structs.Windows;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class Win32Helper
    {
        #region Win32 Constants

        public const int AutoOptimizationMemoryUsageInterval = 5;

        public const uint WM_MOUSEMOVE = 0x0200;

        internal const uint WM_COMMAND = 0x0111;
        internal const uint WM_USER = 0x0400;

        internal const int MIN_ALL = 419;
        internal const int MIN_ALL_UNDO = 416;

        internal static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(-2147483647);

        internal const uint MB_YESNO = 0x00000004;
        internal const uint MB_ICONWARNING = 0x00000030;
        internal const uint MB_DEFBUTTON1 = 0x00000000;
        internal const int IDYES = 6;
        internal const int SM_CLEANBOOT = 67;

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_APPWINDOW = 0x00040000;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;

        public const int IDC_ARROW = 32512;

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
            public const int FlagsNoBuffering = 536870912;
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
            public const int SystemCombinePhysicalMemoryInformation = 130;
            public const int SystemFileCacheInformation = 21;
            public const int SystemMemoryListInformation = 80;
            public const int SystemRegistryReconciliationInformation = 155;
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
            public const int ErrorAccessDenied = 5;
            public const int ErrorSuccess = 0;
        }

        public static class Keyboard
        {
            public const int WmHotkey = 786;
        }

        public const int GWL_STYLE = -16;
        public const int WS_BORDER = 0x00800000;
        public const int WS_THICKFRAME = 0x00040000;

        public const int WS_CAPTION = 0x00C00000;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const int WM_HOTKEY = 0x0312;
        public const int WM_USER_REGISTER_HOTKEY = 0x0401;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        public const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        // Common ShowWindow commands
        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOW = 5;
        public const int SW_MINIMIZE = 6;
        public const int SW_RESTORE = 9;

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
        internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

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
        internal static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, ref long lpLuid);

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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool FlushFileBuffers(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

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

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        internal static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DnsFlushResolverCache();

        [DllImport("Iphlpapi.dll", SetLastError = true)]
        internal static extern uint FlushIpNetTable(int dwIfIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        internal static extern sbyte GetMessage(out Structs.MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        internal static extern bool TranslateMessage(ref Structs.MSG lpMsg);

        [DllImport("user32.dll")]
        internal static extern IntPtr DispatchMessage(ref Structs.MSG lpMsg);

        [DllImport("user32.dll")]
        internal static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int CreatePseudoConsole(Structs.COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessW(string? lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref Structs.STARTUPINFOEX lpStartupInfo, out Structs.PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("User32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX counters, uint size);

        #endregion

        #region Delegates & Private Fields
        public delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData);
        private static SubclassProc? _dragDropSubclassDelegate;
        private static Action<string[]>? _onFilesDroppedCallback;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("shell32.dll")]
        public static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

        [DllImport("shell32.dll")]
        public static extern void DragFinish(IntPtr hDrop);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr changeFilterStruct);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr hCursor);
        #endregion

        #region Security & Integrity Native API
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern IntPtr GetSidSubAuthority(IntPtr pSid, uint nSubAuthority);
        #endregion

        #region Public Methods
        public static void InitializeAdminDragDrop(IntPtr hWnd, Action<string[]> onFilesDropped)
        {
            _onFilesDroppedCallback = onFilesDropped;

            uint WM_DROPFILES = 0x0233;
            uint WM_COPYDATA = 0x004A;
            uint WM_COPYGLOBALDATA = 0x0049;

            ChangeWindowMessageFilterEx(hWnd, WM_DROPFILES, 1, IntPtr.Zero);
            ChangeWindowMessageFilterEx(hWnd, WM_COPYDATA, 1, IntPtr.Zero);
            ChangeWindowMessageFilterEx(hWnd, WM_COPYGLOBALDATA, 1, IntPtr.Zero);

            DragAcceptFiles(hWnd, true);

            _dragDropSubclassDelegate = new SubclassProc(DragDropSubclassProc);
            SetWindowSubclass(hWnd, _dragDropSubclassDelegate, 1, IntPtr.Zero);

            Debug.WriteLine("[Win32Helper] Native Drag & Drop Hook Initialized.");
        }

        public static void HideFromTaskbar(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle &= ~WS_EX_APPWINDOW; // Remove from Taskbar
            exStyle |= WS_EX_TOOLWINDOW; // Make it a ToolWindow
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);

            // Refresh the window frame to apply changes immediately
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
        }

        public static void LogProcessIntegrityLevel()
        {
            IntPtr hToken = IntPtr.Zero;
            if (OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0008, out hToken))
            {
                try
                {
                    uint dwLengthNeeded;
                    GetTokenInformation(hToken, 25, IntPtr.Zero, 0, out dwLengthNeeded);
                    IntPtr pTIL = Marshal.AllocHGlobal((int)dwLengthNeeded);

                    try
                    {
                        if (GetTokenInformation(hToken, 25, pTIL, dwLengthNeeded, out dwLengthNeeded))
                        {
                            IntPtr pSid = Marshal.ReadIntPtr(pTIL);
                            IntPtr pSubAuthorityCount = GetSidSubAuthorityCount(pSid);
                            int subAuthorityCount = Marshal.ReadByte(pSubAuthorityCount);

                            IntPtr pRID = GetSidSubAuthority(pSid, (uint)subAuthorityCount - 1);
                            int rid = Marshal.ReadInt32(pRID);

                            string level = rid switch
                            {
                                0x0000 => "Untrusted",
                                0x1000 => "Low",
                                0x2000 => "Medium",
                                0x3000 => "High (Administrator)",
                                0x4000 => "System",
                                _ => $"Unknown (0x{rid:X})"
                            };

                            Debug.WriteLine($"[Security] Process Integrity Level: {level}");
                        }
                    }
                    finally { Marshal.FreeHGlobal(pTIL); }
                }
                finally { CloseHandle(hToken); }
            }
        }
        #endregion

        #region Private Subclass Processing
        private static IntPtr DragDropSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == 0x0233)
            {
                Debug.WriteLine("[Win32Helper] Raw Drop Detected!");
                IntPtr hDrop = wParam;

                uint fileCount = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                List<string> files = new List<string>();

                for (uint i = 0; i < fileCount; i++)
                {
                    StringBuilder sb = new StringBuilder(260);
                    DragQueryFile(hDrop, i, sb, (uint)sb.Capacity);
                    files.Add(sb.ToString());
                }

                DragFinish(hDrop);
                Debug.WriteLine($"[Win32Helper] Extracted {files.Count} files natively.");

                _onFilesDroppedCallback?.Invoke(files.ToArray());

                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
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