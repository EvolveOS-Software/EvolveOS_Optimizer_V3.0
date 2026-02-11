using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using System.Runtime.CompilerServices;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public class HoneycombPanel : Panel
    {
        private bool _isLayoutActive = false;

        private static readonly ConditionalWeakTable<FrameworkElement, FrameworkElement> _contentCache =
            new ConditionalWeakTable<FrameworkElement, FrameworkElement>();

        public static readonly DependencyProperty IsAnimationEnabledProperty =
            DependencyProperty.Register(
                nameof(IsAnimationEnabled),
                typeof(bool),
                typeof(HoneycombPanel),
                new PropertyMetadata(true, OnIsAnimationEnabledChanged));

        public bool IsAnimationEnabled
        {
            get => (bool)GetValue(IsAnimationEnabledProperty);
            set => SetValue(IsAnimationEnabledProperty, value);
        }

        private const double HexWidth = 85.0;
        private const double HexHeight = 95.0;
        private const double XSpacingFactor = 1.15;
        private const double YSpacingFactor = 1.15;
        private const double RotationAngle = 30.0;

        private bool _isTimerTriggered = false;
        private bool _isInitialLoad = true;
        private DispatcherTimer _resizeTimer;
        private int _lastColumnCount = -1;
        private Size _lastSize = new Size(0, 0);

        private const int FixedColumns = 13;

        public static bool EnableEntranceAnimation { get; set; } = true;
        public static int ScaleDurationMs { get; set; } = 500;
        public static int PositionDurationMs { get; set; } = 500;
        public static int StaggerMs { get; set; } = 15;
        public static double ManualVerticalPush { get; set; } = 0;
        public static double ManualHorizontalPush { get; set; } = 0;

        public Action? AnimationFinished { get; set; }

        public HoneycombPanel()
        {
            this.ManipulationMode = ManipulationModes.None;

            _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _resizeTimer.Tick += (s, e) =>
            {
                _resizeTimer.Stop();
                _isTimerTriggered = true;
                InvalidateArrange();
            };

            this.Loaded += (s, e) =>
            {
                _isInitialLoad = true;
                InvalidateArrange();
            };
        }

        private static void OnIsAnimationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HoneycombPanel panel && !(bool)e.NewValue) panel.StopAllAnimations();
        }

        private void StopAllAnimations()
        {
            foreach (UIElement child in Children)
            {
                var visual = ElementCompositionPreview.GetElementVisual(child);
                visual.StopAnimation("Offset");
                visual.StopAnimation("Scale");
                visual.StopAnimation("Opacity");
                visual.Opacity = 1.0f;
                visual.Scale = new Vector3(1, 1, 1);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double xStep = HexWidth * XSpacingFactor;
            double yStep = HexHeight * YSpacingFactor;
            int visibleCount = Children.Count(c => c.Visibility != Visibility.Collapsed);

            if (visibleCount == 0) return new Size(0, 0);

            int maxFitColumns = (int)Math.Max(1, Math.Floor((availableSize.Width - HexWidth) / xStep) + 1);
            int columnsToUse = Math.Min(FixedColumns, maxFitColumns);
            int rowCount = (int)Math.Ceiling((double)visibleCount / columnsToUse);
            int actualCols = Math.Min(visibleCount, columnsToUse);

            double clusterWidth = ((actualCols - 1) * xStep) + HexWidth;
            double clusterHeight = ((rowCount - 1) * yStep) + HexHeight + (actualCols > 1 ? yStep / 2.0 : 0);

            foreach (UIElement child in Children)
            {
                if (child.Visibility == Visibility.Collapsed) continue;
                child.Measure(new Size(HexWidth, HexHeight));
            }

            return new Size(
                double.IsInfinity(availableSize.Width) ? clusterWidth : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? clusterHeight : availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_isLayoutActive) return finalSize;

            var visibleItems = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            int visibleCount = visibleItems.Count;
            if (visibleCount == 0) return finalSize;

            _isLayoutActive = true;
            try
            {
                double xStepBase = HexWidth * XSpacingFactor;
                double yStepBase = HexHeight * YSpacingFactor;

                int maxFitColumns = (int)Math.Max(1, Math.Floor((finalSize.Width - HexWidth) / xStepBase) + 1);
                int columnsToUse = Math.Min(FixedColumns, maxFitColumns);

                bool isReflowing = (Math.Abs(finalSize.Width - _lastSize.Width) > 50 ||
                                   (_lastColumnCount != -1 && _lastColumnCount != columnsToUse) ||
                                   _isTimerTriggered) && IsAnimationEnabled;

                bool triggerEntrance = _isInitialLoad && EnableEntranceAnimation;

                _lastColumnCount = columnsToUse;
                _lastSize = finalSize;
                _isTimerTriggered = false;
                _isInitialLoad = false;

                int actualCols = Math.Min(visibleCount, columnsToUse);
                int actualRows = (int)Math.Ceiling((double)visibleCount / columnsToUse);

                double baseGroupWidth = ((actualCols - 1) * xStepBase) + HexWidth;
                double baseGroupHeight = ((actualRows - 1) * yStepBase) + HexHeight + (actualCols > 1 ? yStepBase / 2.0 : 0);

                double scaleX = (finalSize.Width * 0.9) / baseGroupWidth;
                double scaleY = (finalSize.Height * 0.9) / baseGroupHeight;
                float uniformScale = (float)Math.Max(1.0, Math.Min(scaleX, scaleY));

                double xStep = xStepBase * uniformScale;
                double yStep = yStepBase * uniformScale;
                double scaledHexWidth = HexWidth * uniformScale;
                double scaledHexHeight = HexHeight * uniformScale;

                double groupWidth = ((actualCols - 1) * xStep) + scaledHexWidth;
                double groupHeight = ((actualRows - 1) * yStep) + scaledHexHeight + (actualCols > 1 ? (yStep / 2.0) : 0);

                double xOffsetStart = (finalSize.Width - groupWidth) / 4.0 + ManualHorizontalPush;
                double yOffsetStart = (finalSize.Height - groupHeight) / 2.0 + ManualVerticalPush;

                for (int i = 0; i < visibleCount; i++)
                {
                    var fe = visibleItems[i] as FrameworkElement;
                    if (fe == null) continue;

                    int col = i % columnsToUse;
                    int row = i / columnsToUse;

                    double slotCenterX = xOffsetStart + (col * xStep) + (scaledHexWidth / 2.0);
                    double slotCenterY = yOffsetStart + (row * yStep) + (scaledHexHeight / 2.0);

                    if (col % 2 != 0) slotCenterY += yStep / 2.0;

                    fe.Arrange(new Rect(0, 0, HexWidth, HexHeight));

                    var visual = ElementCompositionPreview.GetElementVisual(fe);
                    visual.Size = new Vector2((float)HexWidth, (float)HexHeight);
                    visual.AnchorPoint = new Vector2(0.5f, 0.5f);

                    visual.CenterPoint = new Vector3((float)HexWidth / 2, (float)HexHeight / 2, 0);
                    visual.RotationAngleInDegrees = (float)RotationAngle;

                    CounterRotateContent(fe);

                    float finalX = (float)slotCenterX;
                    float finalY = (float)slotCenterY;

                    ApplyCompositionMove(fe, finalX, finalY, uniformScale, i, triggerEntrance, isReflowing);
                }
            }
            finally
            {
                _isLayoutActive = false;
            }

            return finalSize;
        }

        private void ApplyCompositionMove(FrameworkElement fe, float x, float y, float scale, int index, bool isEntrance, bool isReflowing)
        {
            var visual = ElementCompositionPreview.GetElementVisual(fe);
            var compositor = visual.Compositor;
            var delay = TimeSpan.FromMilliseconds(index * (isReflowing ? StaggerMs : 0));

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.175f, 0.885f), new Vector2(0.320f, 1.275f));

            var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
            offsetAnim.InsertKeyFrame(1.0f, new Vector3(x, y, 0), easing);
            offsetAnim.Duration = TimeSpan.FromMilliseconds(PositionDurationMs);
            offsetAnim.DelayTime = delay;
            visual.StartAnimation("Offset", offsetAnim);

            var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
            scaleAnim.InsertKeyFrame(1.0f, new Vector3(scale, scale, 1.0f), easing);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(ScaleDurationMs);
            scaleAnim.DelayTime = delay;
            visual.StartAnimation("Scale", scaleAnim);

            if (isEntrance)
            {
                visual.Opacity = 0;
                var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
                fadeAnim.InsertKeyFrame(1.0f, 1.0f);
                fadeAnim.Duration = TimeSpan.FromMilliseconds(ScaleDurationMs);
                fadeAnim.DelayTime = delay;
                visual.StartAnimation("Opacity", fadeAnim);
            }

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, e) => { this.IsHitTestVisible = true; AnimationFinished?.Invoke(); };
            batch.End();
        }

        private void CounterRotateContent(FrameworkElement fe)
        {
            if (fe is ContentControl contentControl && contentControl.Content is FrameworkElement content)
            {
                var visual = ElementCompositionPreview.GetElementVisual(content);
                var parentVisual = ElementCompositionPreview.GetElementVisual(fe);
                var compositor = visual.Compositor;

                visual.AnchorPoint = new Vector2(0.5f, 0.5f);

                try
                {
                    var bindAnim = compositor.CreateExpressionAnimation("Vector3(parent.Size.X / 2, parent.Size.Y / 2, 0)");
                    bindAnim.SetReferenceParameter("parent", parentVisual);

                    visual.StartAnimation("Offset", bindAnim);
                    visual.RotationAngleInDegrees = (float)-RotationAngle;
                }
                catch { }
            }
        }
    }
}