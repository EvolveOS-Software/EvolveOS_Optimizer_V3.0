namespace EvolveOS_Optimizer.Core.Model
{
    internal class ProcessManagerModel
    {
        public string Name { get; set; } = string.Empty;
        public int Id
        {
            get; set;
        }
        public double MemoryMB
        {
            get; set;
        }
        public int ThreadCount
        {
            get; set;
        }
        public string MemoryDisplay => $"{MemoryMB:F1} MB";
        public double MemoryPercent => Math.Min(MemoryMB / 500.0 * 100, 100);

        public void UpdateFrom(ProcessManagerModel other)
        {
            Name = other.Name;
            MemoryMB = other.MemoryMB;
            ThreadCount = other.ThreadCount;
        }
    }
}
