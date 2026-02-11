using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class BackgroundQueue
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task QueueTask(Func<Task> taskFunc)
        {
            await _semaphore.WaitAsync();
            try
            {
                await taskFunc();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}