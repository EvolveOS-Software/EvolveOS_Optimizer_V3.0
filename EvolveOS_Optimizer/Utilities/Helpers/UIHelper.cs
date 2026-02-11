using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using WinRT;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class UIHelper
    {
        private static bool _isProcessing = false;
        private static DesktopAcrylicController? _currentController;

        public static void ApplyBackdrop(Window window, string name)
        {
            if (window == null || _isProcessing) return;

            if (name == "AcrylicThin" && _currentController != null)
            {
                UpdateAcrylicProperties();
                return;
            }

            window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                if (_isProcessing) return;
                _isProcessing = true;

                try
                {
                    window.SystemBackdrop = null;

                    if (_currentController != null)
                    {
                        var old = _currentController;
                        _currentController = null;
                        old.Dispose();
                    }

                    await Task.Delay(32);

                    if (name == "AcrylicThin")
                    {
                        SetAcrylicThinBackdrop(window);
                    }
                    else
                    {
                        window.SystemBackdrop = name switch
                        {
                            "Mica" => new MicaBackdrop() { Kind = MicaKind.Base },
                            "MicaAlt" => new MicaBackdrop() { Kind = MicaKind.BaseAlt },
                            "Acrylic" => new DesktopAcrylicBackdrop(),
                            _ => null
                        };
                    }
                }
                catch { /* Prevent crash on window close */ }
                finally { _isProcessing = false; }
            });
        }

        private static void UpdateAcrylicProperties()
        {
            var controller = _currentController;

            if (controller != null)
            {
                var color = ToColor(SettingsEngine.AcrylicTintColor);
                float opacity = (float)SettingsEngine.AcrylicOpacity;
                float luminosity = (float)SettingsEngine.AcrylicLuminosity;

                controller.TintColor = color;
                controller.FallbackColor = color;
                controller.TintOpacity = opacity;

                controller.LuminosityOpacity = luminosity + 0.001f;
                controller.LuminosityOpacity = luminosity;

                Debug.WriteLine($"[UIHelper] Properties Updated: {SettingsEngine.AcrylicTintColor} at {opacity} opacity");
            }
        }

        /*private static void UpdateAcrylicProperties()
        {
            if (_currentController != null)
            {
                float opacity = (float)SettingsEngine.AcrylicOpacity;
                float luminosity = (float)SettingsEngine.AcrylicLuminosity;
                var color = ToColor(SettingsEngine.AcrylicTintColor);

                _currentController.TintColor = color;
                _currentController.FallbackColor = color;

                // Apply the values
                _currentController.LuminosityOpacity = luminosity;
                _currentController.TintOpacity = opacity;

                // THE NUDGE: A tiny toggle to force the GPU to refresh the transparency
                _currentController.LuminosityOpacity = luminosity + 0.001f;
                _currentController.LuminosityOpacity = luminosity;
            }
        }*/

        private static void SetAcrylicThinBackdrop(Window window)
        {
            try
            {
                if (!DesktopAcrylicController.IsSupported()) return;

                var config = new SystemBackdropConfiguration();
                var controller = new DesktopAcrylicController();
                _currentController = controller;
                controller.Kind = DesktopAcrylicKind.Thin;

                var target = window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();
                controller.AddSystemBackdropTarget(target);
                controller.SetSystemBackdropConfiguration(config);

                window.Closed += (s, e) => {
                    _currentController = null;
                    controller?.Dispose();
                };

                UpdateAcrylicProperties();
            }
            catch { _currentController = null; }
        }

        public static Color ToColor(string hex)
        {
            hex = hex.Replace("#", string.Empty);

            if (hex.Length < 6) return Colors.Black;

            byte a = 255;
            int pos = 0;

            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(pos, 2), System.Globalization.NumberStyles.HexNumber);
                pos += 2;
            }

            byte r = byte.Parse(hex.Substring(pos, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(pos + 2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(pos + 4, 2), System.Globalization.NumberStyles.HexNumber);

            return ColorHelper.FromArgb(a, r, g, b);
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

        public static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                return FindParent<T>(parentObject);
            }
        }

        public static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && child is FrameworkElement fe && fe.Name == name)
                {
                    return typedChild;
                }

                var result = FindVisualChildByName<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}