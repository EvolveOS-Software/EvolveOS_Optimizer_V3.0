using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    internal sealed class PackagesModel : ViewModelBase
    {
        private string? _name;
        private bool _installed;
        private bool _isUnavailable;

        public string Name
        {
            get => _name ?? string.Empty;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public bool Installed
        {
            get => _installed;
            set { _installed = value; OnPropertyChanged(); }
        }

        public bool IsUnavailable
        {
            get => _isUnavailable;
            set { _isUnavailable = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }
}
