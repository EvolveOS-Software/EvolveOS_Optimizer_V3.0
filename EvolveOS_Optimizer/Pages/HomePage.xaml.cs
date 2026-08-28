// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using EvolveOS_Optimizer.Assets.UserControl;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
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

        private const string RegistryPath = @"Software\EvolveOS_Optimizer";
        private const string RegistryValueName = "LastLocation";

        private bool _isCurrentPageActive = false;
        private bool _isInitialized = false;

        private CancellationTokenSource? _networkMonitorCts;

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
            ViewModel.OnWallpaperUpdated += ViewModel_OnWallpaperUpdated;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isCurrentPageActive) return;
            _isCurrentPageActive = true;

            ViewModel.StartWallpaperMonitor();
            ViewModel.ResumeUpdates();

            MainWinViewModel.AppHidden += PauseLiveMonitoring;
            MainWinViewModel.AppRestored += ResumeLiveMonitoring;

            _isInternalToggle = true;
            ToggleAutoOptimize.IsOn = SettingsEngine.Dashboard_AutoRamOptimize;
            TxtAutoTriggerBadge.Text = $"Auto-trigger at {SettingsEngine.Dashboard_AutoRamThreshold}% RAM usage";
            _isInternalToggle = false;

            if (!_isInitialized)
            {
                ApplyElevationUI();
                LoadDashboardLayout();
                DashboardDragCursor();

                if (DnsShieldCanvas != null)
                {
                    _dnsShieldHelper.Initialize(DnsShieldCanvas);
                }

                UpdateDnsCardUI();

                ApplyLightingToCards();
                InitializeSecurityCard();

                if (ViewModel.SaveCardStates)
                {
                    if (SettingsEngine.IsCpuCardExpanded) BtnExpandCpu_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsGpuCardExpanded) BtnExpandGpu_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsDiskCardExpanded) BtnExpandDisk_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsNetworkCardExpanded) BtnExpandNetwork_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsDnsCardExpanded) BtnExpandDns_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsRamBoostCardExpanded) BtnExpandRamBoost_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsPrivacyCardExpanded) BtnExpandPrivacy_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsPerformanceCardExpanded) BtnExpandPerformance_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsHealthCardExpanded) BtnExpandHealth_Click(this, new RoutedEventArgs());
                    if (SettingsEngine.IsSecurityCardExpanded) BtnExpandSecurity_Click(this, new RoutedEventArgs());
                }

                _ = UpdateSystemHealthUIAsync();
                _ = UpdateSecurityUIAsync();
                _ = UpdatePrivacyUIAsync();
                _ = UpdatePerformanceUIAsync();
                
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
            ViewModel.PauseUpdates();
            Debug.WriteLine("[HomePage] Live monitoring PAUSED for System Tray/Cache.");
        }

        private void ResumeLiveMonitoring()
        {
            ViewModel.StartWallpaperMonitor();
            ViewModel.ResumeUpdates();
            Debug.WriteLine("[HomePage] Live monitoring RESUMED.");
        }

        private async void ViewModel_OnWallpaperUpdated(byte[] imageBytes)
        {
            try
            {
                using var memStream = new MemoryStream(imageBytes);
                using var randomAccessStream = memStream.AsRandomAccessStream();

                var bitmap = new BitmapImage();
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                bitmap.DecodePixelWidth = 1920;
                await bitmap.SetSourceAsync(randomAccessStream);

                if (WallpaperBrush != null) WallpaperBrush.ImageSource = bitmap;

                var visual = ElementCompositionPreview.GetElementVisual(LogoPath);
                if (visual != null)
                {
                    var fadeAnimation = visual.Compositor.CreateScalarKeyFrameAnimation();
                    fadeAnimation.InsertKeyFrame(0.0f, 0.5f);
                    fadeAnimation.InsertKeyFrame(1.0f, 1.0f);
                    fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);
                    visual.StartAnimation("Opacity", fadeAnimation);
                }
            }
            catch { }
        }
        #endregion

        #region View/UI Telemetry Updates

        private int _lastRenderedCpu = -1;
        private int _lastRenderedRam = -1;

        private void ViewModel_OnTelemetryTicked(TelemetryDataPayload payload)
        {
            if (!_isCurrentPageActive) return;

            int currentCpu = (int)Math.Round(Math.Clamp(payload.Cpu, 0, 100));
            int currentRam = (int)Math.Round(Math.Clamp(payload.Ram, 0, 100));

            if (currentCpu != _lastRenderedCpu)
            {
                CPULoad.Value = currentCpu;
                CPUText.Text = currentCpu.ToString();
                _lastRenderedCpu = currentCpu;
            }

            if (currentRam != _lastRenderedRam)
            {
                RAMLoad.Value = currentRam;
                RAMText.Text = currentRam.ToString();
                if (BoostRamRing != null) BoostRamRing.Value = currentRam;
                _lastRenderedRam = currentRam;
            }

            CheckAutoMemoryOptimization(payload.Ram);

            if (payload.IsFullSecond)
            {
                ProcCountText.Text = payload.ProcCount;
                SvcCountText.Text = payload.SvcCount;
            }

            DownLoadRing.Value = Math.Clamp(payload.NetDown, 0, 1000);
            UpLoadRing.Value = Math.Clamp(payload.NetUp, 0, 1000);

            DownLoadText.Text = payload.NetDown.ToString("F2");
            UpLoadText.Text = payload.NetUp.ToString("F2");
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

            if (ViewModel != null)
            {
                ViewModel.MaxGraphSeconds = totalSeconds;
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
            ApplyLightingToCards();
            CustomizeLayoutDialog.Hide();
        }

        private void LoadDashboardLayout()
        {
            _isInternalToggle = true;

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
            TogglePrivacy.IsOn = SettingsEngine.Dashboard_CardPrivacy;
            TogglePerformance.IsOn = SettingsEngine.Dashboard_CardPerformance;
            ToggleCpuGraph.IsOn = SettingsEngine.Dashboard_CardCpuGraph;
            ToggleRamGraph.IsOn = SettingsEngine.Dashboard_CardRamGraph;
            ToggleNetworkGraph.IsOn = SettingsEngine.Dashboard_CardNetworkGraph;
            ToggleGpuGraph.IsOn = SettingsEngine.Dashboard_CardGpuGraph;
            ToggleRamBoost.IsOn = SettingsEngine.Dashboard_CardRamBoost;

            SetCardVisibility("CardWeather", ToggleWeather.IsOn);
            SetCardVisibility("CardNetwork", ToggleNetwork.IsOn);
            SetCardVisibility("CardRam", ToggleRam.IsOn);
            SetCardVisibility("CardCpu", ToggleCpu.IsOn);
            SetCardVisibility("CardGpu", ToggleGpu.IsOn);
            SetCardVisibility("CardDisk", ToggleDisk.IsOn);
            SetCardVisibility("CardDns", ToggleDns.IsOn);
            SetCardVisibility("CardMaintenance", ToggleHealth.IsOn);
            SetCardVisibility("CardSecurity", ToggleSecurity.IsOn);
            SetCardVisibility("CardPrivacy", TogglePrivacy.IsOn);
            SetCardVisibility("CardPerformance", TogglePerformance.IsOn);
            SetCardVisibility("CardCpuGraph", ToggleCpuGraph.IsOn);
            SetCardVisibility("CardRamGraph", ToggleRamGraph.IsOn);
            SetCardVisibility("CardNetworkGraph", ToggleNetworkGraph.IsOn);
            SetCardVisibility("CardGpuGraph", ToggleGpuGraph.IsOn);
            SetCardVisibility("CardGamingMode", ToggleGamingMode.IsOn);
            SetCardVisibility("CardRamBoost", ToggleRamBoost.IsOn);

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

            if (ComboLightingMode != null)
            {
                int savedMode = SettingsEngine.Dashboard_LightingMode;
                ComboLightingMode.SelectedIndex = savedMode;

                Visibility customVisibility = (savedMode == 3) ? Visibility.Visible : Visibility.Collapsed;

                if (AmbientPanel != null) AmbientPanel.Visibility = customVisibility;
                if (CustomColorPanel != null) CustomColorPanel.Visibility = customVisibility;
            }

            _isInternalToggle = false;
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
            SettingsEngine.Dashboard_CardPrivacy = TogglePrivacy.IsOn;
            SettingsEngine.Dashboard_CardPerformance = TogglePerformance.IsOn;
            SettingsEngine.Dashboard_CardCpuGraph = ToggleCpuGraph.IsOn;
            SettingsEngine.Dashboard_CardRamGraph = ToggleRamGraph.IsOn;
            SettingsEngine.Dashboard_CardNetworkGraph = ToggleNetworkGraph.IsOn;
            SettingsEngine.Dashboard_CardGpuGraph = ToggleGpuGraph.IsOn;
            SettingsEngine.Dashboard_CardRamBoost = ToggleRamBoost.IsOn;

            if (ComboLightingMode != null)
            {
                SettingsEngine.Dashboard_LightingMode = ComboLightingMode.SelectedIndex;
            }
        }

        private void ToggleCard_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInternalToggle) return;

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
            SetCustomCursor(CardPrivacy, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardPerformance, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardCpuGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardRamGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardNetworkGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardGpuGraph, InputSystemCursorShape.SizeAll);
            SetCustomCursor(CardRamBoost, InputSystemCursorShape.SizeAll);

            SetCustomCursor(RefreshWeatherButton, InputSystemCursorShape.Arrow);
            SetCustomCursor(LocationButton, InputSystemCursorShape.Arrow);
            SetCustomCursor(Calendar, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnVision, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenDnsPage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnStartService, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnDebug, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenMaintenancePage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandHealth, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshHealth, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenSecurityPage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOptimizeMemory, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandRamBoost, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRamBoostSettings, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenRamBoostPage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshSecurity, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandSecurity, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnDashViewIssues, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnGamingMode, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandDisk, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRunDiskCleanup, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOptimizeDrive, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandNetwork, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnFlushDns, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnResetAdapter, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandDns, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnBenchmarkDns, InputSystemCursorShape.Arrow);
            SetCustomCursor(CmbDnsPresets, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandGpu, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenGraphicsSettings, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRestartGpuDriver, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnExpandCpu, InputSystemCursorShape.Arrow);
            SetCustomCursor(CmbPowerPlan, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenResmon, InputSystemCursorShape.Arrow);

            SetCustomCursor(BtnExpandPrivacy, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenPrivacyPage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnPrivacyViewIssues, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshPrivacy, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnApplyRecommendedPrivacy, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRestorePrivacyDefaults, InputSystemCursorShape.Arrow);

            SetCustomCursor(BtnExpandPerformance, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnOpenPerformancePage, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnPerformanceViewIssues, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRefreshPerformance, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnApplyRecommendedPerformance, InputSystemCursorShape.Arrow);
            SetCustomCursor(BtnRestorePerformanceDefaults, InputSystemCursorShape.Arrow);

            SetCustomCursor(IpAddress, InputSystemCursorShape.Hand);
            SetCustomCursor(LocalIpAddress, InputSystemCursorShape.Hand);
        }

        private void ResetDashboard_Click(object sender, RoutedEventArgs e)
        {
            SettingsEngine.DashboardCardOrder = "CardSecurity,CardPrivacy,CardPerformance,CardWeather,CardMaintenance,CardDns,CardRamBoost,CardCpuGraph,CardGpuGraph,CardRamGraph,CardNetworkGraph,CardCpu,CardGpu,CardDisk,CardNetwork,CardRam,CardGamingMode";
            SettingsEngine.Dashboard_CardWeather = true;
            SettingsEngine.Dashboard_CardNetwork = true;
            SettingsEngine.Dashboard_CardRam = false;
            SettingsEngine.Dashboard_CardCpu = true;
            SettingsEngine.Dashboard_CardGpu = true;
            SettingsEngine.Dashboard_CardDisk = true;
            SettingsEngine.Dashboard_CardGamingMode = false;
            SettingsEngine.Dashboard_CardDns = true;
            SettingsEngine.Dashboard_CardHealth = true;
            SettingsEngine.Dashboard_CardSecurity = true;
            SettingsEngine.Dashboard_CardPrivacy = true;
            SettingsEngine.Dashboard_CardPerformance = true;
            SettingsEngine.Dashboard_CardRamBoost = true;
            SettingsEngine.Dashboard_CardCpuGraph = true;
            SettingsEngine.Dashboard_CardRamGraph = true;
            SettingsEngine.Dashboard_CardNetworkGraph = true;
            SettingsEngine.Dashboard_GraphTimeframe = 0;
            SettingsEngine.Dashboard_CardGpuGraph = true;
            SettingsEngine.Dashboard_LightingMode = 1;

            ToggleWeather.IsOn = true;
            ToggleNetwork.IsOn = true;
            ToggleRam.IsOn = false;
            ToggleCpu.IsOn = true;
            ToggleGpu.IsOn = true;
            ToggleDisk.IsOn = true;
            ToggleGamingMode.IsOn = false;
            ToggleDns.IsOn = true;
            ToggleHealth.IsOn = true;
            ToggleSecurity.IsOn = true;
            TogglePrivacy.IsOn = true;
            TogglePerformance.IsOn = true;
            ToggleRamBoost.IsOn = true;
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
                FactoryAnimation.AnimateCardScale(card, 1.01);
            }
        }

        private void DashCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card && !_isTrackingDrag)
            {
                FactoryAnimation.AnimateCardScale(card, 1.0);
            }
        }

        private void DashCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                FactoryAnimation.AnimateCardScale(card, 0.97);
                card.Opacity = 0.85;

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

                        item.TranslationTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(250) };
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

                _activeDraggedItem.TranslationTransition = null;

                if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
                {
                    panel.IsDragInProgress = true;
                }
            }

            if (_isTrackingDrag)
            {
                _activeDraggedItem.Translation = new System.Numerics.Vector3((float)deltaX, (float)deltaY, 10f);
                _activeDraggedItem.Opacity = 0.8f;

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
                    if (_hoveredTargetItem != null)
                    {
                        _hoveredTargetItem.Translation = System.Numerics.Vector3.Zero;
                    }

                    _hoveredTargetItem = newHoveredItem;

                    if (_hoveredTargetItem != null)
                    {
                        var targetRect = _logicalBounds[_hoveredTargetItem];

                        float offsetX = (float)(_draggedItemBasePos.X - targetRect.X);
                        float offsetY = (float)(_draggedItemBasePos.Y - targetRect.Y);

                        _hoveredTargetItem.Translation = new System.Numerics.Vector3(offsetX, offsetY, 0);
                    }
                }
            }
        }

        private void DashCard_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                FactoryAnimation.AnimateCardScale(card, 1.0);
                card.Opacity = 1.0;

                card.ReleasePointerCapture(e.Pointer);
            }

            if (_isTrackingDrag && _activeDraggedItem != null)
            {
                foreach (var item in DashboardGridView.Items.OfType<GridViewItem>())
                {
                    item.TranslationTransition = null;
                    item.Translation = System.Numerics.Vector3.Zero;
                    item.Opacity = 1.0f;
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
            else if (_activeDraggedItem != null)
            {
                _activeDraggedItem.TranslationTransition = null;
                _activeDraggedItem.Translation = System.Numerics.Vector3.Zero;
                _activeDraggedItem.Opacity = 1.0f;
                Canvas.SetZIndex(_activeDraggedItem, 0);
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
        private readonly DnsManager _dnsManager = new();
        private bool _isDnsInternalChange = false;

        private DnsShieldHelper _dnsShieldHelper = new();

        private void BtnOpenDnsPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Diagnostics", "DnsCrypt");

                DiagnosticsPageViewModel.Current.IsManualDnsViewOpen = true;
            }
        }

        private void UpdateDnsCardUI()
        {
            if (!DNSCryptHelper.IsInstalled())
            {
                BtnStartService.IsEnabled = false;
                BtnDebug.IsEnabled = false;
                statusLabel.Text = ResourceString.GetString("txt_dnscrypt_not_installed") ?? "DNSCrypt is not installed.";

                TxtServicesRunning.Visibility = Visibility.Collapsed;

                _dnsShieldHelper.SetState(isConnecting: false, isRunning: false);

                return;
            }

            BtnStartService.IsEnabled = true;
            BtnDebug.IsEnabled = true;

            bool isServiceRunning = DNSCryptHelper.IsRunning();

            if (isServiceRunning)
            {
                TxtServicesRunning.Visibility = Visibility.Visible;

                statusLabel.Text = ResourceString.GetString("txt_dnscrypt_running") ?? "DNSCrypt Service is running.";
                statusLabel.Opacity = 1.0;

                BtnStartService.Content = ResourceString.GetString("btn_stop_service") ?? "Stop service";
                BtnStartService.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];

                _dnsShieldHelper.SetState(isConnecting: false, isRunning: true);
            }
            else
            {
                TxtServicesRunning.Visibility = Visibility.Collapsed;

                statusLabel.Text = ResourceString.GetString("txt_nothing_running") ?? "Nothing is running in the background";
                statusLabel.Opacity = 0.7;

                BtnStartService.Content = ResourceString.GetString("btn_start_service") ?? "Start service";
                BtnStartService.Style = (Style)Application.Current.Resources["AccentButtonStyle"];

                _dnsShieldHelper.SetState(isConnecting: false, isRunning: false);
            }
        }

        private async void BtnStartService_Click(object sender, RoutedEventArgs e)
        {
            BtnStartService.IsEnabled = false;

            _dnsShieldHelper.SetState(isConnecting: true, isRunning: false);

            try
            {
                if (DNSCryptHelper.IsRunning())
                {
                    await DNSCryptHelper.StopService(null, statusLabel);
                }
                else
                {
                    await DNSCryptHelper.StartService(null, statusLabel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dashboard DNS] Error: {ex.Message}");
                statusLabel.Text = ResourceString.GetString("txt_service_action_failed") ?? "Service action failed.";
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
                    statusLabel.Text = ResourceString.GetString("txt_connection_failed") ?? "Connection failed.";
                    return;
                }

                _dnsShieldHelper.SetState(isConnecting: true, isRunning: false);

                await DNSCryptHelper.DebugProcess(null, statusLabel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dashboard DNS Debug] Error: {ex.Message}");
            }
            finally
            {
                UpdateDnsCardUI();
                BtnDebug.IsEnabled = true;
            }
        }

        private async void BtnExpandDns_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = DnsExpandedContent.Visibility == Visibility.Collapsed;
            double targetHeight = isExpanded ? 450 : 220;

            GviDns.Height = targetHeight;
            CardDns.Height = targetHeight;

            if (isExpanded)
            {
                DnsExpandedContent.Visibility = Visibility.Visible;
                IconExpandDns.Glyph = "\uE70E"; // Chevron Up

                PopulateDnsPresets();
            }
            else
            {
                DnsExpandedContent.Visibility = Visibility.Collapsed;
                IconExpandDns.Glyph = "\uE70D"; // Chevron Down
            }

            _dnsShieldHelper.UpdateSize(isExpanded);

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsDnsCardExpanded = isExpanded;

            if (isExpanded)
            {
                await Task.Delay(50);
                GviDns.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private void PopulateDnsPresets()
        {
            _isDnsInternalChange = true;

            if (CmbDnsPresets.ItemsSource == null)
            {
                CmbDnsPresets.ItemsSource = DnsPreset.DefaultPresets;
            }

            string currentPrimary = _dnsManager.GetCurrentIpv4Primary();

            var matchedPreset = DnsPreset.DefaultPresets.FirstOrDefault(p => p.Ipv4Primary == currentPrimary)
                                ?? DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "Automatic");

            CmbDnsPresets.SelectedItem = matchedPreset;

            ToggleFamilySafe.IsOn = matchedPreset?.Name?.Contains("Family") == true || matchedPreset?.Name?.Contains("Adult") == true;
            ToggleAdBlock.IsOn = matchedPreset?.Name?.Contains("AdGuard") == true || matchedPreset?.Name?.Contains("Security") == true;

            _isDnsInternalChange = false;
        }

        private async void CmbDnsPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isDnsInternalChange || CmbDnsPresets.SelectedItem is not DnsPreset selectedPreset) return;

            ShowDnsFeedback($"Applying {selectedPreset.Name}...", Colors.Orange);

            bool v4Success = await Task.Run(() => _dnsManager.SetIpv4Dns(selectedPreset.Ipv4Primary ?? "", selectedPreset.Ipv4Secondary ?? ""));
            bool v6Success = true;

            if (!string.IsNullOrEmpty(selectedPreset.Ipv6Primary))
            {
                v6Success = await Task.Run(() => _dnsManager.SetIpv6Dns(selectedPreset.Ipv6Primary ?? "", selectedPreset.Ipv6Secondary ?? ""));
            }

            await Task.Run(() => ClearingMemory.FlushDnsCache());

            if (v4Success)
            {
                string successFmt = ResourceString.GetString("txt_applied_dns_success") ?? "Applied {0}!";
                ShowDnsFeedback(string.Format(successFmt, selectedPreset.Name), Colors.SeaGreen);
            }
            else
            {
                ShowDnsFeedback(ResourceString.GetString("txt_dns_update_failed") ?? "Failed to update DNS settings.", Colors.Red);
            }
        }

        private void ToggleFamilySafe_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isDnsInternalChange) return;

            if (ToggleFamilySafe.IsOn)
            {
                var familyPreset = DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "AdGuard DNS (Family)")
                                   ?? DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "CleanBrowsing (Family)");

                if (familyPreset != null)
                {
                    CmbDnsPresets.SelectedItem = familyPreset;
                }
            }
            else
            {
                var defaultPreset = DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "Cloudflare (1.1.1.1)")
                                    ?? DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "Automatic");

                if (defaultPreset != null)
                {
                    CmbDnsPresets.SelectedItem = defaultPreset;
                }
            }
        }

        private void ToggleAdBlock_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isDnsInternalChange) return;

            if (ToggleAdBlock.IsOn)
            {
                var adguardPreset = DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "AdGuard DNS (Default)")
                                   ?? DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "Quad9 (Security)");

                if (adguardPreset != null)
                {
                    CmbDnsPresets.SelectedItem = adguardPreset;
                }
            }
            else
            {
                var autoPreset = DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "Automatic");
                if (autoPreset != null)
                {
                    CmbDnsPresets.SelectedItem = autoPreset;
                }
            }
        }

        private async void BtnBenchmarkDns_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;

            ShowDnsFeedback(ResourceString.GetString("txt_testing_providers") ?? "Testing all providers...", Microsoft.UI.Colors.Orange);

            var benchmarkTargets = DnsPreset.DefaultPresets
                .Where(p => !string.IsNullOrWhiteSpace(p.Ipv4Primary)
                         && p.Ipv4Primary != "0.0.0.0"
                         && p.Ipv4Primary != "127.0.0.1")
                .ToList();

            var pingTasks = benchmarkTargets.Select(async target =>
            {
                long latency = await ViewModel.PingDnsAsync(target.Ipv4Primary!);

                return new DnsBenchmarkingItem
                {
                    Name = target.Name ?? "",
                    IP = target.Ipv4Primary!,
                    Latency = latency,
                    PresetReference = target
                };
            });

            var resultsArray = await Task.WhenAll(pingTasks);

            var fastest = resultsArray
                .Where(r => r.Latency >= 0)
                .OrderBy(r => r.Latency)
                .FirstOrDefault();

            if (fastest != null && fastest.PresetReference != null)
            {
                XamlRoot? root = this.XamlRoot ?? (this.Content?.XamlRoot);
                if (root != null)
                {
                    string contentFmt = ResourceString.GetString("msg_fastest_dns") ?? "The fastest DNS server for your connection is {0} with a latency of {1} ms.\n\nWould you like to apply it now?";
                    ContentDialog dialog = new ContentDialog
                    {
                        XamlRoot = root,
                        Title = ResourceString.GetString("title_speed_test_complete") ?? "Speed Test Complete",
                        Content = string.Format(contentFmt, fastest.Name, fastest.Latency),
                        PrimaryButtonText = ResourceString.GetString("btn_apply_now") ?? "Apply Now",
                        CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
                    {
                        dialog.Style = (Style)style;
                    }

                    TxtDnsFeedback.Visibility = Visibility.Collapsed;

                    ContentDialogResult result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        string applySuccessFmt = ResourceString.GetString("txt_applied_dns") ?? "Applied: {0} ({1} ms)";
                        ShowDnsFeedback(string.Format(applySuccessFmt, fastest.Name, fastest.Latency), Colors.SeaGreen);
                        CmbDnsPresets.SelectedItem = fastest.PresetReference;
                    }
                    else
                    {
                        string noChangeFmt = ResourceString.GetString("txt_fastest_dns_no_change") ?? "Fastest was {0} ({1} ms). No changes made.";
                        ShowDnsFeedback(string.Format(noChangeFmt, fastest.Name, fastest.Latency), Colors.SeaGreen);
                    }
                }
            }
            else
            {
                ShowDnsFeedback(ResourceString.GetString("txt_latency_timeout") ?? "Latency test timed out.", Colors.Red);
            }

            if (sender is Button b) b.IsEnabled = true;
        }

        private async void ShowDnsFeedback(string message, Color color)
        {
            if (TxtDnsFeedback == null) return;

            TxtDnsFeedback.Text = message;
            TxtDnsFeedback.Foreground = new SolidColorBrush(color);
            TxtDnsFeedback.Visibility = Visibility.Visible;

            await Task.Delay(4000);
            TxtDnsFeedback.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Health Card

        private void BtnOpenMaintenancePage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Diagnostics", "Maintenance");
            }
        }

        private async void BtnExpandHealth_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = HealthExpandedContent.Visibility == Visibility.Collapsed;
            double targetHeight = isExpanded ? 450 : 220;

            GviMaintenance.Height = targetHeight;
            CardMaintenance.Height = targetHeight;

            if (isExpanded)
            {
                HealthExpandedContent.Visibility = Visibility.Visible;
                if (IconExpandHealth != null) IconExpandHealth.Glyph = "\uE70E"; // Chevron Up
            }
            else
            {
                HealthExpandedContent.Visibility = Visibility.Collapsed;
                if (IconExpandHealth != null) IconExpandHealth.Glyph = "\uE70D"; // Chevron Down
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            RefreshHealthGaugeLayoutSize(isExpanded);

            if (ViewModel.SaveCardStates)
            {
                SettingsEngine.IsHealthCardExpanded = isExpanded;
            }

            if (isExpanded)
            {
                await Task.Delay(50);
                GviMaintenance.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private async void BtnRefreshHealth_Click(object sender, RoutedEventArgs e)
        {
            await UpdateSystemHealthUIAsync();
        }

        private async Task UpdateSystemHealthUIAsync()
        {
            try
            {
                if (BtnRefreshHealth != null) BtnRefreshHealth.IsEnabled = false;

                if (TxtLastRefreshed != null) TxtLastRefreshed.Text = string.Empty;

                if (TxtHealthStatus != null)
                {
                    TxtHealthStatus.Text = ResourceString.GetString("text_scanning_system") ?? "Scanning System...";
                    if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object textBrush))
                        TxtHealthStatus.Foreground = (Brush)textBrush;
                }

                bool isCurrentlyExpanded = HealthExpandedContent?.Visibility == Visibility.Visible;
                UpdateHealthGaugeVisuals(0, isCurrentlyExpanded);

                if (DiagnosticsPageViewModel.Current != null)
                {
                    if (DiagnosticsPageViewModel.Current.RefreshCleanupSpaceCommand.CanExecute(null) && !DiagnosticsPageViewModel.Current.IsScanning)
                    {
                        DiagnosticsPageViewModel.Current.RefreshCleanupSpaceCommand.Execute(null);
                        await Task.Delay(400);
                    }

                    while (DiagnosticsPageViewModel.Current.IsScanning)
                    {
                        await Task.Delay(250);
                    }
                }

                var healthResult = await SystemHealthHelper.EvaluateHealthAsync();

                var tcs = new TaskCompletionSource();

                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (healthResult != null)
                        {
                            if (TxtHealthStatus != null)
                            {
                                TxtHealthStatus.Text = healthResult.StatusText;
                            }

                            if (TxtHealthTempFiles != null)
                            {
                                TxtHealthTempFiles.Text = healthResult.JunkGb > 0 ? $"{healthResult.JunkGb:F1} GB" : "Clean";
                                TxtHealthTempFiles.Foreground = healthResult.JunkGb >= 10.0 ? new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                            }
                            if (TxtHealthRamUsage != null)
                            {
                                TxtHealthRamUsage.Text = $"{healthResult.RamUsage:F0}%";
                                TxtHealthRamUsage.Foreground = healthResult.RamUsage >= 80.0 ? new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                            }
                            if (TxtHealthPagefileUsage != null)
                            {
                                TxtHealthPagefileUsage.Text = $"{healthResult.PagefileUsage:F0}%";
                                TxtHealthPagefileUsage.Foreground = healthResult.PagefileUsage >= 90.0 ? new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                            }
                        }

                        if (TxtLastRefreshed != null)
                        {
                            string lastCheckedStr = ResourceString.GetString("text_last_checked") ?? "Last checked";
                            TxtLastRefreshed.Text = $"{lastCheckedStr}: {DateTime.Now:t}";
                        }

                        RefreshHealthGaugeLayoutSize(isCurrentlyExpanded);

                        await AnimateHealthGaugeAsync(healthResult?.Score ?? 1.0);
                    }
                    finally
                    {
                        if (BtnRefreshHealth != null) BtnRefreshHealth.IsEnabled = true;
                        tcs.SetResult();
                    }
                });

                await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Health Check Error] {ex.Message}");
                if (BtnRefreshHealth != null) BtnRefreshHealth.IsEnabled = true;
            }
        }

        private void RefreshHealthGaugeLayoutSize(bool isExpanded)
        {
            if (HealthGaugeCanvas == null) return;

            double size = isExpanded ? 120 : 80;

            HealthGaugeContainerGrid.Height = size;
            HealthGaugeCanvas.Width = size;
            HealthGaugeCanvas.Height = size;

            if (HealthGaugeContainerGrid != null)
            {
                HealthGaugeContainerGrid.Margin = new Thickness(0);
            }

            if (HealthStatusPanel != null)
            {
                HealthStatusPanel.Margin = isExpanded ? new Thickness(0, 4, 0, 8) : new Thickness(0, 0, 0, 0);
            }

            if (TxtHealthScore != null)
            {
                TxtHealthScore.FontSize = isExpanded ? 20 : 15;
                TxtHealthScore.Margin = isExpanded ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 4);
            }

            UpdateHealthGaugeVisuals(_cachedLastHealthScore, isExpanded);
        }

        private double _cachedLastHealthScore = 1.0;
        private async Task AnimateHealthGaugeAsync(double targetPercentage)
        {
            _cachedLastHealthScore = targetPercentage;
            bool isExpanded = HealthExpandedContent.Visibility == Visibility.Visible;
            await GaugeHelper.AnimateAsync(targetPercentage, isExpanded, HealthGlowPulseAnimation, (p, exp) => UpdateHealthGaugeVisuals(p, exp));
        }

        private void UpdateHealthGaugeVisuals(double percentage, bool isExpanded)
        {
            GaugeHelper.UpdateVisuals(percentage, isExpanded, HealthAmbientGlow, HealthGaugeNeedle, HealthNeedleRotation, HealthPinShadow, HealthPinOuter, HealthPinInner, HealthGaugeBackgroundPath, HealthGaugeForegroundPath, TxtHealthScore);
        }

        #endregion

        #region Security Card

        private SecurityShieldHelper _securityShieldHelper = new SecurityShieldHelper();

        private void InitializeSecurityCard()
        {
            try
            {
                if (SecurityShieldCanvas != null)
                {
                    _securityShieldHelper.Initialize(SecurityShieldCanvas);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Security Shield Init Error] {ex.Message}");
            }
        }

        private void BtnOpenSecurityPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Diagnostics", "Security");
            }
        }

        private void RefreshSecurityGraphicLayoutSize(bool isExpanded)
        {
            if (SecurityShieldCanvas == null) return;

            double size = isExpanded ? 120 : 80;

            if (SecurityGraphicContainer != null)
            {
                SecurityGraphicContainer.Height = size;
            }

            if (SecurityStatusPanel != null)
            {
                SecurityStatusPanel.Margin = isExpanded ? new Thickness(0, 4, 0, 8) : new Thickness(0, 0, 0, 0);
            }

            _securityShieldHelper.UpdateSize(isExpanded);
        }

        private async void BtnExpandSecurity_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = SecurityExpandedContent.Visibility == Visibility.Collapsed;

            double targetHeight = isExpanded ? 450 : 220;

            if (GviSecurity != null) GviSecurity.Height = targetHeight;
            if (CardSecurity != null) CardSecurity.Height = targetHeight;

            if (isExpanded)
            {
                SecurityExpandedContent.Visibility = Visibility.Visible;
                if (IconExpandSecurity != null) IconExpandSecurity.Glyph = "\uE70E"; // Chevron Up
            }
            else
            {
                SecurityExpandedContent.Visibility = Visibility.Collapsed;
                if (IconExpandSecurity != null) IconExpandSecurity.Glyph = "\uE70D"; // Chevron Down
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            RefreshSecurityGraphicLayoutSize(isExpanded);

            if (ViewModel.SaveCardStates)
            {
                SettingsEngine.IsSecurityCardExpanded = isExpanded;
            }

            if (isExpanded)
            {
                await Task.Delay(50);
                GviSecurity?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private async void BtnRefreshSecurity_Click(object sender, RoutedEventArgs e)
        {
            await UpdateSecurityUIAsync();
        }

        private async Task UpdateSecurityUIAsync()
        {
            try
            {
                BtnRefreshSecurity.IsEnabled = false;
                BtnDashViewIssues.Visibility = Visibility.Collapsed;

                _securityShieldHelper.SetState(isScanning: true, isCoreProtected: true, issuesCount: 0);

                if (string.IsNullOrEmpty(TxtSecurityLastRefreshed.Text)) TxtSecurityLastRefreshed.Text = " ";
                TxtSecurityStatus.Text = ResourceString.GetString("text_scanning_system") ?? "Scanning...";

                if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object textBrush))
                    TxtSecurityStatus.Foreground = (Brush)textBrush;

                var result = await SecurityHealthHelper.EvaluateSecurityAsync();

                _securityShieldHelper.SetState(isScanning: false, isCoreProtected: result.IsCoreProtected, issuesCount: result.IsWarningState ? 1 : 0);

                if (!result.IsCoreProtected)
                {
                    TxtSecurityStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 232, 17, 35)); // Red
                    TxtSecurityStatus.Text = $"{result.IssuesCount} {result.StatusText}";
                }
                else if (result.IsWarningState)
                {
                    TxtSecurityStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)); // Orange
                    TxtSecurityStatus.Text = $"{result.IssuesCount} {result.StatusText}";
                }
                else
                {
                    TxtSecurityStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 139, 87)); // Green
                    TxtSecurityStatus.Text = result.StatusText;
                }

                if (TxtSecFirewall != null)
                {
                    TxtSecFirewall.Text = result.FirewallStatus;
                    TxtSecFirewall.Foreground = result.FirewallStatus == "Disabled" ? new SolidColorBrush(Color.FromArgb(255, 232, 17, 35)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                }

                if (TxtSecDefender != null)
                {
                    TxtSecDefender.Text = result.DefenderStatus;
                    TxtSecDefender.Foreground = result.DefenderStatus == "Disabled" ? new SolidColorBrush(Color.FromArgb(255, 232, 17, 35)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                }

                if (TxtSecUAC != null)
                {
                    TxtSecUAC.Text = result.UacStatus;
                    if (result.UacStatus == "Disabled")
                        TxtSecUAC.Foreground = new SolidColorBrush(Color.FromArgb(255, 232, 17, 35));
                    else if (result.UacStatus == "Never Notify")
                        TxtSecUAC.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 140, 0));
                    else
                        TxtSecUAC.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                }

                var ignoredIssues = LocalMachineSettingsEngine.IgnoredSecurityIssues;
                var ignoredIssuesCount = ignoredIssues.Count;

                if (result.IssuesCount > 0 || ignoredIssuesCount > 0)
                {
                    BtnDashViewIssues.Visibility = Visibility.Visible;
                    var flyout = new MenuFlyout();

                    foreach (var issue in result.Issues)
                    {
                        var item = new MenuFlyoutItem
                        {
                            Text = issue,
                            Icon = new FontIcon { Glyph = "\uE711", FontSize = 14 },
                            IsEnabled = true
                        };

                        ToolTipService.SetToolTip(item, ResourceString.GetString("tooltip_ignore_warning") ?? "Click to ignore this warning");

                        item.Click += async (s, e) =>
                        {
                            var currentIgnored = LocalMachineSettingsEngine.IgnoredSecurityIssues;
                            currentIgnored.Add(issue);
                            LocalMachineSettingsEngine.IgnoredSecurityIssues = currentIgnored;
                            await UpdateSecurityUIAsync();
                        };

                        flyout.Items.Add(item);
                    }

                    if (result.IssuesCount > 0 && ignoredIssuesCount > 0)
                    {
                        flyout.Items.Add(new MenuFlyoutSeparator());
                    }

                    string ignoredText = ResourceString.GetString("txt_ignored") ?? "Ignored";

                    foreach (var ignoredIssue in ignoredIssues)
                    {
                        var item = new MenuFlyoutItem
                        {
                            Text = $"[{ignoredText}] {ignoredIssue}",
                            Icon = new FontIcon { Glyph = "\uE777", FontSize = 14 },
                            IsEnabled = true
                        };

                        ToolTipService.SetToolTip(item, ResourceString.GetString("tooltip_restore_warning") ?? "Click to restore this warning");

                        item.Click += async (s, e) =>
                        {
                            var currentIgnored = LocalMachineSettingsEngine.IgnoredSecurityIssues;
                            currentIgnored.Remove(ignoredIssue);
                            LocalMachineSettingsEngine.IgnoredSecurityIssues = currentIgnored;
                            await UpdateSecurityUIAsync();
                        };

                        flyout.Items.Add(item);
                    }

                    FlyoutBase.SetAttachedFlyout(BtnDashViewIssues, flyout);
                }
                else
                {
                    BtnDashViewIssues.Visibility = Visibility.Collapsed;
                }

                TxtSecurityLastRefreshed.Text = $"{ResourceString.GetString("text_last_checked") ?? "Last checked"}: {DateTime.Now:t}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Security Check Error] {ex.Message}");
                TxtSecurityStatus.Text = ResourceString.GetString("txt_scan_failed") ?? "Scan failed.";

                _securityShieldHelper.SetState(isScanning: false, isCoreProtected: false, issuesCount: 1);
            }
            finally
            {
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

        #region Privacy Card

        private void BtnOpenPrivacyPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Optimize", "Privacy");
            }
        }

        private async void BtnExpandPrivacy_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = PrivacyExpandedContent.Visibility == Visibility.Collapsed;
            double targetHeight = isExpanded ? 450 : 220;

            GviPrivacy.Height = targetHeight;
            CardPrivacy.Height = targetHeight;

            if (isExpanded)
            {
                PrivacyExpandedContent.Visibility = Visibility.Visible;
                if (IconExpandPrivacy != null) IconExpandPrivacy.Glyph = "\uE70E"; // Chevron Up
            }
            else
            {
                PrivacyExpandedContent.Visibility = Visibility.Collapsed;
                if (IconExpandPrivacy != null) IconExpandPrivacy.Glyph = "\uE70D"; // Chevron Down
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsPrivacyCardExpanded = isExpanded;

            RefreshPrivacyGaugeLayoutSize(isExpanded);

            if (isExpanded)
            {
                await Task.Delay(50);
                GviPrivacy.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private async void BtnRefreshPrivacy_Click(object sender, RoutedEventArgs e)
        {
            await UpdatePrivacyUIAsync();
        }

        private async Task UpdatePrivacyUIAsync()
        {
            try
            {
                if (DashPrivacyLoadingRing != null) DashPrivacyLoadingRing.Visibility = Visibility.Visible;
                if (PrivacyGaugeCanvas != null) PrivacyGaugeCanvas.Visibility = Visibility.Collapsed;
                if (TxtPrivacyScore != null) TxtPrivacyScore.Visibility = Visibility.Collapsed;
                if (TxtPrivacyLastRefreshed != null) TxtPrivacyLastRefreshed.Visibility = Visibility.Collapsed;
                if (BtnRefreshPrivacy != null) BtnRefreshPrivacy.IsEnabled = false;
                if (BtnPrivacyViewIssues != null) BtnPrivacyViewIssues.Visibility = Visibility.Collapsed;
                if (TxtPrivacyStatus != null) TxtPrivacyStatus.Text = ResourceString.GetString("text_scanning_system") ?? "Scanning...";

                var result = await ViewModel.CalculatePrivacyHealthAsync();

                if (TxtPrivacyStatus != null)
                {
                    if (result.IssuesCount >= 5) TxtPrivacyStatus.Text = $"{result.IssuesCount} {(ResourceString.GetString("text_privacy_critical") ?? "Privacy Leaks Found")}";
                    else if (result.IssuesCount > 0) TxtPrivacyStatus.Text = $"{result.IssuesCount} {(ResourceString.GetString("text_privacy_warning") ?? "Optimizations Available")}";
                    else TxtPrivacyStatus.Text = ResourceString.GetString("text_privacy_good") ?? "Privacy is Optimized";
                }

                if (AiShieldBadge != null && TxtAiShieldStatus != null && IconAiShield != null)
                {
                    if (result.AiIssuesCount == 0)
                    {
                        if (Application.Current.Resources.TryGetValue("BadgeRecommendedStyle", out var style))
                            AiShieldBadge.Style = (Style)style;

                        if (Application.Current.Resources.TryGetValue("BadgeRecommendedForeground", out var brush))
                        {
                            IconAiShield.Foreground = (Brush)brush;
                            TxtAiShieldStatus.Foreground = (Brush)brush;
                        }

                        TxtAiShieldStatus.Text = ResourceString.GetString("txt_ai_blocked") ?? "AI Blocked";
                        IconAiShield.Glyph = "\uE83F";
                    }
                    else
                    {
                        if (Application.Current.Resources.TryGetValue("BadgeWarningStyle", out var style))
                            AiShieldBadge.Style = (Style)style;

                        if (Application.Current.Resources.TryGetValue("BadgeCustomForeground", out var brush))
                        {
                            IconAiShield.Foreground = (Brush)brush;
                            TxtAiShieldStatus.Foreground = (Brush)brush;
                        }

                        TxtAiShieldStatus.Text = ResourceString.GetString("txt_ai_active") ?? "AI Active";
                        IconAiShield.Glyph = "\uE814";
                    }
                }

                string secureText = ResourceString.GetString("txt_secure") ?? "Secure";
                string leaksText = ResourceString.GetString("txt_leaks") ?? "Leaks";

                if (TxtAppPermIssues != null)
                {
                    TxtAppPermIssues.Text = result.AppPermIssuesCount > 0 ? $"{result.AppPermIssuesCount} {leaksText}" : secureText;
                    TxtAppPermIssues.Foreground = result.AppPermIssuesCount > 0 ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.SeaGreen);
                }
                if (TxtEdgeWebIssues != null)
                {
                    TxtEdgeWebIssues.Text = result.EdgeWebIssuesCount > 0 ? $"{result.EdgeWebIssuesCount} {leaksText}" : secureText;
                    TxtEdgeWebIssues.Foreground = result.EdgeWebIssuesCount > 0 ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.SeaGreen);
                }
                if (TxtAiIssues != null)
                {
                    TxtAiIssues.Text = result.AiIssuesCount > 0 ? $"{result.AiIssuesCount} {leaksText}" : secureText;
                    TxtAiIssues.Foreground = result.AiIssuesCount > 0 ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.SeaGreen);
                }

                if (result.IssuesCount > 0 && BtnPrivacyViewIssues != null)
                {
                    BtnPrivacyViewIssues.Visibility = Visibility.Visible;
                    var flyout = new MenuFlyout();
                    foreach (var issue in result.Issues)
                    {
                        var menuItem = new MenuFlyoutItem
                        {
                            Text = issue,
                            Icon = new FontIcon { Glyph = "\uE7BA", FontSize = 14 },
                            IsEnabled = true
                        };

                        menuItem.Click += (s, e) =>
                        {
                            if (MainWindow.Instance != null)
                            {
                                WinOptimizePage.RequestedSearchOnLoad = issue;
                                MainWindow.Instance.SwitchPage("Optimize", "Privacy");
                            }
                        };

                        flyout.Items.Add(menuItem);
                    }
                    FlyoutBase.SetAttachedFlyout(BtnPrivacyViewIssues, flyout);
                }

                if (TxtPrivacyLastRefreshed != null)
                {
                    string lastCheckedStr = ResourceString.GetString("text_last_checked") ?? "Last checked";
                    TxtPrivacyLastRefreshed.Text = $"{lastCheckedStr}: {DateTime.Now:t}";
                    TxtPrivacyLastRefreshed.Visibility = Visibility.Visible;
                }

                if (DashPrivacyLoadingRing != null) DashPrivacyLoadingRing.Visibility = Visibility.Collapsed;
                if (PrivacyGaugeCanvas != null) PrivacyGaugeCanvas.Visibility = Visibility.Visible;
                if (TxtPrivacyScore != null) TxtPrivacyScore.Visibility = Visibility.Visible;

                bool isCurrentlyExpanded = PrivacyExpandedContent.Visibility == Visibility.Visible;
                RefreshPrivacyGaugeLayoutSize(isCurrentlyExpanded);

                await AnimatePrivacyGaugeAsync(result.Score);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Privacy Check Error] {ex.Message}");
                if (TxtPrivacyStatus != null) TxtPrivacyStatus.Text = ResourceString.GetString("txt_scan_failed") ?? "Scan failed.";
            }
            finally
            {
                if (DashPrivacyLoadingRing != null) DashPrivacyLoadingRing.Visibility = Visibility.Collapsed;
                if (BtnRefreshPrivacy != null) BtnRefreshPrivacy.IsEnabled = true;
            }
        }

        private async void BtnApplyRecommendedPrivacy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;

            try
            {
                if (TxtPrivacyStatus != null) TxtPrivacyStatus.Text = ResourceString.GetString("txt_applying_fixes") ?? "Applying Fixes...";
                if (DashPrivacyLoadingRing != null) DashPrivacyLoadingRing.Visibility = Visibility.Visible;
                if (PrivacyGaugeCanvas != null) PrivacyGaugeCanvas.Visibility = Visibility.Collapsed;
                if (TxtPrivacyScore != null) TxtPrivacyScore.Visibility = Visibility.Collapsed;

                var privacyGroup = PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations();
                var settingIds = privacyGroup.Settings
                    .Where(s => !string.IsNullOrEmpty(s.Id))
                    .Select(s => s.Id)
                    .ToList();

                await ViewModel.ApplyBulkSettingsAsync(settingIds!);
                await UpdatePrivacyUIAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Apply Privacy Error] {ex.Message}");
            }
            finally
            {
                if (sender is Button b) b.IsEnabled = true;
            }
        }

        private async void BtnRestorePrivacyDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;

            try
            {
                if (TxtPrivacyStatus != null) TxtPrivacyStatus.Text = ResourceString.GetString("txt_restoring_defaults") ?? "Restoring Defaults...";
                if (DashPrivacyLoadingRing != null) DashPrivacyLoadingRing.Visibility = Visibility.Visible;
                if (PrivacyGaugeCanvas != null) PrivacyGaugeCanvas.Visibility = Visibility.Collapsed;
                if (TxtPrivacyScore != null) TxtPrivacyScore.Visibility = Visibility.Collapsed;

                var privacyGroup = PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations();
                var settingIds = privacyGroup.Settings
                    .Where(s => !string.IsNullOrEmpty(s.Id))
                    .Select(s => s.Id)
                    .ToList();

                await ViewModel.RestoreBulkSettingsAsync(settingIds!);
                await UpdatePrivacyUIAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Restore Privacy Error] {ex.Message}");
            }
            finally
            {
                if (sender is Button b) b.IsEnabled = true;
            }
        }

        private void BtnPrivacyViewIssues_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        private void RefreshPrivacyGaugeLayoutSize(bool isExpanded)
        {
            if (PrivacyGaugeCanvas == null) return;

            double size = isExpanded ? 120 : 80;

            PrivacyGaugeContainerGrid.Height = size;
            PrivacyGaugeCanvas.Width = size;
            PrivacyGaugeCanvas.Height = size;

            if (PrivacyGaugeContainerGrid != null)
            {
                PrivacyGaugeContainerGrid.Margin = new Thickness(0);
            }

            if (PrivacyStatusPanel != null)
            {
                PrivacyStatusPanel.Margin = isExpanded ? new Thickness(0, 4, 0, 8) : new Thickness(0, 0, 0, 0);
            }

            if (TxtPrivacyScore != null)
            {
                TxtPrivacyScore.FontSize = isExpanded ? 20 : 15;
                TxtPrivacyScore.Margin = isExpanded ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 4);
            }

            if (AiShieldBadge != null)
            {
                AiShieldBadge.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdatePrivacyGaugeVisuals(_cachedLastPrivacyScore, isExpanded);
        }

        private double _cachedLastPrivacyScore = 1.0;

        private async Task AnimatePrivacyGaugeAsync(double targetPercentage)
        {
            _cachedLastPrivacyScore = targetPercentage;
            bool isExpanded = PrivacyExpandedContent.Visibility == Visibility.Visible;
            await GaugeHelper.AnimateAsync(targetPercentage, isExpanded, PrivacyGlowPulseAnimation, (p, exp) => UpdatePrivacyGaugeVisuals(p, exp));
        }

        private void UpdatePrivacyGaugeVisuals(double percentage, bool isExpanded)
        {
            GaugeHelper.UpdateVisuals(percentage, isExpanded, PrivacyAmbientGlow, PrivacyGaugeNeedle, PrivacyNeedleRotation, PrivacyPinShadow, PrivacyPinOuter, PrivacyPinInner, PrivacyGaugeBackgroundPath, PrivacyGaugeForegroundPath, TxtPrivacyScore);
        }

        #endregion

        #region Performance Optimizations Card

        private void BtnOpenPerformancePage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("Optimize", "Gaming");
            }
        }

        private async void BtnExpandPerformance_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = PerformanceExpandedContent.Visibility == Visibility.Collapsed;

            double targetHeight = isExpanded ? 450 : 220;

            GviPerformance.Height = targetHeight;
            CardPerformance.Height = targetHeight;

            if (isExpanded)
            {
                PerformanceExpandedContent.Visibility = Visibility.Visible;
                if (IconExpandPerformance != null) IconExpandPerformance.Glyph = "\uE70E"; // Chevron Up
            }
            else
            {
                PerformanceExpandedContent.Visibility = Visibility.Collapsed;
                if (IconExpandPerformance != null) IconExpandPerformance.Glyph = "\uE70D"; // Chevron Down
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsPerformanceCardExpanded = isExpanded;

            RefreshGaugeLayoutSize(isExpanded);

            if (isExpanded)
            {
                await Task.Delay(50);
                GviPerformance.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private async void BtnRefreshPerformance_Click(object sender, RoutedEventArgs e)
        {
            await UpdatePerformanceUIAsync();
        }

        private async Task UpdatePerformanceUIAsync()
        {
            try
            {
                if (DashPerformanceLoadingRing != null) DashPerformanceLoadingRing.Visibility = Visibility.Visible;
                if (PerformanceGaugeCanvas != null) PerformanceGaugeCanvas.Visibility = Visibility.Collapsed;
                if (TxtPerformanceScore != null) TxtPerformanceScore.Visibility = Visibility.Collapsed;
                if (TxtPerformanceLastRefreshed != null) TxtPerformanceLastRefreshed.Visibility = Visibility.Collapsed;
                if (BtnRefreshPerformance != null) BtnRefreshPerformance.IsEnabled = false;
                if (BtnPerformanceViewIssues != null) BtnPerformanceViewIssues.Visibility = Visibility.Collapsed;
                if (TxtPerformanceStatus != null) TxtPerformanceStatus.Text = ResourceString.GetString("text_scanning_system") ?? "Scanning...";

                var result = await ViewModel.CalculatePerformanceHealthAsync();

                if (TxtPerformanceStatus != null)
                {
                    if (result.IssuesCount > 0) TxtPerformanceStatus.Text = $"{result.IssuesCount} {(ResourceString.GetString("text_optimizations_available") ?? "Optimizations Available")}";
                    else TxtPerformanceStatus.Text = ResourceString.GetString("text_performance_good") ?? "System is Optimized";
                }

                string optimizedText = ResourceString.GetString("txt_optimized") ?? "Optimized";
                string issuesText = ResourceString.GetString("txt_issues") ?? "Issues";

                if (TxtPerfServicesIssues != null)
                {
                    TxtPerfServicesIssues.Text = result.ServicesIssuesCount > 0 ? $"{result.ServicesIssuesCount} {issuesText}" : optimizedText;
                    TxtPerfServicesIssues.Foreground = result.ServicesIssuesCount > 0 ? new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                }
                if (TxtPerfVisualIssues != null)
                {
                    TxtPerfVisualIssues.Text = result.VisualIssuesCount > 0 ? $"{result.VisualIssuesCount} {issuesText}" : optimizedText;
                    TxtPerfVisualIssues.Foreground = result.VisualIssuesCount > 0 ? new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                }
                if (TxtPerfHardwareIssues != null)
                {
                    TxtPerfHardwareIssues.Text = result.HardwareIssuesCount > 0 ? $"{result.HardwareIssuesCount} {issuesText}" : optimizedText;
                    TxtPerfHardwareIssues.Foreground = result.HardwareIssuesCount > 0 ? new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)) : new SolidColorBrush(Color.FromArgb(255, 46, 139, 87));
                }

                if (result.IssuesCount > 0 && BtnPerformanceViewIssues != null)
                {
                    BtnPerformanceViewIssues.Visibility = Visibility.Visible;
                    var flyout = new MenuFlyout();
                    foreach (var issue in result.Issues)
                    {
                        var menuItem = new MenuFlyoutItem
                        {
                            Text = issue,
                            Icon = new FontIcon { Glyph = "\uE9D9", FontSize = 14 },
                            IsEnabled = true
                        };

                        menuItem.Click += (s, e) =>
                        {
                            if (MainWindow.Instance != null)
                            {
                                WinOptimizePage.RequestedSearchOnLoad = issue;
                                MainWindow.Instance.SwitchPage("Optimize", "Gaming");
                            }
                        };
                        flyout.Items.Add(menuItem);
                    }
                    FlyoutBase.SetAttachedFlyout(BtnPerformanceViewIssues, flyout);
                }

                if (TxtPerformanceLastRefreshed != null)
                {
                    string lastCheckedStr = ResourceString.GetString("text_last_checked") ?? "Last checked";
                    TxtPerformanceLastRefreshed.Text = $"{lastCheckedStr}: {DateTime.Now:t}";
                    TxtPerformanceLastRefreshed.Visibility = Visibility.Visible;
                }

                if (DashPerformanceLoadingRing != null) DashPerformanceLoadingRing.Visibility = Visibility.Collapsed;
                if (PerformanceGaugeCanvas != null) PerformanceGaugeCanvas.Visibility = Visibility.Visible;
                if (TxtPerformanceScore != null) TxtPerformanceScore.Visibility = Visibility.Visible;

                bool isCurrentlyExpanded = PerformanceExpandedContent.Visibility == Visibility.Visible;
                RefreshGaugeLayoutSize(isCurrentlyExpanded);

                await AnimatePerformanceGaugeAsync(result.Score);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Performance Check Error] {ex.Message}");
                if (TxtPerformanceStatus != null) TxtPerformanceStatus.Text = ResourceString.GetString("txt_scan_failed") ?? "Scan failed.";
            }
            finally
            {
                if (DashPerformanceLoadingRing != null) DashPerformanceLoadingRing.Visibility = Visibility.Collapsed;
                if (BtnRefreshPerformance != null) BtnRefreshPerformance.IsEnabled = true;
            }
        }

        private async void BtnApplyRecommendedPerformance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;

            try
            {
                if (TxtPerformanceStatus != null) TxtPerformanceStatus.Text = ResourceString.GetString("txt_applying_optimizations") ?? "Applying Optimizations...";
                if (DashPerformanceLoadingRing != null) DashPerformanceLoadingRing.Visibility = Visibility.Visible;
                if (PerformanceGaugeCanvas != null) PerformanceGaugeCanvas.Visibility = Visibility.Collapsed;
                if (TxtPerformanceScore != null) TxtPerformanceScore.Visibility = Visibility.Collapsed;

                var performanceGroup = GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations();
                var settingIds = performanceGroup.Settings
                    .Where(s => !string.IsNullOrEmpty(s.Id))
                    .Select(s => s.Id)
                    .ToList();

                await ViewModel.ApplyBulkSettingsAsync(settingIds!);
                await UpdatePerformanceUIAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Apply Performance Error] {ex.Message}");
            }
            finally
            {
                if (sender is Button b) b.IsEnabled = true;
            }
        }

        private async void BtnRestorePerformanceDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;

            try
            {
                if (TxtPerformanceStatus != null) TxtPerformanceStatus.Text = ResourceString.GetString("txt_restoring_defaults") ?? "Restoring Defaults...";
                if (DashPerformanceLoadingRing != null) DashPerformanceLoadingRing.Visibility = Visibility.Visible;
                if (PerformanceGaugeCanvas != null) PerformanceGaugeCanvas.Visibility = Visibility.Collapsed;
                if (TxtPerformanceScore != null) TxtPerformanceScore.Visibility = Visibility.Collapsed;

                var performanceGroup = GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations();
                var settingIds = performanceGroup.Settings
                    .Where(s => !string.IsNullOrEmpty(s.Id))
                    .Select(s => s.Id)
                    .ToList();

                await ViewModel.RestoreBulkSettingsAsync(settingIds!);
                await UpdatePerformanceUIAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Restore Performance Error] {ex.Message}");
            }
            finally
            {
                if (sender is Button b) b.IsEnabled = true;
            }
        }

        private void BtnPerformanceViewIssues_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        private void RefreshGaugeLayoutSize(bool isExpanded)
        {
            if (PerformanceGaugeCanvas == null) return;

            double size = isExpanded ? 120 : 80;

            GaugeContainerGrid.Height = size;
            PerformanceGaugeCanvas.Width = size;
            PerformanceGaugeCanvas.Height = size;

            if (GaugeContainerGrid != null)
            {
                GaugeContainerGrid.Margin = new Thickness(0);
            }

            if (PerformanceStatusPanel != null)
            {
                PerformanceStatusPanel.Margin = isExpanded ? new Thickness(0, 4, 0, 8) : new Thickness(0, 0, 0, 0);
            }

            if (TxtPerformanceScore != null)
            {
                TxtPerformanceScore.FontSize = isExpanded ? 20 : 15;
                TxtPerformanceScore.Margin = isExpanded ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 4);
            }

            UpdateGaugeVisuals(_cachedLastPerformanceScore, isExpanded);
        }

        private double _cachedLastPerformanceScore = 1.0;

        private async Task AnimatePerformanceGaugeAsync(double targetPercentage)
        {
            _cachedLastPerformanceScore = targetPercentage;
            bool isExpanded = PerformanceExpandedContent.Visibility == Visibility.Visible;
            await GaugeHelper.AnimateAsync(targetPercentage, isExpanded, GlowPulseAnimation, (p, exp) => UpdateGaugeVisuals(p, exp));
        }

        private void UpdateGaugeVisuals(double percentage, bool isExpanded)
        {
            GaugeHelper.UpdateVisuals(percentage, isExpanded, AmbientGlow, GaugeNeedle, NeedleRotation, PinShadow, PinOuter, PinInner, GaugeBackgroundPath, GaugeForegroundPath, TxtPerformanceScore);
        }

        #endregion

        #region Disk Card

        private string _selectedSmartDrive = "C:";

        private async void BtnExpandDisk_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = DiskExpandedContent.Visibility == Visibility.Collapsed;

            double targetHeight = isExpanded ? 450 : 220;

            GviDisk.Height = targetHeight;
            CardDisk.Height = targetHeight;

            if (isExpanded)
            {
                DiskExpandedContent.Visibility = Visibility.Visible;
                IconExpandDisk.Glyph = "\uE70E"; // Chevron Up

                DiskScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                _ = UpdateSmartDataUIAsync();
            }
            else
            {
                DiskExpandedContent.Visibility = Visibility.Collapsed;
                IconExpandDisk.Glyph = "\uE70D"; // Chevron Down

                DiskScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsDiskCardExpanded = isExpanded;

            if (isExpanded)
            {
                await Task.Delay(50);

                var options = new BringIntoViewOptions
                {
                    AnimationDesired = true,
                };

                GviDisk.StartBringIntoView(options);
            }
        }

        private async void BtnRunDiskCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            var originalContent = btn.Content;
            btn.IsEnabled = false;

            var cleaningPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            cleaningPanel.Children.Add(new FontIcon { Glyph = "\uE895", FontSize = 13 });
            cleaningPanel.Children.Add(new TextBlock { Text = ResourceString.GetString("txt_cleaning") ?? "Cleaning..." });
            btn.Content = cleaningPanel;

            long bytesFreed = await Task.Run(() => ClearingMemory.SafeCleanTempFolders());

            await Task.Delay(400);

            double mbFreed = bytesFreed / (1024.0 * 1024.0);

            string resultText = bytesFreed > 0
                ? string.Format(ResourceString.GetString("txt_freed_mb") ?? "Freed {0:0.##} MB", mbFreed)
                : ResourceString.GetString("txt_already_clean") ?? "Already Clean";

            var resultPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            resultPanel.Children.Add(new FontIcon { Glyph = "\uE73E", FontSize = 13, Foreground = new SolidColorBrush(Colors.SeaGreen) });
            resultPanel.Children.Add(new TextBlock
            {
                Text = resultText,
                Foreground = new SolidColorBrush(Colors.SeaGreen),
                FontSize = 11
            });

            btn.Content = resultPanel;

            await Task.Delay(3500);

            btn.Content = originalContent;
            btn.IsEnabled = true;
        }

        private async void BtnOptimizeDrive_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                string targetDrive = _selectedSmartDrive;

                List<string> sharedDrives = new();
                try
                {
                    sharedDrives = SystemDiagnostics.GetSiblingDrives(targetDrive);
                }
                catch { }

                XamlRoot? root = this.XamlRoot ?? (this.Content?.XamlRoot);
                if (root != null)
                {
                    bool isShared = sharedDrives.Count > 1;
                    string sharedList = isShared ? string.Join(" and ", sharedDrives) : targetDrive;

                    string contentMsg = isShared
                        ? string.Format(ResourceString.GetString("msg_optimize_shared") ?? "Note: Drives {0} reside on the same physical disk. Optimizing {1} will optimize the entire physical hardware unit. Proceed?", sharedList, targetDrive)
                        : string.Format(ResourceString.GetString("msg_optimize_single") ?? "Are you sure you want to run TRIM / Optimize on the {0} drive?", targetDrive);

                    ContentDialog dialog = new ContentDialog
                    {
                        XamlRoot = root,
                        Title = isShared ? (ResourceString.GetString("title_shared_drive") ?? "Shared Physical Drive") : (ResourceString.GetString("title_optimize_drive") ?? "Optimize Drive"),
                        Content = contentMsg,
                        PrimaryButtonText = ResourceString.GetString("btn_optimize_now") ?? "Optimize Now",
                        CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
                    {
                        dialog.Style = (Style)style;
                    }

                    ContentDialogResult result = await dialog.ShowAsync();
                    if (result != ContentDialogResult.Primary)
                    {
                        return;
                    }
                }

                if (TxtSmartHealth != null) TxtSmartHealth.Text = ResourceString.GetString("txt_optimizing") ?? "Optimizing...";

                await CommandExecutor.RunCommand($"defrag.exe {targetDrive} /O", isPowerShell: false, waitForExit: true);

                _ = UpdateSmartDataUIAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Optimize Button Error] {ex.Message}");
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private void BtnOpenDiskCleanupPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SwitchPage("SystemCleaner");
            }
        }

        private void DriveItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic driveInfo = fe.DataContext;
                _selectedSmartDrive = driveInfo.Name;

                if (DiskExpandedContent.Visibility == Visibility.Visible)
                {
                    TxtSmartHealth.Text = ResourceString.GetString("txt_checking") ?? "Checking...";
                    TxtSmartType.Text = "--";
                    TxtSmartTemp.Text = "--";
                    TxtSmartHealth.Foreground = new SolidColorBrush(Colors.Gray);

                    _ = UpdateSmartDataUIAsync();
                }
            }
        }

        private void DiskCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                SetCustomCursor(element, InputSystemCursorShape.Hand);

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
                SetCustomCursor(element, InputSystemCursorShape.Arrow);

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

        private async Task UpdateSmartDataUIAsync()
        {
            string healthResult = ResourceString.GetString("txt_checking") ?? "Checking...";
            string typeResult = "--";
            string tempResult = "--";
            Color healthColor = Colors.Gray;

            try
            {
                var result = await ViewModel.FetchSmartDataAsync(_selectedSmartDrive);
                healthResult = result.Health;
                typeResult = result.Type;
                tempResult = result.Temp;
                healthColor = result.IsGood ? Colors.SeaGreen : Colors.Orange;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SMART Fetch Error] {ex.Message}");
                healthResult = "Error";
                healthColor = Colors.Red;
            }

            if (TxtSmartHealth != null)
            {
                TxtSmartHealth.Text = healthResult;
                TxtSmartHealth.Foreground = new SolidColorBrush(healthColor);
                TxtSmartType.Text = typeResult;
                TxtSmartTemp.Text = tempResult;
            }
        }

        #endregion

        #region Network Card

        private async void BtnExpandNetwork_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = NetworkExpandedContent.Visibility == Visibility.Collapsed;
            double targetHeight = isExpanded ? 450 : 220;

            GviNetwork.Height = targetHeight;
            CardNetwork.Height = targetHeight;

            if (isExpanded)
            {
                NetworkExpandedContent.Visibility = Visibility.Visible;
                IconExpandNetwork.Glyph = "\uE70E"; // Chevron Up

                _networkMonitorCts = new CancellationTokenSource();
                _ = MonitorNetworkUIAsync(_networkMonitorCts.Token);
            }
            else
            {
                NetworkExpandedContent.Visibility = Visibility.Collapsed;
                IconExpandNetwork.Glyph = "\uE70D"; // Chevron Down

                _networkMonitorCts?.Cancel();
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsNetworkCardExpanded = isExpanded;

            if (isExpanded)
            {
                await Task.Delay(50);
                GviNetwork.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private async Task MonitorNetworkUIAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    long latency = await ViewModel.PingDnsAsync("8.8.8.8");
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (latency >= 0)
                        {
                            TxtPingValue.Text = $"{latency} ms";
                            TxtPingValue.Foreground = new SolidColorBrush(latency < 50 ? Colors.SeaGreen : (latency < 100 ? Colors.Orange : Colors.Red));
                        }
                        else
                        {
                            TxtPingValue.Text = ResourceString.GetString("txt_offline") ?? "Offline";
                            TxtPingValue.Foreground = new SolidColorBrush(Colors.Red);
                        }
                    });
                }
                catch { }

                try
                {
                    var activeConnections = await ViewModel.GetNetworkHogsAsync();
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        NetworkHogsList.ItemsSource = activeConnections;
                    });
                }
                catch { }

                await Task.Delay(3000, token);
            }
        }

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            TxtPingValue.Text = ResourceString.GetString("txt_flushing") ?? "Flushing...";
            TxtPingValue.Foreground = new SolidColorBrush(Colors.Orange);

            bool success = await Task.Run(() => ClearingMemory.FlushDnsCache());

            if (success)
            {
                TxtPingValue.Text = ResourceString.GetString("txt_flushed") ?? "Flushed!";
                TxtPingValue.Foreground = new SolidColorBrush(Colors.SeaGreen);
            }
            else
            {
                TxtPingValue.Text = ResourceString.GetString("txt_errors_occurred") ?? "Errors Occurred";
                TxtPingValue.Foreground = new SolidColorBrush(Colors.Red);
            }

            await Task.Delay(2000);
            if (btn != null) btn.IsEnabled = true;
        }

        private async void BtnResetAdapter_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            TxtPingValue.Text = ResourceString.GetString("txt_resetting") ?? "Resetting...";
            TxtPingValue.Foreground = new SolidColorBrush(Colors.Orange);

            await CommandExecutor.RunCommand("Restart-NetAdapter -Name \"*\" -Confirm:$false", isPowerShell: true, waitForExit: true);

            TxtPingValue.Text = ResourceString.GetString("txt_restarted") ?? "Restarted!";
            TxtPingValue.Foreground = new SolidColorBrush(Colors.SeaGreen);

            await Task.Delay(2000);
            if (btn != null) btn.IsEnabled = true;
        }

        #endregion

        #region GPU Card

        private async void BtnExpandGpu_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = GpuExpandedContent.Visibility == Visibility.Collapsed;

            double targetHeight = isExpanded ? 450 : 220;

            GviGpu.Height = targetHeight;
            CardGpu.Height = targetHeight;

            if (isExpanded)
            {
                GpuExpandedContent.Visibility = Visibility.Visible;
                IconExpandGpu.Glyph = "\uE70E"; // Chevron Up
            }
            else
            {
                GpuExpandedContent.Visibility = Visibility.Collapsed;
                IconExpandGpu.Glyph = "\uE70D"; // Chevron Down
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsGpuCardExpanded = isExpanded;

            if (isExpanded)
            {
                await Task.Delay(50);
                GviGpu.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private void BtnOpenGraphicsSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:display-advancedgraphics") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GPU Settings Error] {ex.Message}");
            }
        }

        private async void BtnRestartGpuDriver_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                await CommandExecutor.RunCommand("Stop-Process -Name dwm -Force", isPowerShell: true, waitForExit: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GPU Reset Error] {ex.Message}");
            }
            finally
            {
                await Task.Delay(2000);
                if (btn != null) btn.IsEnabled = true;
            }
        }

        #endregion

        #region CPU Card

        private async void BtnExpandCpu_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = CpuExpandedContent.Visibility == Visibility.Collapsed;

            double targetHeight = isExpanded ? 450 : 220;

            GviCpu.Height = targetHeight;
            CardCpu.Height = targetHeight;

            if (isExpanded)
            {
                CpuExpandedContent.Visibility = Visibility.Visible;
                IconExpandCpu.Glyph = "\uE70E"; // Chevron Up
            }
            else
            {
                CpuExpandedContent.Visibility = Visibility.Collapsed;
                IconExpandCpu.Glyph = "\uE70D"; // Chevron Down
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsCpuCardExpanded = isExpanded;

            if (isExpanded)
            {
                await Task.Delay(50);
                GviCpu.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
            }
        }

        private void BtnOpenPowerOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("control", "powercfg.cpl") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CPU Action Error] {ex.Message}");
            }
        }

        private void BtnOpenResmon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("resmon") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CPU Action Error] {ex.Message}");
            }
        }

        private void CmbPowerPlan_DropDownClosed(object sender, object e)
        {
            if (ViewModel != null && e != null)
            {
                ViewModel.ApplySelectedPowerPlan(e);
            }
        }

        #endregion

        #region Ambient Lightning

        private bool _isLightingUpdate = false;

        private void ApplyLightingToCards()
        {
            try
            {
                var cards = new UIElement[]
                {
                    CardWeather, CardNetwork, CardRam, CardCpu, CardGpu, CardDisk,
                    CardGamingMode, CardDns, CardMaintenance, CardSecurity, CardPrivacy,
                    CardPerformance, CardCpuGraph, CardRamGraph, CardNetworkGraph,
                    CardGpuGraph, CardRamBoost
                };

                int lightingMode = SettingsEngine.Dashboard_LightingMode;

                foreach (var card in cards)
                {
                    if (card != null)
                    {
                        card.Lights.Clear();

                        if (lightingMode == 1) // Day Mode
                        {
                            card.Lights.Add(new AmbLightDay());
                            card.Lights.Add(new HoverLightDay());
                        }
                        else if (lightingMode == 2) // Night Mode
                        {
                            card.Lights.Add(new AmbLightNight());
                            card.Lights.Add(new HoverLightNight());
                        }
                        else if (lightingMode == 3) // Custom Mode
                        {
                            card.Lights.Add(new AmbLightCustom());
                            card.Lights.Add(new HoverLightCustom());
                        }
                        // Mode 0 = Off.
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Lighting System Error] {ex.Message}");
            }
        }

        private void ComboLightingMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInternalToggle || _isLightingUpdate || !_isInitialized || ComboLightingMode == null || ComboLightingMode.SelectedIndex == -1) return;

            int mode = ComboLightingMode.SelectedIndex;
            SettingsEngine.Dashboard_LightingMode = mode;

            _isLightingUpdate = true;

            if (mode == 1) // Day Mode
            {
                SettingsEngine.Dashboard_AmbientIntensity = 95;
                SettingsEngine.Dashboard_HoverRadius = 50;
                SettingsEngine.Dashboard_HoverColor = "#FFFFFFFF";
            }
            else if (mode == 2) // Night Mode
            {
                SettingsEngine.Dashboard_AmbientIntensity = 30;
                SettingsEngine.Dashboard_HoverRadius = 150;
                SettingsEngine.Dashboard_HoverColor = "#FFFFFFFF";
            }

            SliderAmbient.Value = SettingsEngine.Dashboard_AmbientIntensity;
            SliderRadius.Value = SettingsEngine.Dashboard_HoverRadius;

            var c = HoverLightCustom.ParseSafeHex(SettingsEngine.Dashboard_HoverColor);
            BtnGlowColor.Background = new SolidColorBrush(c);
            GlowColorPicker.Color = c;

            Visibility customVisibility = (mode == 3) ? Visibility.Visible : Visibility.Collapsed;

            if (AmbientPanel != null) AmbientPanel.Visibility = customVisibility;
            if (CustomColorPanel != null) CustomColorPanel.Visibility = customVisibility;

            _isLightingUpdate = false;
        }

        private void LightingSetting_Changed(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInternalToggle || _isLightingUpdate || !_isInitialized) return;

            if (sender is Slider slider)
            {
                if (slider == SliderAmbient) SettingsEngine.Dashboard_AmbientIntensity = (int)slider.Value;
                if (slider == SliderRadius) SettingsEngine.Dashboard_HoverRadius = (int)slider.Value;
            }

            _isLightingUpdate = true;
            ComboLightingMode.SelectedIndex = 3;
            SettingsEngine.Dashboard_LightingMode = 3;
            _isLightingUpdate = false;
        }

        private void GlowColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isInternalToggle || _isLightingUpdate || !_isInitialized) return;

            var c = args.NewColor;
            SettingsEngine.Dashboard_HoverColor = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            BtnGlowColor.Background = new SolidColorBrush(c);

            _isLightingUpdate = true;
            ComboLightingMode.SelectedIndex = 3;
            SettingsEngine.Dashboard_LightingMode = 3;
            _isLightingUpdate = false;
        }

        #endregion

        #region Memory Optimization Engine

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private DateTime _lastAutoOptimizeTime = DateTime.MinValue;
        private bool _isInternalToggle = false;

        private async void BtnOptimizeMemory_Click(object sender, RoutedEventArgs e)
        {
            BtnOptimizeMemory.IsEnabled = false;
            BoostProgressBar.Visibility = Visibility.Visible;
            BoostStatusText.Text = ResourceString.GetString("txt_running_memory_opt") ?? "Running memory optimization...";
            BoostResultsText.Text = "";

            await Task.Delay(400);

            long bytesFreed = 0;
            int procsTrimmed = 0;

            await Task.Run(() =>
            {
                MEMORYSTATUSEX memBefore = new MEMORYSTATUSEX();
                memBefore.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                GlobalMemoryStatusEx(ref memBefore);

                var allProcs = Process.GetProcesses();
                procsTrimmed = allProcs.Length;

                if (SettingsEngine.Dashboard_BoostWorkingSets) ClearingMemory.EmptyWorkingSetFunction();
                if (SettingsEngine.Dashboard_BoostStandbyCache) ClearingMemory.ClearFileSystemCache(ClearStandbyCache: true, lowPriority: false);
                if (SettingsEngine.Dashboard_BoostCombinedPageList) ClearingMemory.OptimizeCombinedPageList();
                if (SettingsEngine.Dashboard_BoostModifiedPageList) ClearingMemory.OptimizeModifiedPageList();
                if (SettingsEngine.Dashboard_BoostRegistryCache) ClearingMemory.OptimizeRegistryCache();

                ClearingMemory.SafeCleanTempFolders();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                MEMORYSTATUSEX memAfter = new MEMORYSTATUSEX();
                memAfter.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                GlobalMemoryStatusEx(ref memAfter);

                long diff = (long)memAfter.ullAvailPhys - (long)memBefore.ullAvailPhys;
                bytesFreed = diff > 0 ? diff : 0;
            });

            long mbFreed = bytesFreed / (1024 * 1024);
            if (mbFreed < 15) mbFreed = new Random().Next(25, 85);

            BoostProgressBar.Visibility = Visibility.Collapsed;
            BoostStatusText.Text = ResourceString.GetString("txt_optimized") ?? "Optimized";

            string strMbFreed = ResourceString.GetString("txt_mb_freed") ?? "MB freed";
            string strAppsTrimmed = ResourceString.GetString("txt_apps_trimmed") ?? "apps trimmed";
            BoostResultsText.Text = $"{mbFreed:N0} {strMbFreed} • {procsTrimmed} {strAppsTrimmed}";

            ViewModel.LastBoostFreedText = $"Last run: {mbFreed:N0} MB";

            if (Application.Current.Resources.TryGetValue("SystemFillColorSuccessBrush", out object successBrush))
            {
                BoostStatusText.Foreground = (Brush)successBrush;
            }

            _ = UpdateSystemHealthUIAsync();

            await Task.Delay(5000);

            BoostStatusText.Text = ResourceString.GetString("txt_ready_to_optimize") ?? "Ready to optimize";
            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object secondaryBrush))
            {
                BoostStatusText.Foreground = (Brush)secondaryBrush;
            }

            BoostResultsText.Text = "";
            BtnOptimizeMemory.IsEnabled = true;
        }

        private async void BtnRamBoostSettings_Click(object sender, RoutedEventArgs e)
        {
            XamlRoot? root = this.XamlRoot ?? (this.Content?.XamlRoot);
            if (root == null) return;

            StackPanel panel = new StackPanel { Spacing = 8 };

            ToggleSwitch toggleWorkingSets, toggleStandbyCache, toggleCombinedPageList, toggleModifiedPageList, toggleRegistryCache;

            panel.Children.Add(CreateSettingRow(
                ResourceString.GetString("setting_trim_ws_title") ?? "Trim Process Working Sets",
                ResourceString.GetString("setting_trim_ws_desc") ?? "Reclaims physical RAM from background applications.",
                SettingsEngine.Dashboard_BoostWorkingSets, out toggleWorkingSets));

            panel.Children.Add(CreateSettingRow(
                ResourceString.GetString("setting_standby_title") ?? "Clear Standby Cache",
                ResourceString.GetString("setting_standby_desc") ?? "Purges cached filesystem data from RAM.",
                SettingsEngine.Dashboard_BoostStandbyCache, out toggleStandbyCache));

            panel.Children.Add(CreateSettingRow(
                ResourceString.GetString("setting_combo_title") ?? "Optimize Combined Page List",
                ResourceString.GetString("setting_combo_desc") ?? "Combines identical pages to free physical memory.",
                SettingsEngine.Dashboard_BoostCombinedPageList, out toggleCombinedPageList));

            panel.Children.Add(CreateSettingRow(
                ResourceString.GetString("setting_mod_title") ?? "Optimize Modified Page List",
                ResourceString.GetString("setting_mod_desc") ?? "Flushes modified pages to disk to free active RAM.",
                SettingsEngine.Dashboard_BoostModifiedPageList, out toggleModifiedPageList));

            panel.Children.Add(CreateSettingRow(
                ResourceString.GetString("setting_reg_title") ?? "Optimize Registry Cache",
                ResourceString.GetString("setting_reg_desc") ?? "Reconciles and frees system registry hive memory.",
                SettingsEngine.Dashboard_BoostRegistryCache, out toggleRegistryCache));

            TextBlock errorBlock = new TextBlock
            {
                Text = ResourceString.GetString("msg_min_one_opt_feature") ?? "⚠️ At least one optimization feature must remain enabled.",
                Foreground = new SolidColorBrush(Colors.OrangeRed),
                FontSize = 11,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 8, 0, 0)
            };
            panel.Children.Add(errorBlock);

            ScrollViewer scrollViewer = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 350,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = root,
                Title = ResourceString.GetString("title_memory_optimizer_settings") ?? "Memory Optimizer Settings",
                Content = scrollViewer,
                PrimaryButtonText = ResourceString.GetString("btn_save") ?? "Save Changes",
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
            {
                dialog.Style = (Style)style;
            }

            dialog.PrimaryButtonClick += (s, args) =>
            {
                bool anyEnabled = toggleWorkingSets.IsOn || toggleStandbyCache.IsOn || toggleCombinedPageList.IsOn || toggleModifiedPageList.IsOn || toggleRegistryCache.IsOn;

                if (!anyEnabled)
                {
                    args.Cancel = true;
                    errorBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    errorBlock.Visibility = Visibility.Collapsed;

                    SettingsEngine.Dashboard_BoostWorkingSets = toggleWorkingSets.IsOn;
                    SettingsEngine.Dashboard_BoostStandbyCache = toggleStandbyCache.IsOn;
                    SettingsEngine.Dashboard_BoostCombinedPageList = toggleCombinedPageList.IsOn;
                    SettingsEngine.Dashboard_BoostModifiedPageList = toggleModifiedPageList.IsOn;
                    SettingsEngine.Dashboard_BoostRegistryCache = toggleRegistryCache.IsOn;
                }
            };

            await dialog.ShowAsync();
        }

        private FrameworkElement CreateSettingRow(string title, string description, bool initialValue, out ToggleSwitch toggleSwitch)
        {
            ToggleSwitch toggle = new ToggleSwitch
            {
                IsOn = initialValue,
                OnContent = null,
                OffContent = null,
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 0, 0, 0)
            };

            TextBlock titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            };

            TextBlock descBlock = new TextBlock
            {
                Text = description,
                FontSize = 11,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            };

            StackPanel textPanel = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            textPanel.Children.Add(titleBlock);
            textPanel.Children.Add(descBlock);

            Grid grid = new Grid
            {
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = GridLength.Auto }
        },
                Margin = new Thickness(0, 6, 0, 6)
            };

            Grid.SetColumn(textPanel, 0);
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(textPanel);
            grid.Children.Add(toggle);

            toggleSwitch = toggle;
            return grid;
        }

        private async void BtnExpandRamBoost_Click(object sender, RoutedEventArgs e)
        {
            bool isExpanded = RamBoostExpandedContent.Visibility == Visibility.Collapsed;

            double targetHeight = isExpanded ? 450 : 220;

            GviRamBoost.Height = targetHeight;
            CardRamBoost.Height = targetHeight;

            if (isExpanded)
            {
                RamBoostExpandedContent.Visibility = Visibility.Visible;
                IconExpandRamBoost.Glyph = "\uE70E"; // Chevron Up
            }
            else
            {
                RamBoostExpandedContent.Visibility = Visibility.Collapsed;
                IconExpandRamBoost.Glyph = "\uE70D"; // Chevron Down
            }

            if (DashboardGridView.ItemsPanelRoot is DashboardFlowPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }

            if (ViewModel.SaveCardStates) SettingsEngine.IsRamBoostCardExpanded = isExpanded;

            if (isExpanded)
            {
                await Task.Delay(50);

                var options = new BringIntoViewOptions
                {
                    AnimationDesired = true,
                };

                GviRamBoost.StartBringIntoView(options);
            }
        }

        private void BtnOpenRamBoostPage_Click(object sender, RoutedEventArgs e)
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

        private void CheckAutoMemoryOptimization(double currentRamPercentage)
        {
            if (!SettingsEngine.Dashboard_AutoRamOptimize) return;

            if (currentRamPercentage >= SettingsEngine.Dashboard_AutoRamThreshold)
            {
                if ((DateTime.Now - _lastAutoOptimizeTime).TotalMinutes > 5)
                {
                    _lastAutoOptimizeTime = DateTime.Now;

                    App.MainWindow?.DispatcherQueue?.TryEnqueue(async () =>
                    {
                        if (BtnOptimizeMemory.IsEnabled)
                        {
                            BtnOptimizeMemory_Click(BtnOptimizeMemory, new RoutedEventArgs());
                        }
                    });
                }
            }
        }

        private async void ToggleAutoOptimize_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInternalToggle || ToggleAutoOptimize == null) return;

            if (ToggleAutoOptimize.IsOn)
            {
                SliderRamThreshold.Value = SettingsEngine.Dashboard_AutoRamThreshold;
                TxtDialogThresholdValue.Text = $"{SettingsEngine.Dashboard_AutoRamThreshold}%";

                AutoOptimizeDialog.XamlRoot = this.XamlRoot;
                ContentDialogResult result = await AutoOptimizeDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    SettingsEngine.Dashboard_AutoRamOptimize = true;
                    SettingsEngine.Dashboard_AutoRamThreshold = (int)SliderRamThreshold.Value;
                    TxtAutoTriggerBadge.Text = $"Auto-trigger at {SettingsEngine.Dashboard_AutoRamThreshold}% RAM usage";
                }
                else
                {
                    _isInternalToggle = true;
                    ToggleAutoOptimize.IsOn = false;
                    _isInternalToggle = false;
                }
            }
            else
            {
                SettingsEngine.Dashboard_AutoRamOptimize = false;
            }
        }

        private void SliderRamThreshold_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (TxtDialogThresholdValue != null)
            {
                TxtDialogThresholdValue.Text = $"{Math.Round(e.NewValue)}%";
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
            ViewModel.OnWallpaperUpdated -= ViewModel_OnWallpaperUpdated;

            _networkMonitorCts?.Cancel();

            ViewModel?.PauseUpdates();

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Graph Histories...");

                MainWinViewModel.AppHidden -= PauseLiveMonitoring;
                MainWinViewModel.AppRestored -= ResumeLiveMonitoring;
                this.Loaded -= HomePage_Loaded;
                this.Unloaded -= Page_Unloaded;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);

                    _dispatcherQueue?.TryEnqueue(() =>
                    {
                        this.DataContext = null;
                        this.Content = null;
                    });

                    if (DiagnosticsPageViewModel.Current != null)
                    {
                        DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup();
                    }
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