using System.Threading;
using Microsoft.UI.Xaml.Media.Animation;
using EvolveOS_Optimizer.Utilities.Animation;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed partial class DescriptionBlock : Microsoft.UI.Xaml.Controls.UserControl
    {
        private CancellationTokenSource? _scrollCts;
        private CancellationTokenSource? _debounceCts;
        private Storyboard? _currentStoryboard;

        public string DefaultText
        {
            get => (string)GetValue(DefaultTextProperty);
            set => SetValue(DefaultTextProperty, value);
        }

        public static readonly DependencyProperty DefaultTextProperty =
            DependencyProperty.Register(
                nameof(DefaultText),
                typeof(string),
                typeof(DescriptionBlock),
                new PropertyMetadata(string.Empty, OnDefaultTextChanged));

        private static void OnDefaultTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DescriptionBlock block && e.NewValue is string text)
            {
                if (!block.IsLoaded)
                {
                    block.FunctionDescription.Text = text;
                    return;
                }

                TypewriterAnimation.Create(text, block.FunctionDescription, TimeSpan.FromMilliseconds(300));
            }
        }

        public string Text
        {
            get => FunctionDescription.Text;
            set
            {
                CancelAndDispose(ref _debounceCts);
                CancelAndDispose(ref _scrollCts);

                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                StopScrolling();
                Scroller.ChangeView(null, 0, null, true);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(100, token);

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!token.IsCancellationRequested)
                            {
                                _ = StartTypewriterWithDebounce(value, token);
                            }
                        });
                    }
                    catch (OperationCanceledException) { }
                });
            }
        }

        private async Task StartTypewriterWithDebounce(string text, CancellationToken token)
        {
            try
            {
                await Task.Delay(50, token);

                if (token.IsCancellationRequested) return;

                TimeSpan duration = text.Length <= 50 ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromMilliseconds(300);

                TypewriterAnimation.Create(text, FunctionDescription, duration);

                CancelAndDispose(ref _scrollCts);
                _scrollCts = new CancellationTokenSource();

                await StartAutoScrollAsync(text, _scrollCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[DescriptionBlock] Error: {ex.Message}"); }
        }

        public DescriptionBlock()
        {
            this.InitializeComponent();
            this.Unloaded += (s, e) =>
            {
                CancelAndDispose(ref _scrollCts);
                CancelAndDispose(ref _debounceCts);
                StopScrolling();
                this.DataContext = null;
            };
        }

        private void StopScrolling()
        {
            _currentStoryboard?.Stop();
            _currentStoryboard = null;
        }

        private async Task StartAutoScrollAsync(string text, CancellationToken token)
        {
            try
            {
                await Task.Delay(2000, token);

                if (text == DefaultText || token.IsCancellationRequested) return;

                double maxOffset = Scroller.ScrollableHeight;
                if (maxOffset <= 0) return;

                double durationSeconds = Math.Max(2.0, maxOffset / 20);

                _currentStoryboard = new Storyboard();
                DoubleAnimation animation = new DoubleAnimation
                {
                    From = 0,
                    To = maxOffset,
                    Duration = new Duration(TimeSpan.FromSeconds(durationSeconds)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };

                Storyboard.SetTarget(animation, Scroller);
                Storyboard.SetTargetProperty(animation, "(local:DescriptionBlock.ScrollViewerBehavior.VerticalOffset)");

                _currentStoryboard.Children.Add(animation);

                if (!token.IsCancellationRequested)
                {
                    _currentStoryboard.Begin();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void CancelAndDispose(ref CancellationTokenSource? cts)
        {
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch (ObjectDisposedException) { }
                finally { cts = null; }
            }
        }

        public static class ScrollViewerBehavior
        {
            public static readonly DependencyProperty VerticalOffsetProperty =
                DependencyProperty.RegisterAttached("VerticalOffset", typeof(double), typeof(ScrollViewerBehavior), new PropertyMetadata(0.0, OnVerticalOffsetChanged));

            public static double GetVerticalOffset(ScrollViewer viewer) => (double)viewer.GetValue(VerticalOffsetProperty);
            public static void SetVerticalOffset(ScrollViewer viewer, double value) => viewer.SetValue(VerticalOffsetProperty, value);

            private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                if (d is ScrollViewer viewer)
                {
                    viewer.ChangeView(null, (double)e.NewValue, null, true);
                }
            }
        }
    }
}