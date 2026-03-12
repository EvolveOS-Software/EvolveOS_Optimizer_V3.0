// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Security;
using Windows.UI.Notifications;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class NativeToastHelper
    {
        public static void SendNativeToast(string title, string message)
        {
            try
            {
                string aumid = "EvolveOS.Optimizer.App";

                var stats = message.Split(new[] { " | ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

                string textLinesXml = "";
                foreach (var stat in stats)
                {
                    textLinesXml += $"<text hint-style='body' hint-wrap='true'>{SecurityElement.Escape(stat.Trim())}</text>";
                }

                string xmlPayload = $@"
                <toast scenario='reminder'>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{SecurityElement.Escape(title)}</text>
                            <group>
                                <subgroup>
                                    {textLinesXml}
                                </subgroup>
                            </group>
                        </binding>
                    </visual>
                </toast>";

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlPayload);

                var toast = new ToastNotification(xmlDoc)
                {
                    Tag = "Maintenance",
                    Group = "Optimizer"
                };

                toast.Activated += (sender, args) =>
                {
                    App.UIThreadDispatcher?.TryEnqueue(() =>
                    {
                        if (App.MainWindow != null)
                        {
                            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                            Win32Helper.ShowWindow(hwnd, 5);
                            Win32Helper.ShowWindow(hwnd, 9);
                            Win32Helper.SetForegroundWindow(hwnd);
                        }
                    });
                };

                ToastNotificationManager.CreateToastNotifier(aumid).Show(toast);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotifyLog] WinRT Toast Error: {ex.Message}");
            }
        }
    }
}