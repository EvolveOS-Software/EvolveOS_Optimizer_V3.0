using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Core.Base
{
    public class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        protected bool _isDisposed = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        public virtual void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            if (_dispatcherQueue?.HasThreadAccess ?? true)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
            else
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
                });
            }
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propName);
            return true;
        }

        protected void ClearPropertyChangedListeners()
        {
            if (PropertyChanged != null)
            {
                foreach (var d in PropertyChanged.GetInvocationList())
                {
                    PropertyChanged -= (PropertyChangedEventHandler)d;
                }
            }
        }



        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isDisposed = true;
                ClearPropertyChangedListeners();
                Debug.WriteLine($"[Memory Management] {this.GetType().Name} base disposal complete.");
            }
        }
    }
}