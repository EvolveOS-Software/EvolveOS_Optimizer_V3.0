// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Core.Model
{
    public class CategoryDisplayItem : INotifyPropertyChanged
    {
        public RecordType Type { get; }
        public string Name { get; }
        public string Symbol { get; }

        private bool _isSelected;

        public CategoryDisplayItem(RecordType type, string name, string symbol)
        {
            Type = type;
            Name = name;
            Symbol = symbol;
        }

        public override string ToString() => Name;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
