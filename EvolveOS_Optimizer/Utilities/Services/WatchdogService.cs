// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class WatchdogService
    {
        [DllImport("EvolveOS_Watchdog.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern void InitializeDatabaseWatchdog(string dbFileName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void EnsureWatchdogAndStart(string dbFileName)
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string dllPath = Path.Combine(exeDir, "EvolveOS_Watchdog.dll");

            bool isFileReady = false;
            for (int i = 0; i < 10; i++)
            {
                if (File.Exists(dllPath))
                {
                    try
                    {
                        using (FileStream fs = File.Open(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            isFileReady = true;
                            break;
                        }
                    }
                    catch (IOException)
                    {
                        // File is locked. Wait 250ms and try again.
                    }
                }
                Thread.Sleep(250);
            }

            if (!isFileReady)
            {
                ShowFatalErrorAndExit(
                    "A critical component (EvolveOS_Watchdog.dll) is missing or blocked by your Antivirus.\n\nPlease whitelist the application or reinstall."
                );
            }

            SetDllDirectory(exeDir);

            try
            {
                InitializeDatabaseWatchdog(dbFileName);
            }
            catch (Exception ex)
            {
                ShowFatalErrorAndExit(
                    $"An unexpected error occurred while starting the database watchdog:\n\n{ex.Message}"
                );
            }
            finally
            {
                SetDllDirectory(null!);
            }
        }

        private static void ShowFatalErrorAndExit(string message)
        {
            uint MB_FATAL_ERROR = 0x00040010;

            MessageBox(IntPtr.Zero, message, "EvolveOS Optimizer - Fatal Error", MB_FATAL_ERROR);

            Process.GetCurrentProcess().Kill();
            Environment.Exit(1);
        }
    }
}