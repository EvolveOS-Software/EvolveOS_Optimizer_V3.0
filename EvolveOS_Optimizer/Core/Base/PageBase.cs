using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Base
{
    public abstract class PageBase : Page, IPurgeable
    {
        public PageBase()
        {
            this.Unloaded += (s, e) => Purge();
        }

        public virtual void Purge()
        {
            if (this.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            this.DataContext = null;
        }
    }
}
