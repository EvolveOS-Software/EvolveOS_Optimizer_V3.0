// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Enums;

public static class Memory
{
    [Flags]
    public enum Areas
    {
        None = 0,
        CombinedPageList = 1,
        ModifiedFileCache = 2,
        ModifiedPageList = 4,
        RegistryCache = 8,
        StandbyList = 16,
        StandbyListLowPriority = 32,
        SystemFileCache = 64,
        WorkingSet = 128,
        DiskCleanup = 256,
        WindowsOld = 512,
        FlushDns = 1024
    }

    public static class Optimization
    {
        public enum Reason
        {
            LowMemory,
            Manual,
            Schedule
        }
    }

    public enum Unit { B, KB, MB, GB, TB, PB, EB, ZB, YB }
}