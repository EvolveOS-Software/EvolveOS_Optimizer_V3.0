using EvolveOS_Optimizer.Core.Model.MemoryModel;

namespace EvolveOS_Optimizer.Core.Interfaces
{
    public interface IMemoryService
    {
        Memory Memory { get; }
        Task Optimize(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas);
    }
}
