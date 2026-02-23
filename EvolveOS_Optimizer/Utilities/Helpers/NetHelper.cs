namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class NetHelper
    {
        public static void FlushDns()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ipconfig",
                        Arguments = "/flushdns",
                        Verb = "runas",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(5000);
            }
            catch (Exception)
            {
                // Logging or error handling here
            }
        }
    }
}
