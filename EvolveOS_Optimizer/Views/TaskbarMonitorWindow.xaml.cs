// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class TaskbarMonitorWindow : Window
    {
        #region Native Interop
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        private const uint ABM_GETTASKBARPOS = 0x00000005;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }
        #endregion

        #region Fields
        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;
        private readonly DispatcherQueueTimer _syncTimer;
        private readonly DispatcherQueueTimer _uiWatchdogTimer;

        private bool _isHiddenBySystem = false;
        private int _currentHorizontalOffset = 300;
        private int _currentVerticalOffset = 180;
        private int _initialOffset;

        private bool _isDragging = false;
        private int _dragStartX;
        private int _dragStartY;

        private uint _lastEdge = 999;
        private int _lastOffset = -1;
        #endregion

        #region Constructor
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

            UpdateTaskbarPosition(forceUpdate: true);

            var queue = DispatcherQueue.GetForCurrentThread();
            _syncTimer = queue.CreateTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(500);
            _syncTimer.Tick += SyncTimer_Tick;
            _syncTimer.Start();

            _uiWatchdogTimer = queue.CreateTimer();
            _uiWatchdogTimer.Interval = TimeSpan.FromMilliseconds(32);
            _uiWatchdogTimer.Tick += UiWatchdogTimer_Tick;
            _uiWatchdogTimer.Start();
        }
        #endregion

        #region Taskbar Positioning
        private uint GetTaskbarEdge()
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
            return abd.uEdge;
        }

        private void UpdateTaskbarPosition(bool forceUpdate = false)
        {
            uint currentEdge = GetTaskbarEdge();
            int currentOffset = (currentEdge == 0 || currentEdge == 2) ? _currentVerticalOffset : _currentHorizontalOffset;

            if (forceUpdate || currentEdge != _lastEdge || currentOffset != _lastOffset)
            {
                _lastEdge = currentEdge;
                _lastOffset = currentOffset;

                if (currentEdge == 0 || currentEdge == 2)
                {
                    _appWindow.Resize(new SizeInt32(40, 300));

                    if (StatsPanel != null)
                    {
                        MainBorder.MinWidth = 0;
                        MainBorder.MinHeight = 280;

                        StatsPanel.Orientation = Orientation.Vertical;
                        StatsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                        StatsPanel.Spacing = 16;

                        CpuPanel.Orientation = Orientation.Vertical;
                        RamPanel.Orientation = Orientation.Vertical;
                        GpuPanel.Orientation = Orientation.Vertical;
                        NetPanel.Orientation = Orientation.Vertical;

                        CpuPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                        RamPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                        GpuPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                        NetPanel.HorizontalAlignment = HorizontalAlignment.Stretch;

                        CpuPanel.Width = double.NaN;
                        RamPanel.Width = double.NaN;
                        GpuPanel.Width = double.NaN;
                        NetPanel.Width = double.NaN;

                        TxtCpu.TextAlignment = TextAlignment.Center;
                        TxtRam.TextAlignment = TextAlignment.Center;
                        TxtGpu.TextAlignment = TextAlignment.Center;
                        TxtNet.TextAlignment = TextAlignment.Center;

                        TxtCpu.FontSize = 10;
                        TxtRam.FontSize = 10;
                        TxtGpu.FontSize = 10;
                        TxtNet.FontSize = 9;

                        TxtNet.TextWrapping = TextWrapping.Wrap;

                        BtnClose.Margin = new Thickness(0, 5, 0, 0);
                        BtnClose.HorizontalAlignment = HorizontalAlignment.Center;
                    }

                    TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, _currentVerticalOffset, 40, 300);
                }
                else
                {
                    _appWindow.Resize(new SizeInt32(355, 40));

                    if (StatsPanel != null)
                    {
                        MainBorder.MinWidth = 280;
                        MainBorder.MinHeight = 0;

                        StatsPanel.Orientation = Orientation.Horizontal;
                        StatsPanel.HorizontalAlignment = HorizontalAlignment.Center;
                        StatsPanel.Spacing = 10;

                        CpuPanel.Orientation = Orientation.Horizontal;
                        RamPanel.Orientation = Orientation.Horizontal;
                        GpuPanel.Orientation = Orientation.Horizontal;
                        NetPanel.Orientation = Orientation.Horizontal;

                        CpuPanel.HorizontalAlignment = HorizontalAlignment.Left;
                        RamPanel.HorizontalAlignment = HorizontalAlignment.Left;
                        GpuPanel.HorizontalAlignment = HorizontalAlignment.Left;
                        NetPanel.HorizontalAlignment = HorizontalAlignment.Left;

                        CpuPanel.Width = 50;
                        RamPanel.Width = 50;
                        GpuPanel.Width = 50;
                        NetPanel.Width = 115;

                        TxtCpu.TextAlignment = TextAlignment.Left;
                        TxtRam.TextAlignment = TextAlignment.Left;
                        TxtGpu.TextAlignment = TextAlignment.Left;
                        TxtNet.TextAlignment = TextAlignment.Left;

                        TxtCpu.FontSize = 12;
                        TxtRam.FontSize = 12;
                        TxtGpu.FontSize = 12;
                        TxtNet.FontSize = 12;

                        TxtNet.TextWrapping = TextWrapping.NoWrap;

                        BtnClose.Margin = new Thickness(5, 0, 0, 0);
                        BtnClose.HorizontalAlignment = HorizontalAlignment.Right;
                    }

                    TaskbarOverlayManager.PositionInsideTaskbar(_hWnd, _currentHorizontalOffset, 355, 40);
                }
            }
        }
        #endregion

        #region Timers
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
                UpdateTaskbarPosition(forceUpdate: true);
                _isHiddenBySystem = false;
            }
            else if (!shouldHide)
            {
                UpdateTaskbarPosition();
            }
        }

        private void SyncTimer_Tick(object sender, object e)
        {
            if (_isHiddenBySystem) return;

            var vm = DiagnosticsPageViewModel.Current;
            if (vm != null)
            {
                TxtCpu.Text = vm.CurrentCpuLoadStr;
                TxtRam.Text = vm.CurrentRamLoadStr;
                TxtGpu.Text = vm.CurrentGpuLoadStr;
                TxtNet.Text = vm.CurrentNetworkLoadSecondaryStr;
            }
        }
        #endregion

        #region Pointer Events
        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            RootGrid.CapturePointer(e.Pointer);
            _isDragging = true;

            GetCursorPos(out POINT pt);
            _dragStartX = pt.X;
            _dragStartY = pt.Y;

            uint edge = GetTaskbarEdge();
            _initialOffset = (edge == 0 || edge == 2) ? _currentVerticalOffset : _currentHorizontalOffset;
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                GetCursorPos(out POINT pt);
                uint edge = GetTaskbarEdge();

                if (edge == 0 || edge == 2)
                {
                    int deltaY = pt.Y - _dragStartY;
                    _currentVerticalOffset = _initialOffset - deltaY;
                }
                else
                {
                    int deltaX = pt.X - _dragStartX;
                    _currentHorizontalOffset = _initialOffset - deltaX;
                }

                UpdateTaskbarPosition();
            }
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
        }
        #endregion

        #region Buttons
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _syncTimer.Stop();
            _uiWatchdogTimer.Stop();
            this.Close();
        }
        #endregion
    }
}