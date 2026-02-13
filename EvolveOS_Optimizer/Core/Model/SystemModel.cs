using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    public sealed class SystemModel : ITypedPageItem<double>
    {
        public string Name { get; set; } = string.Empty;
        public bool State { get; set; }
        public double Value { get; set; }
        public bool IsFaulted { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}