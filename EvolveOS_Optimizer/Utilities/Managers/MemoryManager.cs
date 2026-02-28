// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Runtime;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    internal static class MemoryManager
    {
        public static void ForceFullCleanup()
        {
            try
            {
                //Debug.WriteLine("[MemoryManager] Full Garbage Collection Triggered...");

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

                GC.WaitForPendingFinalizers();

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                // Log the result
                //long memoryUsed = Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024;
                //Debug.WriteLine($"[Memory Management] Deep Cleanup Complete. Private Bytes: {memoryUsed} MB");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryManager] Cleanup Error: {ex.Message}");
            }
        }
    }
}
