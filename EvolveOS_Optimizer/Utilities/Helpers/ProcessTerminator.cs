namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal static class ProcessTerminator
    {
        internal static async Task KillProcessTreeAsync(int processId)
        {
            if (processId <= 0)
            {
                return;
            }

            await RunCommandAsync("taskkill", $"/T /F /PID {processId}");

            var tasks = new List<Task>
            {
                RunCommandAsync("sc", "stop TrustedInstaller"),

                RunCommandAsync("sc", "stop msiserver"),

                RunCommandAsync("taskkill", "/F /IM dism.exe"),

                RunCommandAsync("taskkill", "/F /IM DismHost.exe"),

                RunCommandAsync("taskkill", "/F /IM sfc.exe"),

                RunCommandAsync("taskkill", "/F /IM chkdsk.exe"),

                RunCommandAsync("taskkill", "/F /IM TiWorker.exe"),

                RunCommandAsync("taskkill", "/F /IM wusa.exe")
            };

            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                // Some tasks took too long
            }
            catch
            {
                // Ignore other errors during cleanup
            }

            await Task.Delay(500);

            var finalKillTasks = new List<Task>
            {
                RunCommandAsync("taskkill", "/F /IM dism.exe"),
                RunCommandAsync("taskkill", "/F /IM DismHost.exe"),
                RunCommandAsync("taskkill", "/F /IM sfc.exe"),
                RunCommandAsync("taskkill", "/F /IM TiWorker.exe")
            };

            try
            {
                await Task.WhenAll(finalKillTasks).WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Ignore errors
            }
        }

        private static async Task RunCommandAsync(string fileName, string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();

                using var cts = new System.Threading.CancellationTokenSource(2000);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(); } catch { }
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
