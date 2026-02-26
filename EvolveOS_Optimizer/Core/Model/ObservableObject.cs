using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    public class ObservableItem<T> : ObservableObject
    {
        private bool _isEnabled;
        private string _tooltip;

        public ObservableItem(string name, Func<T> getter, Action<T> setter, bool isEnabled = true, string tooltip = "")
        {
            Getter = getter;
            _isEnabled = isEnabled;
            Name = name;
            Setter = setter;
            _tooltip = tooltip;
        }

        public Func<T> Getter { get; private set; }
        public string Name { get; private set; }
        public Action<T> Setter { get; private set; }
        public string Tooltip { get => _tooltip; set { _tooltip = value; OnPropertyChanged(); } }
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }
        public T Value
        {
            get { return Getter != null ? Getter() : default(T)!; }
            set { if (Setter != null) { Setter(value); OnPropertyChanged(); } }
        }
    }
}
