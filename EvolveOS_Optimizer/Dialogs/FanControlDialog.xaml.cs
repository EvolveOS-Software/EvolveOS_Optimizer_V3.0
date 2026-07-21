// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Shapes;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class FanControlWindow : Window
    {
        private const int GWLP_HWNDPARENT = -8;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        private bool _isDraggingDot = false;
        private Grid? _draggedDot;
        private FanCurvePoint? _draggedPoint;

        private bool _isShowingThermalSettings = false;

        public ObservableCollection<FanControlViewModel> CaseFans => FanControlEngine.Instance.CaseFans;
        public ObservableCollection<FanControlViewModel> CpuFans => FanControlEngine.Instance.CpuFans;
        public ObservableCollection<FanControlViewModel> GpuFans => FanControlEngine.Instance.GpuFans;
        public ObservableCollection<FanControlViewModel> WaterPumps => FanControlEngine.Instance.WaterPumps;

        private readonly Dictionary<FanControlViewModel, Polyline> _curveLines = new();

        public FanControlWindow()
        {
            this.InitializeComponent();

            RootGrid.DataContext = this;

            UIHelper.SetOverlay(true, true);
            ConfigureWindow();

            LoadThermalSettings();

            RootGrid.Loaded += RootElement_Loaded;
        }

        private void RootElement_Loaded(object sender, RoutedEventArgs e)
        {
            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
            FanControlEngine.Instance.Initialize();
        }

        private void PresetSilent_Click(object sender, RoutedEventArgs e) { ApplyGlobalPreset(0); }
        private void PresetBalanced_Click(object sender, RoutedEventArgs e) { ApplyGlobalPreset(1); }
        private void PresetExtreme_Click(object sender, RoutedEventArgs e) { ApplyGlobalPreset(2); }

        private void ApplyGlobalPreset(int mode)
        {
            FanControlEngine.Instance.ApplyGlobalPreset(mode);
            foreach (var vm in FanControlEngine.Instance.AllFans)
            {
                RedrawCurveLine(vm);
            }
        }

        private async void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FanControlViewModel vm)
            {
                await vm.CalibrateFanAsync();
            }
        }

        private void LoadThermalSettings()
        {
            ToggleWarnings.IsOn = LocalMachineSettingsEngine.EnableThermalWarnings;
            ToggleShutdown.IsOn = LocalMachineSettingsEngine.EnableThermalShutdown;
            ToggleAlarms.IsOn = LocalMachineSettingsEngine.EnableAudibleAlarms;
            ToggleLogging.IsOn = LocalMachineSettingsEngine.EnableThermalLogging;

            NumCooldown.Value = LocalMachineSettingsEngine.WarningCooldownMinutes > 0 ? LocalMachineSettingsEngine.WarningCooldownMinutes : 5;
            ComboEmergencyAction.SelectedIndex = LocalMachineSettingsEngine.EmergencyAction >= 0 ? LocalMachineSettingsEngine.EmergencyAction : 0;

            NumEmergencyDelay.Value = LocalMachineSettingsEngine.EmergencyThresholdSeconds > 0 ? LocalMachineSettingsEngine.EmergencyThresholdSeconds : 5;

            NumCpuWarn.Value = LocalMachineSettingsEngine.CpuWarningTemp;
            NumCpuMax.Value = LocalMachineSettingsEngine.CpuMaxTemp;

            NumGpuWarn.Value = LocalMachineSettingsEngine.GpuWarningTemp;
            NumGpuMax.Value = LocalMachineSettingsEngine.GpuMaxTemp;

            NumRamWarn.Value = LocalMachineSettingsEngine.RamWarningTemp;
            NumRamMax.Value = LocalMachineSettingsEngine.RamMaxTemp;

            NumMoboWarn.Value = LocalMachineSettingsEngine.MoboWarningTemp;
            NumMoboMax.Value = LocalMachineSettingsEngine.MoboMaxTemp;
        }

        private void ToggleView_Click(object sender, RoutedEventArgs e)
        {
            if (!_isShowingThermalSettings)
            {
                SlideToThermalAnimation.Begin();
                HeaderTitle.Text = ResourceString.GetString("fan_title_thermal_safeguards");
                ToggleViewText.Text = ResourceString.GetString("fan_btn_fan_controls");
            }
            else
            {
                SlideToFansAnimation.Begin();
                HeaderTitle.Text = ResourceString.GetString("fan_title_system_fan_control");
                ToggleViewText.Text = ResourceString.GetString("fan_btn_thermal_settings");
            }

            _isShowingThermalSettings = !_isShowingThermalSettings;
        }

        private void SaveThermalBtn_Click(object sender, RoutedEventArgs e)
        {
            LocalMachineSettingsEngine.EnableThermalWarnings = ToggleWarnings.IsOn;
            LocalMachineSettingsEngine.EnableThermalShutdown = ToggleShutdown.IsOn;
            LocalMachineSettingsEngine.EnableAudibleAlarms = ToggleAlarms.IsOn;
            LocalMachineSettingsEngine.EnableThermalLogging = ToggleLogging.IsOn;

            LocalMachineSettingsEngine.WarningCooldownMinutes = double.IsNaN(NumCooldown.Value) ? 5 : (int)NumCooldown.Value;
            LocalMachineSettingsEngine.EmergencyAction = ComboEmergencyAction.SelectedIndex;

            int delayVal = double.IsNaN(NumEmergencyDelay.Value) ? 5 : (int)NumEmergencyDelay.Value;
            LocalMachineSettingsEngine.EmergencyThresholdSeconds = Math.Clamp(delayVal, 1, 120);

            int cpuWarn = double.IsNaN(NumCpuWarn.Value) ? 80 : (int)NumCpuWarn.Value;
            int cpuMax = double.IsNaN(NumCpuMax.Value) ? 95 : (int)NumCpuMax.Value;

            int gpuWarn = double.IsNaN(NumGpuWarn.Value) ? 80 : (int)NumGpuWarn.Value;
            int gpuMax = double.IsNaN(NumGpuMax.Value) ? 95 : (int)NumGpuMax.Value;

            int ramWarn = double.IsNaN(NumRamWarn.Value) ? 65 : (int)NumRamWarn.Value;
            int ramMax = double.IsNaN(NumRamMax.Value) ? 80 : (int)NumRamMax.Value;

            int moboWarn = double.IsNaN(NumMoboWarn.Value) ? 60 : (int)NumMoboWarn.Value;
            int moboMax = double.IsNaN(NumMoboMax.Value) ? 80 : (int)NumMoboMax.Value;

            LocalMachineSettingsEngine.CpuWarningTemp = Math.Min(cpuWarn, cpuMax - 5);
            LocalMachineSettingsEngine.CpuMaxTemp = cpuMax;

            LocalMachineSettingsEngine.GpuWarningTemp = Math.Min(gpuWarn, gpuMax - 5);
            LocalMachineSettingsEngine.GpuMaxTemp = gpuMax;

            LocalMachineSettingsEngine.RamWarningTemp = Math.Min(ramWarn, ramMax - 5);
            LocalMachineSettingsEngine.RamMaxTemp = ramMax;

            LocalMachineSettingsEngine.MoboWarningTemp = Math.Min(moboWarn, moboMax - 5);
            LocalMachineSettingsEngine.MoboMaxTemp = moboMax;

            SlideToFansAnimation.Begin();
            HeaderTitle.Text = ResourceString.GetString("fan_title_system_fan_control");
            ToggleViewText.Text = ResourceString.GetString("fan_btn_thermal_settings");
            _isShowingThermalSettings = false;
        }

        private void ResetThermalBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleWarnings.IsOn = true;
            ToggleShutdown.IsOn = false;
            ToggleAlarms.IsOn = false;
            ToggleLogging.IsOn = false;

            NumCooldown.Value = 5;
            ComboEmergencyAction.SelectedIndex = 0;
            NumEmergencyDelay.Value = 5;

            NumCpuWarn.Value = 80; NumCpuMax.Value = 95;
            NumGpuWarn.Value = 80; NumGpuMax.Value = 95;
            NumRamWarn.Value = 65; NumRamMax.Value = 80;
            NumMoboWarn.Value = 60; NumMoboMax.Value = 80;
        }

        private async void EditFan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FanControlViewModel vm)
            {
                var nameBox = new TextBox { Text = vm.Name, Header = ResourceString.GetString("fan_dialog_sensor_name"), Margin = new Thickness(0, 0, 0, 16) };

                var typeBox = new ComboBox { Header = ResourceString.GetString("fan_dialog_category"), HorizontalAlignment = HorizontalAlignment.Stretch };
                typeBox.Items.Add(ResourceString.GetString("fan_cat_cpu"));
                typeBox.Items.Add(ResourceString.GetString("fan_cat_pump"));
                typeBox.Items.Add(ResourceString.GetString("fan_cat_chassis"));
                typeBox.Items.Add(ResourceString.GetString("fan_cat_gpu"));

                typeBox.SelectedIndex = vm.DeviceType == CoolingDeviceType.CpuFan ? 0 :
                                        vm.DeviceType == CoolingDeviceType.WaterPump ? 1 :
                                        vm.DeviceType == CoolingDeviceType.GpuFan ? 3 : 2;

                var panel = new StackPanel();
                panel.Children.Add(nameBox);
                panel.Children.Add(typeBox);

                var dialog = new ContentDialog
                {
                    Title = ResourceString.GetString("fan_dialog_edit_title"),
                    Content = panel,
                    PrimaryButtonText = ResourceString.GetString("fan_dialog_save"),
                    CloseButtonText = ResourceString.GetString("fan_dialog_cancel"),
                    XamlRoot = this.RootGrid.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    CpuFans.Remove(vm);
                    WaterPumps.Remove(vm);
                    CaseFans.Remove(vm);
                    GpuFans.Remove(vm);

                    var newType = typeBox.SelectedIndex == 0 ? CoolingDeviceType.CpuFan :
                                  typeBox.SelectedIndex == 1 ? CoolingDeviceType.WaterPump :
                                  typeBox.SelectedIndex == 3 ? CoolingDeviceType.GpuFan :
                                  CoolingDeviceType.CaseFan;

                    vm.SavePreferences(nameBox.Text, newType);

                    switch (vm.DeviceType)
                    {
                        case CoolingDeviceType.CpuFan: CpuFans.Add(vm); break;
                        case CoolingDeviceType.WaterPump: WaterPumps.Add(vm); break;
                        case CoolingDeviceType.GpuFan: GpuFans.Add(vm); break;
                        default: CaseFans.Add(vm); break;
                    }
                }
            }
        }

        private void ConfigureWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

            var mainWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;
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

        private void AutoButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            if (btn.Tag is FanControlViewModel vm)
            {
                vm.RevertToAuto();
                vm.CurrentPercentage = vm.DeviceType == CoolingDeviceType.WaterPump ? 100f : 0f;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            UIHelper.SetOverlay(false);
        }

        private void CurveGrid_Loaded(object sender, RoutedEventArgs e)
        {
            var grid = (Grid)sender;
            if (grid.DataContext is FanControlViewModel vm)
            {
                var polyline = (Polyline)grid.FindName("CurveLine");
                _curveLines[vm] = polyline;
                RedrawCurveLine(vm);
            }
        }

        private void RedrawCurveLine(FanControlViewModel vm)
        {
            if (_curveLines.TryGetValue(vm, out Polyline? line))
            {
                var points = new Microsoft.UI.Xaml.Media.PointCollection();
                var sortedPoints = vm.CurvePoints.OrderBy(p => p.Temperature).ToList();

                if (sortedPoints.Any())
                {
                    double firstY = 150.0 - (sortedPoints.First().SpeedPercentage / 100.0 * 150.0);
                    points.Add(new Windows.Foundation.Point(0, firstY));

                    foreach (var pt in sortedPoints)
                    {
                        double x = ((pt.Temperature - 20) / 80.0) * 400.0;
                        double y = 150.0 - (pt.SpeedPercentage / 100.0 * 150.0);
                        points.Add(new Windows.Foundation.Point(x, y));
                    }

                    double lastY = 150.0 - (sortedPoints.Last().SpeedPercentage / 100.0 * 150.0);
                    points.Add(new Windows.Foundation.Point(400, lastY));
                }

                line.Points = points;
            }
        }

        private void Point_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is FanCurvePoint point)
            {
                if (VisualTreeHelper.GetParent(grid) is ContentPresenter presenter)
                {
                    Canvas.SetLeft(presenter, ((point.Temperature - 20) / 80.0) * 400.0);
                    Canvas.SetTop(presenter, 150.0 - (point.SpeedPercentage / 100.0 * 150.0));
                }
            }
        }

        private void Point_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is FanCurvePoint point)
            {
                _isDraggingDot = true;
                _draggedDot = grid;
                _draggedPoint = point;

                grid.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void Point_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDraggingDot && _draggedDot != null && _draggedDot == (sender as Grid) && _draggedPoint != null)
            {
                var canvas = GetParentCanvas(_draggedDot);
                if (canvas != null)
                {
                    var ptr = e.GetCurrentPoint(canvas).Position;

                    double newTemp = (ptr.X / 5.0) + 20.0;
                    double newSpeed = (150.0 - ptr.Y) / 1.5;

                    _draggedPoint.Temperature = Math.Clamp(newTemp, 20, 100);
                    _draggedPoint.SpeedPercentage = Math.Clamp(newSpeed, 0, 100);

                    if (VisualTreeHelper.GetParent(_draggedDot) is ContentPresenter presenter)
                    {
                        Canvas.SetLeft(presenter, ((_draggedPoint.Temperature - 20) / 80.0) * 400.0);
                        Canvas.SetTop(presenter, 150.0 - (_draggedPoint.SpeedPercentage / 100.0 * 150.0));
                    }

                    var vm = GetViewModelFromElement(_draggedDot);
                    if (vm != null)
                    {
                        RedrawCurveLine(vm);
                    }
                }
            }
        }

        private void Point_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDraggingDot && _draggedDot != null)
            {
                _draggedDot.ReleasePointerCapture(e.Pointer);

                var vm = GetViewModelFromElement(_draggedDot);
                if (vm != null)
                {
                    vm.IsManualControl = true;

                    vm.SavePreferences(vm.Name, vm.DeviceType);

                    HardwareTemperatureService.Instance.UpdateSensors();
                    vm.UpdateReadings();
                }

                _isDraggingDot = false;
                _draggedDot = null;
                _draggedPoint = null;
                e.Handled = true;
            }
        }

        private void AddCurvePoint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FanControlViewModel vm)
            {
                if (vm.CurvePoints.Count >= 8) return;

                var sorted = vm.CurvePoints.OrderBy(p => p.Temperature).ToList();
                double maxGap = 0;
                int insertIndex = 0;

                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    double gap = sorted[i + 1].Temperature - sorted[i].Temperature;
                    if (gap > maxGap)
                    {
                        maxGap = gap;
                        insertIndex = i;
                    }
                }

                double newTemp = 60;
                double newSpeed = 50;

                if (sorted.Count >= 2 && maxGap > 0)
                {
                    newTemp = sorted[insertIndex].Temperature + (maxGap / 2.0);
                    newSpeed = (sorted[insertIndex].SpeedPercentage + sorted[insertIndex + 1].SpeedPercentage) / 2.0;
                }
                else if (sorted.Count > 0)
                {
                    newTemp = Math.Min(100, sorted.Last().Temperature + 10);
                    newSpeed = Math.Min(100, sorted.Last().SpeedPercentage + 10);
                }

                vm.CurvePoints.Add(new FanCurvePoint(Math.Round(newTemp), Math.Round(newSpeed)));

                RedrawCurveLine(vm);
                vm.IsManualControl = true;
                vm.SavePreferences(vm.Name, vm.DeviceType);
            }
        }

        private void ResetCurveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FanControlViewModel vm)
            {
                vm.CurvePoints.Clear();

                vm.CurvePoints.Add(new FanCurvePoint(30, 30));
                vm.CurvePoints.Add(new FanCurvePoint(50, 50));
                vm.CurvePoints.Add(new FanCurvePoint(70, 75));
                vm.CurvePoints.Add(new FanCurvePoint(85, 100));

                RedrawCurveLine(vm);
                vm.IsManualControl = true;
                vm.SavePreferences(vm.Name, vm.DeviceType);
            }
        }

        private void Point_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is FanCurvePoint point)
            {
                var vm = GetViewModelFromElement(grid);

                if (vm != null && vm.CurvePoints.Count > 4)
                {
                    vm.CurvePoints.Remove(point);
                    RedrawCurveLine(vm);
                    vm.SavePreferences(vm.Name, vm.DeviceType);
                }
            }
        }

        private Canvas? GetParentCanvas(DependencyObject element)
        {
            while (element != null)
            {
                if (element is Canvas c) return c;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private FanControlViewModel? GetViewModelFromElement(DependencyObject element)
        {
            while (element != null)
            {
                if (element is FrameworkElement fe && fe.DataContext is FanControlViewModel vm)
                    return vm;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }
    }

    public class TempToXConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {
                double temp = System.Convert.ToDouble(value);
                return ((temp - 20) / 80.0) * 400.0;
            }
            return 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class SpeedToYConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {
                double speed = System.Convert.ToDouble(value);
                return 150.0 - ((speed / 100.0) * 150.0);
            }
            return 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}