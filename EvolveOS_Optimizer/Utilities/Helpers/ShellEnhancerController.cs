// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class ShellEnhancerController
    {
        #region Native Taskbar Restoration P/Invokes
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint LWA_ALPHA = 0x2;
        private const int SW_SHOW = 5;
        #endregion

        private const string EnhancerProcessName = "EvolveOS_ShellEnhancer";
        private const string EnhancerExeName = "EvolveOS_ShellEnhancer.exe";
        private const string PipeName = "EvolveOS_ShellPipe";

        #region Lifecycle Management
        public static async Task StartEnhancerAsync()
        {
            string exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;
            string dir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
            string path = Path.Combine(dir, EnhancerExeName);

            if (File.Exists(path))
            {
                var existingProcesses = Process.GetProcessesByName(EnhancerProcessName);
                foreach (var p in existingProcesses)
                {
                    try
                    {
                        p.Kill();
                        await p.WaitForExitAsync();
                    }
                    catch { }
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        WorkingDirectory = dir,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    Process.Start(psi);

                    int retries = 20;
                    while (Process.GetProcessesByName(EnhancerProcessName).Length == 0 && retries > 0)
                    {
                        await Task.Delay(500);
                        retries--;
                    }

                    if (retries > 0)
                    {
                        await Task.Delay(1500);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IPC] Failed to start Enhancer: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[IPC CRITICAL ERROR] Could not find {EnhancerExeName} at {path}");
            }
        }

        private static void ForceRestoreWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
            SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);

            ShowWindow(hWnd, SW_SHOW);
        }

        public static void StopEnhancer()
        {
            foreach (var process in Process.GetProcessesByName(EnhancerProcessName))
            {
                try { process.Kill(); } catch { }
            }

            ForceRestoreWindow(FindWindow("Shell_TrayWnd", null));

            EnumWindows((hWnd, lParam) =>
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);

                if (sb.ToString() == "Shell_SecondaryTrayWnd")
                {
                    ForceRestoreWindow(hWnd);
                }
                return true;
            }, IntPtr.Zero);
        }
        #endregion

        #region Inter-Process Communication
        public static async Task SendCommandAsync(string command)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);

                await pipeClient.ConnectAsync(2000);

                using var writer = new StreamWriter(pipeClient);
                writer.AutoFlush = true;
                await writer.WriteLineAsync(command);
            }
            catch (TimeoutException)
            {
                Debug.WriteLine($"[IPC Timeout] Shell Enhancer is not listening. Command dropped: {command}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IPC Error] {ex.Message}");
            }
        }
        #endregion
    }
}