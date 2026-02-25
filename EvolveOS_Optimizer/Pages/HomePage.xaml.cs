using System.Net.NetworkInformation;
using System.Reflection;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
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
        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();
        private readonly DispatcherQueue _dispatcherQueue;
        private DispatcherTimer? _monitoringTimer;
        private string _lastWallpaperPath = string.Empty;

        private NetworkInterface[]? _activeInterfaces;
        private DateTime _lastInterfaceUpdate = DateTime.MinValue;

        private const string RegistryPath = @"Software\EvolveOS_Optimizer";
        private const string RegistryValueName = "LastLocation";

        private long _lastDownloadBytes = 0;
        private long _lastUploadBytes = 0;
        private DateTime _lastUpdateTime = DateTime.Now;
        private bool _isFirstTick = true;

        private UIElement? _draggedCard;
        #endregion

        public HomePageViewModel ViewModel { get; } = new();

        #region Constructor & Page Lifecycle
        public HomePage()
        {
            this.InitializeComponent();
            LogoGrid.Translation = new System.Numerics.Vector3(0, 0, 32);

            this.DataContext = new HomePageViewModel();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue not found.");

            this.Loaded += HomePage_Loaded;
            this.Unloaded += Page_Unloaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyElevationUI();
            LoadWeather();
            LoadDashboardLayout();
            DashboardDragCursor();

            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

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

            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

            if (this.DataContext is IDisposable disposableVM)
            {
                disposableVM.Dispose();
            }

            this.DataContext = null;

            this.Loaded -= HomePage_Loaded;
            this.Unloaded -= Page_Unloaded;

            //Debug.WriteLine("[HomePage] Memory leaks plugged. Static events unhooked.");
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

        private (long Down, long Up) GetCurrentNetworkBytes()
        {
            long d = 0, u = 0;
            try
            {
                if (_activeInterfaces == null || (DateTime.Now - _lastInterfaceUpdate).TotalSeconds > 60)
                {
                    _activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                     ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .ToArray();

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

        #region Weather Handlers

        private void LocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is HomePageViewModel vm)
            {
                _ = vm.FetchWeatherAsync(vm.WeatherLocation);
            }
        }

        private void Calendar_MouseEnter(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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

        private void Calendar_MouseLeave(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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

        private void DiskCard_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
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

        private void DiskCard_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
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
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegistryPath);
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

        private void CustomLocationBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
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
            ToggleNetwork.IsOn = SettingsEngine.Dashboard_CardNetwork;
            ToggleRam.IsOn = SettingsEngine.Dashboard_CardRam;
            ToggleCpu.IsOn = SettingsEngine.Dashboard_CardCpu;
            ToggleGpu.IsOn = SettingsEngine.Dashboard_CardGpu;
            ToggleDisk.IsOn = SettingsEngine.Dashboard_CardDisk;

            CardWeather.Visibility = Visibility.Visible;
            CardNetwork.Visibility = ToggleNetwork.IsOn ? Visibility.Visible : Visibility.Collapsed;
            CardRam.Visibility = ToggleRam.IsOn ? Visibility.Visible : Visibility.Collapsed;
            CardCpu.Visibility = ToggleCpu.IsOn ? Visibility.Visible : Visibility.Collapsed;
            CardGpu.Visibility = ToggleGpu.IsOn ? Visibility.Visible : Visibility.Collapsed;
            CardDisk.Visibility = ToggleDisk.IsOn ? Visibility.Visible : Visibility.Collapsed;

            string savedOrder = SettingsEngine.DashboardCardOrder;
            if (!string.IsNullOrWhiteSpace(savedOrder))
            {
                var order = savedOrder.Split(',');
                var currentCards = DashboardPanel.Children.OfType<FrameworkElement>().ToList();

                DashboardPanel.Children.Clear();

                var weatherCard = currentCards.FirstOrDefault(c => c.Name == "CardWeather");
                if (weatherCard != null)
                {
                    DashboardPanel.Children.Add(weatherCard);
                    currentCards.Remove(weatherCard);
                }

                foreach (var name in order)
                {
                    var card = currentCards.FirstOrDefault(c => c.Name == name);
                    if (card != null)
                    {
                        DashboardPanel.Children.Add(card);
                        currentCards.Remove(card);
                    }
                }

                foreach (var card in currentCards)
                {
                    DashboardPanel.Children.Add(card);
                }
            }
        }

        private void SaveDashboardLayout()
        {
            var order = DashboardPanel.Children.OfType<FrameworkElement>()
                .Where(c => c.Name != "CardWeather")
                .Select(c => c.Name).ToList();

            SettingsEngine.DashboardCardOrder = string.Join(",", order);

            SettingsEngine.Dashboard_CardNetwork = ToggleNetwork.IsOn;
            SettingsEngine.Dashboard_CardRam = ToggleRam.IsOn;
            SettingsEngine.Dashboard_CardCpu = ToggleCpu.IsOn;
            SettingsEngine.Dashboard_CardGpu = ToggleGpu.IsOn;
            SettingsEngine.Dashboard_CardDisk = ToggleDisk.IsOn;
        }

        private void ToggleCard_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ts.Tag is string cardName)
            {
                var card = DashboardPanel.Children.OfType<FrameworkElement>().FirstOrDefault(c => c.Name == cardName);
                if (card != null)
                {
                    card.Visibility = ts.IsOn ? Visibility.Visible : Visibility.Collapsed;
                    SaveDashboardLayout();
                }
            }
        }

        private void DashCard_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            _draggedCard = sender;
            args.AllowedOperations = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

            if (sender is FrameworkElement card)
            {
                string cleanName = card.Name.Replace("Card", "");
                args.Data.SetText($"Moving {cleanName}...");
            }

            sender.Opacity = 0.5;
        }

        private void DashCard_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedCard != null && sender is FrameworkElement targetCard)
            {
                if (targetCard.Name == "CardWeather" || targetCard == _draggedCard)
                {
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                    e.Handled = true;
                    return;
                }

                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                e.Handled = true;
            }
        }

        private void DashCard_Drop(object sender, DragEventArgs e)
        {
            if (_draggedCard != null && sender is FrameworkElement targetCard && targetCard.Name != "CardWeather")
            {
                int draggedIndex = DashboardPanel.Children.IndexOf(_draggedCard);
                int targetIndex = DashboardPanel.Children.IndexOf(targetCard);

                if (draggedIndex >= 0 && targetIndex > 0 && draggedIndex != targetIndex)
                {
                    DashboardPanel.Children.RemoveAt(draggedIndex);
                    DashboardPanel.Children.Insert(targetIndex, _draggedCard);

                    SaveDashboardLayout();
                }
            }
        }

        private void DashCard_DropCompleted(UIElement sender, DropCompletedEventArgs args)
        {
            sender.Opacity = 1.0;

            _draggedCard = null;
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
            // Set the "Move" cursor for the draggable cards
            SetCustomCursor(CardNetwork, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardRam, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpu, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGpu, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardDisk, Microsoft.UI.Input.InputSystemCursorShape.SizeAll);

            // OVERRIDES: Set the standard Arrow (or Hand) cursor for clickable elements INSIDE the cards
            SetCustomCursor(BtnVision, Microsoft.UI.Input.InputSystemCursorShape.Arrow);

            // Text blocks with "PointerPressed" events to copy text. 
            SetCustomCursor(IpAddress, Microsoft.UI.Input.InputSystemCursorShape.Hand);
            SetCustomCursor(LocalIpAddress, Microsoft.UI.Input.InputSystemCursorShape.Hand);
        }

        private void ResetDashboard_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingsEngine.DashboardCardOrder = "CardNetwork,CardRam,CardCpu,CardGpu,CardDisk";
            SettingsEngine.Dashboard_CardNetwork = true;
            SettingsEngine.Dashboard_CardRam = true;
            SettingsEngine.Dashboard_CardCpu = true;
            SettingsEngine.Dashboard_CardGpu = true;
            SettingsEngine.Dashboard_CardDisk = true;

            ToggleNetwork.IsOn = true;
            ToggleRam.IsOn = true;
            ToggleCpu.IsOn = true;
            ToggleGpu.IsOn = true;
            ToggleDisk.IsOn = true;

            LoadDashboardLayout();
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

        #region Privacy & Masking Logic
        private void BtnVision_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
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

            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(1.0f, targetOpacity);
            animation.Duration = TimeSpan.FromMilliseconds(250);

            visual.StartAnimation("Opacity", animation);
        }

        private void StartShimmer(LinearGradientBrush brush, string stopName)
        {
            Storyboard storyboard = new Storyboard();

            DoubleAnimation animation = new DoubleAnimation
            {
                From = -0.5,
                To = 1.5,
                Duration = new Duration(TimeSpan.FromSeconds(2)),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = false
            };

            Storyboard.SetTarget(animation, brush.GradientStops[1]);
            Storyboard.SetTargetProperty(animation, "Offset");

            storyboard.Children.Add(animation);
            storyboard.Begin();
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

            if (DashboardPanel != null)
            {
                DashboardPanel.Children.Clear();
            }

            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

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

            // CLEAR LOCAL COLLECTIONS
            // Lists or Observables, clear to drop refs
            // _someLocalList?.Clear();

            Debug.WriteLine("[HomePage] Purge complete. 0 remaining references.");
        }
        #endregion
    }
}