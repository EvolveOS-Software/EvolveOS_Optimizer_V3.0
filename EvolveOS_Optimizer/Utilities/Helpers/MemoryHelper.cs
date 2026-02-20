using System;
using System.Diagnostics;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Maintenance;


namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class MemoryHelper
    {
        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    using (Process process = Process.GetCurrentProcess())
                    {
                        Win32Helper.SetProcessWorkingSetSize(process.Handle, -1, -1);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Memory trim failed: {ex.Message}");
            }
        }

        public static void OneClickBoost()
        {
            try
            {
                ClearingMemory.ClearFileSystemCache(ClearStandbyCache: true, lowPriority: false);

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                TrimWorkingSet();

                ClearingMemory.EmptyWorkingSetFunction();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(new Exception("One-Click Boost failed", ex));
            }
        }
    }
}
