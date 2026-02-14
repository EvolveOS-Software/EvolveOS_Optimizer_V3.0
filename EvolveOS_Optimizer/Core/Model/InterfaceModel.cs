using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    internal sealed class InterfaceModel : IBasePageItem
    {
        public string Name { get; set; } = string.Empty;
        public bool State { get; set; }

        public bool IsFaulted { get; set; }
    }
}
