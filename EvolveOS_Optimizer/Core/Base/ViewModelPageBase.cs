using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace EvolveOS_Optimizer.Core.Base
{
    internal interface IBasePageItem
    {
        string Name { get; set; }
        bool State { get; set; }
        bool IsFaulted { get; set; }
    }

    internal interface ITypedPageItem<T> : IBasePageItem
    {
        T Value { get; set; }
    }

    internal abstract class ViewModelPageBase<TModel, TTweaksClass> : ViewModelBase
        where TModel : class, IBasePageItem, new()
        where TTweaksClass : new()
    {
        public ObservableCollection<TModel> Toggles { get; private set; }

        public TModel? this[string name] => Toggles.FirstOrDefault(d => d.Name == name);

        protected abstract Dictionary<string, object> GetControlStates();
        protected abstract void Analyze(TTweaksClass tweaks);

        protected ViewModelPageBase()
        {
            TTweaksClass tweaks = new TTweaksClass();
            Analyze(tweaks);

            var items = GetControlStates().Select(kvp => CreateModelFromState(kvp.Key, kvp.Value));
            Toggles = new ObservableCollection<TModel>(items);
        }

        private TModel CreateModelFromState(string name, object parameter)
        {
            TModel model = new TModel { Name = name };

            switch (parameter)
            {
                case bool b:
                    model.State = b;
                    break;
                case double d when model is ITypedPageItem<double> doubleItem:
                    doubleItem.Value = d;
                    break;
                case string s when model is ITypedPageItem<string> stringItem:
                    stringItem.Value = s;
                    if (s.Contains("!!")) model.IsFaulted = true;
                    break;
            }

            OnModelCreated(model);
            return model;
        }

        public override void Dispose()
        {
            Toggles?.Clear();
            base.Dispose();

            Debug.WriteLine($"[Memory Management] {this.GetType().Name} disposed and Toggles cleared.");
        }

        protected virtual void OnModelCreated(TModel model) { }
    }
}