using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    public sealed partial class HomePageModel : ViewModelBase
    {
        public string? Name { get; set; }

        private string? _data;
        public string? Data
        {
            get => _data;
            set
            {
                _data = value;
                OnPropertyChanged();
            }
        }

        public int BlurValue { get; set; }

        public Visibility IpVisibility { get; set; }
    }
}