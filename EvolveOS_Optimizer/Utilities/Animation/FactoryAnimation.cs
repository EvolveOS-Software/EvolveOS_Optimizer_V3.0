using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using System.Numerics;

namespace EvolveOS_Optimizer.Utilities.Animation
{
    internal static class FactoryAnimation
    {
        internal static void AnimateHexagonCardLiftIn(FrameworkElement container, double liftValue = -8)
        {
            var transform = container.RenderTransform as CompositeTransform;
            if (transform == null) return;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var scaleXAnim = new DoubleAnimation { To = 1.05, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation { To = 1.05, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = ease };

            var liftYAnim = new DoubleAnimation { To = liftValue, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = ease };

            var sb = new Storyboard();
            Storyboard.SetTarget(scaleXAnim, transform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");

            Storyboard.SetTarget(scaleYAnim, transform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

            Storyboard.SetTarget(liftYAnim, transform);
            Storyboard.SetTargetProperty(liftYAnim, "TranslateY");

            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);
            sb.Children.Add(liftYAnim);
            sb.Begin();
        }

        internal static void AnimateHexagonCardLiftOut(FrameworkElement container)
        {
            var transform = container.RenderTransform as CompositeTransform;
            if (transform == null) return;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var sb = new Storyboard();

            var animX = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = ease };
            var animY = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = ease };
            var animL = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = ease };

            Storyboard.SetTarget(animX, transform);
            Storyboard.SetTargetProperty(animX, "ScaleX");
            Storyboard.SetTarget(animY, transform);
            Storyboard.SetTargetProperty(animY, "ScaleY");
            Storyboard.SetTarget(animL, transform);
            Storyboard.SetTargetProperty(animL, "TranslateY");

            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Children.Add(animL);
            sb.Begin();
        }

        internal static void AnimateEntrance(FrameworkElement targetElement, int index)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(targetElement);
            Compositor compositor = visual.Compositor;

            visual.Opacity = 0.0f;
            visual.Scale = new Vector3(0.0f, 0.0f, 1.0f);

            visual.CenterPoint = new Vector3((float)targetElement.ActualWidth / 2, (float)targetElement.ActualHeight / 2, 0);

            TimeSpan duration = TimeSpan.FromMilliseconds(450);
            TimeSpan delay = TimeSpan.FromMilliseconds(index * 35);

            var easeOutBack = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.175f, 0.885f),
                new Vector2(0.320f, 1.275f)
            );

            var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
            scaleAnim.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f), easeOutBack);
            scaleAnim.Duration = duration;
            scaleAnim.DelayTime = delay;

            var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(1.0f, 1.0f);
            opacityAnim.Duration = duration;
            opacityAnim.DelayTime = delay;

            visual.StartAnimation("Scale", scaleAnim);
            visual.StartAnimation("Opacity", opacityAnim);
        }

        internal static void AnimateCardScale(FrameworkElement element, double targetScale)
        {
            FrameworkElement targetElement = element;

            if (element is Border border && border.Child is FrameworkElement child)
            {
                targetElement = child;
            }
            else if (element is ContentControl contentControl && contentControl.Content is FrameworkElement content)
            {
                targetElement = content;
            }

            targetElement.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

            if (targetElement.RenderTransform is not CompositeTransform transform)
            {
                transform = new CompositeTransform();
                targetElement.RenderTransform = transform;
            }

            var storyboard = new Storyboard();

            var animX = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            var animY = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };

            Storyboard.SetTarget(animX, transform);
            Storyboard.SetTargetProperty(animX, "ScaleX");

            Storyboard.SetTarget(animY, transform);
            Storyboard.SetTargetProperty(animY, "ScaleY");

            storyboard.Children.Add(animX);
            storyboard.Children.Add(animY);
            storyboard.Begin();
        }

        internal static void AnimateErrorShake(UIElement target)
        {
            if (target == null) return;

            ElementCompositionPreview.SetIsTranslationEnabled(target, true);

            Visual visual = ElementCompositionPreview.GetElementVisual(target);
            Compositor compositor = visual.Compositor;

            ScalarKeyFrameAnimation shakeAnim = compositor.CreateScalarKeyFrameAnimation();
            shakeAnim.Duration = TimeSpan.FromSeconds(0.4);

            shakeAnim.InsertKeyFrame(0.00f, 0f);
            shakeAnim.InsertKeyFrame(0.15f, 10f);
            shakeAnim.InsertKeyFrame(0.35f, -10f);
            shakeAnim.InsertKeyFrame(0.55f, 5f);
            shakeAnim.InsertKeyFrame(0.75f, -5f);
            shakeAnim.InsertKeyFrame(1.00f, 0f);

            visual.StartAnimation("Translation.X", shakeAnim);
        }
    }
}
