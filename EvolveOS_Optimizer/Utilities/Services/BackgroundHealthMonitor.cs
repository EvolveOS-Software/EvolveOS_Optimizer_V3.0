using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class BackgroundHealthMonitor
    {
        private static bool _isRunning = false;
        private static DateTime _lastCriticalNotificationTime = DateTime.MinValue;

        public static async Task StartMonitoringAsync(dynamic sharedViewModel)
        {
            if (_isRunning) return;
            _isRunning = true;

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

            while (await timer.WaitForNextTickAsync())
            {
                try
                {
                    if (sharedViewModel.RefreshCleanupSpaceCommand.CanExecute(null))
                    {
                        sharedViewModel.RefreshCleanupSpaceCommand.Execute(null);
                    }

                    int timeout = 0;
                    while (sharedViewModel.IsScanning && timeout < 30)
                    {
                        await Task.Delay(1000);
                        timeout++;
                    }

                    double ramPercentage = sharedViewModel.Computer?.Memory?.Physical?.Used?.Percentage ?? 0;
                    double totalRamGb = sharedViewModel.Computer?.Memory?.Physical?.Total?.Gigabytes ?? 16.0;

                    double vRamPercentage = sharedViewModel.Computer?.Memory?.Virtual?.Used?.Percentage ?? 0;
                    double totalVRamGb = sharedViewModel.Computer?.Memory?.Virtual?.Total?.Gigabytes ?? 16.0;

                    double junkGigabytes = ParseSizeToGigabytes(sharedViewModel.TotalSpaceToFree);

                    var healthResult = SystemHealthHelper.EvaluateHealth(
                        ramPercentage, totalRamGb,
                        vRamPercentage, totalVRamGb,
                        junkGigabytes);

                    if (healthResult.PenaltyScore >= 4 && (DateTime.Now - _lastCriticalNotificationTime).TotalHours >= 4)
                    {
                        _lastCriticalNotificationTime = DateTime.Now;

                        string criticalTitle = ResourceString.GetString("toast_health_critical_title") ?? "System Health Critical";
                        string criticalMsg = ResourceString.GetString("toast_health_critical_msg") ?? "System resources are heavily loaded or disk space is low. Click to optimize.";

                        SendCriticalHealthToast(criticalTitle, criticalMsg, sharedViewModel);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug($"[BackgroundMonitor] Error: {ex.Message}");
                }
            }
        }

        public static void SendCriticalHealthToast(string title, string message, dynamic sharedViewModel)
        {
            try
            {
                string aumid = "EvolveOS.Optimizer.App";

                string optimizeBtnText = ResourceString.GetString("toast_btn_optimize") ?? "Optimize Now";
                string ignoreBtnText = ResourceString.GetString("toast_btn_ignore") ?? "Ignore";

                string xmlPayload = $@"
        <toast scenario='reminder' launch='action=wakeup'>
            <visual>
                <binding template='ToastGeneric'>
                    <text>{title}</text>
                    <text>{message}</text>
                </binding>
            </visual>
            <actions>
                <action content='{optimizeBtnText}' arguments='optimize' activationType='background'/>
                <action content='{ignoreBtnText}' arguments='ignore' activationType='background'/>
            </actions>
        </toast>";

                var xmlDoc = new Windows.Data.Xml.Dom.XmlDocument();
                xmlDoc.LoadXml(xmlPayload);

                var toast = new Windows.UI.Notifications.ToastNotification(xmlDoc)
                {
                    Tag = "HealthWarning",
                    Group = "Optimizer"
                };

                toast.Activated += (sender, args) =>
                {
                    var toastArgs = args as Windows.UI.Notifications.ToastActivatedEventArgs;
                    string clickedArgument = toastArgs?.Arguments ?? "action=wakeup";

                    if (clickedArgument == "ignore")
                    {
                        System.Diagnostics.Debug.WriteLine("[NotifyLog] User clicked Ignore.");
                        return;
                    }

                    // Route EVERYTHING through the UI Thread
                    App.UIThreadDispatcher?.TryEnqueue(async () =>
                    {
                        // 1. Restore the Window so the Progress UI and Dialogs are visible
                        if (App.Current.MainWindow != null)
                        {
                            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
                            Win32Helper.ShowWindow(hwnd, 5);
                            Win32Helper.ShowWindow(hwnd, 9);
                            Win32Helper.SetForegroundWindow(hwnd);
                        }

                        // 2. Run the FULL Optimization
                        if (clickedArgument == "optimize")
                        {
                            System.Diagnostics.Debug.WriteLine("[NotifyLog] Starting FULL optimization via ViewModel...");

                            // Call your actual, comprehensive Optimize method!
                            // (Ensure you pass the correct Enum reason your app expects, like Manual or Automatic)
                            if (sharedViewModel != null)
                            {
                                await sharedViewModel.Optimize(Enums.Memory.Optimization.Reason.Manual);

                                // Note: You no longer need to send a "Success" toast here, 
                                // because your Optimize method already handles its own completion messages!
                            }
                        }
                    });
                };

                Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(aumid).Show(toast);
                System.Diagnostics.Debug.WriteLine("[NotifyLog] Raw Health Toast with buttons sent.");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotifyLog] WinRT Toast Error: {ex.Message}");
            }
        }

        private static double ParseSizeToGigabytes(string sizeString)
        {
            if (string.IsNullOrWhiteSpace(sizeString)) return 0;
            return 0;
        }
    }
}
