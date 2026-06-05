// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Enums;

public static class Log
{
    [Flags]
    public enum Levels
    {
        Debug = 1,
        Information = 2,
        Warning = 4,
        Error = 8
    }
}
