using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
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
            Visual visual = ElementCompositionPreview.GetElementVisual(child);
            Vector3 targetOffset = new Vector3((float)newPos.X, (float)newPos.Y, 0f);

            if (!_lastPos.ContainsKey(child))
            {
                _lastPos[child] = newPos;
                visual.Offset = targetOffset;
                return;
            }

            if (Math.Abs(_lastPos[child].X - newPos.X) < 0.5 && Math.Abs(_lastPos[child].Y - newPos.Y) < 0.5) return;

            _lastPos[child] = newPos;
            var moveAnim = visual.Compositor.CreateVector3KeyFrameAnimation();
            moveAnim.InsertKeyFrame(1.0f, targetOffset, visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0.0f), new Vector2(0.2f, 1.0f)));
            moveAnim.Duration = TimeSpan.FromMilliseconds(450);
            visual.StartAnimation("Offset", moveAnim);
        }
    }
}