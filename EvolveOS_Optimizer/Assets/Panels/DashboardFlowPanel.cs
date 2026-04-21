// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed class DashboardFlowPanel : Panel
    {
        private readonly Dictionary<UIElement, Point> _lastPos = new();

        public bool IsDragInProgress { get; set; } = false;

        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
            nameof(HorizontalSpacing), typeof(double), typeof(DashboardFlowPanel),
            new PropertyMetadata(10.0, OnLayoutPropertyChanged));

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
            nameof(VerticalSpacing), typeof(double), typeof(DashboardFlowPanel),
            new PropertyMetadata(10.0, OnLayoutPropertyChanged));

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
        }

        private const int LargeCardColumnSpan = 2;
        private const int LargeCardRowSpan = 2;
        private const double SmallCardFixedUnitWidth = 356.0;
        private const double SmallCardFixedUnitHeight = 220.0;

        private readonly List<bool[]> _cellOccupancy = new List<bool[]>();

        private UIElement? BigCard
        {
            get
            {
                foreach (var child in Children)
                {
                    if (child.Visibility != Visibility.Visible) continue;
                    if (child is GridViewItem gvi && gvi.Content is FrameworkElement fe && fe.Name == "CardWeather")
                        return child;
                    if (child is FrameworkElement directFe && directFe.Name == "CardWeather")
                        return child;
                }
                return null;
            }
        }

        private void EnsureRowExists(int rowIndex, int totalColumns)
        {
            while (_cellOccupancy.Count <= rowIndex)
                _cellOccupancy.Add(new bool[totalColumns]);
        }

        private (int row, int col) FindNextAvailableCellWithSpan(int colSpan, int rowSpan, int totalColumns)
        {
            for (int r = 0; ; r++)
            {
                EnsureRowExists(r + rowSpan - 1, totalColumns);
                for (int c = 0; c <= totalColumns - colSpan; c++)
                {
                    bool isAreaFree = true;
                    for (int ar = r; ar < r + rowSpan; ar++)
                    {
                        for (int ac = c; ac < c + colSpan; ac++)
                        {
                            if (_cellOccupancy[ar][ac]) { isAreaFree = false; break; }
                        }
                        if (!isAreaFree) break;
                    }
                    if (isAreaFree) return (r, c);
                }
            }
        }

        private int CalculateTotalColumns(double availableWidth)
        {
            if (availableWidth <= 0 || double.IsInfinity(availableWidth)) return 4;
            int dynamicTotalColumns = (int)Math.Floor((availableWidth + HorizontalSpacing) / (SmallCardFixedUnitWidth + HorizontalSpacing));
            return Math.Max(LargeCardColumnSpan, dynamicTotalColumns);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _cellOccupancy.Clear();
            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            if (visibleChildren.Count == 0) return new Size(0, 0);

            int totalColumns = CalculateTotalColumns(availableSize.Width);
            var bigCard = BigCard;

            var bigCardWidth = (SmallCardFixedUnitWidth * LargeCardColumnSpan) + HorizontalSpacing;
            var bigCardHeight = (SmallCardFixedUnitHeight * LargeCardRowSpan) + VerticalSpacing;

            double maxLayoutHeight = 0;

            foreach (var child in visibleChildren)
            {
                bool isBig = (child == bigCard);
                int colSpan = isBig ? LargeCardColumnSpan : 1;
                int rowSpan = isBig ? LargeCardRowSpan : 1;

                (int row, int col) = FindNextAvailableCellWithSpan(colSpan, rowSpan, totalColumns);

                for (int r = row; r < row + rowSpan; r++)
                {
                    EnsureRowExists(r, totalColumns);
                    for (int c = col; c < col + colSpan; c++) _cellOccupancy[r][c] = true;
                }

                double w = isBig ? bigCardWidth : SmallCardFixedUnitWidth;
                double h = isBig ? bigCardHeight : SmallCardFixedUnitHeight;

                child.Measure(new Size(w, h));

                maxLayoutHeight = Math.Max(maxLayoutHeight, (row + rowSpan) * (SmallCardFixedUnitHeight + VerticalSpacing));
            }

            double desiredWidth = (totalColumns * SmallCardFixedUnitWidth) + ((totalColumns - 1) * HorizontalSpacing);
            return new Size(desiredWidth, maxLayoutHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _cellOccupancy.Clear();
            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            if (visibleChildren.Count == 0) return finalSize;

            int totalColumns = CalculateTotalColumns(finalSize.Width);
            var bigCard = BigCard;

            var bigCardWidth = (SmallCardFixedUnitWidth * LargeCardColumnSpan) + HorizontalSpacing;
            var bigCardHeight = (SmallCardFixedUnitHeight * LargeCardRowSpan) + VerticalSpacing;

            foreach (var child in visibleChildren)
            {
                bool isBig = (child == bigCard);
                int colSpan = isBig ? LargeCardColumnSpan : 1;
                int rowSpan = isBig ? LargeCardRowSpan : 1;

                (int row, int col) = FindNextAvailableCellWithSpan(colSpan, rowSpan, totalColumns);

                for (int r = row; r < row + rowSpan; r++)
                {
                    EnsureRowExists(r, totalColumns);
                    for (int c = col; c < col + colSpan; c++) _cellOccupancy[r][c] = true;
                }

                double w = isBig ? bigCardWidth : SmallCardFixedUnitWidth;
                double h = isBig ? bigCardHeight : SmallCardFixedUnitHeight;
                double targetX = col * (SmallCardFixedUnitWidth + HorizontalSpacing);
                double targetY = row * (SmallCardFixedUnitHeight + VerticalSpacing);

                child.Arrange(new Rect(targetX, targetY, w, h));

                AnimateChild(child, new Point(targetX, targetY));
            }

            return finalSize;
        }

        private void AnimateChild(UIElement child, Point newPos)
        {
            if (IsDragInProgress) return;

            Visual visual = ElementCompositionPreview.GetElementVisual(child);

            Vector3 targetOffset = Vector3.Zero;

            if (!_lastPos.ContainsKey(child))
            {
                _lastPos[child] = newPos;
                visual.Offset = targetOffset;
                return;
            }

            if (Math.Abs(_lastPos[child].X - newPos.X) > 0.5 || Math.Abs(_lastPos[child].Y - newPos.Y) > 0.5)
            {
                var oldPos = _lastPos[child];
                _lastPos[child] = newPos;

                Vector3 startOffset = new Vector3((float)(oldPos.X - newPos.X), (float)(oldPos.Y - newPos.Y), 0f);
                visual.Offset = startOffset;

                var moveAnim = visual.Compositor.CreateVector3KeyFrameAnimation();
                moveAnim.InsertKeyFrame(1.0f, Vector3.Zero);
                moveAnim.Duration = TimeSpan.FromMilliseconds(450);
                visual.StartAnimation("Offset", moveAnim);
            }
        }
    }
}