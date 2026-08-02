// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class LightingBootstrapper
    {
        private static readonly string AppDirectory = AppDomain.CurrentDomain.BaseDirectory;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetDllDirectory(string lpPathName);

        public static void StartLightingSystem()
        {
            try
            {
                // 🚀 FORCE C# to look for the DLL in our app's exact folder
                string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory;
                SetDllDirectory(exeDir);

                LaunchOpenRGBServer();
                System.Threading.Thread.Sleep(2000);

                // Now when this runs, it guarantees it finds the file!
                if (LightingNativeBridge.InitLighting())
                {
                    Debug.WriteLine("[Lighting] Successfully connected to OpenRGB Server!");
                }
            }
            catch (Exception ex)
            {
                // 🚀 Put a breakpoint here, or check your Output window to see exactly why it failed!
                Debug.WriteLine($"[Lighting] Bootstrapper failed: {ex.Message}");
            }
        }

        private static void LaunchOpenRGBServer()
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            // 🚀 Point to the unzipped folder!
            string openRgbDir = Path.Combine(exeDir, "OpenRGB_Server");
            string openRgbExePath = Path.Combine(openRgbDir, "OpenRGB.exe");

            if (!File.Exists(openRgbExePath))
            {
                Debug.WriteLine($"[Lighting] OpenRGB.exe not found at path: {openRgbExePath}");
                return;
            }

            var existingProcesses = Process.GetProcessesByName("OpenRGB");
            if (existingProcesses.Length > 0) return;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = openRgbExePath,
                    // 🚀 NEW: Force local host to bypass the Windows Firewall popup!
                    Arguments = "--server --server-host 127.0.0.1 --minimized",
                    WorkingDirectory = openRgbDir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Lighting] ERROR launching OpenRGB: {ex.Message}");
            }
        }
    }
}