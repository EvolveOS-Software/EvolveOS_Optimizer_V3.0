using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    internal sealed class ServicesModel : ViewModelBase, IBasePageItem
    {
        public string Name { get; set; } = string.Empty;
        private bool _state;
        public bool State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }
        public bool IsFaulted { get; set; }
    }
}
