namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class UIHelper
    {
        private static bool _isProcessing = false;

        public static void ApplyBackdrop(Window window, string name)
        {
            if (window == null || _isProcessing) return;

            window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            {
                _isProcessing = true;
                try
                {
                    window.SystemBackdrop = null;

                    await Task.Yield();
                    await Task.Delay(50);

                    window.SystemBackdrop = name switch
                    {
                        "Mica" => new MicaBackdrop()
                        { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base },
                        "MicaAlt" => new MicaBackdrop()
                        { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt },
                        "Acrylic" => new DesktopAcrylicBackdrop(),
                        _ => null
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Backdrop Safe-Guard] {ex.Message}");
                }
                finally
                {
                    _isProcessing = false;
                }
            });
        }
    }
}