// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
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

                    var healthResult = await SystemHealthHelper.EvaluateHealthAsync();

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

        public static void SendCriticalHealthToast(string title, string message, Core.ViewModel.DiagnosticsPageViewModel sharedViewModel)
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
                    if (args is not Windows.UI.Notifications.ToastActivatedEventArgs toastArgs) return;

                    string clickedArgument = toastArgs.Arguments ?? "action=wakeup";
                    if (clickedArgument == "ignore") return;

                    App.UIThreadDispatcher?.TryEnqueue(async () =>
                    {
                        if (App.MainWindow != null)
                        {
                            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                            Win32Helper.ShowWindow(hwnd, 5);
                            Win32Helper.ShowWindow(hwnd, 9);
                            Win32Helper.SetForegroundWindow(hwnd);
                        }

                        if (clickedArgument == "optimize" && sharedViewModel != null)
                        {
                            await NotificationManager.ExecuteBackgroundOptimizationAsync(sharedViewModel);
                        }
                    });
                };

                Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(aumid).Show(toast);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[NotifyLog] WinRT Toast Error: {ex.Message}");
            }
        }

        private static double ParseSizeToGigabytes(string sizeString)
        {
            if (string.IsNullOrWhiteSpace(sizeString)) return 0;
            return 0;
        }
    }
}
