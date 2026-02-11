using Microsoft.UI.Xaml.Media.Animation;
using System.Text;
using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Animation
{
    internal sealed class TypewriterAnimation
    {
        private static readonly Dictionary<TextBlock, CancellationTokenSource> ActiveCancellations = new();

        internal static void Create(string textToAnimate, TextBlock textBlock, TimeSpan duration)
        {

            if (ActiveCancellations.TryGetValue(textBlock, out var oldCts))
            {
                oldCts.Cancel();
                ActiveCancellations.Remove(textBlock);
            }

            var cts = new CancellationTokenSource();
            ActiveCancellations[textBlock] = cts;

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
            if (token.IsCancellationRequested) return;

            int charCount = text.Length;
            if (charCount == 0) return;

            double targetMs = totalDuration.TotalMilliseconds;
            double charPerMs = charCount / targetMs;
            int intervalMs = 10;
            int charsToAppend = (int)Math.Max(1, Math.Round(charPerMs * intervalMs));

            StringBuilder sb = new StringBuilder();

            try
            {
                for (int i = 0; i < charCount; i += charsToAppend)
                {

                    if (token.IsCancellationRequested) return;

                    int remaining = charCount - i;
                    int currentChunkSize = Math.Min(charsToAppend, remaining);
                    sb.Append(text.Substring(i, currentChunkSize));

                    target.Text = sb.ToString() + "|";

                    await Task.Delay(intervalMs);
                }

                for (int blink = 0; blink < 3; blink++)
                {
                    if (token.IsCancellationRequested) return;
                    target.Text = text + " ";
                    await Task.Delay(400);

                    if (token.IsCancellationRequested) return;
                    target.Text = text + "|";
                    await Task.Delay(400);
                }

                target.Text = text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Typewriter Error: {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    ActiveCancellations.Remove(target);
                }
            }
        }
    }
}