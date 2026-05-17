using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    public sealed class SystemModel : ViewModelBase, ITypedPageItem<double>
    {
        public string Name { get; set; } = string.Empty;

        private bool _state;
        public bool State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        public double Value { get; set; }
        public bool IsFaulted { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}