using System.Net.Http;
using System.Net.NetworkInformation;
using EvolveOS_Optimizer.Utilities.Managers;
using static EvolveOS_Optimizer.Utilities.Managers.NotificationManager;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class NetworkHelper
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        public static async Task<bool> IsConnectedAsync()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                Debug.WriteLine("[NetworkHelper] Hardware check reported no network available.");
                ShowOfflineNotification();
                return false;
            }

            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync("1.1.1.1", 2000);
                    if (reply.Status == IPStatus.Success)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkHelper] Ping 1.1.1.1 failed: {ex.Message}");
            }

            string[] endpoints = {
                "http://www.msftconnecttest.com/connecttest.txt",
                "https://www.google.com/generate_204"
            };

            foreach (var url in endpoints)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

                    using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkHelper] Failed {url}: {ex.Message}");
                }
            }

            ShowOfflineNotification();
            return false;
        }

        private static void ShowOfflineNotification()
        {
            string message = ResourceString.GetString("no_internet_connection_notif_key")
                             ?? "No internet connection detected.";

            NotificationManager.Show("Error", message)
                        .WithSeverity(NoticeSeverity.Error)
                        .WithDuration(5000)
                        .Create();
        }
    }
}