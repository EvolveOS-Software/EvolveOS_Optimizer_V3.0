// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.Panels
{
    public class SecurityFlowPanel : Panel
    {
        #region Dependency Properties

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double), typeof(SecurityFlowPanel), new PropertyMetadata(12.0, OnPanelPropertyChanged));

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(nameof(VerticalSpacing), typeof(double), typeof(SecurityFlowPanel), new PropertyMetadata(12.0, OnPanelPropertyChanged));

        public double HorizontalOffset
        {
            get => (double)GetValue(HorizontalOffsetProperty);
            set => SetValue(HorizontalOffsetProperty, value);
        }

        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register(nameof(HorizontalOffset), typeof(double), typeof(SecurityFlowPanel), new PropertyMetadata(0.0, OnPanelPropertyChanged));

        #endregion

        private static void OnPanelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SecurityFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double currentX = 0;
            double currentY = 0;
            double maxRowHeight = 0;
            double panelWidth = 0;

            foreach (UIElement child in Children)
            {
                child.Measure(availableSize);
                Size desiredSize = child.DesiredSize;

                if (currentX + desiredSize.Width > availableSize.Width && currentX > 0)
                {
                    currentX = 0;
                    currentY += maxRowHeight + VerticalSpacing;
                    maxRowHeight = 0;
                }

                maxRowHeight = Math.Max(maxRowHeight, desiredSize.Height);
                currentX += desiredSize.Width + HorizontalSpacing;
                panelWidth = Math.Max(panelWidth, currentX - HorizontalSpacing);
            }

            return new Size(
                double.IsInfinity(availableSize.Width) ? panelWidth : availableSize.Width,
                currentY + maxRowHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double currentY = 0;
            int i = 0;

            while (i < Children.Count)
            {
                List<UIElement> rowChildren = new List<UIElement>();
                double rowWidth = 0;
                double maxRowHeight = 0;

                while (i < Children.Count)
                {
                    var child = Children[i];
                    double childWidth = child.DesiredSize.Width;

                    if (rowChildren.Count > 0 && rowWidth + HorizontalSpacing + childWidth > finalSize.Width)
                        break;

                    rowChildren.Add(child);
                    rowWidth += (rowChildren.Count > 1 ? HorizontalSpacing : 0) + childWidth;
                    maxRowHeight = Math.Max(maxRowHeight, child.DesiredSize.Height);
                    i++;
                }

                double xOffset = ((finalSize.Width - rowWidth) / 2) + HorizontalOffset;

                double currentX = xOffset;
                foreach (var child in rowChildren)
                {
                    child.Arrange(new Rect(currentX, currentY, child.DesiredSize.Width, child.DesiredSize.Height));
                    currentX += child.DesiredSize.Width + HorizontalSpacing;
                }

                currentY += maxRowHeight + VerticalSpacing;
            }

            return finalSize;
        }
    }
}