using Microsoft.UI.Xaml.Media.Animation;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Utilities.Animation
{
    internal sealed class TypewriterAnimation
    {
        private static readonly ConditionalWeakTable<TextBlock, CancellationTokenSource> ActiveCancellations = new();

        internal static void Create(string textToAnimate, TextBlock textBlock, TimeSpan duration)
        {
            if (textBlock == null) return;

            if (ActiveCancellations.TryGetValue(textBlock, out var oldCts))
            {
                try
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }
                catch (ObjectDisposedException) { }
                ActiveCancellations.Remove(textBlock);
            }

            var cts = new CancellationTokenSource();
            ActiveCancellations.Add(textBlock, cts);

            textBlock.Opacity = 0;
            Storyboard storyboard = new Storyboard();
            DoubleAnimation opacityAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(duration)
            };
            Storyboard.SetTarget(opacityAnim, textBlock);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            storyboard.Children.Add(opacityAnim);
            storyboard.Begin();

            _ = RunTypewriterAsync(textToAnimate, textBlock, duration, cts.Token);
        }

        private static async Task RunTypewriterAsync(string text, TextBlock target, TimeSpan totalDuration, CancellationToken token)
        {
            if (token.IsCancellationRequested || target == null) return;

            var dispatcher = target.DispatcherQueue;
            if (dispatcher == null) return;

            int charCount = text.Length;
            if (charCount == 0) return;

            double totalMs = totalDuration.TotalMilliseconds;
            int intervalMs = 15;
            double charsPerMs = charCount / totalMs;
            int charsToAppend = (int)Math.Max(1, Math.Round(charsPerMs * intervalMs));

            StringBuilder sb = new StringBuilder();

            try
            {
                for (int i = 0; i < charCount; i += charsToAppend)
                {
                    if (token.IsCancellationRequested) return;

                    await Task.Delay(intervalMs, token);

                    int remaining = charCount - i;
                    int currentChunkSize = Math.Min(charsToAppend, remaining);
                    sb.Append(text.Substring(i, currentChunkSize));

                    string currentText = sb.ToString() + "|";

                    dispatcher.TryEnqueue(() =>
                    {
                        if (!token.IsCancellationRequested && target.IsLoaded)
                        {
                            target.Text = currentText;
                        }
                    });
                }

                dispatcher.TryEnqueue(() =>
                {
                    if (!token.IsCancellationRequested && target.IsLoaded)
                        target.Text = text;
                });
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Typewriter] Actual Error: {ex.Message}");
            }
            finally
            {
                ActiveCancellations.Remove(target);
            }
        }
    }
}