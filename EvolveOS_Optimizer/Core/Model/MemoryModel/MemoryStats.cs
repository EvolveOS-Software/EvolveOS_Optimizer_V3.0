namespace EvolveOS_Optimizer.Core.Model.MemoryModel
{
    public class MemoryStats
    {
        public MemoryStats(long free, long total, int? used = null)
        {
            Free = new MemorySize(free);
            Total = new MemorySize(total);
            Used = new MemorySize(total >= free ? total - free : free - total);

            if (used == null)
            {
                used = Used.Value > 0 && Total.Value > 0 ? (int)(Used.Value * 100 / Total.Value) : 0;
            }

            Free.Percentage = (int)(100 - used);
            Used.Percentage = (int)used;
        }

        public MemorySize Free { get; private set; }
        public MemorySize Total { get; private set; }
        public MemorySize Used { get; private set; }

        public override string ToString()
        {
            return string.Format("({0:0.#} {1}) {2} | {3} - {4} | {5}", Total.Value, Total.Unit, "Used:", Used, "Free:", Free);
        }
    }
}
