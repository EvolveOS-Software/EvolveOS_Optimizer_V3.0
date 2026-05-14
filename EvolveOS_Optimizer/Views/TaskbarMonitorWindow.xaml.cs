// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Windowing;
using EvolveOS_Optimizer.Utilities.Helpers;
using WinRT.Interop;
using Windows.Graphics;

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

        private int _currentXOffset = 400;
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

            AppWindow.Changed += AppWindow_Changed;

            TaskbarOverlayManager.InjectIntoTaskbar(_hWnd);

            // Position (e.g., 400px from the right edge, 6px down from the top of the taskbar)
            TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, 650, 6);

            _appWindow.Resize(new SizeInt32(355, 40));

            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _syncTimer = queue.CreateTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(500);
            _syncTimer.Tick += SyncTimer_Tick;
            _syncTimer.Start();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange)
            {
                var taskbarRect = TaskbarOverlayManager.GetTaskbarRect();
                _currentXOffset = taskbarRect.Right - sender.Position.X;
            }
        }

        private void SyncTimer_Tick(object sender, object e)
        {
            var vm = Core.ViewModel.DiagnosticsPageViewModel.Current;
            if (vm != null)
            {
                TxtCpu.Text = vm.CurrentCpuLoadStr;
                TxtRam.Text = vm.CurrentRamLoadStr;
                TxtGpu.Text = vm.CurrentGpuLoadStr;
                TxtNet.Text = vm.CurrentNetworkLoadSecondaryStr;
            }
        }

        private void RootGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RootGrid.CapturePointer(e.Pointer);
            _isDragging = true;

            GetCursorPos(out POINT pt);
            _dragStartX = pt.X;
            _initialXOffset = _currentXOffset;
        }

        private void RootGrid_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                GetCursorPos(out POINT pt);
                int deltaX = pt.X - _dragStartX;

                _currentXOffset = _initialXOffset - deltaX;

                TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, _currentXOffset, 6);
            }
        }

        private void RootGrid_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isDragging = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
        }

        private int GetRandom(int min, int max) => new Random().Next(min, max);

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _syncTimer.Stop();
            this.Close();
        }
    }
}