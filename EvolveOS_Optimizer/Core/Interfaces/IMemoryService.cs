// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces
{
    public interface IMemoryService
    {
        Memory Memory { get; }
        Task Optimize(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas);
    }
}
