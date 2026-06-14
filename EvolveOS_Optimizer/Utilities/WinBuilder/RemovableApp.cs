using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public class RemovableApp : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string? IconPath { get; set; }
        public string? Description { get; set; }

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