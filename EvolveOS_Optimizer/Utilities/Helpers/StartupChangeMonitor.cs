// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class StartupChangeMonitor
    {
        private static bool _isWatching = false;
        private static HashSet<string> _knownApps = new();

        public static async void StartWatching()
        {
            if (_isWatching) return;
            _isWatching = true;

            var initialApps = await StartupManagerHelper.GetStartupAppsAsync();
            _knownApps = initialApps.Select(a => a.Name).ToHashSet();

            await Task.Run(async () =>
            {
                while (_isWatching)
                {
                    await Task.Delay(TimeSpan.FromMinutes(10));

                    var currentApps = await StartupManagerHelper.GetStartupAppsAsync();
                    foreach (var app in currentApps)
                    {
                        if (!_knownApps.Contains(app.Name))
                        {
                            _knownApps.Add(app.Name);

                            _ = ErrorLogging.LogInfo($"Startup Change Detected: {app.DisplayName} added itself to startup.");

                            string title = ResourceString.GetString("startup_manager_page_toast_new_app_title");
                            string message = string.Format(ResourceString.GetString("startup_manager_page_toast_new_app_message"), app.DisplayName);

                            NotificationManager.SendNativeToast(title, message);
                        }
                    }
                }
            });
        }
    }
}