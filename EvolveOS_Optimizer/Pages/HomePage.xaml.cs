using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.Win32;
using System.Net.NetworkInformation;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class HomePage : Page
{
    private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();
    private readonly DispatcherQueue _dispatcherQueue;
    private DispatcherTimer? _monitoringTimer;
    private string _lastWallpaperPath = string.Empty;

    private long _lastDownloadBytes = 0;
    private long _lastUploadBytes = 0;
    private DateTime _lastUpdateTime = DateTime.Now;
    private bool _isFirstTick = true;

    public HomePage()
    {
        this.InitializeComponent();
        LogoGrid.Translation = new System.Numerics.Vector3(0, 0, 32);

        this.DataContext = new HomePageViewModel();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue not found.");

        this.Loaded += HomePage_Loaded;
    }

    private void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyElevationUI();

        var stats = GetCurrentNetworkBytes();
        _lastDownloadBytes = stats.Down;
        _lastUploadBytes = stats.Up;
        _lastUpdateTime = DateTime.Now;

        StartMonitoring();

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
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    private (long Down, long Up) GetCurrentNetworkBytes()
    {
        long d = 0, u = 0;
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                var stats = ni.GetIPStatistics();
                d += stats.BytesReceived;
                u += stats.BytesSent;
            }
        }
        catch { }
        return (d, u);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_monitoringTimer != null)
        {
            _monitoringTimer.Stop();
            _monitoringTimer = null;
        }

        try
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomePage] Error detaching SystemEvents: {ex.Message}");
        }

        if (this.DataContext is IDisposable disposableVM)
        {
            disposableVM.Dispose();
        }

        this.DataContext = null;
        this.Content = null;

        this.Loaded -= HomePage_Loaded;
        this.Unloaded -= Page_Unloaded;

        Debug.WriteLine("[HomePage] Nuclear disposal complete. Content and DataContext cleared.");

        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Real-time Monitoring
    private void StartMonitoring()
    {
        _ = UpdateHardwareStats();

        _monitoringTimer = new DispatcherTimer();
        _monitoringTimer.Interval = TimeSpan.FromSeconds(2);
        _monitoringTimer.Tick += async (s, e) =>
        {
            if (this.XamlRoot == null) return;
            await UpdateHardwareStats();
        };
        _monitoringTimer.Start();
    }

    private async Task UpdateHardwareStats()
    {
        try
        {
            string pCount = await _systemDiagnostics.GetProcessCount();
            string sCount = await _systemDiagnostics.GetServicesCount();
            double cpuPercentage = await _systemDiagnostics.GetTotalProcessorUsage();
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
                dlMbps = ((currentStats.Down - _lastDownloadBytes) / timeDiff / (1024.0 * 1024.0)) * 8.0;
                ulMbps = ((currentStats.Up - _lastUploadBytes) / timeDiff / (1024.0 * 1024.0)) * 8.0;
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
                    vm.RefreshStats();
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

                    string currentPath = _systemDiagnostics.GetWallpaperPath();
                    if (currentPath != _lastWallpaperPath)
                    {
                        _lastWallpaperPath = currentPath;
                        AnimateWallpaperChange(vm);
                    }
                }
            });
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
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
            StatusLabel.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
        }
        else
        {
            AdminWarningBanner.Visibility = Visibility.Collapsed;
            WallInfoBanner.Visibility = Visibility.Visible;
            StatusLabel.Text = ResourceString.GetString("status_elevated_active");
            if (Application.Current.Resources.TryGetValue("Brush_Success", out object brush))
                StatusLabel.Foreground = (Brush)brush;
            else
                StatusLabel.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
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

    private void AnimateWallpaperChange(HomePageViewModel vm)
    {
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(LogoPath);
        var compositor = visual.Compositor;
        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
        fadeAnimation.InsertKeyFrame(0.0f, 0.0f);
        fadeAnimation.InsertKeyFrame(1.0f, 1.0f);
        fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);
        vm.RefreshWallpaper();
        visual.StartAnimation("Opacity", fadeAnimation);
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

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Desktop || e.Category == UserPreferenceCategory.General)
        {
            _dispatcherQueue.TryEnqueue(() => (this.DataContext as HomePageViewModel)?.RefreshWallpaper());
        }
    }
    #endregion

    // private static readonly List<string> AvailableWeatherLocations = new List<string>
    // {
    //    "New York", "London", "Paris", "Berlin", "Tokyo", "Singapore", "Sydney" 
    //  ... (Keep your full list here)
    // };
}