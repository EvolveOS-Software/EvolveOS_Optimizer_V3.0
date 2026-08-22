// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Numerics;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Utilities.Animation
{
    #region Night Mode Lighting

    public class AmbLightNight : XamlLight
    {
        protected override string GetId() => typeof(AmbLightNight).FullName!;

        protected override void OnConnected(UIElement newElement)
        {
            var compositor = ElementCompositionPreview.GetElementVisual(newElement).Compositor;
            var ambientLight = compositor.CreateAmbientLight();

            ambientLight.Color = Colors.White;
            ambientLight.Intensity = 0.3f;

            CompositionLight = ambientLight;
            XamlLight.AddTargetElement(GetId(), newElement);
        }

        protected override void OnDisconnected(UIElement oldElement)
        {
            XamlLight.RemoveTargetElement(GetId(), oldElement);
            CompositionLight?.Dispose();
            CompositionLight = null;
        }
    }

    public class HoverLightNight : XamlLight
    {
        private SpotLight? _spotLight;
        private UIElement? _targetElement;

        protected override string GetId() => typeof(HoverLightNight).FullName!;

        protected override void OnConnected(UIElement newElement)
        {
            _targetElement = newElement;
            var compositor = ElementCompositionPreview.GetElementVisual(newElement).Compositor;

            _spotLight = compositor.CreateSpotLight();
            _spotLight.InnerConeColor = Colors.White;
            _spotLight.OuterConeColor = Colors.Transparent;

            _spotLight.InnerConeAngleInDegrees = 30f;
            _spotLight.OuterConeAngleInDegrees = 60f;
            _spotLight.Offset = new Vector3(-1000, -1000, 150f);

            CompositionLight = _spotLight;
            XamlLight.AddTargetElement(GetId(), newElement);

            newElement.PointerMoved += OnPointerMoved;
            newElement.PointerExited += OnPointerExited;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_spotLight != null && _targetElement != null)
            {
                var pos = e.GetCurrentPoint(_targetElement).Position;

                float radius = (float)SettingsEngine.Dashboard_HoverRadius;
                _spotLight.Offset = new Vector3((float)pos.X, (float)pos.Y, radius);
            }
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_spotLight != null) _spotLight.Offset = new Vector3(-1000, -1000, 150f);
        }

        protected override void OnDisconnected(UIElement oldElement)
        {
            if (_targetElement != null)
            {
                _targetElement.PointerMoved -= OnPointerMoved;
                _targetElement.PointerExited -= OnPointerExited;
            }

            XamlLight.RemoveTargetElement(GetId(), oldElement);
            CompositionLight?.Dispose();
            CompositionLight = null;
        }
    }

    #endregion

    #region Day Mode Lighting

    public class AmbLightDay : XamlLight
    {
        protected override string GetId() => typeof(AmbLightDay).FullName!;

        protected override void OnConnected(UIElement newElement)
        {
            var compositor = ElementCompositionPreview.GetElementVisual(newElement).Compositor;
            var ambientLight = compositor.CreateAmbientLight();

            ambientLight.Color = Colors.White;
            ambientLight.Intensity = 0.95f;

            CompositionLight = ambientLight;
            XamlLight.AddTargetElement(GetId(), newElement);
        }

        protected override void OnDisconnected(UIElement oldElement)
        {
            XamlLight.RemoveTargetElement(GetId(), oldElement);
            CompositionLight?.Dispose();
            CompositionLight = null;
        }
    }

    public class HoverLightDay : XamlLight
    {
        private SpotLight? _spotLight;
        private UIElement? _targetElement;

        protected override string GetId() => typeof(HoverLightDay).FullName!;

        protected override void OnConnected(UIElement newElement)
        {
            _targetElement = newElement;
            var compositor = ElementCompositionPreview.GetElementVisual(newElement).Compositor;

            _spotLight = compositor.CreateSpotLight();
            _spotLight.InnerConeColor = ColorHelper.FromArgb(255, 200, 200, 200);
            _spotLight.OuterConeColor = Colors.Transparent;

            _spotLight.InnerConeAngleInDegrees = 45f;
            _spotLight.OuterConeAngleInDegrees = 90f;
            _spotLight.Offset = new Vector3(-1000, -1000, 200f);

            CompositionLight = _spotLight;
            XamlLight.AddTargetElement(GetId(), newElement);

            newElement.PointerMoved += OnPointerMoved;
            newElement.PointerExited += OnPointerExited;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_spotLight != null && _targetElement != null)
            {
                var pos = e.GetCurrentPoint(_targetElement).Position;

                float radius = (float)SettingsEngine.Dashboard_HoverRadius;
                _spotLight.Offset = new Vector3((float)pos.X, (float)pos.Y, radius);
            }
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_spotLight != null) _spotLight.Offset = new Vector3(-1000, -1000, 200f);
        }

        protected override void OnDisconnected(UIElement oldElement)
        {
            if (_targetElement != null)
            {
                _targetElement.PointerMoved -= OnPointerMoved;
                _targetElement.PointerExited -= OnPointerExited;
            }

            XamlLight.RemoveTargetElement(GetId(), oldElement);
            CompositionLight?.Dispose();
            CompositionLight = null;
        }
    }

    #endregion

    #region Custom Mode Lighting

    public class AmbLightCustom : XamlLight
    {
        protected override string GetId() => typeof(AmbLightCustom).FullName!;

        protected override void OnConnected(UIElement newElement)
        {
            var compositor = ElementCompositionPreview.GetElementVisual(newElement).Compositor;
            var ambientLight = compositor.CreateAmbientLight();

            ambientLight.Color = Colors.White;

            float intensity = SettingsEngine.Dashboard_AmbientIntensity / 100f;
            ambientLight.Intensity = Math.Max(0.05f, intensity);

            CompositionLight = ambientLight;
            XamlLight.AddTargetElement(GetId(), newElement);
        }

        protected override void OnDisconnected(UIElement oldElement)
        {
            XamlLight.RemoveTargetElement(GetId(), oldElement);
            CompositionLight?.Dispose();
            CompositionLight = null;
        }
    }

    public class HoverLightCustom : XamlLight
    {
        private SpotLight? _spotLight;
        private UIElement? _targetElement;

        public static Color ParseSafeHex(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6) hex = "FF" + hex;
                return ColorHelper.FromArgb(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16),
                    Convert.ToByte(hex.Substring(6, 2), 16));
            }
            catch
            {
                return Colors.White;
            }
        }

        protected override string GetId() => typeof(HoverLightCustom).FullName!;

        protected override void OnConnected(UIElement newElement)
        {
            _targetElement = newElement;
            var compositor = ElementCompositionPreview.GetElementVisual(newElement).Compositor;

            _spotLight = compositor.CreateSpotLight();
            _spotLight.InnerConeColor = ParseSafeHex(SettingsEngine.Dashboard_HoverColor);
            _spotLight.OuterConeColor = Colors.Transparent;

            _spotLight.InnerConeAngleInDegrees = 40f;
            _spotLight.OuterConeAngleInDegrees = 80f;

            float radius = Math.Max(50f, SettingsEngine.Dashboard_HoverRadius);
            _spotLight.Offset = new Vector3(-1000, -1000, radius);

            CompositionLight = _spotLight;
            XamlLight.AddTargetElement(GetId(), newElement);

            newElement.PointerMoved += OnPointerMoved;
            newElement.PointerExited += OnPointerExited;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_spotLight != null && _targetElement != null)
            {
                var pos = e.GetCurrentPoint(_targetElement).Position;
                float radius = Math.Max(50f, SettingsEngine.Dashboard_HoverRadius);
                _spotLight.Offset = new Vector3((float)pos.X, (float)pos.Y, radius);
            }
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_spotLight != null)
            {
                float radius = Math.Max(50f, SettingsEngine.Dashboard_HoverRadius);
                _spotLight.Offset = new Vector3(-1000, -1000, radius);
            }
        }

        protected override void OnDisconnected(UIElement oldElement)
        {
            if (_targetElement != null)
            {
                _targetElement.PointerMoved -= OnPointerMoved;
                _targetElement.PointerExited -= OnPointerExited;
            }

            XamlLight.RemoveTargetElement(GetId(), oldElement);
            CompositionLight?.Dispose();
            CompositionLight = null;
        }
    }

    #endregion
}