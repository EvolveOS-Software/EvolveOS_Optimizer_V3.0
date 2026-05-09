// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.Numerics;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using WinRT;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class UIHelper
    {
        private static bool _isProcessing = false;
        private static DesktopAcrylicController? _currentController;
        private static int _overlayRequestCount = 0;

        #region Existing UI Helper Methods

        public static void SetOverlay(bool isVisible, bool bringToFront = false)
        {
            var mainWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;

            if (mainWindow == null) return;

            if (isVisible) _overlayRequestCount++;
            else _overlayRequestCount--;

            if (_overlayRequestCount < 0) _overlayRequestCount = 0;

            bool shouldActuallyShow = _overlayRequestCount > 0;

            mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (mainWindow.Content is FrameworkElement rootElement && rootElement.DataContext != null)
                {
                    var vmType = rootElement.DataContext.GetType();
                    var overlayProp = vmType.GetProperty("IsOverlayVisible");

                    if (overlayProp != null && overlayProp.CanWrite)
                    {
                        overlayProp.SetValue(rootElement.DataContext, shouldActuallyShow);
                    }
                }

                if (isVisible && bringToFront)
                {
                    mainWindow.Activate();
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                    var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = AppWindow.GetFromWindowId(windowId);
                    appWindow.MoveInZOrderAtTop();
                }
            });
        }

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

                    if (name == "AcrylicThin") SetAcrylicThinBackdrop(window);
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
            if (_currentController != null)
            {
                var color = ToColor(SettingsEngine.AcrylicTintColor);
                float opacity = (float)SettingsEngine.AcrylicOpacity;
                float luminosity = (float)SettingsEngine.AcrylicLuminosity;

                _currentController.TintColor = color;
                _currentController.FallbackColor = color;
                _currentController.TintOpacity = opacity;

                _currentController.LuminosityOpacity = luminosity + 0.001f;
                _currentController.LuminosityOpacity = luminosity;
            }
        }

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

                window.Closed += (s, e) =>
                {
                    _currentController = null;
                    controller?.Dispose();
                };

                UpdateAcrylicProperties();
            }
            catch { _currentController = null; }
        }

        public static double GetScaleAdjustment(IntPtr hWnd)
        {
            uint dpi = Win32Helper.GetDpiForWindow(hWnd);
            return (double)dpi / 96.0;
        }

        public static Brush GetBrushFromHex(string hex)
        {
            hex = hex.Replace("#", "");
            byte a = 255;
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new SolidColorBrush(ColorHelper.FromArgb(a, r, g, b));
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
            return Color.FromArgb(a, r, g, b);
        }

        public static string ToHexCode(byte red, byte green, byte blue, byte? alpha = null)
        {
            if (alpha != null)
            {
                return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", alpha, red, green, blue);
            }

            return string.Format("#{0:X2}{1:X2}{2:X2}", red, green, blue);
        }

        public static void RegisterPageTransition(FrameworkElement container, FrameworkElement contextSource)
        {
            PropertyChangedEventHandler? propHandler = null;
            SizeChangedEventHandler? sizeHandler = null;
            RoutedEventHandler? unloadHandler = null;

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
                scaleAnimation.Duration = TimeSpan.FromMilliseconds(300);

                var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                opacityAnimation.Target = "Opacity";
                opacityAnimation.InsertKeyFrame(0.0f, 0.0f);
                opacityAnimation.InsertKeyFrame(1.0f, 1.0f);
                opacityAnimation.Duration = TimeSpan.FromMilliseconds(250);

                var animationGroup = compositor.CreateAnimationGroup();
                animationGroup.Add(scaleAnimation);
                animationGroup.Add(opacityAnimation);

                visual.CenterPoint = new Vector3((float)container.ActualWidth / 2, (float)container.ActualHeight / 2, 0);

                sizeHandler = (sender, args) =>
                {
                    visual.CenterPoint = new Vector3((float)args.NewSize.Width / 2, (float)args.NewSize.Height / 2, 0);
                };
                container.SizeChanged += sizeHandler;

                if (contextSource.DataContext is MainWinViewModel vm)
                {
                    propHandler = (sender, args) =>
                    {
                        if (args.PropertyName == nameof(MainWinViewModel.CurrentViewTag))
                        {
                            visual.Scale = new Vector3(0.92f, 0.92f, 1.0f);
                            visual.Opacity = 0.0f;
                            visual.StartAnimationGroup(animationGroup);
                        }
                    };
                    vm.PropertyChanged += propHandler;
                }

                unloadHandler = (sender, args) =>
                {
                    container.Unloaded -= unloadHandler;
                    if (sizeHandler != null) container.SizeChanged -= sizeHandler;

                    if (contextSource.DataContext is MainWinViewModel vmRef && propHandler != null)
                    {
                        vmRef.PropertyChanged -= propHandler;
                    }

                    visual.StopAnimation("Scale");
                    visual.StopAnimation("Opacity");

                    visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);
                    visual.Opacity = 1.0f;

                    ElementCompositionPreview.SetElementChildVisual(container, null);
                    container.DataContext = null;

                    propHandler = null;
                    sizeHandler = null;
                    unloadHandler = null;
                };

                container.Unloaded += unloadHandler;
            };
        }

        /* No Bounce
         public static void RegisterPageTransition(FrameworkElement container, FrameworkElement contextSource)
        {
            PropertyChangedEventHandler? propHandler = null;
            SizeChangedEventHandler? sizeHandler = null;
            RoutedEventHandler? unloadHandler = null;

            container.Loaded += (s, e) =>
            {
                var visual = ElementCompositionPreview.GetElementVisual(container);
                var compositor = visual.Compositor;

                // The "Fluent / Modern" Glide: Fast acceleration, smooth deceleration. No bounce!
                var snappyEasing = compositor.CreateCubicBezierEasingFunction(
                    new Vector2(0.1f, 0.9f),
                    new Vector2(0.2f, 1.0f)
                );

                var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                scaleAnimation.Target = "Scale";
                scaleAnimation.InsertKeyFrame(0.0f, new Vector3(0.92f, 0.92f, 1.0f));
                // Applied the new snappy easing here
                scaleAnimation.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f), snappyEasing);
                scaleAnimation.Duration = TimeSpan.FromMilliseconds(300);

                var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                opacityAnimation.Target = "Opacity";
                opacityAnimation.InsertKeyFrame(0.0f, 0.0f);
                // Applied the same snappy easing to opacity for a synchronized feel
                opacityAnimation.InsertKeyFrame(1.0f, 1.0f, snappyEasing);
                opacityAnimation.Duration = TimeSpan.FromMilliseconds(250);

                var animationGroup = compositor.CreateAnimationGroup();
                animationGroup.Add(scaleAnimation);
                animationGroup.Add(opacityAnimation);

                visual.CenterPoint = new Vector3((float)container.ActualWidth / 2, (float)container.ActualHeight / 2, 0);

                sizeHandler = (sender, args) =>
                {
                    visual.CenterPoint = new Vector3((float)args.NewSize.Width / 2, (float)args.NewSize.Height / 2, 0);
                };
                container.SizeChanged += sizeHandler;

                if (contextSource.DataContext is MainWinViewModel vm)
                {
                    propHandler = (sender, args) =>
                    {
                        if (args.PropertyName == nameof(MainWinViewModel.CurrentViewTag))
                        {
                            visual.Scale = new Vector3(0.92f, 0.92f, 1.0f);
                            visual.Opacity = 0.0f;
                            visual.StartAnimationGroup(animationGroup);
                        }
                    };
                    vm.PropertyChanged += propHandler;
                }

                unloadHandler = (sender, args) =>
                {
                    container.Unloaded -= unloadHandler;
                    if (sizeHandler != null) container.SizeChanged -= sizeHandler;

                    if (contextSource.DataContext is MainWinViewModel vmRef && propHandler != null)
                    {
                        vmRef.PropertyChanged -= propHandler;
                    }

                    visual.StopAnimation("Scale");
                    visual.StopAnimation("Opacity");

                    visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);
                    visual.Opacity = 1.0f;

                    ElementCompositionPreview.SetElementChildVisual(container, null);
                    container.DataContext = null;

                    propHandler = null;
                    sizeHandler = null;
                    unloadHandler = null;
                };

                container.Unloaded += unloadHandler;
            };
        }
        */

        public static T? FindParent<T>(this DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        public static T? FindVisualChildByName<T>(this DependencyObject parent, string name) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && child is FrameworkElement fe && fe.Name == name)
                    return typedChild;


                var result = FindVisualChildByName<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) yield return typedChild;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        public static T? FindDescendant<T>(this DependencyObject element) where T : DependencyObject
        {
            if (element == null) return null;

            if (element is T target) return target;

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = FindDescendant<T>(child);
                if (result != null) return result;
            }

            return null;
        }

        #endregion

        #region UI Extension Methods

        public static string GetHex(this Brush obj, bool includeAlpha = false)
        {
            if (obj is SolidColorBrush solidBrush)
            {
                return solidBrush.Color.GetHex(includeAlpha);
            }
            return "#00000000";
        }

        public static string GetHex(this Color obj, bool includeAlpha = false)
        {
            if (includeAlpha) return ToHexCode(obj.R, obj.G, obj.B, obj.A);
            return ToHexCode(obj.R, obj.G, obj.B);
        }

        public static bool IsEquals(this Color obj, Color color)
        {
            return obj.A == color.A && obj.R == color.R && obj.G == color.G && obj.B == color.B;
        }

        public static Brush? ToBrush(this Brush obj)
        {
            return obj as SolidColorBrush;
        }

        public static Brush ToBrush(this string obj, Brush fallbackValue)
        {
            try
            {
                var color = (Color)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Color), obj);
                return new SolidColorBrush(color);
            }
            catch
            {
                return fallbackValue;
            }
        }

        public static Color ToColor(this SolidColorBrush obj)
        {
            if (obj == null) return Microsoft.UI.Colors.Transparent;
            return obj.Color;
        }

        #endregion
    }
}