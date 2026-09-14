// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.IO.Pipes;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class ShellEnhancerController
    {
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

        public static void StopEnhancer()
        {
            foreach (var process in Process.GetProcessesByName(EnhancerProcessName))
            {
                try { process.Kill(); } catch { }
            }

            IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
            if (trayWnd != IntPtr.Zero) ShowWindow(trayWnd, 5);

            IntPtr secondaryTray = IntPtr.Zero;
            while ((secondaryTray = FindWindowEx(IntPtr.Zero, secondaryTray, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                ShowWindow(secondaryTray, 5);
            }
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