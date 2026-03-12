using System.Numerics;
using Windows.Foundation;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;

namespace EvolveOS_Optimizer.Assets.Panels
{
    public class RowInfo
    {
        public List<UIElement> Elements { get; set; } = new List<UIElement>();
        public double Height { get; set; }
        public double Width { get; set; }
    }

    public class FlowWrapPanel : Panel
    {
        #region Dependency Properties

        public static readonly DependencyProperty AnimationDurationMsProperty =
            DependencyProperty.Register(
                nameof(AnimationDurationMs),
                typeof(double),
                typeof(FlowWrapPanel),
                new PropertyMetadata(500.0, OnArrangePropertyChanged));

        public double AnimationDurationMs
        {
            get => (double)GetValue(AnimationDurationMsProperty);
            set => SetValue(AnimationDurationMsProperty, value);
        }

        public static readonly DependencyProperty UseCubicEaseProperty =
            DependencyProperty.Register(
                nameof(UseCubicEase),
                typeof(bool),
                typeof(FlowWrapPanel),
                new PropertyMetadata(true, OnArrangePropertyChanged));

        public bool UseCubicEase
        {
            get => (bool)GetValue(UseCubicEaseProperty);
            set => SetValue(UseCubicEaseProperty, value);
        }

        public static readonly DependencyProperty PreviousArrangeRectProperty =
            DependencyProperty.RegisterAttached(
                "PreviousArrangeRect",
                typeof(Rect),
                typeof(FlowWrapPanel),
                new PropertyMetadata(Rect.Empty));

        public static Rect GetPreviousArrangeRect(UIElement element)
        {
            return (Rect)element.GetValue(PreviousArrangeRectProperty);
        }

        public static void SetPreviousArrangeRect(UIElement element, Rect value)
        {
            element.SetValue(PreviousArrangeRectProperty, value);
        }

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                nameof(HorizontalSpacing),
                typeof(double),
                typeof(FlowWrapPanel),
                new PropertyMetadata(5.0, OnMeasurePropertyChanged));

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                nameof(VerticalSpacing),
                typeof(double),
                typeof(FlowWrapPanel),
                new PropertyMetadata(5.0, OnMeasurePropertyChanged));

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        #endregion

        #region Property Changed Callbacks

        private static void OnMeasurePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FlowWrapPanel panel)
            {
                panel.InvalidateMeasure();
            }
        }

        private static void OnArrangePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FlowWrapPanel panel)
            {
                panel.InvalidateArrange();
            }
        }

        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            double currentX = 0;
            double currentY = 0;
            double maxRowHeight = 0;
            double panelWidth = 0;

            bool isFirstInRow = true;

            foreach (UIElement child in Children)
            {
                child.Measure(new Size(availableSize.Width, availableSize.Height));

                double requiredWidth = child.DesiredSize.Width;
                double spacingNeeded = isFirstInRow ? 0 : HorizontalSpacing;

                double prospectiveRightEdge = currentX + spacingNeeded + requiredWidth;

                if (prospectiveRightEdge > availableSize.Width && !isFirstInRow)
                {
                    panelWidth = Math.Max(panelWidth, currentX);

                    currentX = requiredWidth;
                    currentY += maxRowHeight + VerticalSpacing;
                    maxRowHeight = 0;
                    isFirstInRow = true;

                    prospectiveRightEdge = requiredWidth;
                }

                currentX = prospectiveRightEdge + HorizontalSpacing;

                panelWidth = Math.Max(panelWidth, prospectiveRightEdge);
                maxRowHeight = Math.Max(maxRowHeight, child.DesiredSize.Height);

                isFirstInRow = false;
            }

            return new Size(
                Math.Min(availableSize.Width, panelWidth),
                currentY + maxRowHeight
            );
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            List<RowInfo> rows = new List<RowInfo>();
            RowInfo currentRow = new RowInfo();
            double currentX = 0;
            double maxRowHeight = 0;

            foreach (UIElement child in Children)
            {
                double leadingSpacing = currentRow.Elements.Count > 0 ? HorizontalSpacing : 0;
                double childWidth = child.DesiredSize.Width;

                double prospectiveRightEdge = currentX + leadingSpacing + childWidth;

                if (prospectiveRightEdge > finalSize.Width && currentRow.Elements.Count > 0)
                {
                    currentRow.Height = maxRowHeight;
                    currentRow.Width = currentX;
                    rows.Add(currentRow);

                    currentRow = new RowInfo();
                    currentX = 0;
                    maxRowHeight = 0;
                    leadingSpacing = 0;
                    prospectiveRightEdge = childWidth;
                }

                currentRow.Elements.Add(child);

                currentX = prospectiveRightEdge + HorizontalSpacing;
                maxRowHeight = Math.Max(maxRowHeight, child.DesiredSize.Height);
            }

            if (currentRow.Elements.Any())
            {
                currentRow.Height = maxRowHeight;
                currentRow.Width = currentX - HorizontalSpacing;
                rows.Add(currentRow);
            }

            double y = 0;
            foreach (var row in rows)
            {
                double x = 0;
                foreach (var child in row.Elements)
                {
                    double spacing = x == 0 ? 0 : HorizontalSpacing;
                    x += spacing;

                    var newRect = new Rect(x, y, child.DesiredSize.Width, child.DesiredSize.Height);
                    var oldRect = GetPreviousArrangeRect(child);

                    child.Arrange(newRect);

                    if (oldRect != Rect.Empty && oldRect != newRect)
                    {
                        try
                        {
                            Visual visual = ElementCompositionPreview.GetElementVisual(child);
                            Compositor compositor = visual.Compositor;

                            float offsetX = (float)(oldRect.X - newRect.X);
                            float offsetY = (float)(oldRect.Y - newRect.Y);

                            visual.Properties.InsertVector3("Translation", new Vector3(offsetX, offsetY, 0));
                            ElementCompositionPreview.SetIsTranslationEnabled(child, true);

                            Vector3KeyFrameAnimation anim = compositor.CreateVector3KeyFrameAnimation();
                            anim.Duration = TimeSpan.FromMilliseconds(AnimationDurationMs);

                            if (UseCubicEase)
                            {
                                var easeOut = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.0f), new Vector2(0.0f, 1.0f));
                                anim.InsertKeyFrame(1.0f, Vector3.Zero, easeOut);
                            }
                            else
                            {
                                anim.InsertKeyFrame(1.0f, Vector3.Zero);
                            }

                            visual.StartAnimation("Translation", anim);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"FlowWrapPanel Animation Error: {ex.Message}");
                        }
                    }
                    else if (oldRect == Rect.Empty)
                    {
                        Visual visual = ElementCompositionPreview.GetElementVisual(child);
                        visual.Properties.InsertVector3("Translation", Vector3.Zero);
                    }

                    SetPreviousArrangeRect(child, newRect);
                    x += child.DesiredSize.Width;
                }

                y += row.Height + VerticalSpacing;
            }

            return finalSize;
        }
    }
}