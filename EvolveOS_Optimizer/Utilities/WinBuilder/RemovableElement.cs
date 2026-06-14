using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public class RemovableElement : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsCapability { get; set; }
        public string? IconPath { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}