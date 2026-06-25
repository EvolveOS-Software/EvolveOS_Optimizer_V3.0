// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class TaskbarMonitorWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _syncTimer;
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _uiWatchdogTimer;

        private bool _isHiddenBySystem = false;

        private int _currentXOffset = 650;
        private bool _isDragging = false;
        private int _dragStartX;
        private int _initialXOffset;

        public TaskbarMonitorWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
            }

            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            TaskbarOverlayManager.InjectIntoTaskbar(_hWnd);

            // Position (e.g., 400px from the right edge, 6px down from the top of the taskbar)
            TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, 650, 6);

            TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, _currentXOffset, 6);

            _appWindow.Resize(new SizeInt32(355, 40));

            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _syncTimer = queue.CreateTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(500);
            _syncTimer.Tick += SyncTimer_Tick;
            _syncTimer.Start();

            _uiWatchdogTimer = queue.CreateTimer();
            _uiWatchdogTimer.Interval = TimeSpan.FromMilliseconds(32); // ~30 FPS
            _uiWatchdogTimer.Tick += UiWatchdogTimer_Tick;
            _uiWatchdogTimer.Start();
        }

        private void UiWatchdogTimer_Tick(object sender, object e)
        {
            bool shouldHide = TaskbarOverlayManager.ShouldHideWidget();

            if (shouldHide && !_isHiddenBySystem)
            {
                _appWindow.Hide();
                _isHiddenBySystem = true;
            }
            else if (!shouldHide && _isHiddenBySystem)
            {
                _appWindow.Show();
                TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, _currentXOffset, 8);
                _isHiddenBySystem = false;
            }
        }

        private void SyncTimer_Tick(object sender, object e)
        {
            if (_isHiddenBySystem) return;

            var vm = Core.ViewModel.DiagnosticsPageViewModel.Current;
            if (vm != null)
            {
                TxtCpu.Text = vm.CurrentCpuLoadStr;
                TxtRam.Text = vm.CurrentRamLoadStr;
                TxtGpu.Text = vm.CurrentGpuLoadStr;
                TxtNet.Text = vm.CurrentNetworkLoadSecondaryStr;
            }
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            RootGrid.CapturePointer(e.Pointer);
            _isDragging = true;

            GetCursorPos(out POINT pt);
            _dragStartX = pt.X;
            _initialXOffset = TaskbarOverlayManager.GetCurrentWidgetXOffset(_hWnd);
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                GetCursorPos(out POINT pt);
                int deltaX = pt.X - _dragStartX;

                _currentXOffset = _initialXOffset - deltaX;

                TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, _currentXOffset, 6);
            }
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
        }

        private int GetRandom(int min, int max) => new Random().Next(min, max);

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _syncTimer.Stop();
            _uiWatchdogTimer.Stop();
            this.Close();
        }
    }
}