namespace EvolveOS_Optimizer.Core.Interfaces
{
    public interface IComputerService : IMemoryService, IOperatingSystem
    {
        void RefreshMemory();

        event Action<byte, string> OnOptimizeProgressUpdate;
    }
}
