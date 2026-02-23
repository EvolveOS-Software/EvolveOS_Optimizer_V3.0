using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Model.Log
{
    public class LogOptimizationData : ILogData, IJsonSerializable
    {
        public LogOptimizationData()
        {
            Duration = string.Empty;
            Reason = string.Empty;
            MemoryAreas = new List<LogOptimizationDataMemoryArea>();
        }

        public string Duration { get; set; }

        public List<LogOptimizationDataMemoryArea> MemoryAreas { get; private set; }

        public string Reason { get; set; }

        public object ToJson()
        {
            var memoryAreas = MemoryAreas
                .OrderBy(m => m.Name)
                .Select(m => string.IsNullOrEmpty(m.Error)
                    ? new { name = m.Name, duration = m.Duration }
                    : (object)new { name = m.Name, duration = m.Duration, error = m.Error })
                .ToList();

            return new
            {
                reason = Reason,
                duration = Duration,
                memoryAreas
            };
        }

        public override string ToString()
        {
            return string.Format("{0} ({1}) - {2} area(s)", Reason, Duration, MemoryAreas.Count);
        }
    }
}