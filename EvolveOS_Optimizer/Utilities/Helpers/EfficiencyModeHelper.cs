// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class EfficiencyModeHelper
    {
        #region Properties
        public static bool IsUIWakeLockActive { get; set; } = false;
        #endregion

        #region Constants
        private const uint PROCESS_SET_INFORMATION = 0x0200;
        private const uint IDLE_PRIORITY_CLASS = 0x00000040;
        private const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        #endregion

        #region Structures
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }
        #endregion

        #region Native Methods
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(IntPtr hProcess, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE processInformation, uint processInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        #endregion

        public static void SetCurrentProcessEfficiencyMode(bool enable)
        {
            if (enable && IsUIWakeLockActive)
            {
                Debug.WriteLine("[EcoQoS] Blocked sleep request. UI Wake Lock is active.");
                return;
            }

            IntPtr hProcess = IntPtr.Zero;
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                hProcess = OpenProcess(PROCESS_SET_INFORMATION, false, pid);

                if (hProcess == IntPtr.Zero)
                {
                    Debug.WriteLine($"[EcoQoS] Failed to open process handle. Win32 Error: {Marshal.GetLastWin32Error()}");
                    return;
                }

                uint priorityClass = enable ? IDLE_PRIORITY_CLASS : NORMAL_PRIORITY_CLASS;
                if (!SetPriorityClass(hProcess, priorityClass))
                {
                    Debug.WriteLine($"[EcoQoS] Failed to set priority class. Win32 Error: {Marshal.GetLastWin32Error()}");
                }

                var state = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = 1,
                    ControlMask = 1u,
                    StateMask = enable ? 1u : 0u
                };

                bool success = SetProcessInformation(hProcess, 4, ref state, (uint)Marshal.SizeOf(state));

                if (!success)
                {
                    Debug.WriteLine($"[EcoQoS] SetProcessInformation failed. Win32 Error: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Debug.WriteLine($"[EcoQoS] Successfully applied Efficiency Mode: {enable}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EcoQoS] Exception: {ex.Message}");
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }
    }
}