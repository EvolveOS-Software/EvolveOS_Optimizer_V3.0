// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    public class ObservableItem<T> : ObservableObject
    {
        #region Fields
        private bool _isEnabled;
        private string _tooltip;
        #endregion

        #region Constructor
        public ObservableItem(string name, Func<T> getter, Action<T> setter, bool isEnabled = true, string tooltip = "")
        {
            Getter = getter;
            _isEnabled = isEnabled;
            Name = name;
            Setter = setter;
            _tooltip = tooltip;
        }
        #endregion

        #region Properties
        public Func<T> Getter { get; private set; }
        public string Name { get; private set; }
        public Action<T> Setter { get; private set; }

        public string Tooltip
        {
            get => _tooltip;
            set { _tooltip = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public T Value
        {
            get => Getter != null ? Getter() : default(T)!;
            set
            {
                if (Setter != null)
                {
                    Setter(value);
                    OnPropertyChanged();
                }
            }
        }
        #endregion
    }
}