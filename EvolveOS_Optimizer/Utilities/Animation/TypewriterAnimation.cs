using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EvolveOS_Optimizer.Utilities.Animation
{
    internal sealed class TypewriterAnimation
    {
        // Keep track of active tasks to allow cancellation/replacement
        private static readonly Dictionary<TextBlock, CancellationTokenSource> ActiveCancellations = new();

        internal static void Create(string textToAnimate, TextBlock textBlock, TimeSpan duration)
        {
            if (textBlock == null || string.IsNullOrEmpty(textToAnimate)) return;

            // 1. Stop any existing animation on this TextBlock
            if (ActiveCancellations.TryGetValue(textBlock, out var oldCts))
            {
                oldCts.Cancel();
                ActiveCancellations.Remove(textBlock);
            }

            // 2. Create new cancellation token for this run
            var cts = new CancellationTokenSource();
            ActiveCancellations[textBlock] = cts;

            // 3. Handle Opacity Animation (Native WinUI 3 Storyboard)
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

            // 4. Handle Typewriter effect via Async Loop (Replacement for StringKeyFrames)
            _ = RunTypewriterAsync(textToAnimate, textBlock, duration, cts.Token);
        }

        private static async Task RunTypewriterAsync(string text, TextBlock target, TimeSpan totalDuration, System.Threading.CancellationToken token)
        {
            int charCount = text.Length;
            int intervalMs = (int)(totalDuration.TotalMilliseconds / charCount);
            StringBuilder sb = new StringBuilder();

            try
            {
                for (int i = 0; i < charCount; i++)
                {
                    // Check if we were cancelled by a newer animation request
                    if (token.IsCancellationRequested) return;

                    sb.Append(text[i]);
                    target.Text = sb.ToString();

                    // Delay between characters
                    await Task.Delay(intervalMs);
                }
            }
            catch (Exception)
            {
                // Silently handle task disposal
            }
            finally
            {
                // Clean up dictionary if we finished naturally
                if (!token.IsCancellationRequested && ActiveCancellations.ContainsKey(target))
                {
                    ActiveCancellations.Remove(target);
                }
            }
        }
    }
}