using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.Panels
{
    public class SecurityFlowPanel : Panel
    {
        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                nameof(HorizontalSpacing),
                typeof(double),
                typeof(SecurityFlowPanel),
                new PropertyMetadata(12.0, OnPanelPropertyChanged));

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                nameof(VerticalSpacing),
                typeof(double),
                typeof(SecurityFlowPanel),
                new PropertyMetadata(12.0, OnPanelPropertyChanged));

        public int DesiredColumns
        {
            get => (int)GetValue(DesiredColumnsProperty);
            set => SetValue(DesiredColumnsProperty, value);
        }

        public static readonly DependencyProperty DesiredColumnsProperty =
            DependencyProperty.Register(
                nameof(DesiredColumns),
                typeof(int),
                typeof(SecurityFlowPanel),
                new PropertyMetadata(0, OnPanelPropertyChanged));

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
            int currentColumn = 0;

            foreach (UIElement child in Children)
            {
                child.Measure(availableSize);
                Size desiredSize = child.DesiredSize;

                bool outOfSpace = currentX + desiredSize.Width > availableSize.Width;
                bool hitColumnLimit = DesiredColumns > 0 && currentColumn >= DesiredColumns;

                if ((outOfSpace || hitColumnLimit) && currentX > 0)
                {
                    currentX = 0;
                    currentY += maxRowHeight + VerticalSpacing;
                    maxRowHeight = 0;
                    currentColumn = 0;
                }

                maxRowHeight = Math.Max(maxRowHeight, desiredSize.Height);

                currentX += desiredSize.Width + HorizontalSpacing;
                currentColumn++;

                panelWidth = Math.Max(panelWidth, currentX - HorizontalSpacing);
            }

            double totalHeight = currentY + maxRowHeight;

            return new Size(
                double.IsInfinity(availableSize.Width) ? panelWidth : availableSize.Width,
                totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double currentX = 0;
            double currentY = 0;
            double maxRowHeight = 0;
            int currentColumn = 0;

            foreach (UIElement child in Children)
            {
                Size desiredSize = child.DesiredSize;

                bool outOfSpace = currentX + desiredSize.Width > finalSize.Width;
                bool hitColumnLimit = DesiredColumns > 0 && currentColumn >= DesiredColumns;

                if ((outOfSpace || hitColumnLimit) && currentX > 0)
                {
                    currentX = 0;
                    currentY += maxRowHeight + VerticalSpacing;
                    maxRowHeight = 0;
                    currentColumn = 0;
                }

                child.Arrange(new Rect(currentX, currentY, desiredSize.Width, desiredSize.Height));

                currentX += desiredSize.Width + HorizontalSpacing;
                maxRowHeight = Math.Max(maxRowHeight, desiredSize.Height);
                currentColumn++;
            }

            return finalSize;
        }
    }
}