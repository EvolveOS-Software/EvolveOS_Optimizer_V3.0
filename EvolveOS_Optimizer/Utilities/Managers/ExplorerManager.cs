namespace EvolveOS_Optimizer.Utilities.Managers
{
    internal class ExplorerManager
    {
        internal static readonly Dictionary<string, bool> PackageMapping = new[]
        {
            new { Package = "Widgets", NeedRestart = true },
            new { Package = "Edge", NeedRestart = true }
        }.ToDictionary(x => x.Package, x => x.NeedRestart);

        internal static void Restart(Action? action = null)
        {
            Task.Run(() =>
            {
                try
                {
                    Process[] explorers = Process.GetProcessesByName("explorer");
                    foreach (Process p in explorers)
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(3000);
                        }
                        catch { /* Process already exiting */ }
                    }

                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error killing explorer: {ex.Message}");
                }
                finally
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/factory,{EFD469A7-7E0A-4517-8B39-45873948DA31}",
                        UseShellExecute = true
                    });
                }
            });
        }
    }
}