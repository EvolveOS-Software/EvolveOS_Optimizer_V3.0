// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class GaugeHelper
    {
        public static void UpdateVisuals(
            double percentage,
            bool isExpanded,
            Ellipse? ambientGlow,
            Path? gaugeNeedle,
            RotateTransform? needleRotation,
            Ellipse? pinShadow,
            Ellipse? pinOuter,
            Ellipse? pinInner,
            Path? backgroundPath,
            Path? foregroundPath,
            TextBlock? scoreText)
        {
            double startAngle = -135;
            double totalSweep = 270;
            double currentAngle = startAngle + (totalSweep * percentage);

            if (Math.Abs(currentAngle - startAngle) < 0.1) currentAngle = startAngle + 0.1;

            double canvasCenter = isExpanded ? 60 : 40;
            double radius = isExpanded ? 44 : 29;

            if (ambientGlow != null)
            {
                double glowRadius = radius - (isExpanded ? 7 : 5);
                double glowSize = glowRadius * 2;
                ambientGlow.Width = glowSize;
                ambientGlow.Height = glowSize;
                Canvas.SetLeft(ambientGlow, canvasCenter - glowRadius);
                Canvas.SetTop(ambientGlow, canvasCenter - glowRadius);

                var baseColor = percentage < 0.5 ? Color.FromArgb(255, 255, 69, 0) :
                                percentage < 0.8 ? Color.FromArgb(255, 255, 140, 0) :
                                Color.FromArgb(255, 46, 139, 87);

                var radialBrush = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    RadiusX = 0.5,
                    RadiusY = 0.5,
                    GradientOrigin = new Point(0.5, 0.5)
                };
                radialBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(50, baseColor.R, baseColor.G, baseColor.B), Offset = 0.0 });
                radialBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), Offset = 1.0 });

                ambientGlow.Fill = radialBrush;
            }

            if (gaugeNeedle != null && needleRotation != null)
            {
                needleRotation.CenterX = canvasCenter;
                needleRotation.CenterY = canvasCenter;
                needleRotation.Angle = currentAngle;
                gaugeNeedle.Data = isExpanded
                    ? XamlBindingHelper.ConvertValue(typeof(Geometry), "M 58,60 L 62,60 L 60,16 Z") as Geometry
                    : XamlBindingHelper.ConvertValue(typeof(Geometry), "M 38,40 L 42,40 L 40,11 Z") as Geometry;
            }

            if (pinOuter != null && pinInner != null)
            {
                double pinOuterSize = isExpanded ? 14 : 10;
                double pinInnerSize = isExpanded ? 6 : 4;

                if (pinShadow != null)
                {
                    pinShadow.Width = pinOuterSize;
                    pinShadow.Height = pinOuterSize;
                    Canvas.SetLeft(pinShadow, canvasCenter - (pinOuterSize / 2) + 1);
                    Canvas.SetTop(pinShadow, canvasCenter - (pinOuterSize / 2) + 2);
                }

                pinOuter.Width = pinOuterSize; pinOuter.Height = pinOuterSize;
                Canvas.SetLeft(pinOuter, canvasCenter - (pinOuterSize / 2));
                Canvas.SetTop(pinOuter, canvasCenter - (pinOuterSize / 2));

                pinInner.Width = pinInnerSize; pinInner.Height = pinInnerSize;
                Canvas.SetLeft(pinInner, canvasCenter - (pinInnerSize / 2));
                Canvas.SetTop(pinInner, canvasCenter - (pinInnerSize / 2));
            }

            if (backgroundPath != null && foregroundPath != null)
            {
                double strokeThick = isExpanded ? 10 : 7;
                backgroundPath.StrokeThickness = strokeThick;
                foregroundPath.StrokeThickness = strokeThick;
            }

            DrawArc(backgroundPath, -135, 135, radius, new Point(canvasCenter, canvasCenter));
            DrawArc(foregroundPath, startAngle, currentAngle, radius, new Point(canvasCenter, canvasCenter));

            if (scoreText != null) scoreText.Text = $"{(int)(percentage * 100)}%";

            if (foregroundPath != null)
            {
                var gradientBrush = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(canvasCenter - radius, canvasCenter),
                    EndPoint = new Point(canvasCenter + radius, canvasCenter)
                };

                gradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 69, 0), Offset = 0.0 });
                gradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 140, 0), Offset = 0.5 });
                gradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 46, 139, 87), Offset = 1.0 });

                foregroundPath.Stroke = gradientBrush;
            }
        }

        public static async Task AnimateAsync(
            double targetPercentage,
            bool isExpanded,
            Storyboard? pulseAnimation,
            Action<double, bool> updateAction)
        {
            if (pulseAnimation != null) pulseAnimation.Stop();

            double currentPercentage = 0;
            double animationDurationMs = 800;
            double fps = 60;
            double steps = animationDurationMs / (1000 / fps);
            double stepValue = targetPercentage / steps;

            for (int i = 0; i <= steps; i++)
            {
                currentPercentage = i * stepValue;
                updateAction(currentPercentage, isExpanded);
                await Task.Delay((int)(1000 / fps));
            }

            updateAction(targetPercentage, isExpanded);

            if (pulseAnimation != null)
            {
                pulseAnimation.Begin();
            }
        }

        public static void DrawArc(Path? path, double startAngle, double endAngle, double radius, Point center)
        {
            if (path == null) return;

            double startRad = (startAngle - 90) * Math.PI / 180.0;
            double endRad = (endAngle - 90) * Math.PI / 180.0;

            Point startPoint = new Point(
                center.X + radius * Math.Cos(startRad),
                center.Y + radius * Math.Sin(startRad));

            Point endPoint = new Point(
                center.X + radius * Math.Cos(endRad),
                center.Y + radius * Math.Sin(endRad));

            bool largeArc = Math.Abs(endAngle - startAngle) > 180.0;

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };

            figure.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                IsLargeArc = largeArc,
                SweepDirection = SweepDirection.Clockwise
            });

            geometry.Figures.Add(figure);
            path.Data = geometry;
        }
    }
}