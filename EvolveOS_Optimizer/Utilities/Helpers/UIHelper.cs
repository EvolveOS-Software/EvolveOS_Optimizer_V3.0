using EvolveOS_Optimizer.Core.ViewModel;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

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

        public static void RegisterPageTransition(ContentControl container, FrameworkElement contextSource)
        {
            container.Loaded += (s, e) =>
            {
                var visual = ElementCompositionPreview.GetElementVisual(container);
                var compositor = visual.Compositor;

                var elasticEasing = compositor.CreateCubicBezierEasingFunction(
                    new Vector2(0.3f, 1.5f),
                    new Vector2(0.5f, 1.0f)
                );

                var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                scaleAnimation.Target = "Scale";
                scaleAnimation.InsertKeyFrame(0.0f, new Vector3(0.92f, 0.92f, 1.0f));
                scaleAnimation.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f), elasticEasing);
                scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);

                var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                opacityAnimation.Target = "Opacity";
                opacityAnimation.InsertKeyFrame(0.0f, 0.0f);
                opacityAnimation.InsertKeyFrame(1.0f, 1.0f);
                opacityAnimation.Duration = TimeSpan.FromMilliseconds(350);

                var animationGroup = compositor.CreateAnimationGroup();
                animationGroup.Add(scaleAnimation);
                animationGroup.Add(opacityAnimation);

                visual.CenterPoint = new Vector3((float)container.ActualWidth / 2, (float)container.ActualHeight / 2, 0);
                container.SizeChanged += (sender, args) =>
                {
                    visual.CenterPoint = new Vector3((float)args.NewSize.Width / 2, (float)args.NewSize.Height / 2, 0);
                };

                if (contextSource.DataContext is MainWinViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(MainWinViewModel.CurrentView))
                        {
                            visual.Scale = new Vector3(0.92f, 0.92f, 1.0f);
                            visual.Opacity = 0.0f;
                            visual.StartAnimationGroup(animationGroup);
                        }
                    };
                }
            };
        }
    }
}