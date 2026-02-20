using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.Panels
{
    public class FlowPanel : Panel
    {
        public double HorizontalGap
        {
            get { return (double)GetValue(HorizontalGapProperty); }
            set { SetValue(HorizontalGapProperty, value); }
        }

        public static readonly DependencyProperty HorizontalGapProperty =
            DependencyProperty.Register(nameof(HorizontalGap), typeof(double), typeof(FlowPanel), new PropertyMetadata(12.0, OnGapChanged));

        public double VerticalGap
        {
            get { return (double)GetValue(VerticalGapProperty); }
            set { SetValue(VerticalGapProperty, value); }
        }

        public static readonly DependencyProperty VerticalGapProperty =
            DependencyProperty.Register(nameof(VerticalGap), typeof(double), typeof(FlowPanel), new PropertyMetadata(12.0, OnGapChanged));

        public int DesiredColumns
        {
            get { return (int)GetValue(DesiredColumnsProperty); }
            set { SetValue(DesiredColumnsProperty, value); }
        }

        public static readonly DependencyProperty DesiredColumnsProperty =
            DependencyProperty.Register(nameof(DesiredColumns), typeof(int), typeof(FlowPanel), new PropertyMetadata(3, OnGapChanged));

        public static readonly DependencyProperty ColumnSpanProperty =
            DependencyProperty.RegisterAttached("ColumnSpan", typeof(int), typeof(FlowPanel), new PropertyMetadata(1, OnGapChanged));

        public static int GetColumnSpan(DependencyObject obj) => (int)obj.GetValue(ColumnSpanProperty);
        public static void SetColumnSpan(DependencyObject obj, int value) => obj.SetValue(ColumnSpanProperty, value);

        private static void OnGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
            else if (d is UIElement element && VisualTreeHelper.GetParent(element) is FlowPanel parentPanel)
            {
                parentPanel.InvalidateMeasure();
                parentPanel.InvalidateArrange();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count == 0 || double.IsPositiveInfinity(availableSize.Width))
                return new Size(0, 0);

            int numCols = DesiredColumns;
            double totalGapWidth = HorizontalGap * (numCols - 1);
            double calculatedChildWidth = Math.Max(0, (availableSize.Width - totalGapWidth) / numCols);

            double[] colHeights = new double[numCols];

            foreach (UIElement child in Children)
            {
                int span = Math.Min(numCols, Math.Max(1, GetColumnSpan(child)));
                double childWidth = (calculatedChildWidth * span) + (HorizontalGap * (span - 1));

                child.Measure(new Size(childWidth, double.PositiveInfinity));

                int bestCol = 0;
                double minPlacementY = double.MaxValue;

                for (int i = 0; i <= numCols - span; i++)
                {
                    double maxY = 0;
                    for (int s = 0; s < span; s++) maxY = Math.Max(maxY, colHeights[i + s]);

                    if (maxY < minPlacementY)
                    {
                        minPlacementY = maxY;
                        bestCol = i;
                    }
                }

                double newHeight = minPlacementY + child.DesiredSize.Height + VerticalGap;
                for (int s = 0; s < span; s++) colHeights[bestCol + s] = newHeight;
            }

            double maxColHeight = 0;
            foreach (var h in colHeights) maxColHeight = Math.Max(maxColHeight, h);

            return new Size(availableSize.Width, maxColHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count == 0) return finalSize;

            int numCols = DesiredColumns;
            double totalGapWidth = HorizontalGap * (numCols - 1);
            double calculatedChildWidth = Math.Max(0, (finalSize.Width - totalGapWidth) / numCols);

            double[] colHeights = new double[numCols];

            foreach (UIElement child in Children)
            {
                int span = Math.Min(numCols, Math.Max(1, GetColumnSpan(child)));
                double childWidth = (calculatedChildWidth * span) + (HorizontalGap * (span - 1));

                int bestCol = 0;
                double minPlacementY = double.MaxValue;

                for (int i = 0; i <= numCols - span; i++)
                {
                    double maxY = 0;
                    for (int s = 0; s < span; s++) maxY = Math.Max(maxY, colHeights[i + s]);

                    if (maxY < minPlacementY)
                    {
                        minPlacementY = maxY;
                        bestCol = i;
                    }
                }

                double x = bestCol * (calculatedChildWidth + HorizontalGap);
                double y = minPlacementY;

                child.Arrange(new Rect(x, y, childWidth, child.DesiredSize.Height));

                double newHeight = minPlacementY + child.DesiredSize.Height + VerticalGap;
                for (int s = 0; s < span; s++) colHeights[bestCol + s] = newHeight;
            }

            return finalSize;
        }
    }
}