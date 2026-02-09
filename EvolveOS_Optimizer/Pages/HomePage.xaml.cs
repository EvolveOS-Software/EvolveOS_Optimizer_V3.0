using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class HomePage : Page
{
    private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();
    private readonly DispatcherQueue _dispatcherQueue;
    private DispatcherTimer? _monitoringTimer;
    private string _lastWallpaperPath = string.Empty;

    public HomePage()
    {
        this.InitializeComponent();

        LogoGrid.Translation = new System.Numerics.Vector3(0, 0, 32);

        var vm = new HomePageViewModel();
        this.DataContext = vm;

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("DispatcherQueue not found.");

        this.Loaded += HomePage_Loaded;
        this.Unloaded += HomePage_Unloaded;

        this.Loaded += (s, e) =>
        {
            if (this.DataContext is HomePageViewModel vm)
            {
                ApplyElevationUI();
                StartMonitoring();
            }
        };

        this.Unloaded += (s, e) => _monitoringTimer?.Stop();
    }

    private void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyElevationUI();
        StartMonitoring();

        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    private void HomePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _monitoringTimer?.Stop();

        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
    }

    #region Elevation & Admin Logic
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
        AdminWarningBanner.Visibility = Visibility.Collapsed;

        try
        {
            string? exePath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                });

                Application.Current.Exit();
            }
            else
            {
                Debug.WriteLine("[Elevation] Could not determine executable path.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Elevation] Failed: {ex.Message}");
        }
    }
    #endregion

    #region Real-time Monitoring
    private void StartMonitoring()
    {
        _monitoringTimer = new DispatcherTimer();
        _monitoringTimer.Interval = TimeSpan.FromSeconds(2);
        _monitoringTimer.Tick += async (s, e) =>
        {
            if (this.Parent == null)
            {
                _monitoringTimer?.Stop();
                return;
            }

            string pCount = await _systemDiagnostics.GetProcessCount();
            string sCount = await _systemDiagnostics.GetServicesCount();

            HardwareData.RunningProcessesCount = pCount;
            HardwareData.RunningServicesCount = sCount;

            try
            {
                if (this.DataContext is HomePageViewModel vm)
                {
                    vm.RefreshStats();
                    vm.UpdateDateTime();

                    string currentPath = _systemDiagnostics.GetWallpaperPath();
                    if (currentPath != _lastWallpaperPath)
                    {
                        _lastWallpaperPath = currentPath;

                        // Get the compositor from the LogoPath
                        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(LogoPath);
                        var compositor = visual.Compositor;

                        // 1. Create the Fade Animation via the Compositor
                        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
                        fadeAnimation.InsertKeyFrame(0.0f, 0.0f); // Start at 0% opacity
                        fadeAnimation.InsertKeyFrame(1.0f, 1.0f); // End at 100% opacity
                        fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);

                        // 2. Update the image
                        vm.RefreshWallpaper();

                        // 3. Start the animation on the "Opacity" property
                        visual.StartAnimation("Opacity", fadeAnimation);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Monitoring] DataContext access failed: {ex.Message}");
                _monitoringTimer?.Stop();
            }
        };
        _monitoringTimer.Start();
    }
    #endregion

    #region UI Interactions
    private void HandleCopyingData_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        string textToCopy = string.Empty;

        if (sender is Run runText) textToCopy = runText.Text;
        else if (sender is TextBlock tb) textToCopy = tb.Text;

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
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (this.DataContext is HomePageViewModel vm)
                {
                    // This now triggers the OnPropertyChanged we just added!
                    vm.RefreshWallpaper();
                }
            });
        }
    }

    /*private void BtnVision_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            bool isHidden = btn.IsChecked ?? false;
            // In WinUI 3, Blur is applied via CanvasDevice or specialized Win2D effects.
            // If you are using a simple UI element for blur:
            // IpAddress.Blur(isHidden ? 10 : 0);
        }
    }*/
    #endregion

    // private static readonly List<string> AvailableWeatherLocations = new List<string>
    // {
    //    "New York", "London", "Paris", "Berlin", "Tokyo", "Singapore", "Sydney" 
    //  ... (Keep your full list here)
    // };
}