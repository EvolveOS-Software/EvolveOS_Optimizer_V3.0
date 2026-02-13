using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.Panels
{
    public sealed class TweakFlowPanel : Panel
    {
        private readonly Dictionary<UIElement, Point> _lastPos = new();
        private readonly List<bool[]> _cellOccupancy = new();

        public double HorizontalSpacing { get => (double)GetValue(HorizontalSpacingProperty); set => SetValue(HorizontalSpacingProperty, value); }
        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double), typeof(TweakFlowPanel), new PropertyMetadata(10.0));

        public double VerticalSpacing { get => (double)GetValue(VerticalSpacingProperty); set => SetValue(VerticalSpacingProperty, value); }
        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(nameof(VerticalSpacing), typeof(double), typeof(TweakFlowPanel), new PropertyMetadata(10.0));

        public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(TweakFlowPanel), new PropertyMetadata(464.0));

        private const double CardH = 82.0;
        private const double Bleed = 5;

        protected override Size MeasureOverride(Size availableSize)
        {
            var visibleChildren = Children.Where(c => c.Visibility == Visibility.Visible).ToList();
            if (visibleChildren.Count == 0) return new Size(0, 0);

            double slotWidth = ItemWidth + Bleed;
            double slotHeight = CardH + Bleed;

            double widthForCalc = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
            int columnCount = Math.Max(1, (int)((widthForCalc + HorizontalSpacing) / (slotWidth + HorizontalSpacing)));

            _cellOccupancy.Clear();

            var bigCard = visibleChildren.FirstOrDefault(c => (c as FrameworkElement)?.Tag?.ToString() == "SliderGroup");

            double bigCardLogicH = (CardH * 3) + (VerticalSpacing * 2);

            if (bigCard != null)
            {
                bigCard.Measure(new Size(slotWidth, bigCardLogicH + Bleed));
                for (int r = 0; r < 3; r++)
                {
                    EnsureRowExists(r, columnCount);
                    _cellOccupancy[r][0] = true;
                }
            }

            double maxH = bigCardLogicH + Bleed;

            foreach (var child in visibleChildren)
            {
                if (child == bigCard) continue;

                child.Measure(new Size(slotWidth, slotHeight));
                var (r, c) = FindFirstEmptyCell(columnCount);
                _cellOccupancy[r][c] = true;

                double y = (r * (CardH + VerticalSpacing)) + slotHeight;
                maxH = Math.Max(maxH, y);
            }

            return new Size(widthForCalc, maxH + VerticalSpacing);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var visibleChildren = Children.Where(c => c.Visibility == Visibility.Visible).ToList();
            if (visibleChildren.Count == 0) return finalSize;

            double slotWidth = ItemWidth + Bleed;
            double slotHeight = CardH + Bleed;

            int columnCount = Math.Max(1, (int)((finalSize.Width + HorizontalSpacing) / (slotWidth + HorizontalSpacing)));
            _cellOccupancy.Clear();

            var bigCard = visibleChildren.FirstOrDefault(c => (c as FrameworkElement)?.Tag?.ToString() == "SliderGroup");

            if (bigCard != null)
            {
                double bigCardLogicH = (CardH * 3) + (VerticalSpacing * 2);
                for (int r = 0; r < 3; r++)
                {
                    EnsureRowExists(r, columnCount);
                    _cellOccupancy[r][0] = true;
                }

                bigCard.Arrange(new Rect(0, 0, slotWidth, bigCardLogicH + Bleed));
                AnimateChild(bigCard, new Point(0, 0));
            }

            foreach (var child in visibleChildren)
            {
                if (child == bigCard) continue;

                var (r, c) = FindFirstEmptyCell(columnCount);
                _cellOccupancy[r][c] = true;

                double x = c * (slotWidth + HorizontalSpacing);
                double y = r * (CardH + VerticalSpacing);

                child.Arrange(new Rect(x, y, slotWidth, slotHeight));
                AnimateChild(child, new Point(x, y));
            }

            return finalSize;
        }

        private void EnsureRowExists(int rowIndex, int colCount)
        {
            while (_cellOccupancy.Count <= rowIndex)
                _cellOccupancy.Add(new bool[colCount]);
        }

        private (int row, int col) FindFirstEmptyCell(int totalCols)
        {
            for (int r = 0; ; r++)
            {
                EnsureRowExists(r, totalCols);
                for (int c = 0; c < totalCols; c++)
                {
                    if (c < _cellOccupancy[r].Length && !_cellOccupancy[r][c])
                        return (r, c);
                }
            }
        }

        private void AnimateChild(UIElement child, Point newPos)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(child);
            Vector3 targetOffset = new Vector3((float)newPos.X, (float)newPos.Y, 0f);
            if (!_lastPos.ContainsKey(child)) { _lastPos[child] = newPos; visual.Offset = targetOffset; return; }
            if (Math.Abs(_lastPos[child].X - newPos.X) < 0.5 && Math.Abs(_lastPos[child].Y - newPos.Y) < 0.5) return;
            _lastPos[child] = newPos;
            var moveAnim = visual.Compositor.CreateVector3KeyFrameAnimation();
            moveAnim.InsertKeyFrame(1.0f, targetOffset, visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0.0f), new Vector2(0.2f, 1.0f)));
            moveAnim.Duration = TimeSpan.FromMilliseconds(450);
            visual.StartAnimation("Offset", moveAnim);
        }
    }
}