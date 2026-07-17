// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class CpuCoreTemperaturesWindow : Window
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


        public CpuCoreTemperaturesViewModel ViewModel { get; }

        public CpuCoreTemperaturesWindow()
        {
            this.InitializeComponent();

            ViewModel = new CpuCoreTemperaturesViewModel();

            UIHelper.SetOverlay(true, true);
            ConfigureWindow();
            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += RootElement_Loaded;
            }
        }

        private async void RootElement_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();
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
            int physicalHeight = (int)(840 * scale);

            var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int x = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - physicalWidth) / 2);
                int y = displayArea.WorkArea.Y + ((displayArea.WorkArea.Height - physicalHeight) / 2);

                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, physicalWidth, physicalHeight));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            ViewModel.StopPolling();

            UIHelper.SetOverlay(false);
        }
    }
}