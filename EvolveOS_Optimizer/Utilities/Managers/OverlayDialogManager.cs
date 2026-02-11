using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;

internal sealed class OverlayDialogManager
{
    private readonly FrameworkElement _overlay;
    private readonly Button _btnPrimary;
    private readonly Button _btnSecondary;

    internal OverlayDialogManager(FrameworkElement overlay, Button btnPrimary, Button btnSecondary)
    {
        _overlay = overlay;
        _btnPrimary = btnPrimary;
        _btnSecondary = btnSecondary;
    }

    internal async Task<bool> Show()
    {
        var tcs = new TaskCompletionSource<bool>();
        _overlay.Visibility = Visibility.Visible;

        var visual = ElementCompositionPreview.GetElementVisual(_overlay);
        visual.Opacity = 0;

        var fadeIn = visual.Compositor.CreateScalarKeyFrameAnimation();
        fadeIn.InsertKeyFrame(1.0f, 1.0f);
        fadeIn.Duration = TimeSpan.FromSeconds(0.2);
        visual.StartAnimation("Opacity", fadeIn);

        void OnPrimary(object s, RoutedEventArgs e) { Detach(); tcs.TrySetResult(true); }
        void OnSecondary(object s, RoutedEventArgs e) { Detach(); tcs.TrySetResult(false); }

        void Detach()
        {
            _btnPrimary.Click -= OnPrimary;
            _btnSecondary.Click -= OnSecondary;
        }

        _btnPrimary.Click += OnPrimary;
        _btnSecondary.Click += OnSecondary;

        bool result = await tcs.Task;

        var fadeOut = visual.Compositor.CreateScalarKeyFrameAnimation();
        fadeOut.InsertKeyFrame(1.0f, 0.0f);
        fadeOut.Duration = TimeSpan.FromSeconds(0.15);

        var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Opacity", fadeOut);
        batch.Completed += (s, e) => _overlay.Visibility = Visibility.Collapsed;
        batch.End();

        return result;
    }
}