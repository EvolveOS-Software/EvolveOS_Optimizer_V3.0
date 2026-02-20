using EvolveOS_Optimizer.Core.Model.MemoryModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public class Computer
    {
        public Computer()
        {
            Memory = new Memory();
            OperatingSystem = new OperatingSystem();
        }

        public Memory Memory { get; set; }

        public OperatingSystem OperatingSystem { get; set; }

    }
}
