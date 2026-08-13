// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Reflection;
using EvolveOS_Optimizer.Assets.UserControl;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Win32;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class HomePage : Page, IPurgeable
    {
        #region Fields
        private GridViewItem? _activeDraggedItem = null;
        private GridViewItem? _hoveredTargetItem = null;
        private Border? _activeDraggedCard = null;
        private bool _isTrackingDrag = false;

        private Dictionary<GridViewItem, Rect> _logicalBounds = new();
        private Point _dragStartPoint;
        private Point _draggedItemBasePos;

        private readonly DispatcherQueue _dispatcherQueue;

        private DispatcherTimer? _wallpaperTimer;

        private string _currentWallpaperPath = string.Empty;
        private DateTime _currentWallpaperWriteTime = DateTime.MinValue;

        private const string RegistryPath = @"Software\EvolveOS_Optimizer";
        private const string RegistryValueName = "LastLocation";

        private bool _isCurrentPageActive = false;
        private bool _isInitialized = false;

        private List<double> _cpuHistory = new List<double>();
        private List<double> _ramHistory = new List<double>();
        private List<double> _netDownHistory = new List<double>();
        private List<double> _netUpHistory = new List<double>();
        private List<double> _gpuHistory = new List<double>();

        private int _maxCpuDataPoints = 300;
        private int _maxRamDataPoints = 300;
        private int _maxNetDataPoints = 300;
        private int _maxGpuDataPoints = 300;
        #endregion

        public HomePageViewModel ViewModel { get; } = new();

        #region Constructor & Page Lifecycle
        public HomePage()
        {
            this.InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            LogoGrid.Translation = new System.Numerics.Vector3(0, 0, 32);

            this.DataContext = ViewModel;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue not found.");

            this.Loaded += HomePage_Loaded;
            this.Unloaded += Page_Unloaded;

            ViewModel.OnTelemetryTicked += ViewModel_OnTelemetryTicked;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isCurrentPageActive) return;
            _isCurrentPageActive = true;

            StartWallpaperMonitor();
            ResumeLiveMonitoring();

            MainWinViewModel.AppHidden += PauseLiveMonitoring;
            MainWinViewModel.AppRestored += ResumeLiveMonitoring;

            if (!_isInitialized)
            {
                ApplyElevationUI();
                LoadWeather();
                LoadDashboardLayout();
                DashboardDragCursor();
                UpdateDnsCardUI();

                _ = CalculateSystemHealthAsync();
                _ = CalculateSecurityHealthAsync();

                StartShimmer(IpShimmerBrush, "Stop2");
                StartShimmer(LocalIpShimmerBrush, "LocalStop2");

                if (this.DataContext is HomePageViewModel vm)
                {
                    UpdateIpPrivacy(vm.StateButtonVision);
                }

                _isInitialized = true;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_isCurrentPageActive) return;
            _isCurrentPageActive = false;

            MainWinViewModel.AppHidden -= PauseLiveMonitoring;
            MainWinViewModel.AppRestored -= ResumeLiveMonitoring;

            PauseLiveMonitoring();

            Debug.WriteLine("[HomePage] Page cached and background engines paused.");

            _ = Purge();
        }

        private void PauseLiveMonitoring()
        {
            _wallpaperTimer?.Stop();

            if (this.DataContext is HomePageViewModel vm)
            {
                vm.PauseUpdates();
            }

            Debug.WriteLine("[HomePage] Live monitoring PAUSED for System Tray/Cache.");
        }

        private void ResumeLiveMonitoring()
        {
            _wallpaperTimer?.Start();

            if (this.DataContext is HomePageViewModel vm)
            {
                vm.ResumeUpdates();
            }

            Debug.WriteLine("[HomePage] Live monitoring RESUMED.");
        }
        #endregion

        #region View/UI Telemetry Updates (Linked to ViewModel)
        private void ViewModel_OnTelemetryTicked(TelemetryDataPayload payload)
        {
            if (!_isCurrentPageActive) return;

            CPULoad.Value = Math.Clamp(payload.Cpu, 0, 100);
            RAMLoad.Value = Math.Clamp(payload.Ram, 0, 100);
            CPUText.Text = ((int)Math.Round(payload.Cpu)).ToString();
            RAMText.Text = ((int)Math.Round(payload.Ram)).ToString();

            if (payload.IsFullSecond)
            {
                ProcCountText.Text = payload.ProcCount;
                SvcCountText.Text = payload.SvcCount;
            }

            DownLoadRing.Value = Math.Clamp(payload.NetDown, 0, 1000);
            UpLoadRing.Value = Math.Clamp(payload.NetUp, 0, 1000);

            DownLoadText.Text = payload.NetDown.ToString("F2");
            UpLoadText.Text = payload.NetUp.ToString("F2");

            UpdateCpuGraph(payload.Cpu);
            UpdateRamGraph(payload.Ram);
            UpdateNetworkGraph(payload.NetDown, payload.NetUp);
            UpdateGpuGraph(payload.Gpu);
        }
        #endregion

        #region GPU Graph Logic
        private void UpdateGpuAxisLabels(int totalSeconds)
        {
            if (TxtGpuAxis1 == null || TxtGpuAxis2 == null || TxtGpuAxis3 == null || TxtGpuAxis4 == null) return;

            double step = totalSeconds / 4.0;
            TxtGpuAxis4.Text = FormatTime(totalSeconds);
            TxtGpuAxis3.Text = FormatTime(totalSeconds - step);
            TxtGpuAxis2.Text = FormatTime(totalSeconds - (step * 2));
            TxtGpuAxis1.Text = FormatTime(totalSeconds - (step * 3));
        }

        private void UpdateGpuGraph(double currentGpuUsage)
        {
            _gpuHistory.Add(currentGpuUsage);

            while (_gpuHistory.Count > _maxGpuDataPoints)
            {
                _gpuHistory.RemoveAt(0);
            }

            if (TxtCurrentGpu != null)
                TxtCurrentGpu.Text = $"{Math.Round(currentGpuUsage)}%";

            DrawGpuGraph();
        }

        private void GpuGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGpuGraph();
        }

        private void DrawGpuGraph()
        {
            if (GpuGraphCanvas == null || GpuGraphLine == null || GpuGraphFill == null || GpuGraphDot == null) return;
            if (_gpuHistory.Count < 2 || GpuGraphCanvas.ActualWidth == 0 || GpuGraphCanvas.ActualHeight == 0) return;

            double width = GpuGraphCanvas.ActualWidth;
            double height = GpuGraphCanvas.ActualHeight;
            double maxGpu = 100.0;
            double stepX = width / Math.Max(1, _maxGpuDataPoints - 1);
            double startX = width - ((_gpuHistory.Count - 1) * stepX);
            double startY = height - (_gpuHistory[0] / maxGpu * height);

            Point startPoint = new Point(startX, startY);
            var points = new PointCollection();
            var fillPoints = new PointCollection { startPoint };
            Point lastPoint = startPoint;

            for (int i = 1; i < _gpuHistory.Count; i++)
            {
                double x = startX + (i * stepX);
                double y = Math.Max(0, Math.Min(height, height - (_gpuHistory[i] / maxGpu * height)));
                lastPoint = new Point(x, y);

                points.Add(lastPoint);
                fillPoints.Add(lastPoint);
            }
            fillPoints.Add(new Point(width, height));

            var lineGeo = new PathGeometry();
            var lineFig = new PathFigure { StartPoint = startPoint };
            lineFig.Segments.Add(new PolyLineSegment { Points = points });
            lineGeo.Figures.Add(lineFig);
            GpuGraphLine.Data = lineGeo;

            var fillGeo = new PathGeometry();
            var fillFig = new PathFigure { StartPoint = new Point(startX, height) };
            fillFig.Segments.Add(new PolyLineSegment { Points = fillPoints });
            fillGeo.Figures.Add(fillFig);
            GpuGraphFill.Data = fillGeo;

            GpuGraphDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(GpuGraphDot, lastPoint.X);
            Canvas.SetTop(GpuGraphDot, lastPoint.Y);
        }
        #endregion

        #region Network Graph Logic
        private void UpdateNetAxisLabels(int totalSeconds)
        {
            if (TxtNetAxis1 == null || TxtNetAxis2 == null || TxtNetAxis3 == null || TxtNetAxis4 == null) return;

            double step = totalSeconds / 4.0;

            TxtNetAxis4.Text = FormatTime(totalSeconds);
            TxtNetAxis3.Text = FormatTime(totalSeconds - step);
            TxtNetAxis2.Text = FormatTime(totalSeconds - (step * 2));
            TxtNetAxis1.Text = FormatTime(totalSeconds - (step * 3));
        }

        private void UpdateNetworkGraph(double dlMbps, double ulMbps)
        {
            _netDownHistory.Add(dlMbps);
            _netUpHistory.Add(ulMbps);

            while (_netDownHistory.Count > _maxNetDataPoints) _netDownHistory.RemoveAt(0);
            while (_netUpHistory.Count > _maxNetDataPoints) _netUpHistory.RemoveAt(0);

            if (TxtCurrentDown != null) TxtCurrentDown.Text = $"{Math.Round(dlMbps, 1)} Mbps";
            if (TxtCurrentUp != null) TxtCurrentUp.Text = $"{Math.Round(ulMbps, 1)} Mbps";

            DrawNetGraph();
        }

        private void NetGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawNetGraph();
        }

        private void DrawNetGraph()
        {
            if (NetGraphCanvas == null || NetGraphLineDown == null || NetGraphLineUp == null || NetGraphFillDown == null || NetGraphFillUp == null) return;
            if (_netDownHistory.Count < 2 || NetGraphCanvas.ActualWidth == 0 || NetGraphCanvas.ActualHeight == 0) return;

            double width = NetGraphCanvas.ActualWidth;
            double height = NetGraphCanvas.ActualHeight;

            double maxDown = _netDownHistory.Count > 0 ? _netDownHistory.Max() : 0;
            double maxUp = _netUpHistory.Count > 0 ? _netUpHistory.Max() : 0;
            double absoluteMax = Math.Max(maxDown, maxUp);
            double maxNetScale = Math.Max(10.0, Math.Ceiling(absoluteMax * 1.2));

            if (TxtNetY4 != null)
            {
                TxtNetY4.Text = Math.Round(maxNetScale).ToString();
                TxtNetY3.Text = Math.Round(maxNetScale * 0.75).ToString();
                TxtNetY2.Text = Math.Round(maxNetScale * 0.50).ToString();
                TxtNetY1.Text = Math.Round(maxNetScale * 0.25).ToString();
            }

            double stepX = width / Math.Max(1, _maxNetDataPoints - 1);
            double startX = width - ((_netDownHistory.Count - 1) * stepX);

            double startYDown = height - (_netDownHistory[0] / maxNetScale * height);
            double startYUp = height - (_netUpHistory[0] / maxNetScale * height);

            Point startPointDown = new Point(startX, Math.Max(0, Math.Min(height, startYDown)));
            Point startPointUp = new Point(startX, Math.Max(0, Math.Min(height, startYUp)));

            var downPoints = new PointCollection();
            var upPoints = new PointCollection();
            var downFillPoints = new PointCollection { startPointDown };
            var upFillPoints = new PointCollection { startPointUp };

            Point lastDownPoint = startPointDown;
            Point lastUpPoint = startPointUp;

            for (int i = 1; i < _netDownHistory.Count; i++)
            {
                double x = startX + (i * stepX);
                double yDown = Math.Max(0, Math.Min(height, height - (_netDownHistory[i] / maxNetScale * height)));
                double yUp = Math.Max(0, Math.Min(height, height - (_netUpHistory[i] / maxNetScale * height)));

                lastDownPoint = new Point(x, yDown);
                lastUpPoint = new Point(x, yUp);

                downPoints.Add(lastDownPoint);
                upPoints.Add(lastUpPoint);
                downFillPoints.Add(lastDownPoint);
                upFillPoints.Add(lastUpPoint);
            }

            downFillPoints.Add(new Point(width, height));
            upFillPoints.Add(new Point(width, height));

            var downGeo = new PathGeometry();
            var downFig = new PathFigure { StartPoint = startPointDown };
            downFig.Segments.Add(new PolyLineSegment { Points = downPoints });
            downGeo.Figures.Add(downFig);
            NetGraphLineDown.Data = downGeo;

            var downFillGeo = new PathGeometry();
            var downFillFig = new PathFigure { StartPoint = new Point(startX, height) };
            downFillFig.Segments.Add(new PolyLineSegment { Points = downFillPoints });
            downFillGeo.Figures.Add(downFillFig);
            NetGraphFillDown.Data = downFillGeo;

            var upGeo = new PathGeometry();
            var upFig = new PathFigure { StartPoint = startPointUp };
            upFig.Segments.Add(new PolyLineSegment { Points = upPoints });
            upGeo.Figures.Add(upFig);
            NetGraphLineUp.Data = upGeo;

            var upFillGeo = new PathGeometry();
            var upFillFig = new PathFigure { StartPoint = new Point(startX, height) };
            upFillFig.Segments.Add(new PolyLineSegment { Points = upFillPoints });
            upFillGeo.Figures.Add(upFillFig);
            NetGraphFillUp.Data = upFillGeo;

            NetGraphDotDown.Visibility = Visibility.Visible;
            NetGraphDotUp.Visibility = Visibility.Visible;
            Canvas.SetLeft(NetGraphDotDown, lastDownPoint.X);
            Canvas.SetTop(NetGraphDotDown, lastDownPoint.Y);
            Canvas.SetLeft(NetGraphDotUp, lastUpPoint.X);
            Canvas.SetTop(NetGraphDotUp, lastUpPoint.Y);
        }
        #endregion

        #region RAM Graph Logic
        private void UpdateRamAxisLabels(int totalSeconds)
        {
            if (TxtRamAxis1 == null || TxtRamAxis2 == null || TxtRamAxis3 == null || TxtRamAxis4 == null) return;

            double step = totalSeconds / 4.0;

            TxtRamAxis4.Text = FormatTime(totalSeconds);
            TxtRamAxis3.Text = FormatTime(totalSeconds - step);
            TxtRamAxis2.Text = FormatTime(totalSeconds - (step * 2));
            TxtRamAxis1.Text = FormatTime(totalSeconds - (step * 3));
        }

        private void UpdateRamGraph(double currentRamUsage)
        {
            _ramHistory.Add(currentRamUsage);

            while (_ramHistory.Count > _maxRamDataPoints)
            {
                _ramHistory.RemoveAt(0);
            }

            if (TxtCurrentRam != null)
                TxtCurrentRam.Text = $"{Math.Round(currentRamUsage)}%";

            DrawRamGraph();
        }

        private void RamGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawRamGraph();
        }

        private void DrawRamGraph()
        {
            if (RamGraphCanvas == null || RamGraphLine == null || RamGraphFill == null || RamGraphDot == null) return;
            if (_ramHistory.Count < 2 || RamGraphCanvas.ActualWidth == 0 || RamGraphCanvas.ActualHeight == 0) return;

            double width = RamGraphCanvas.ActualWidth;
            double height = RamGraphCanvas.ActualHeight;
            double maxRam = 100.0;
            double stepX = width / Math.Max(1, _maxRamDataPoints - 1);
            double startX = width - ((_ramHistory.Count - 1) * stepX);
            double startY = height - (_ramHistory[0] / maxRam * height);

            Point startPoint = new Point(startX, startY);
            var points = new PointCollection();
            var fillPoints = new PointCollection { startPoint };
            Point lastPoint = startPoint;

            for (int i = 1; i < _ramHistory.Count; i++)
            {
                double x = startX + (i * stepX);
                double y = Math.Max(0, Math.Min(height, height - (_ramHistory[i] / maxRam * height)));
                lastPoint = new Point(x, y);

                points.Add(lastPoint);
                fillPoints.Add(lastPoint);
            }
            fillPoints.Add(new Point(width, height));

            var lineGeo = new PathGeometry();
            var lineFig = new PathFigure { StartPoint = startPoint };
            lineFig.Segments.Add(new PolyLineSegment { Points = points });
            lineGeo.Figures.Add(lineFig);
            RamGraphLine.Data = lineGeo;

            var fillGeo = new PathGeometry();
            var fillFig = new PathFigure { StartPoint = new Point(startX, height) };
            fillFig.Segments.Add(new PolyLineSegment { Points = fillPoints });
            fillGeo.Figures.Add(fillFig);
            RamGraphFill.Data = fillGeo;

            RamGraphDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(RamGraphDot, lastPoint.X);
            Canvas.SetTop(RamGraphDot, lastPoint.Y);
        }
        #endregion

        #region CPU Graph Logic
        private void UpdateAxisLabels(int totalSeconds)
        {
            if (TxtAxis1 == null || TxtAxis2 == null || TxtAxis3 == null || TxtAxis4 == null) return;

            double step = totalSeconds / 4.0;

            TxtAxis4.Text = FormatTime(totalSeconds);
            TxtAxis3.Text = FormatTime(totalSeconds - step);
            TxtAxis2.Text = FormatTime(totalSeconds - (step * 2));
            TxtAxis1.Text = FormatTime(totalSeconds - (step * 3));
        }

        private string FormatTime(double seconds)
        {
            if (seconds < 60)
                return $"{Math.Round(seconds)}s";

            return TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");
        }

        private void UpdateCpuGraph(double currentCpuUsage)
        {
            _cpuHistory.Add(currentCpuUsage);

            while (_cpuHistory.Count > _maxCpuDataPoints)
            {
                _cpuHistory.RemoveAt(0);
            }

            if (TxtCurrentCpu != null)
                TxtCurrentCpu.Text = $"{Math.Round(currentCpuUsage)}%";

            DrawCpuGraph();
        }

        private void CpuGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawCpuGraph();
        }

        private void DrawCpuGraph()
        {
            if (CpuGraphCanvas == null || CpuGraphLine == null || CpuGraphFill == null || CpuGraphDot == null) return;
            if (_cpuHistory.Count < 2 || CpuGraphCanvas.ActualWidth == 0 || CpuGraphCanvas.ActualHeight == 0) return;

            double width = CpuGraphCanvas.ActualWidth;
            double height = CpuGraphCanvas.ActualHeight;
            double maxCpu = 100.0;
            double stepX = width / Math.Max(1, _maxCpuDataPoints - 1);
            double startX = width - ((_cpuHistory.Count - 1) * stepX);
            double startY = height - (_cpuHistory[0] / maxCpu * height);

            Point startPoint = new Point(startX, startY);
            var points = new PointCollection();
            var fillPoints = new PointCollection { startPoint };
            Point lastPoint = startPoint;

            for (int i = 1; i < _cpuHistory.Count; i++)
            {
                double x = startX + (i * stepX);
                double y = Math.Max(0, Math.Min(height, height - (_cpuHistory[i] / maxCpu * height)));
                lastPoint = new Point(x, y);

                points.Add(lastPoint);
                fillPoints.Add(lastPoint);
            }
            fillPoints.Add(new Point(width, height));

            var lineGeo = new PathGeometry();
            var lineFig = new PathFigure { StartPoint = startPoint };
            lineFig.Segments.Add(new PolyLineSegment { Points = points });
            lineGeo.Figures.Add(lineFig);
            CpuGraphLine.Data = lineGeo;

            var fillGeo = new PathGeometry();
            var fillFig = new PathFigure { StartPoint = new Point(startX, height) };
            fillFig.Segments.Add(new PolyLineSegment { Points = fillPoints });
            fillGeo.Figures.Add(fillFig);
            CpuGraphFill.Data = fillGeo;

            CpuGraphDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(CpuGraphDot, lastPoint.X);
            Canvas.SetTop(CpuGraphDot, lastPoint.Y);
        }
        #endregion

        #region Global Graph Settings
        private void ComboGlobalTimeframe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboGlobalTimeframe == null || ComboGlobalTimeframe.SelectedIndex == -1) return;

            SettingsEngine.Dashboard_GraphTimeframe = ComboGlobalTimeframe.SelectedIndex;

            int totalSeconds = 60;

            switch (ComboGlobalTimeframe.SelectedIndex)
            {
                case 0: totalSeconds = 60; break;
                case 1: totalSeconds = 300; break;
                case 2: totalSeconds = 900; break;
            }

            int pollsPerSecond = 5;
            _maxCpuDataPoints = totalSeconds * pollsPerSecond;
            _maxRamDataPoints = totalSeconds * pollsPerSecond;
            _maxNetDataPoints = totalSeconds * pollsPerSecond;
            _maxGpuDataPoints = totalSeconds * pollsPerSecond;

            UpdateAxisLabels(totalSeconds);    // CPU
            UpdateRamAxisLabels(totalSeconds); // RAM
            UpdateNetAxisLabels(totalSeconds); // Network
            UpdateGpuAxisLabels(totalSeconds); // GPU

            DrawCpuGraph();
            DrawRamGraph();
            DrawNetGraph();
            DrawGpuGraph();
        }
        #endregion

        #region Direct Wallpaper Injection Logic
        private void StartWallpaperMonitor()
        {
            if (_wallpaperTimer == null)
            {
                _wallpaperTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _wallpaperTimer.Tick += CheckWallpaperTimer_Tick;
            }

            _wallpaperTimer.Start();

            CheckWallpaperTimer_Tick(null, null);
        }

        private async void CheckWallpaperTimer_Tick(object? sender, object? e)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");

            if (!File.Exists(path)) return;

            try
            {
                DateTime writeTime = File.GetLastWriteTime(path);

                if (writeTime > _currentWallpaperWriteTime)
                {
                    _currentWallpaperWriteTime = writeTime;
                    await LoadWallpaperIntoBrushAsync(path);
                }
            }
            catch { }
        }

        private async Task LoadWallpaperIntoBrushAsync(string path)
        {
            try
            {
                byte[] imageBytes;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    await fs.CopyToAsync(ms);
                    imageBytes = ms.ToArray();
                }

                using var memStream = new MemoryStream(imageBytes);
                var randomAccessStream = memStream.AsRandomAccessStream();

                var bitmap = new BitmapImage();
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                await bitmap.SetSourceAsync(randomAccessStream);

                if (WallpaperBrush != null)
                {
                    WallpaperBrush.ImageSource = bitmap;
                }

                var visual = ElementCompositionPreview.GetElementVisual(LogoPath);
                var fadeAnimation = visual.Compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.InsertKeyFrame(0.0f, 0.5f);
                fadeAnimation.InsertKeyFrame(1.0f, 1.0f);
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);
                visual.StartAnimation("Opacity", fadeAnimation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Direct Wallpaper Update Failed] {ex.Message}");
            }
        }
        #endregion

        #region Weather Handlers
        private void LocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is HomePageViewModel vm)
            {
                _ = vm.FetchWeatherAsync(vm.WeatherLocation);
            }
        }

        private void Calendar_MouseEnter(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                if (element.TranslationTransition == null)
                {
                    element.TranslationTransition = new Vector3Transition()
                    {
                        Duration = TimeSpan.FromMilliseconds(200)
                    };
                }

                element.Translation = new System.Numerics.Vector3(0, -5, 0);
            }
        }

        private void Calendar_MouseLeave(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                if (element.TranslationTransition == null)
                {
                    element.TranslationTransition = new Vector3Transition()
                    {
                        Duration = TimeSpan.FromMilliseconds(200)
                    };
                }

                element.Translation = new System.Numerics.Vector3(0, 0, 0);
            }
        }

        private void DiskCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;

                visual.CenterPoint = new System.Numerics.Vector3((float)(element.ActualSize.X / 2), (float)(element.ActualSize.Y / 2), 0f);

                var springAnimation = compositor.CreateSpringVector3Animation();
                springAnimation.Target = "Scale";
                springAnimation.FinalValue = new System.Numerics.Vector3(1.05f, 1.05f, 1f);
                springAnimation.DampingRatio = 0.6f;
                springAnimation.Period = TimeSpan.FromMilliseconds(50);

                visual.StartAnimation("Scale", springAnimation);
            }
        }

        private void DiskCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;

                var springAnimation = compositor.CreateSpringVector3Animation();
                springAnimation.Target = "Scale";
                springAnimation.FinalValue = new System.Numerics.Vector3(1f, 1f, 1f);
                springAnimation.DampingRatio = 0.9f;
                springAnimation.Period = TimeSpan.FromMilliseconds(50);

                visual.StartAnimation("Scale", springAnimation);
            }
        }

        private void LoadWeather()
        {
            if (this.DataContext is HomePageViewModel vm)
            {
                if (GlobalAppData.PreloadedWeather != null)
                {
                    vm.UpdateWeatherData(GlobalAppData.PreloadedWeather);
                }
                else
                {
                    _ = vm.FetchWeatherAsync(vm.WeatherLocation);
                }
            }
        }

        #endregion

        #region Registry & Location Logic
        private void SaveLocationToRegistry(string location)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                key.SetValue(RegistryValueName, location);
            }
            catch (Exception ex) { Debug.WriteLine($"[Registry] Save Error: {ex.Message}"); }
        }

        private void UpdateLocation_Click(object sender, RoutedEventArgs e)
        {
            string? newLoc = !string.IsNullOrWhiteSpace(CustomLocationBox.Text)
                    ? CustomLocationBox.Text.Trim()
                    : CityPicker.SelectedItem as string;

            if (!string.IsNullOrEmpty(newLoc))
            {
                ApplyNewLocation(newLoc);
            }
        }

        private void CityPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CityPicker.SelectedItem != null)
            {
                CustomLocationBox.Text = string.Empty;
            }
        }

        private void CustomLocationBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ApplyNewLocation(CustomLocationBox.Text.Trim());
            }
        }

        private void ApplyNewLocation(string location)
        {
            if (ViewModel != null && !string.IsNullOrWhiteSpace(location))
            {
                SaveLocationToRegistry(location);
                ViewModel.WeatherLocation = location;
            }
            LocationFlyout.Hide();
            CustomLocationBox.Text = string.Empty;
        }

        private async void RefreshWeatherButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && ViewModel != null)
            {
                btn.IsEnabled = false;

                var visual = ElementCompositionPreview.GetElementVisual(RefreshIcon);
                var compositor = visual.Compositor;

                visual.RotationAngleInDegrees = 0f;
                visual.CenterPoint = new System.Numerics.Vector3(
                    (float)(RefreshIcon.ActualWidth / 2),
                    (float)(RefreshIcon.ActualHeight / 2),
                    0);

                var rotateAnimation = compositor.CreateScalarKeyFrameAnimation();
                rotateAnimation.InsertKeyFrame(1.0f, 360f);
                rotateAnimation.Duration = TimeSpan.FromMilliseconds(750);
                rotateAnimation.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;

                visual.StartAnimation("RotationAngleInDegrees", rotateAnimation);

                try
                {
                    await ViewModel.FetchWeatherAsync(ViewModel.WeatherLocation, forceRefresh: true);
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Refresh Error] {ex.Message}");
                }
                finally
                {
                    visual.StopAnimation("RotationAngleInDegrees");
                    visual.RotationAngleInDegrees = 0f;
                    btn.IsEnabled = true;
                }
            }
        }
        #endregion

        #region Dashboard Customization (Internal Smooth Visual Drag, Drop, Visibility)

        private async void BtnCustomizeLayout_Click(object sender, RoutedEventArgs e)
        {
            CustomizeLayoutDialog.XamlRoot = this.XamlRoot;
            await CustomizeLayoutDialog.ShowAsync();
        }

        private void BtnCloseCustomizeLayout_Click(object sender, RoutedEventArgs e)
        {
            CustomizeLayoutDialog.Hide();
        }

        private void LoadDashboardLayout()
        {
            ToggleWeather.IsOn = SettingsEngine.Dashboard_CardWeather;
            ToggleNetwork.IsOn = SettingsEngine.Dashboard_CardNetwork;
            ToggleRam.IsOn = SettingsEngine.Dashboard_CardRam;
            ToggleCpu.IsOn = SettingsEngine.Dashboard_CardCpu;
            ToggleGpu.IsOn = SettingsEngine.Dashboard_CardGpu;
            ToggleDisk.IsOn = SettingsEngine.Dashboard_CardDisk;
            ToggleGamingMode.IsOn = SettingsEngine.Dashboard_CardGamingMode;
            ToggleDns.IsOn = SettingsEngine.Dashboard_CardDns;
            ToggleHealth.IsOn = SettingsEngine.Dashboard_CardHealth;
            ToggleSecurity.IsOn = SettingsEngine.Dashboard_CardSecurity;
            ToggleCpuGraph.IsOn = SettingsEngine.Dashboard_CardCpuGraph;
            ToggleRamGraph.IsOn = SettingsEngine.Dashboard_CardRamGraph;
            ToggleNetworkGraph.IsOn = SettingsEngine.Dashboard_CardNetworkGraph;
            ToggleGpuGraph.IsOn = SettingsEngine.Dashboard_CardGpuGraph;

            SetCardVisibility("CardWeather", ToggleWeather.IsOn);
            SetCardVisibility("CardNetwork", ToggleNetwork.IsOn);
            SetCardVisibility("CardRam", ToggleRam.IsOn);
            SetCardVisibility("CardCpu", ToggleCpu.IsOn);
            SetCardVisibility("CardGpu", ToggleGpu.IsOn);
            SetCardVisibility("CardDisk", ToggleDisk.IsOn);
            SetCardVisibility("CardDns", ToggleDns.IsOn);
            SetCardVisibility("CardMaintenance", ToggleHealth.IsOn);
            SetCardVisibility("CardSecurity", ToggleSecurity.IsOn);
            SetCardVisibility("CardCpuGraph", ToggleCpuGraph.IsOn);
            SetCardVisibility("CardRamGraph", ToggleRamGraph.IsOn);
            SetCardVisibility("CardNetworkGraph", ToggleNetworkGraph.IsOn);
            SetCardVisibility("CardGpuGraph", ToggleGpuGraph.IsOn);
            SetCardVisibility("CardGamingMode", ToggleGamingMode.IsOn);

            bool isGamingActive = GamingModeHelper.IsGamingModeActive;
            if (isGamingActive)
            {
                GamingModeButtonLabel.Text = ResourceString.GetString("gm_label_gaming");
                GamingModeBtnText.Text = ResourceString.GetString("gm_btn_disable");

                DashGamingStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/health_good.png"));
                DashGamingStatusImage.Opacity = 1.0;
                GamingSpinner.Visibility = Visibility.Visible;
                GamingSpinner.IsActive = true;
            }
            else
            {
                GamingModeButtonLabel.Text = ResourceString.GetString("gm_label_normal");
                GamingModeBtnText.Text = ResourceString.GetString("gm_btn_enable");

                DashGamingStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/gamingmode_off.png"));
                DashGamingStatusImage.Opacity = 1.0;
                GamingSpinner.Visibility = Visibility.Collapsed;
                GamingSpinner.IsActive = false;
            }

            string savedOrder = SettingsEngine.DashboardCardOrder;
            if (!string.IsNullOrWhiteSpace(savedOrder))
            {
                var orderNames = savedOrder.Split(',');

                for (int i = 0; i < orderNames.Length; i++)
                {
                    var name = orderNames[i];

                    var wrapper = DashboardGridView.Items.OfType<GridViewItem>()
                        .FirstOrDefault(gvi => gvi.Content is FrameworkElement fe && fe.Name == name);

                    if (wrapper != null)
                    {
                        DashboardGridView.Items.Remove(wrapper);
                        DashboardGridView.Items.Insert(i, wrapper);
                    }
                }
            }

            if (ComboGlobalTimeframe != null)
            {
                ComboGlobalTimeframe.SelectedIndex = SettingsEngine.Dashboard_GraphTimeframe;
            }
        }

        private void SetCardVisibility(string cardName, bool isVisible)
        {
            var container = DashboardGridView.Items.OfType<GridViewItem>()
                .FirstOrDefault(i => i.Content is FrameworkElement fe && fe.Name == cardName);

            if (container != null)
            {
                container.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

                if (cardName == "CardWeather" && DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
                {
                    panel.InvalidateMeasure();
                }
            }
        }

        private void SaveDashboardLayout()
        {
            var order = DashboardGridView.Items.OfType<GridViewItem>()
                .Select(i => i.Content)
                .OfType<FrameworkElement>()
                .Select(fe => fe.Name)
                .ToList();

            SettingsEngine.DashboardCardOrder = string.Join(",", order);

            SettingsEngine.Dashboard_CardWeather = ToggleWeather.IsOn;
            SettingsEngine.Dashboard_CardNetwork = ToggleNetwork.IsOn;
            SettingsEngine.Dashboard_CardRam = ToggleRam.IsOn;
            SettingsEngine.Dashboard_CardCpu = ToggleCpu.IsOn;
            SettingsEngine.Dashboard_CardGpu = ToggleGpu.IsOn;
            SettingsEngine.Dashboard_CardDisk = ToggleDisk.IsOn;
            SettingsEngine.Dashboard_CardGamingMode = ToggleGamingMode.IsOn;
            SettingsEngine.Dashboard_CardDns = ToggleDns.IsOn;
            SettingsEngine.Dashboard_CardHealth = ToggleHealth.IsOn;
            SettingsEngine.Dashboard_CardSecurity = ToggleSecurity.IsOn;
            SettingsEngine.Dashboard_CardCpuGraph = ToggleCpuGraph.IsOn;
            SettingsEngine.Dashboard_CardRamGraph = ToggleRamGraph.IsOn;
            SettingsEngine.Dashboard_CardNetworkGraph = ToggleNetworkGraph.IsOn;
            SettingsEngine.Dashboard_CardGpuGraph = ToggleGpuGraph.IsOn;
        }

        private void ToggleCard_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ts.Tag is string cardName)
            {
                SetCardVisibility(cardName, ts.IsOn);
                SaveDashboardLayout();
            }
        }

        private void SetCustomCursor(UIElement element, InputSystemCursorShape shape)
        {
            if (element == null) return;

            var cursor = InputSystemCursor.Create(shape);
            var cursorProperty = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);

            if (cursorProperty != null)
            {
                cursorProperty.SetValue(element, cursor);
            }
        }

        private void DashboardDragCursor()
        {
            SetCustomCursor(CardWeather, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardNetwork, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardRam, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpu, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpuGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGpu, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardDisk, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGamingMode, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardDns, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardMaintenance, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardSecurity, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpuGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardRamGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardNetworkGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGpuGraph, InputSystemCursorShape.SizeAll);

            SetCustomCursor(RefreshWeatherButton, InputSystemCursorShape.Arrow);
            SetCustomCursor(LocationButton, InputSystemCursorShape.Arrow);
            SetCustomCursor(Calendar, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnVision, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenDnsPage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnStartService, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnDebug, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenMaintenancePage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshHealth, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenSecurityPage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshSecurity, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnDashViewIssues, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnGamingMode, InputSystemCursorShape.Arrow);

            SetCustomCursor(IpAddress, InputSystemCursorShape.Hand);
            SetCustomCursor(LocalIpAddress, InputSystemCursorShape.Hand);
        }

        private void ResetDashboard_Click(object sender, RoutedEventArgs e)
        {
            SettingsEngine.DashboardCardOrder = "CardWeather,CardDns,CardSecurity,CardGamingMode,CardMaintenance,CardCpuGraph,CardGpuGraph,CardRamGraph,CardNetworkGraph,CardCpu,CardGpu,CardRam,CardNetwork,CardDisk";
            SettingsEngine.Dashboard_CardWeather = true;
            SettingsEngine.Dashboard_CardNetwork = true;
            SettingsEngine.Dashboard_CardRam = true;
            SettingsEngine.Dashboard_CardCpu = true;
            SettingsEngine.Dashboard_CardGpu = true;
            SettingsEngine.Dashboard_CardDisk = true;
            SettingsEngine.Dashboard_CardGamingMode = true;
            SettingsEngine.Dashboard_CardDns = true;
            SettingsEngine.Dashboard_CardHealth = true;
            SettingsEngine.Dashboard_CardSecurity = true;
            SettingsEngine.Dashboard_CardCpuGraph = true;
            SettingsEngine.Dashboard_CardRamGraph = true;
            SettingsEngine.Dashboard_CardNetworkGraph = true;
            SettingsEngine.Dashboard_GraphTimeframe = 0;
            SettingsEngine.Dashboard_CardGpuGraph = true;

            ToggleWeather.IsOn = true;
            ToggleNetwork.IsOn = true;
            ToggleRam.IsOn = true;
            ToggleCpu.IsOn = true;
            ToggleGpu.IsOn = true;
            ToggleDisk.IsOn = true;
            ToggleGamingMode.IsOn = true;
            ToggleDns.IsOn = true;
            ToggleHealth.IsOn = true;
            ToggleSecurity.IsOn = true;
            ToggleCpuGraph.IsOn = true;
            ToggleRamGraph.IsOn = true;
            ToggleNetworkGraph.IsOn = true;
            ToggleGpuGraph.IsOn = true;

            if (ComboGlobalTimeframe != null)
            {
                ComboGlobalTimeframe.SelectedIndex = 0;
            }

            LoadDashboardLayout();
        }

        private void DashCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card && !_isTrackingDrag)
            {
                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
                FactoryAnimation.AnimateCardScale(card, 1.01);
            }
        }

        private void DashCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card && !_isTrackingDrag)
            {
                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
                FactoryAnimation.AnimateCardScale(card, 1.0);
            }
        }

        private void DashCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorTertiaryBrush", out object tertiaryBrush))
                {
                    card.Background = (Brush)tertiaryBrush;
                }

                var container = DashboardGridView.Items.OfType<GridViewItem>()
                    .FirstOrDefault(i => (i.Content as Border) == card);

                if (container != null)
                {
                    _activeDraggedCard = card;
                    _activeDraggedItem = container;
                    _hoveredTargetItem = null;
                    _isTrackingDrag = false;
                    _dragStartPoint = e.GetCurrentPoint(DashboardGridView).Position;

                    _logicalBounds.Clear();
                    foreach (var item in DashboardGridView.Items.OfType<GridViewItem>())
                    {
                        var transform = item.TransformToVisual(DashboardGridView);
                        var bounds = transform.TransformBounds(new Rect(0, 0, item.ActualWidth, item.ActualHeight));
                        _logicalBounds[item] = bounds;

                        if (item.Content is Border b)
                        {
                            b.TranslationTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(250) };
                        }
                    }

                    if (_logicalBounds.TryGetValue(container, out var draggedBounds))
                    {
                        _draggedItemBasePos = new Point(draggedBounds.X, draggedBounds.Y);
                    }

                    Canvas.SetZIndex(container, 1000);
                    card.CapturePointer(e.Pointer);
                }
            }
        }

        private void DashCard_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_activeDraggedCard == null || _activeDraggedItem == null) return;

            var currentPoint = e.GetCurrentPoint(DashboardGridView).Position;
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            if (!_isTrackingDrag && (Math.Abs(deltaX) > 4 || Math.Abs(deltaY) > 4))
            {
                _isTrackingDrag = true;

                _activeDraggedCard.TranslationTransition = null;

                if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
                {
                    panel.IsDragInProgress = true;
                }
            }

            if (_isTrackingDrag)
            {
                _activeDraggedCard.Translation = new System.Numerics.Vector3((float)deltaX, (float)deltaY, 10f);
                _activeDraggedCard.Opacity = 0.8f;

                GridViewItem? newHoveredItem = null;

                foreach (var item in DashboardGridView.Items.OfType<GridViewItem>())
                {
                    if (item == _activeDraggedItem) continue;

                    if (_logicalBounds.TryGetValue(item, out var bounds))
                    {
                        if (bounds.Contains(currentPoint))
                        {
                            newHoveredItem = item;
                            break;
                        }
                    }
                }

                if (newHoveredItem != _hoveredTargetItem)
                {
                    if (_hoveredTargetItem != null && _hoveredTargetItem.Content is Border oldBorder)
                    {
                        oldBorder.Translation = System.Numerics.Vector3.Zero;
                    }

                    _hoveredTargetItem = newHoveredItem;

                    if (_hoveredTargetItem != null && _hoveredTargetItem.Content is Border targetBorder)
                    {
                        var targetRect = _logicalBounds[_hoveredTargetItem];

                        float offsetX = (float)(_draggedItemBasePos.X - targetRect.X);
                        float offsetY = (float)(_draggedItemBasePos.Y - targetRect.Y);

                        targetBorder.Translation = new System.Numerics.Vector3(offsetX, offsetY, 0);
                    }
                }
            }
        }

        private void DashCard_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
                card.ReleasePointerCapture(e.Pointer);
            }

            if (_isTrackingDrag && _activeDraggedItem != null)
            {
                foreach (var item in DashboardGridView.Items.OfType<GridViewItem>())
                {
                    if (item.Content is Border b)
                    {
                        b.TranslationTransition = null;
                        b.Translation = System.Numerics.Vector3.Zero;
                        b.Opacity = 1.0f;
                    }
                    Canvas.SetZIndex(item, 0);
                }

                if (_hoveredTargetItem != null && _hoveredTargetItem != _activeDraggedItem)
                {
                    var originalTransitions = DashboardGridView.ItemContainerTransitions;
                    DashboardGridView.ItemContainerTransitions = new TransitionCollection();

                    int oldIndex = DashboardGridView.Items.IndexOf(_activeDraggedItem);
                    int newIndex = DashboardGridView.Items.IndexOf(_hoveredTargetItem);

                    if (oldIndex != -1 && newIndex != -1)
                    {
                        var item1 = DashboardGridView.Items[oldIndex];
                        var item2 = DashboardGridView.Items[newIndex];

                        if (oldIndex < newIndex)
                        {
                            DashboardGridView.Items.RemoveAt(newIndex);
                            DashboardGridView.Items.RemoveAt(oldIndex);
                            DashboardGridView.Items.Insert(oldIndex, item2);
                            DashboardGridView.Items.Insert(newIndex, item1);
                        }
                        else
                        {
                            DashboardGridView.Items.RemoveAt(oldIndex);
                            DashboardGridView.Items.RemoveAt(newIndex);
                            DashboardGridView.Items.Insert(newIndex, item1);
                            DashboardGridView.Items.Insert(oldIndex, item2);
                        }

                        SaveDashboardLayout();
                    }

                    DashboardGridView.UpdateLayout();
                    DashboardGridView.ItemContainerTransitions = originalTransitions;
                }
            }
            else if (_activeDraggedCard != null)
            {
                _activeDraggedCard.TranslationTransition = null;
                _activeDraggedCard.Translation = System.Numerics.Vector3.Zero;
                _activeDraggedCard.Opacity = 1.0f;
                if (_activeDraggedItem != null) Canvas.SetZIndex(_activeDraggedItem, 0);
            }

            _activeDraggedCard = null;
            _activeDraggedItem = null;
            _hoveredTargetItem = null;
            _isTrackingDrag = false;

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.IsDragInProgress = false;
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
        }

        private void DashCard_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            DashCard_PointerReleased(sender, e);
        }
        #endregion

        #region Gaming Mode
        private void BtnToggleGamingConsole_Click(object sender, RoutedEventArgs e)
        {
            bool isConsoleOpen = BtnToggleGamingConsole.IsChecked ?? false;

            if (isConsoleOpen)
            {
                GamingVisualGrid.Visibility = Visibility.Collapsed;
                GamingConsoleBorder.Visibility = Visibility.Visible;
                IconToggleConsole.Glyph = "\uE70E";
            }
            else
            {
                GamingConsoleBorder.Visibility = Visibility.Collapsed;
                GamingVisualGrid.Visibility = Visibility.Visible;
                IconToggleConsole.Glyph = "\uE70D";
            }
        }

        private async void BtnGamingMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            bool targetState = !GamingModeHelper.IsGamingModeActive;

            GamingProgressText.Text = targetState
                ? $"> {ResourceString.GetString("gm_ui_initializing")}"
                : $"> {ResourceString.GetString("gm_ui_deactivating")}";

            GamingStatusLabel.Text = targetState
                ? ResourceString.GetString("gm_status_optimizing")
                : ResourceString.GetString("gm_status_restoring");

            btn.IsEnabled = false;

            GamingSpinner.Visibility = Visibility.Visible;
            GamingSpinner.IsActive = true;
            DashGamingStatusImage.Opacity = 1.0;

            if (GamingVisualGrid.Visibility == Visibility.Visible)
            {
                BtnToggleGamingConsole.IsChecked = true;
                BtnToggleGamingConsole_Click(BtnToggleGamingConsole, new RoutedEventArgs());
            }

            var progressReporter = new Progress<string>(message =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    GamingProgressText.Text += $"\n> {message}";
                    GamingProgressScroll.ChangeView(0, GamingProgressScroll.ScrollableHeight, 1);
                });
            });

            try
            {
                bool success = await GamingModeHelper.ToggleGamingModeAsync(targetState, progressReporter);

                if (success)
                {
                    GamingStatusLabel.Text = targetState
                        ? ResourceString.GetString("gm_mode_high_perf")
                        : ResourceString.GetString("gm_mode_standard");

                    GamingProgressText.Text += targetState
                        ? $"\n\n[{ResourceString.GetString("gm_ui_engine_ready")}]"
                        : $"\n\n[{ResourceString.GetString("gm_ui_system_restored")}]";

                    if (targetState)
                    {
                        DashGamingStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/health_good.png"));
                        DashGamingStatusImage.Opacity = 1.0;

                        GamingSpinner.Visibility = Visibility.Visible;
                        GamingSpinner.IsActive = true;

                        GamingModeButtonLabel.Text = ResourceString.GetString("gm_label_gaming");
                        GamingModeBtnText.Text = ResourceString.GetString("gm_btn_disable");
                    }
                    else
                    {
                        DashGamingStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/gamingmode_off.png"));
                        DashGamingStatusImage.Opacity = 1.0;

                        GamingSpinner.IsActive = false;
                        GamingSpinner.Visibility = Visibility.Collapsed;

                        GamingModeButtonLabel.Text = ResourceString.GetString("gm_label_normal");
                        GamingModeBtnText.Text = ResourceString.GetString("gm_btn_enable");
                    }
                }
            }
            catch (Exception ex)
            {
                GamingProgressText.Text += $"\n[{ResourceString.GetString("gm_ui_critical_error")}] {ex.Message}";
                GamingSpinner.IsActive = false;
                GamingSpinner.Visibility = Visibility.Collapsed;

                DashGamingStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/gamingmode_off.png"));
                DashGamingStatusImage.Opacity = 0.8;
            }
            finally
            {
                btn.IsEnabled = true;

                await Task.Delay(1000);
                if (BtnToggleGamingConsole.IsChecked == true)
                {
                    BtnToggleGamingConsole.IsChecked = false;
                    BtnToggleGamingConsole_Click(BtnToggleGamingConsole, new RoutedEventArgs());
                }
            }
        }
        #endregion

        #region DNS Card
        private void BtnOpenDnsPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Utilities");
            }
            else
            {
                Debug.WriteLine("❌ MainWindow.Instance is null!");
            }
        }

        private void UpdateDnsCardUI()
        {
            if (!DNSCryptHelper.IsInstalled())
            {
                BtnStartService.IsEnabled = false;
                BtnDebug.IsEnabled = false;
                statusLabel.Text = "DNSCrypt is not installed.";

                IconServiceStopped.Visibility = Visibility.Visible;
                ImgServiceRunning.Visibility = Visibility.Collapsed;
                TxtServicesRunning.Visibility = Visibility.Collapsed;
                ProgressRingRunServices.Visibility = Visibility.Collapsed;

                return;
            }

            BtnStartService.IsEnabled = true;
            BtnDebug.IsEnabled = true;

            bool isServiceRunning = DNSCryptHelper.IsRunning();

            if (isServiceRunning)
            {
                IconServiceStopped.Visibility = Visibility.Collapsed;
                ImgServiceRunning.Visibility = Visibility.Visible;
                TxtServicesRunning.Visibility = Visibility.Visible;
                ProgressRingRunServices.Visibility = Visibility.Visible;

                statusLabel.Text = "DNSCrypt Service is running.";
                statusLabel.Opacity = 1.0;

                BtnStartService.Content = "Stop service";
                BtnStartService.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
            }
            else
            {
                IconServiceStopped.Visibility = Visibility.Visible;
                ImgServiceRunning.Visibility = Visibility.Collapsed;
                TxtServicesRunning.Visibility = Visibility.Collapsed;
                ProgressRingRunServices.Visibility = Visibility.Collapsed;

                statusLabel.Text = "Nothing is running in the background";
                statusLabel.Opacity = 0.7;

                BtnStartService.Content = "Start service";
                BtnStartService.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
        }

        private async void BtnStartService_Click(object sender, RoutedEventArgs e)
        {
            BtnStartService.IsEnabled = false;

            try
            {
                if (DNSCryptHelper.IsRunning())
                {
                    await DNSCryptHelper.StopService(progressBar, statusLabel);
                }
                else
                {
                    await DNSCryptHelper.StartService(progressBar, statusLabel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dashboard DNS] Error: {ex.Message}");
                statusLabel.Text = "Service action failed.";
            }
            finally
            {
                UpdateDnsCardUI();
                BtnStartService.IsEnabled = true;
            }
        }

        private async void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            BtnDebug.IsEnabled = false;

            try
            {
                bool isConnected = await Task.Run(() => NetworkHelper.IsConnectedAsync());

                if (!isConnected)
                {
                    statusLabel.Text = "Connection failed.";
                    return;
                }

                await DNSCryptHelper.DebugProcess(progressBar, statusLabel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dashboard DNS Debug] Error: {ex.Message}");
            }
            finally
            {
                BtnDebug.IsEnabled = true;
            }
        }
        #endregion

        #region Health Card
        private void BtnOpenMaintenancePage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Diagnostics", "Maintenance");
            }
            else
            {
                Debug.WriteLine("❌ MainWindow.Instance is null!");
            }
        }

        private async void BtnRefreshHealth_Click(object sender, RoutedEventArgs e)
        {
            DashMaintenanceLoadingRing.Visibility = Visibility.Visible;
            DashMaintenanceStatusImage.Visibility = Visibility.Collapsed;

            await CalculateSystemHealthAsync();
        }

        private async Task CalculateSystemHealthAsync()
        {
            try
            {
                DashMaintenanceLoadingRing.Visibility = Visibility.Visible;
                DashMaintenanceStatusImage.Visibility = Visibility.Collapsed;
                TxtLastRefreshed.Visibility = Visibility.Collapsed;
                BtnRefreshHealth.IsEnabled = false;

                TxtHealthStatus.Text = ResourceString.GetString("text_scanning_system") ?? "Scanning System...";

                double ramPercentage = SystemDiagnostics.GetMemoryUsagePercentage();
                double vRamPercentage = SystemDiagnostics.GetVirtualMemoryUsagePercentage();

                await Task.Delay(1500);
                double junkGigabytes = await SystemDiagnostics.GetQuickJunkSizeGigabytesAsync();

                double totalRamGb = SystemDiagnostics.GetTotalPhysicalMemoryGigabytes();
                double totalVRamGb = SystemDiagnostics.GetTotalVirtualMemoryGigabytes();

                var healthResult = SystemHealthHelper.EvaluateHealth(
                    ramPercentage, totalRamGb,
                    vRamPercentage, totalVRamGb,
                    junkGigabytes);

                DashMaintenanceStatusImage.Source = new BitmapImage(new Uri(healthResult.ImagePath));
                TxtHealthStatus.Text = healthResult.StatusText;

                string lastCheckedStr = ResourceString.GetString("text_last_checked") ?? "Last checked";
                TxtLastRefreshed.Text = $"{lastCheckedStr}: {DateTime.Now:t}";

                DashMaintenanceStatusImage.Visibility = Visibility.Visible;
                TxtLastRefreshed.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Health Check Error] {ex.Message}");
                TxtHealthStatus.Text = "Scan failed.";
            }
            finally
            {
                DashMaintenanceLoadingRing.Visibility = Visibility.Collapsed;
                BtnRefreshHealth.IsEnabled = true;
            }
        }
        #endregion

        #region Security Card
        private void BtnOpenSecurityPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Diagnostics", "Security");
            }
            else
            {
                Debug.WriteLine("❌ MainWindow.Instance is null!");
            }
        }

        private async void BtnRefreshSecurity_Click(object sender, RoutedEventArgs e)
        {
            await CalculateSecurityHealthAsync();
        }

        private async Task CalculateSecurityHealthAsync()
        {
            try
            {
                DashSecurityLoadingRing.Visibility = Visibility.Visible;
                DashSecurityStatusImage.Visibility = Visibility.Collapsed;
                TxtSecurityLastRefreshed.Visibility = Visibility.Collapsed;
                BtnRefreshSecurity.IsEnabled = false;

                BtnDashViewIssues.Visibility = Visibility.Collapsed;

                TxtSecurityStatus.Text = ResourceString.GetString("text_scanning_system") ?? "Scanning...";

                int issuesCount = 0;
                bool isCoreProtected = false;
                List<string> securityIssues = new List<string>();

                await Task.Run(async () =>
                {
                    var antivirusInfo = await SecurityDiagnostics.GetAntivirusInfoAsync();
                    bool isAvEnabled = antivirusInfo.IsEnabled;
                    bool isFwEnabled = await SecurityDiagnostics.IsFirewallEnabledAsync();
                    bool isRtEnabled = await SecurityDiagnostics.IsRealTimeProtectionEnabledAsync();
                    bool isUacEnabled = await SecurityDiagnostics.IsUACEnabledAsync();
                    bool isWuEnabled = await SecurityDiagnostics.IsWindowsUpdateEnabledAsync();
                    bool isTpEnabled = await SecurityDiagnostics.IsTamperProtectionEnabledAsync();
                    bool isLsaEnabled = await SecurityDiagnostics.IsLsaProtectionEnabledAsync();
                    bool isRdpEnabled = await SecurityDiagnostics.IsRdpEnabledAsync();
                    bool isRaEnabled = await SecurityDiagnostics.IsRemoteAssistanceEnabledAsync();
                    bool isDevModeEnabled = await SecurityDiagnostics.IsDeveloperModeEnabledAsync();

                    int sacState = await SecurityDiagnostics.GetSmartAppControlStateAsync();
                    bool isSmartAppControlSecure = sacState != 0;

                    string psPolicy = await SecurityDiagnostics.GetPowerShellExecutionPolicyAsync();
                    bool isPsPolicySecure = psPolicy != "Unrestricted" && psPolicy != "Bypass" && psPolicy != "Error";

                    if (!isAvEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_VirusThreatProtection") ?? "Virus & Threat Protection"); }
                    if (!isFwEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_FirewallNetworkProtection") ?? "Firewall is disabled"); }
                    if (!isRtEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_RealTimeProtection") ?? "Real-Time Protection"); }
                    if (!isUacEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_UAC") ?? "UAC Level"); }
                    if (!isWuEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_WindowsUpdate") ?? "Windows Update"); }
                    if (!isTpEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_TamperProtection") ?? "Tamper Protection"); }
                    if (!isSmartAppControlSecure) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_SmartAppControl") ?? "Smart App Control"); }
                    if (!isPsPolicySecure) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_PSExecutionPolicy") ?? "PowerShell Policy"); }
                    if (!isLsaEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_LSAProtection") ?? "LSA Protection"); }
                    if (isRdpEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_RemoteDesktop") ?? "Remote Desktop"); }
                    if (isRaEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_RemoteAssistance") ?? "Remote Assistance"); }
                    if (isDevModeEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_DeveloperMode") ?? "Developer Mode"); }

                    isCoreProtected = isAvEnabled && isFwEnabled && isRtEnabled;
                });

                if (!isCoreProtected)
                {
                    DashSecurityStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/unsecure.png"));
                    DashSecurityStatusImage.Opacity = 1.0;
                    TxtSecurityStatus.Text = $"{issuesCount} {ResourceString.GetString("text_security_critical") ?? "Critical Issues"}";
                }
                else if (issuesCount > 0)
                {
                    DashSecurityStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/secure.png"));
                    DashSecurityStatusImage.Opacity = 0.5;
                    TxtSecurityStatus.Text = $"{issuesCount} {ResourceString.GetString("text_security_warning") ?? "Warnings Found"}";
                }
                else
                {
                    DashSecurityStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/secure.png"));
                    DashSecurityStatusImage.Opacity = 1.0;
                    TxtSecurityStatus.Text = ResourceString.GetString("text_security_good") ?? "System is Secure";
                }

                if (issuesCount > 0)
                {
                    BtnDashViewIssues.Visibility = Visibility.Visible;

                    var flyout = new MenuFlyout();
                    foreach (var issue in securityIssues)
                    {
                        flyout.Items.Add(new MenuFlyoutItem
                        {
                            Text = issue,
                            Icon = new FontIcon { Glyph = "\uE7BA", FontSize = 14 },
                            IsEnabled = false
                        });
                    }

                    FlyoutBase.SetAttachedFlyout(BtnDashViewIssues, flyout);
                }

                TxtSecurityLastRefreshed.Text = $"{ResourceString.GetString("text_last_checked") ?? "Last checked"}: {DateTime.Now:t}";
                DashSecurityStatusImage.Visibility = Visibility.Visible;
                TxtSecurityLastRefreshed.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Security Check Error] {ex.Message}");
                TxtSecurityStatus.Text = "Scan failed.";
            }
            finally
            {
                DashSecurityLoadingRing.Visibility = Visibility.Collapsed;
                BtnRefreshSecurity.IsEnabled = true;
            }
        }

        private void BtnDashViewIssues_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }
        #endregion

        #region Admin & UI Helper Methods
        private void ApplyElevationUI()
        {
            bool isElevated = SystemDiagnostics.IsElevated;
            if (!isElevated)
            {
                AdminWarningBanner.Visibility = Visibility.Visible;
                WallInfoBanner.Visibility = Visibility.Collapsed;
                StatusLabel.Text = ResourceString.GetString("status_limited_optimization");
                StatusLabel.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                AdminWarningBanner.Visibility = Visibility.Collapsed;
                WallInfoBanner.Visibility = Visibility.Visible;
                StatusLabel.Text = ResourceString.GetString("status_elevated_active");
                if (Application.Current.Resources.TryGetValue("Brush_Success", out object brush))
                    StatusLabel.Foreground = (Brush)brush;
                else
                    StatusLabel.Foreground = new SolidColorBrush(Colors.Green);
            }
        }

        private void RestartAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;
            try
            {
                string? exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true, Verb = "runas" });
                    SettingsEngine.SelfReboot();
                }
            }
            catch { }
        }

        private void HandleCopyingData_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            string textToCopy = (sender is Run runText) ? runText.Text : (sender is TextBlock tb) ? tb.Text : string.Empty;
            if (!string.IsNullOrEmpty(textToCopy))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(textToCopy);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
        }
        #endregion

        #region Privacy & Masking Logic
        private void BtnVision_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space || e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
            }
        }

        private void BtnVision_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn)
            {
                UpdateIpPrivacy(btn.IsChecked ?? false);
            }
        }

        private void UpdateIpPrivacy(bool showText)
        {
            float maskOpacity = showText ? 0.0f : 1.0f;
            float textOpacity = showText ? 1.0f : 0.0f;

            AnimateOpacity(IpBlurMask, maskOpacity);
            AnimateOpacity(LocalIpBlurMask, maskOpacity);

            AnimateOpacity(IpAddress, textOpacity);
            AnimateOpacity(LocalIpAddress, textOpacity);
        }

        private void AnimateOpacity(UIElement? element, float targetOpacity)
        {
            if (element == null) return;

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(1.0f, targetOpacity);
            animation.Duration = TimeSpan.FromMilliseconds(250);

            visual.StartAnimation("Opacity", animation);
        }

        private void StartShimmer(LinearGradientBrush brush, string stopName)
        {
            if (brush == null) return;

            TranslateTransform transform = new TranslateTransform { X = -1.0 };
            brush.RelativeTransform = transform;

            Storyboard storyboard = new Storyboard();

            DoubleAnimation animation = new DoubleAnimation
            {
                From = -1.5,
                To = 1.5,
                Duration = new Duration(TimeSpan.FromSeconds(2)),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = false
            };

            if (transform != null)
            {
                Storyboard.SetTarget(animation, transform);
                Storyboard.SetTargetProperty(animation, "X");

                storyboard.Children.Add(animation);

                try
                {
                    storyboard.Begin();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Shimmer failed to start: {ex.Message}");
                }
            }
        }
        #endregion

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            ViewModel.OnTelemetryTicked -= ViewModel_OnTelemetryTicked;

            _wallpaperTimer?.Stop();

            ViewModel?.PauseUpdates();

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Graph Histories...");

                _cpuHistory.Clear();
                _ramHistory.Clear();
                _netDownHistory.Clear();
                _netUpHistory.Clear();
                _gpuHistory.Clear();

                MainWinViewModel.AppHidden -= PauseLiveMonitoring;
                MainWinViewModel.AppRestored -= ResumeLiveMonitoring;
                this.Loaded -= HomePage_Loaded;
                this.Unloaded -= Page_Unloaded;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);

                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        this.Bindings?.StopTracking();
                        this.DataContext = null;
                        this.Content = null;
                    });

                    DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup();
                });
            }
            else
            {
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
            }

            return Task.CompletedTask;
        }
        #endregion
    }
}