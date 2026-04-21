// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Net.NetworkInformation;
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
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class HomePage : Page, IPurgeable
    {
        #region Fields
        private GridViewItem? _draggedWrapper;
        private GridViewItem? _targetWrapper;

        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();
        private readonly DispatcherQueue _dispatcherQueue;

        private DispatcherTimer? _monitoringTimer;
        private DispatcherTimer? _wallpaperTimer;

        private string _currentWallpaperPath = string.Empty;
        private DateTime _currentWallpaperWriteTime = DateTime.MinValue;

        private NetworkInterface[]? _activeInterfaces;
        private DateTime _lastInterfaceUpdate = DateTime.MinValue;

        private const string RegistryPath = @"Software\EvolveOS_Optimizer";
        private const string RegistryValueName = "LastLocation";

        private long _lastDownloadBytes = 0;
        private long _lastUploadBytes = 0;
        private DateTime _lastUpdateTime = DateTime.Now;
        private bool _isFirstTick = true;

        private List<double> _cpuHistory = new List<double>();
        private List<double> _ramHistory = new List<double>();
        private List<double> _netDownHistory = new List<double>();
        private List<double> _netUpHistory = new List<double>();
        private List<double> _gpuHistory = new List<double>();
        #endregion

        public HomePageViewModel ViewModel { get; } = new();

        #region Constructor & Page Lifecycle
        public HomePage()
        {
            this.InitializeComponent();
            LogoGrid.Translation = new System.Numerics.Vector3(0, 0, 32);

            this.DataContext = ViewModel;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue not found.");

            this.Loaded += HomePage_Loaded;
            this.Unloaded += Page_Unloaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyElevationUI();
            LoadWeather();
            LoadDashboardLayout();
            DashboardDragCursor();
            UpdateDnsCardUI();
            await CalculateSystemHealthAsync();
            await CalculateSecurityHealthAsync();

            var stats = GetCurrentNetworkBytes();
            _lastDownloadBytes = stats.Down;
            _lastUploadBytes = stats.Up;
            _lastUpdateTime = DateTime.Now;

            StartMonitoring();
            StartWallpaperMonitor();

            if (HardwareData.Memory.Total == 0)
            {
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    foreach (var obj in searcher.Get())
                    {
                        double totalBytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                        HardwareData.Memory.Total = totalBytes / 1024 / 1024;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HardwareData] Failed to get total RAM: {ex.Message}");
                    HardwareData.Memory.Total = 16384;
                }
            }

            StartShimmer(IpShimmerBrush, "Stop2");
            StartShimmer(LocalIpShimmerBrush, "LocalStop2");

            if (this.DataContext is HomePageViewModel vm)
            {
                UpdateIpPrivacy(vm.StateButtonVision);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_monitoringTimer != null)
            {
                _monitoringTimer.Stop();
                _monitoringTimer = null;
            }

            if (_wallpaperTimer != null)
            {
                _wallpaperTimer.Stop();
                _wallpaperTimer.Tick -= CheckWallpaperTimer_Tick;
                _wallpaperTimer = null;
            }

            if (this.DataContext is IDisposable disposableVM)
            {
                disposableVM.Dispose();
            }

            this.DataContext = null;

            this.Loaded -= HomePage_Loaded;
            this.Unloaded -= Page_Unloaded;
        }
        #endregion

        #region Real-time Monitoring (Hardware & Network)
        private void StartMonitoring()
        {
            _monitoringTimer = new DispatcherTimer();
            _monitoringTimer.Interval = TimeSpan.FromSeconds(2);
            _monitoringTimer.Tick += OnMonitoringTick;
            _monitoringTimer.Start();
        }

        private async void OnMonitoringTick(object? sender, object e)
        {
            if (this.XamlRoot == null) return;
            await UpdateHardwareStats();
        }

        private async Task UpdateHardwareStats()
        {
            try
            {
                string pCount = await _systemDiagnostics.GetProcessCount();
                string sCount = await _systemDiagnostics.GetServicesCount();

                double cpuPercentage = await _systemDiagnostics.GetTotalProcessorUsage();

                int gpuPercentage = await SystemDiagnostics.GetGpuUsage();

                var memInfo = GC.GetGCMemoryInfo();
                double totalBytes = (double)memInfo.TotalAvailableMemoryBytes;
                double availBytes = await _systemDiagnostics.GetPhysicalAvailableMemory();
                double ramPercentage = (totalBytes > 0) ? ((totalBytes - availBytes) / totalBytes) * 100.0 : 0;

                var currentStats = GetCurrentNetworkBytes();
                DateTime now = DateTime.Now;
                double timeDiff = (now - _lastUpdateTime).TotalSeconds;

                double dlMbps = 0, ulMbps = 0;

                if (timeDiff > 0 && !_isFirstTick)
                {
                    if (currentStats.Down >= _lastDownloadBytes)
                    {
                        dlMbps = ((currentStats.Down - _lastDownloadBytes) * 8.0) / timeDiff / 1_000_000.0;
                    }

                    if (currentStats.Up >= _lastUploadBytes)
                    {
                        ulMbps = ((currentStats.Up - _lastUploadBytes) * 8.0) / timeDiff / 1_000_000.0;
                    }
                }

                _lastDownloadBytes = currentStats.Down;
                _lastUploadBytes = currentStats.Up;
                _lastUpdateTime = now;
                _isFirstTick = false;

                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (this.XamlRoot == null) return;

                    if (this.DataContext is HomePageViewModel vm)
                    {
                        vm.RefreshStats(pCount, sCount);
                        vm.UpdateDateTime();

                        CPULoad.Value = Math.Clamp(cpuPercentage, 0, 100);
                        RAMLoad.Value = Math.Clamp(ramPercentage, 0, 100);
                        CPUText.Text = ((int)CPULoad.Value).ToString();
                        RAMText.Text = ((int)RAMLoad.Value).ToString();
                        ProcCountText.Text = pCount;
                        SvcCountText.Text = sCount;

                        DownLoadRing.Value = Math.Clamp(dlMbps, 0, 1000);
                        UpLoadRing.Value = Math.Clamp(ulMbps, 0, 1000);

                        DownLoadText.Text = dlMbps.ToString("F2");
                        UpLoadText.Text = ulMbps.ToString("F2");
                    }

                    UpdateCpuGraph(cpuPercentage);
                    UpdateRamGraph(ramPercentage);
                    UpdateNetworkGraph(dlMbps, ulMbps);
                    UpdateGpuGraph(gpuPercentage);
                });
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private (long Down, long Up) GetCurrentNetworkBytes()
        {
            long d = 0, u = 0;
            try
            {
                if (_activeInterfaces == null || (DateTime.Now - _lastInterfaceUpdate).TotalSeconds > 60)
                {
                    var allInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                    var mainInterface = allInterfaces.FirstOrDefault(ni =>
                        ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        ni.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));

                    if (mainInterface == null)
                    {
                        mainInterface = allInterfaces.FirstOrDefault(ni =>
                            ni.OperationalStatus == OperationalStatus.Up &&
                            (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                             ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                            !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                            !ni.Description.Contains("Pseudo", StringComparison.OrdinalIgnoreCase));
                    }

                    if (mainInterface != null)
                    {
                        _activeInterfaces = new[] { mainInterface };
                    }
                    else
                    {
                        _activeInterfaces = Array.Empty<NetworkInterface>();
                    }

                    _lastInterfaceUpdate = DateTime.Now;
                }

                foreach (var ni in _activeInterfaces)
                {
                    var stats = ni.GetIPStatistics();
                    d += stats.BytesReceived;
                    u += stats.BytesSent;
                }
            }
            catch
            {
                _activeInterfaces = null;
            }
            return (d, u);
        }
        #endregion

        #region GPU Graph Logic
        private int _maxGpuDataPoints = 30;

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
            double stepX = width / (_gpuHistory.Count - 1);

            var geometry = new PathGeometry();
            var fillGeometry = new PathGeometry();

            var figure = new PathFigure();
            var fillFigure = new PathFigure();

            double startY = height - (_gpuHistory[0] / maxGpu * height);

            figure.StartPoint = new Windows.Foundation.Point(0, startY);
            fillFigure.StartPoint = new Windows.Foundation.Point(0, height);
            fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(0, startY) });

            Windows.Foundation.Point lastPoint = figure.StartPoint;

            for (int i = 1; i < _gpuHistory.Count; i++)
            {
                double x = i * stepX;
                double y = height - (_gpuHistory[i] / maxGpu * height);

                y = Math.Max(0, Math.Min(height, y));

                lastPoint = new Windows.Foundation.Point(x, y);

                figure.Segments.Add(new LineSegment { Point = lastPoint });
                fillFigure.Segments.Add(new LineSegment { Point = lastPoint });
            }

            fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(width, height) });

            geometry.Figures.Add(figure);
            fillGeometry.Figures.Add(fillFigure);

            GpuGraphLine.Data = geometry;
            GpuGraphFill.Data = fillGeometry;

            GpuGraphDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(GpuGraphDot, lastPoint.X);
            Canvas.SetTop(GpuGraphDot, lastPoint.Y);
        }
        #endregion

        #region Network Graph Logic
        private int _maxNetDataPoints = 30;

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

            double stepX = width / (_netDownHistory.Count - 1);

            var downGeo = new PathGeometry();
            var upGeo = new PathGeometry();
            var downFig = new PathFigure();
            var upFig = new PathFigure();

            var downFillGeo = new PathGeometry();
            var upFillGeo = new PathGeometry();
            var downFillFig = new PathFigure();
            var upFillFig = new PathFigure();

            double startYDown = height - (_netDownHistory[0] / maxNetScale * height);
            double startYUp = height - (_netUpHistory[0] / maxNetScale * height);

            downFig.StartPoint = new Windows.Foundation.Point(0, Math.Max(0, Math.Min(height, startYDown)));
            upFig.StartPoint = new Windows.Foundation.Point(0, Math.Max(0, Math.Min(height, startYUp)));

            downFillFig.StartPoint = new Windows.Foundation.Point(0, height);
            downFillFig.Segments.Add(new LineSegment { Point = downFig.StartPoint });

            upFillFig.StartPoint = new Windows.Foundation.Point(0, height);
            upFillFig.Segments.Add(new LineSegment { Point = upFig.StartPoint });

            Windows.Foundation.Point lastDownPoint = downFig.StartPoint;
            Windows.Foundation.Point lastUpPoint = upFig.StartPoint;

            for (int i = 1; i < _netDownHistory.Count; i++)
            {
                double x = i * stepX;

                double yDown = height - (_netDownHistory[i] / maxNetScale * height);
                double yUp = height - (_netUpHistory[i] / maxNetScale * height);

                yDown = Math.Max(0, Math.Min(height, yDown));
                yUp = Math.Max(0, Math.Min(height, yUp));

                lastDownPoint = new Windows.Foundation.Point(x, yDown);
                lastUpPoint = new Windows.Foundation.Point(x, yUp);

                downFig.Segments.Add(new LineSegment { Point = lastDownPoint });
                upFig.Segments.Add(new LineSegment { Point = lastUpPoint });

                downFillFig.Segments.Add(new LineSegment { Point = lastDownPoint });
                upFillFig.Segments.Add(new LineSegment { Point = lastUpPoint });
            }

            downFillFig.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(width, height) });
            upFillFig.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(width, height) });

            downGeo.Figures.Add(downFig);
            upGeo.Figures.Add(upFig);

            downFillGeo.Figures.Add(downFillFig);
            upFillGeo.Figures.Add(upFillFig);

            NetGraphLineDown.Data = downGeo;
            NetGraphLineUp.Data = upGeo;

            NetGraphFillDown.Data = downFillGeo;
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

        private int _maxRamDataPoints = 30;

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
            double stepX = width / (_ramHistory.Count - 1);

            var geometry = new PathGeometry();
            var fillGeometry = new PathGeometry();

            var figure = new PathFigure();
            var fillFigure = new PathFigure();

            double startY = height - (_ramHistory[0] / maxRam * height);

            figure.StartPoint = new Windows.Foundation.Point(0, startY);
            fillFigure.StartPoint = new Windows.Foundation.Point(0, height);
            fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(0, startY) });

            Windows.Foundation.Point lastPoint = figure.StartPoint;

            for (int i = 1; i < _ramHistory.Count; i++)
            {
                double x = i * stepX;
                double y = height - (_ramHistory[i] / maxRam * height);

                y = Math.Max(0, Math.Min(height, y));

                lastPoint = new Windows.Foundation.Point(x, y);

                figure.Segments.Add(new LineSegment { Point = lastPoint });
                fillFigure.Segments.Add(new LineSegment { Point = lastPoint });
            }

            fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(width, height) });

            geometry.Figures.Add(figure);
            fillGeometry.Figures.Add(fillFigure);

            RamGraphLine.Data = geometry;
            RamGraphFill.Data = fillGeometry;

            RamGraphDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(RamGraphDot, lastPoint.X);
            Canvas.SetTop(RamGraphDot, lastPoint.Y);
        }
        #endregion

        #region CPU Graph Logic
        private int _maxCpuDataPoints = 30;

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
            double stepX = width / (_cpuHistory.Count - 1);

            var geometry = new PathGeometry();
            var fillGeometry = new PathGeometry();

            var figure = new PathFigure();
            var fillFigure = new PathFigure();

            double startY = height - (_cpuHistory[0] / maxCpu * height);

            figure.StartPoint = new Windows.Foundation.Point(0, startY);
            fillFigure.StartPoint = new Windows.Foundation.Point(0, height);
            fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(0, startY) });

            Windows.Foundation.Point lastPoint = figure.StartPoint;

            for (int i = 1; i < _cpuHistory.Count; i++)
            {
                double x = i * stepX;
                double y = height - (_cpuHistory[i] / maxCpu * height);

                y = Math.Max(0, Math.Min(height, y));

                lastPoint = new Windows.Foundation.Point(x, y);

                figure.Segments.Add(new LineSegment { Point = lastPoint });
                fillFigure.Segments.Add(new LineSegment { Point = lastPoint });
            }

            fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(width, height) });

            geometry.Figures.Add(figure);
            fillGeometry.Figures.Add(fillFigure);

            CpuGraphLine.Data = geometry;
            CpuGraphFill.Data = fillGeometry;

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

            _maxCpuDataPoints = totalSeconds / 2;
            _maxRamDataPoints = totalSeconds / 2;
            _maxNetDataPoints = totalSeconds / 2;
            _maxGpuDataPoints = totalSeconds / 2;

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
            _wallpaperTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _wallpaperTimer.Tick += CheckWallpaperTimer_Tick;
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
                    element.TranslationTransition = new Microsoft.UI.Xaml.Vector3Transition()
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
                    element.TranslationTransition = new Microsoft.UI.Xaml.Vector3Transition()
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

        #region Dashboard Customization (Drag, Drop, Visibility)
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

                DashGamingStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/PngImages/health_good.png"));
                DashGamingStatusImage.Opacity = 1.0;
                GamingSpinner.Visibility = Visibility.Visible;
                GamingSpinner.IsActive = true;
            }
            else
            {
                GamingModeButtonLabel.Text = ResourceString.GetString("gm_label_normal");
                GamingModeBtnText.Text = ResourceString.GetString("gm_btn_enable");

                DashGamingStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/PngImages/gamingmode_off.png"));
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

        private void DashboardGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 0)
            {
                _draggedWrapper = DashboardGridView.ContainerFromItem(e.Items[0]) as GridViewItem;

                if (_draggedWrapper == null && e.Items[0] is GridViewItem gvi)
                {
                    _draggedWrapper = gvi;
                }

                if (_draggedWrapper == null && e.Items[0] is FrameworkElement fe)
                {
                    _draggedWrapper = DashboardGridView.Items.OfType<GridViewItem>()
                        .FirstOrDefault(i => (i.Content as FrameworkElement) == fe || i == fe);
                }

                if (_draggedWrapper != null)
                {
                    e.Data.SetText("Swap");
                    e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                    Debug.WriteLine($"[Drag] SUCCESS: Started dragging {(_draggedWrapper.Content as FrameworkElement)?.Name}");
                }
                else
                {
                    Debug.WriteLine("[Drag] ERROR: Could not identify the dragged wrapper.");
                }
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.IsDragInProgress = true;
            }
        }

        private void DashboardGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            Debug.WriteLine($"[Drag] Final Check - Dragged: {_draggedWrapper != null}, Target: {_targetWrapper != null}");

            if (_draggedWrapper != null && _targetWrapper != null && _draggedWrapper != _targetWrapper)
            {
                int oldIndex = DashboardGridView.Items.IndexOf(_draggedWrapper);
                int newIndex = DashboardGridView.Items.IndexOf(_targetWrapper);

                if (oldIndex != -1 && newIndex != -1)
                {
                    Debug.WriteLine($"[Drag] EXECUTING SWAP: {oldIndex} -> {newIndex}");

                    DashboardGridView.Items.RemoveAt(oldIndex);
                    DashboardGridView.Items.Insert(newIndex, _draggedWrapper);

                    SaveDashboardLayout();
                }
            }

            _draggedWrapper = null;
            _targetWrapper = null;

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.IsDragInProgress = false;
                panel.InvalidateMeasure();
            }
        }

        private void Card_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.DragUIOverride.Clear();

            if (sender is FrameworkElement targetCard)
            {
                string cleanName = targetCard.Name.Replace("Card", "");
                string formatString = ResourceString.GetString("txt_swap_with_format");

                e.DragUIOverride.Caption = string.Format(formatString, cleanName);
            }
            else
            {
                e.DragUIOverride.Caption = ResourceString.GetString("txt_swap_positions");
            }

            //e.DragUIOverride.Caption = ResourceString.GetString("txt_release_to_swap");
            e.DragUIOverride.IsCaptionVisible = true;

            e.Handled = true;
        }

        private void Card_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement fe && _draggedWrapper != null)
            {
                var container = DashboardGridView.Items.OfType<GridViewItem>()
                    .FirstOrDefault(i => (i.Content as FrameworkElement) == fe);

                if (container != null && container != _draggedWrapper)
                {
                    _targetWrapper = container;
                    Debug.WriteLine($"[Drag] Sticky Target Set: {fe.Name}");
                }
            }
        }

        private void SetCustomCursor(UIElement element, Microsoft.UI.Input.InputSystemCursorShape shape)
        {
            if (element == null) return;

            var cursor = Microsoft.UI.Input.InputSystemCursor.Create(shape);
            var cursorProperty = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);

            if (cursorProperty != null)
            {
                cursorProperty.SetValue(element, cursor);
            }
        }

        private void DashboardDragCursor()
        {
            SetCustomCursor(CardWeather, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardNetwork, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardRam, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpu, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpuGraph, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGpu, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardDisk, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGamingMode, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardDns, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardMaintenance, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardSecurity, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpuGraph, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardRamGraph, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardNetworkGraph, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGpuGraph, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);

            SetCustomCursor(RefreshWeatherButton, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(LocationButton, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(Calendar, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnVision, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenDnsPage, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnStartService, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnDebug, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenMaintenancePage, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshHealth, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenSecurityPage, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshSecurity, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnDashViewIssues, Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnGamingMode, Microsoft.UI.Input.InputSystemCursorShape.Arrow);

            SetCustomCursor(IpAddress, Microsoft.UI.Input.InputSystemCursorShape.Hand);
            SetCustomCursor(LocalIpAddress, Microsoft.UI.Input.InputSystemCursorShape.Hand);
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
            if (sender is Border card)
            {
                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
                FactoryAnimation.AnimateCardScale(card, 1.01);
            }
        }

        private void DashCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
                FactoryAnimation.AnimateCardScale(card, 1.0);
            }
        }

        private void DashCard_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
                FactoryAnimation.AnimateCardScale(card, 1.01);
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
                else if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out object secondaryBrush))
                {
                    card.Background = (Brush)secondaryBrush;
                }

                FactoryAnimation.AnimateCardScale(card, 0.98);
            }
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
                        DashGamingStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/PngImages/health_good.png"));
                        DashGamingStatusImage.Opacity = 1.0;

                        GamingSpinner.Visibility = Visibility.Visible;
                        GamingSpinner.IsActive = true;

                        GamingModeButtonLabel.Text = ResourceString.GetString("gm_label_gaming");
                        GamingModeBtnText.Text = ResourceString.GetString("gm_btn_disable");
                    }
                    else
                    {
                        DashGamingStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/PngImages/gamingmode_off.png"));
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

                DashGamingStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/PngImages/gamingmode_off.png"));
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
                MainWindow.Instance.SwitchPage("Maintenance");
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

                DashMaintenanceStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(healthResult.ImagePath));
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
                MainWindow.Instance.SwitchPage("Security");
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
                    // Gather all statuses
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

                    if (!isAvEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_VirusThreatProtection")); }
                    if (!isFwEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_FirewallNetworkProtection")); }
                    if (!isRtEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_RealTimeProtection")); }
                    if (!isUacEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_UAC")); }
                    if (!isWuEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_WindowsUpdate")); }
                    if (!isTpEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_TamperProtection")); }
                    if (!isSmartAppControlSecure) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_SmartAppControl")); }
                    if (!isPsPolicySecure) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_PSExecutionPolicy")); }
                    if (!isLsaEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_LSAProtection")); }
                    if (isRdpEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_RemoteDesktop")); }
                    if (isRaEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_RemoteAssistance")); }
                    if (isDevModeEnabled) { issuesCount++; securityIssues.Add(ResourceString.GetString("SecurityPage_DeveloperMode")); }

                    isCoreProtected = isAvEnabled && isFwEnabled && isRtEnabled;
                });

                if (!isCoreProtected)
                {
                    DashSecurityStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/unsecure.png"));
                    TxtSecurityStatus.Text = $"{issuesCount} {ResourceString.GetString("text_security_critical") ?? "Critical Issues"}";
                }
                else if (issuesCount > 0)
                {
                    DashSecurityStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/secure.png"));
                    TxtSecurityStatus.Text = $"{issuesCount} {ResourceString.GetString("text_security_warning") ?? "Warnings Found"}";
                }
                else
                {
                    DashSecurityStatusImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/PngImages/secure.png"));
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
                    Application.Current.Exit();
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
            if (e.Key == Windows.System.VirtualKey.Space || e.Key == Windows.System.VirtualKey.Enter)
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
        public void Purge()
        {
            Debug.WriteLine("[HomePage] Deep Purge Initiated...");

            if (_monitoringTimer != null)
            {
                _monitoringTimer.Stop();
                _monitoringTimer.Tick -= OnMonitoringTick;
                _monitoringTimer = null;
            }

            if (_wallpaperTimer != null)
            {
                _wallpaperTimer.Stop();
                _wallpaperTimer.Tick -= CheckWallpaperTimer_Tick;
                _wallpaperTimer = null;
            }

            if (DashboardGridView != null)
            {
                DashboardGridView.DragItemsStarting -= DashboardGridView_DragItemsStarting;
                DashboardGridView.DragItemsCompleted -= DashboardGridView_DragItemsCompleted;

                DashboardGridView.Items.Clear();
            }

            HardwareData.ClearResources();

            if (this.DataContext is IDisposable vm)
            {
                vm.Dispose();
                Debug.WriteLine("[HomePage] ViewModel disposed and unhooked.");
            }

            IpShimmerBrush = null;
            LocalIpShimmerBrush = null;
            this.Content = null;
            this.DataContext = null;

            if (ForecastList != null) ForecastList.ItemsSource = null;
            if (DiskDrivesList != null) DiskDrivesList.ItemsSource = null;

            this.Loaded -= HomePage_Loaded;
            this.Unloaded -= Page_Unloaded;

            Debug.WriteLine("[HomePage] Purge complete. 0 remaining references.");
        }
        #endregion
    }
}