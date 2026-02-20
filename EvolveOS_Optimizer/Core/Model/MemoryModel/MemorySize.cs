using EvolveOS_Optimizer.Utilities.Extensions;

namespace EvolveOS_Optimizer.Core.Model.MemoryModel
{
    public class MemorySize
    {
        public MemorySize(long bytes)
        {
            Bytes = bytes;

            var memory = bytes.ToMemoryUnit();

            Unit = memory.Value;
            Value = memory.Key;
        }

        public long Bytes { get; private set; }
        public int Percentage { get; set; }
        public Enums.Memory.Unit Unit { get; private set; }
        public double Value { get; private set; }

        public override string ToString()
        {
            return string.Format("{0:0.#} {1} ({2}%)", Value, Unit, Percentage);
        }
    }
}
