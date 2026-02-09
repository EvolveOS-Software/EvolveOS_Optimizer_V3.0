using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;

namespace EvolveOS_Optimizer.Utilities.Animation
{
    public static class NumberAnimation
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.RegisterAttached("Value", typeof(string), typeof(NumberAnimation),
                new PropertyMetadata("0", OnValueChanged));

        public static string GetValue(DependencyObject obj) => (string)obj.GetValue(ValueProperty);
        public static void SetValue(DependencyObject obj, string value) => obj.SetValue(ValueProperty, value);

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                if (!double.TryParse(e.OldValue?.ToString(), out double from)) from = 0;
                if (!double.TryParse(e.NewValue?.ToString(), out double to)) to = 0;

                var visual = ElementCompositionPreview.GetElementVisual(textBlock);
                var compositor = visual.Compositor;

                var propSet = compositor.CreatePropertySet();
                propSet.InsertScalar("Value", (float)from);

                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(1.0f, (float)to);
                animation.Duration = TimeSpan.FromMilliseconds(500);

                var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                propSet.StartAnimation("Value", animation);

                UpdateTextWithStep(textBlock, from, to);

                batch.End();
            }
        }

        private static async void UpdateTextWithStep(TextBlock tb, double from, double to)
        {
            int steps = 15;
            double diff = (to - from) / steps;

            var dispatcher = tb.DispatcherQueue;

            for (int i = 0; i <= steps; i++)
            {
                try
                {
                    double current = from + (diff * i);
                    string resultText = Math.Round(current).ToString();

                    if (dispatcher == null) break;

                    dispatcher.TryEnqueue(() =>
                    {
                        try { tb.Text = resultText; } catch { /* Element might be disposed */ }
                    });

                    await System.Threading.Tasks.Task.Delay(33);
                }
                catch { break; }
            }

            dispatcher?.TryEnqueue(() =>
            {
                try { tb.Text = Math.Round(to).ToString(); } catch { }
            });
        }
    }
}