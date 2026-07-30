// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class RgbControlWindow : Window, INotifyPropertyChanged
    {
        private const int GWLP_HWNDPARENT = -8;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        private bool _isShowingSettings = false;
        private readonly RgbControlEngine _rgbEngine = RgbControlEngine.Instance;

        public ObservableCollection<RgbZoneViewModel> MotherboardZones { get; } = new();
        public ObservableCollection<RgbZoneViewModel> FanZones { get; } = new();

        // Empty state UI trackers
        private bool _hasMotherboardDevices = false;
        public bool HasMotherboardDevices { get => _hasMotherboardDevices; set => SetField(ref _hasMotherboardDevices, value); }

        private bool _hasFanDevices = false;
        public bool HasFanDevices { get => _hasFanDevices; set => SetField(ref _hasFanDevices, value); }

        public RgbControlWindow()
        {
            this.InitializeComponent();
            RootGrid.DataContext = this;

            UIHelper.SetOverlay(true, true);
            ConfigureWindow();

            RootGrid.Loaded += RootElement_Loaded;
        }

        private async void RootElement_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);

            // Load saved settings into UI
            ToggleStartWithWindows.IsOn = LocalMachineSettingsEngine.RgbStartWithWindows;
            ToggleOverrideOem.IsOn = LocalMachineSettingsEngine.RgbOverrideOem;
            ToggleTurnOffWithScreen.IsOn = LocalMachineSettingsEngine.RgbTurnOffWithScreen;

            try
            {
                await _rgbEngine.InitializeAsync();

                MotherboardZones.Clear();
                FanZones.Clear();

                // Iterate through the unified devices (both Native and RGB.NET)
                foreach (var device in _rgbEngine.Devices)
                {
                    var vm = new RgbZoneViewModel(
                        _rgbEngine,
                        device.Id,
                        string.IsNullOrEmpty(device.Name) ? "RGB Controller" : device.Name,
                        $"{device.LedCount} LEDs"
                    );

                    // Basic routing to categorize items in the UI
                    if (device.Name.Contains("Motherboard", StringComparison.OrdinalIgnoreCase) ||
                        device.Name.Contains("Mainboard", StringComparison.OrdinalIgnoreCase) ||
                        device.IsNative) // Dynamic Lighting usually represents the Motherboard ARGB headers
                    {
                        MotherboardZones.Add(vm);
                    }
                    else
                    {
                        FanZones.Add(vm);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RgbControlWindow] Lighting Engine Error: {ex.Message}");
            }
            finally
            {
                // Update the visibility state of the empty banners
                HasMotherboardDevices = MotherboardZones.Count > 0;
                HasFanDevices = FanZones.Count > 0;
            }
        }

        private void ToggleView_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (!_isShowingSettings)
            {
                SlideToSettingsAnimation.Begin();
                HeaderTitle.Text = "Advanced Lighting Settings";
                ToggleViewText.Text = "Back to Zones";
            }
            else
            {
                SlideToZonesAnimation.Begin();
                HeaderTitle.Text = "System Lighting Control";
                ToggleViewText.Text = "Lighting Settings";
            }

            _isShowingSettings = !_isShowingSettings;
        }

        private async void PresetOff_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var offColor = Color.FromArgb(255, 0, 0, 0);
            await _rgbEngine.SetAllColorsAsync(offColor);
        }

        private async void PresetStatic_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var staticColor = Color.FromArgb(255, 0, 120, 215); // Evolve Blue
            await _rgbEngine.SetAllColorsAsync(staticColor);
        }

        private async void PresetSync_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var syncColor = Color.FromArgb(255, 255, 255, 255); // Pure White
            await _rgbEngine.SetAllColorsAsync(syncColor);
        }

        private void CloseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            UIHelper.SetOverlay(false);
        }

        private void ConfigureWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

            var mainWindow = (Microsoft.UI.Xaml.Application.Current as Microsoft.UI.Xaml.Application)?.GetType().GetProperty("MainWindow")?.GetValue(Microsoft.UI.Xaml.Application.Current) as Window;
            if (mainWindow != null)
            {
                IntPtr mainHWnd = WindowNative.GetWindowHandle(mainWindow);
                SetWindowLongPtr(hWnd, GWLP_HWNDPARENT, mainHWnd);
            }

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            double scale = UIHelper.GetScaleAdjustment(hWnd);
            int physicalWidth = (int)(800 * scale);
            int physicalHeight = (int)(750 * scale);

            var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int x = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - physicalWidth) / 2);
                int y = displayArea.WorkArea.Y + ((displayArea.WorkArea.Height - physicalHeight) / 2);

                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, physicalWidth, physicalHeight));
            }
        }

        private void ToggleStartWithWindows_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            LocalMachineSettingsEngine.RgbStartWithWindows = ToggleStartWithWindows.IsOn;
        }

        private void ToggleOverrideOem_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            bool isEnabled = ToggleOverrideOem.IsOn;
            LocalMachineSettingsEngine.RgbOverrideOem = isEnabled;

            // Execute immediately in the background so it doesn't stutter the UI thread
            Task.Run(() => OemManager.OverrideOemSoftware(isEnabled));
        }

        private void ToggleTurnOffWithScreen_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            LocalMachineSettingsEngine.RgbTurnOffWithScreen = ToggleTurnOffWithScreen.IsOn;
        }

        private void AllowBackgroundLighting_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                // Opens the Windows 11 Dynamic Lighting settings page directly
                Process.Start(new ProcessStartInfo("ms-settings:personalization-dynamiclighting")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open Windows Settings: {ex.Message}");
            }
        }

        // Standard INotifyPropertyChanged implementation for Window
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public partial class RgbZoneViewModel : ObservableObject
    {
        private readonly RgbControlEngine _engine;
        private CancellationTokenSource? _colorDebounceCts;

        public string DeviceId { get; }

        private string _name = "";
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _deviceType = "";
        public string DeviceType { get => _deviceType; set => SetProperty(ref _deviceType, value); }

        private string _iconGlyph = "\xE7F8";
        public string IconGlyph { get => _iconGlyph; set => SetProperty(ref _iconGlyph, value); }

        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private Color _selectedColor = Color.FromArgb(255, 0, 120, 215);
        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (SetProperty(ref _selectedColor, value))
                {
                    DebounceApplyColorAsync(value);
                }
            }
        }

        public RgbZoneViewModel(RgbControlEngine engine, string deviceId, string name, string deviceType)
        {
            _engine = engine;
            DeviceId = deviceId;
            Name = name;
            DeviceType = deviceType;

            // 🚀 NEW: Load the exact color the user had previously selected
            _selectedColor = _engine.GetSavedColor(deviceId);
        }

        private async void DebounceApplyColorAsync(Color winUiColor)
        {
            if (!_isEnabled) return;

            _colorDebounceCts?.Cancel();
            _colorDebounceCts = new CancellationTokenSource();
            var token = _colorDebounceCts.Token;

            try
            {
                // 🚀 REDUCED FROM 500ms TO 20ms
                // The native API handles high-speed buffer writing, so we get smooth, real-time dragging!
                await Task.Delay(20, token);

                if (!token.IsCancellationRequested)
                {
                    await _engine.SetDeviceColorAsync(DeviceId, winUiColor);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when user keeps dragging
            }
        }
    }
}