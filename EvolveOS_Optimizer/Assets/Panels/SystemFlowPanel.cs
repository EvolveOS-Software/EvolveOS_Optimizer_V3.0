using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.Panels
{
    public sealed class SystemFlowPanel : Panel
    {
        private readonly Dictionary<UIElement, Point> _lastPos = new();

        public double HorizontalSpacing { get => (double)GetValue(HorizontalSpacingProperty); set => SetValue(HorizontalSpacingProperty, value); }
        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double), typeof(SystemFlowPanel), new PropertyMetadata(10.0));

        public double VerticalSpacing { get => (double)GetValue(VerticalSpacingProperty); set => SetValue(VerticalSpacingProperty, value); }
        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(nameof(VerticalSpacing), typeof(double), typeof(SystemFlowPanel), new PropertyMetadata(10.0));

        public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(SystemFlowPanel), new PropertyMetadata(464.0));

        private const double Bleed = 10;

        protected override Size MeasureOverride(Size availableSize)
        {
            var visibleChildren = Children.Where(c => c.Visibility == Visibility.Visible).ToList();
            if (visibleChildren.Count == 0) return new Size(0, 0);

            double slotWidth = ItemWidth + Bleed;
            double widthForCalc = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
            int columnCount = Math.Max(1, (int)((widthForCalc + HorizontalSpacing) / (slotWidth + HorizontalSpacing)));

            double[] colHeights = new double[columnCount];

            foreach (var child in visibleChildren)
            {
                child.Measure(new Size(slotWidth, double.PositiveInfinity));

                int targetCol = Array.IndexOf(colHeights, colHeights.Min());
                colHeights[targetCol] += child.DesiredSize.Height + VerticalSpacing;
            }

            double maxH = colHeights.Max();
            if (maxH > 0) maxH -= VerticalSpacing;

            return new Size(widthForCalc, maxH + Bleed);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var visibleChildren = Children.Where(c => c.Visibility == Visibility.Visible).ToList();
            if (visibleChildren.Count == 0) return finalSize;

            double slotWidth = ItemWidth + Bleed;
            int columnCount = Math.Max(1, (int)((finalSize.Width + HorizontalSpacing) / (slotWidth + HorizontalSpacing)));

            double[] colHeights = new double[columnCount];

            foreach (var child in visibleChildren)
            {
                int targetCol = Array.IndexOf(colHeights, colHeights.Min());

                double x = targetCol * (slotWidth + HorizontalSpacing);
                double y = colHeights[targetCol];

                double childHeight = child.DesiredSize.Height;

                child.Arrange(new Rect(x, y, slotWidth, childHeight + Bleed));

                AnimateChild(child, new Point(x, y));

                colHeights[targetCol] += childHeight + VerticalSpacing;
            }

            return finalSize;
        }

        private void AnimateChild(UIElement child, Point newPos)
        {
            if (child is not FrameworkElement element) return;

            if (element.RenderTransform is not TransformGroup group)
            {
                group = new TransformGroup();
                group.Children.Add(new TranslateTransform());
                element.RenderTransform = group;
            }

            var trans = (TranslateTransform)group.Children[0];

            if (!_lastPos.ContainsKey(child))
            {
                _lastPos[child] = newPos;
                return;
            }

            Point oldPos = _lastPos[child];

            if (Math.Abs(oldPos.X - newPos.X) < 0.5 && Math.Abs(oldPos.Y - newPos.Y) < 0.5) return;

            _lastPos[child] = newPos;

            double deltaX = oldPos.X - newPos.X + trans.X;
            double deltaY = oldPos.Y - newPos.Y + trans.Y;

            trans.X = deltaX;
            trans.Y = deltaY;

            Storyboard sb = new Storyboard();

            DoubleAnimation animX = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(450) };
            DoubleAnimation animY = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(450) };

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