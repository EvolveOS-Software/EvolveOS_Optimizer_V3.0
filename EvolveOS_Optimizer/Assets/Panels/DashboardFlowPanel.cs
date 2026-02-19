using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed class DashboardFlowPanel : Panel
    {
        private readonly Dictionary<UIElement, Point> _lastPos = new();

        // 1. Fixed for WinUI 3: Replaced FrameworkPropertyMetadata with PropertyMetadata and a Callback
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

        // This callback forces the panel to redraw if you change the spacing in XAML
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

        // Added Bleed to protect drop shadows and rounded corners
        private const double Bleed = 10.0;

        private double _smallCardWidth = SmallCardFixedUnitWidth;
        private double _smallCardHeight = SmallCardFixedUnitHeight;
        private readonly List<bool[]> _cellOccupancy = new List<bool[]>();

        // 2. Fixed for WinUI 3: Replaced InternalChildren with Children
        private UIElement? BigCard => Children.FirstOrDefault(c => c.Visibility == Visibility.Visible);

        private int CalculateTotalColumns(double availableWidth)
        {
            if (availableWidth <= 0 || double.IsInfinity(availableWidth)) return 4;
            double slotWidth = _smallCardWidth + Bleed;
            int dynamicTotalColumns = (int)Math.Floor((availableWidth + HorizontalSpacing) / (slotWidth + HorizontalSpacing));
            return Math.Max(LargeCardColumnSpan, dynamicTotalColumns);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _cellOccupancy.Clear();
            _smallCardWidth = SmallCardFixedUnitWidth;
            _smallCardHeight = SmallCardFixedUnitHeight;

            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            if (visibleChildren.Count == 0) return new Size(0, 0);

            int totalColumns = CalculateTotalColumns(availableSize.Width);

            var bigCardWidth = (_smallCardWidth * LargeCardColumnSpan) + HorizontalSpacing;
            var bigCardHeight = (_smallCardHeight * LargeCardRowSpan) + VerticalSpacing;

            var bigCard = BigCard;
            if (bigCard != null)
            {
                bigCard.Measure(new Size(bigCardWidth + Bleed, bigCardHeight + Bleed));
            }

            var unitConstraint = new Size(_smallCardWidth + Bleed, _smallCardHeight + Bleed);
            foreach (var child in visibleChildren.Where(c => c != bigCard))
            {
                child.Measure(unitConstraint);
            }

            var desiredWidth = (totalColumns * _smallCardWidth) + (Math.Max(0, totalColumns - 1) * HorizontalSpacing);

            int smallCardCount = visibleChildren.Except(new[] { BigCard }).Count();
            int requiredRows = LargeCardRowSpan;
            int cellsAvailableBesideBigCard = (totalColumns * LargeCardRowSpan) - (LargeCardColumnSpan * LargeCardRowSpan);
            int smallCardsOverflowing = Math.Max(0, smallCardCount - cellsAvailableBesideBigCard);
            int overflowRows = (int)Math.Ceiling((double)smallCardsOverflowing / totalColumns);

            requiredRows += overflowRows;

            double requiredHeight = (requiredRows * _smallCardHeight) + (Math.Max(0, requiredRows - 1) * VerticalSpacing);

            if (requiredHeight <= 0 && bigCard != null) requiredHeight = bigCardHeight;

            return new Size(desiredWidth, requiredHeight + Bleed);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // 4. Fixed for WinUI 3: Replaced InternalChildren with Children
            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            if (visibleChildren.Count == 0)
            {
                double fixedWidth = CalculateTotalColumns(finalSize.Width) * SmallCardFixedUnitWidth + (Math.Max(0, CalculateTotalColumns(finalSize.Width) - 1) * HorizontalSpacing);
                return new Size(fixedWidth, finalSize.Height);
            }

            _smallCardWidth = SmallCardFixedUnitWidth;
            _smallCardHeight = SmallCardFixedUnitHeight;

            var bigCardWidth = (_smallCardWidth * LargeCardColumnSpan) + HorizontalSpacing;
            var bigCardHeight = (_smallCardHeight * LargeCardRowSpan) + VerticalSpacing;

            int totalColumns = CalculateTotalColumns(finalSize.Width);
            _cellOccupancy.Clear();
            int currentRow = 0;
            int currentCol = 0;

            void EnsureRowExists(int rowIndex)
            {
                while (_cellOccupancy.Count <= rowIndex) _cellOccupancy.Add(new bool[totalColumns]);
            }

            (int row, int col) FindNextAvailableCell(int startRow, int startCol = 0)
            {
                for (int r = startRow; ; r++)
                {
                    EnsureRowExists(r);
                    for (int c = startCol; c < totalColumns; c++)
                    {
                        if (!_cellOccupancy[r][c]) return (r, c);
                    }
                    startCol = 0;
                }
            }

            var bigCard = BigCard;
            double maxLayoutHeight = 0;

            if (bigCard != null)
            {
                for (int r = 0; r < LargeCardRowSpan; r++)
                {
                    for (int c = 0; c < LargeCardColumnSpan; c++)
                    {
                        EnsureRowExists(r);
                        if (c < totalColumns) _cellOccupancy[r][c] = true;
                    }
                }

                // Add Bleed to layout bounds
                var rect = new Rect(0, 0, bigCardWidth + Bleed, bigCardHeight + Bleed);
                bigCard.Arrange(rect);
                AnimateChild(bigCard, new Point(0, 0));

                currentCol = LargeCardColumnSpan;
                currentRow = 0;
                maxLayoutHeight = bigCardHeight;
            }

            foreach (UIElement child in visibleChildren)
            {
                if (child == bigCard) continue;

                (currentRow, currentCol) = FindNextAvailableCell(currentRow, currentCol);

                if (currentRow < _cellOccupancy.Count && currentCol < totalColumns)
                {
                    _cellOccupancy[currentRow][currentCol] = true;
                }

                double x = (currentCol * (_smallCardWidth + HorizontalSpacing));
                double y = (currentRow * (_smallCardHeight + VerticalSpacing));

                var rect = new Rect(x, y, _smallCardWidth + Bleed, _smallCardHeight + Bleed);
                child.Arrange(rect);
                AnimateChild(child, new Point(x, y));

                maxLayoutHeight = Math.Max(maxLayoutHeight, y + _smallCardHeight);
                currentCol++;
            }

            return new Size(finalSize.Width, maxLayoutHeight + Bleed);
        }

        // Smooth composition animations
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