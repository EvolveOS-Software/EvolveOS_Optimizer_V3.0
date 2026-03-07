// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.Panels
{
    public class UserAccountsFlowPanel : Panel
    {
        private class ColumnInfo
        {
            internal List<UIElement> Elements { get; set; } = new List<UIElement>();
            internal double Width { get; set; }
            internal double Height { get; set; }
        }

        private readonly Dictionary<UIElement, Point> _lastPos = new Dictionary<UIElement, Point>();

        #region Dependency Properties
        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
            nameof(HorizontalSpacing), typeof(double), typeof(UserAccountsFlowPanel), new PropertyMetadata(12.5, OnLayoutChanged));

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
            nameof(VerticalSpacing), typeof(double), typeof(UserAccountsFlowPanel), new PropertyMetadata(12.5, OnLayoutChanged));

        public static readonly DependencyProperty ContentAlignmentProperty = DependencyProperty.Register(
            nameof(ContentAlignment), typeof(HorizontalAlignment), typeof(UserAccountsFlowPanel), new PropertyMetadata(HorizontalAlignment.Left, OnLayoutChanged));

        public static readonly DependencyProperty UseMasonryLayoutProperty = DependencyProperty.Register(
            nameof(UseMasonryLayout), typeof(bool), typeof(UserAccountsFlowPanel), new PropertyMetadata(true, OnLayoutChanged));

        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
            nameof(ItemWidth), typeof(double), typeof(UserAccountsFlowPanel), new PropertyMetadata(268.0, OnLayoutChanged));

        public double HorizontalSpacing { get => (double)GetValue(HorizontalSpacingProperty); set => SetValue(HorizontalSpacingProperty, value); }
        public double VerticalSpacing { get => (double)GetValue(VerticalSpacingProperty); set => SetValue(VerticalSpacingProperty, value); }
        public HorizontalAlignment ContentAlignment { get => (HorizontalAlignment)GetValue(ContentAlignmentProperty); set => SetValue(ContentAlignmentProperty, value); }
        public bool UseMasonryLayout { get => (bool)GetValue(UseMasonryLayoutProperty); set => SetValue(UseMasonryLayoutProperty, value); }
        public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UserAccountsFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
        }
        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            if (visibleChildren.Count == 0)
            {
                return new Size(0, 0);
            }

            double width = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
            Size childConstraint = new Size(ItemWidth, double.PositiveInfinity);

            foreach (UIElement child in visibleChildren)
            {
                child.Measure(childConstraint);
            }

            var columns = CreateOptimalColumns(width, double.PositiveInfinity, visibleChildren);
            double neededHeight = columns.Count == 0 ? 0 : columns.Max(c => c.Height);

            return new Size(width, neededHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            if (visibleChildren.Count == 0)
            {
                return finalSize;
            }

            var columns = CreateOptimalColumns(finalSize.Width, finalSize.Height, visibleChildren);

            double totalContentWidth = (columns.Count * ItemWidth) + (Math.Max(0, columns.Count - 1) * HorizontalSpacing);

            double xOffset = ContentAlignment switch
            {
                HorizontalAlignment.Center => (finalSize.Width - totalContentWidth) / 2.0,
                HorizontalAlignment.Right => finalSize.Width - totalContentWidth,
                _ => 0
            };

            double currentX = xOffset;

            foreach (var col in columns)
            {
                double currentY = 0;
                foreach (var child in col.Elements)
                {
                    Point newPos = new Point(currentX, currentY);

                    child.Arrange(new Rect(newPos, new Size(ItemWidth, child.DesiredSize.Height)));

                    AnimateChild(child, newPos);

                    currentY += child.DesiredSize.Height + VerticalSpacing;
                }
                currentX += ItemWidth + HorizontalSpacing;
            }

            return finalSize;
        }

        private List<ColumnInfo> CreateOptimalColumns(double availableWidth, double availableHeight, List<UIElement> visibleChildren)
        {
            int columnCount = (int)Math.Floor((availableWidth + HorizontalSpacing) / (ItemWidth + HorizontalSpacing));
            columnCount = Math.Max(1, columnCount);

            var columns = Enumerable.Range(0, columnCount).Select(_ => new ColumnInfo()).ToList();
            double[] heights = new double[columnCount];

            foreach (UIElement child in visibleChildren)
            {
                int targetIndex = UseMasonryLayout ? Array.IndexOf(heights, heights.Min()) : visibleChildren.IndexOf(child) % columnCount;

                columns[targetIndex].Elements.Add(child);
                heights[targetIndex] += child.DesiredSize.Height + VerticalSpacing;
                columns[targetIndex].Height = heights[targetIndex];
                columns[targetIndex].Width = ItemWidth;
            }

            return columns;
        }

        private void AnimateChild(UIElement child, Point newPos)
        {
            if (child is not FrameworkElement element) return;

            if (element.RenderTransform is not TransformGroup group)
            {
                group = new TransformGroup();
                group.Children.Add(new TranslateTransform());
                group.Children.Add(new ScaleTransform());
                element.RenderTransform = group;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var trans = (TranslateTransform)group.Children[0];
            var scale = (ScaleTransform)group.Children[1];

            bool isFirstTime = !_lastPos.ContainsKey(child);

            if (isFirstTime || Math.Abs(_lastPos[child].X - newPos.X) > 0.5 || Math.Abs(_lastPos[child].Y - newPos.Y) > 0.5)
            {
                Point oldLayoutPos = isFirstTime ? new Point(0, 0) : _lastPos[child];
                _lastPos[child] = newPos;

                if (isFirstTime)
                {
                    int index = Children.IndexOf(child);
                    double delayMs = index * 35;

                    element.Opacity = 0;
                    scale.ScaleX = 0;
                    scale.ScaleY = 0;
                    trans.X = 0;
                    trans.Y = 0;

                    Storyboard sb = new Storyboard();

                    DoubleAnimation scaleXAnim = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(450), BeginTime = TimeSpan.FromMilliseconds(delayMs) };
                    DoubleAnimation scaleYAnim = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(450), BeginTime = TimeSpan.FromMilliseconds(delayMs) };
                    DoubleAnimation opacityAnim = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(450), BeginTime = TimeSpan.FromMilliseconds(delayMs) };

                    scaleXAnim.EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
                    scaleYAnim.EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };

                    Storyboard.SetTarget(scaleXAnim, scale);
                    Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");

                    Storyboard.SetTarget(scaleYAnim, scale);
                    Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

                    Storyboard.SetTarget(opacityAnim, element);
                    Storyboard.SetTargetProperty(opacityAnim, "Opacity");

                    sb.Children.Add(scaleXAnim);
                    sb.Children.Add(scaleYAnim);
                    sb.Children.Add(opacityAnim);

                    sb.Begin();
                }
                else if (element.IsLoaded)
                {
                    double deltaX = (oldLayoutPos.X + trans.X) - newPos.X;
                    double deltaY = (oldLayoutPos.Y + trans.Y) - newPos.Y;

                    trans.X = deltaX;
                    trans.Y = deltaY;

                    Storyboard sb = new Storyboard();

                    DoubleAnimation animX = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400) };
                    DoubleAnimation animY = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400) };

                    var moveEase = new CubicEase { EasingMode = EasingMode.EaseOut };
                    animX.EasingFunction = moveEase;
                    animY.EasingFunction = moveEase;

                    Storyboard.SetTarget(animX, trans);
                    Storyboard.SetTargetProperty(animX, "X");

                    Storyboard.SetTarget(animY, trans);
                    Storyboard.SetTargetProperty(animY, "Y");

                    sb.Children.Add(animX);
                    sb.Children.Add(animY);

                    sb.Begin();
                }
            }
        }
    }
}