// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using System.Threading.Tasks;

namespace EvolveOS_Optimizer.Core.Base
{
    public abstract class PageBase : Page, IPurgeable
    {
        public PageBase()
        {
            this.Unloaded += (s, e) => _ = Purge();
        }

        public virtual Task Purge()
        {
            if (this.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            this.DataContext = null;

            return Task.CompletedTask;
        }
    }
}