using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class NavigationHelper
    {
        public static void PurgePage(Page? page)
        {
            if (page == null) return;

            try
            {
                if (page is IPurgeable purgeablePage)
                {
                    purgeablePage.Purge();
                    Debug.WriteLine($"[Purge] {page.GetType().Name} Purge() executed.");
                }

                if (page.DataContext is IDisposable vm)
                {
                    vm.Dispose();
                }

                page.DataContext = null;
                page.Content = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Purge Error] {page.GetType().Name}: {ex.Message}");
            }
        }

        public static async Task TriggerDeepCleanupAsync(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        {
            await Task.Delay(1000);

            MemoryManager.ForceFullCleanup();
        }
    }
}