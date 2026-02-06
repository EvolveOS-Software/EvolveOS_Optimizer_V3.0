using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;

namespace EvolveOS_Optimizer
{
    public sealed partial class MainWindow : Window
    {
        private AppWindow? _appWindow;
        private IntPtr _hWnd;

        public MainWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var viewModel = RootGrid.DataContext as MainWinViewModel;
            if (viewModel != null)
            {
                // future use
            }

            if (_appWindow != null)
            {
                _appWindow.SetIcon("Assets/EvolveOS_Optimizer.ico");

                var titleBar = _appWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                _appWindow.Resize(new Windows.Graphics.SizeInt32(1575, 870));
            }

            WindowHelper.RegisterMinWidthHeight(_hWnd, 700, 400);

            SetBackdrop(new MicaBackdrop());
            CenterWindow();

            // ContentFrame.Navigate(typeof(Pages.HomePage));
        }

        public void SetBackdrop(SystemBackdrop backdrop)
        {
            this.SystemBackdrop = backdrop;
        }

        public void SetBackdropByName(string name)
        {
            this.SystemBackdrop = name switch
            {
                "Mica" => new MicaBackdrop(),
                "MicaAlt" => new MicaBackdrop() { Kind = MicaKind.BaseAlt },
                "Acrylic" => new DesktopAcrylicBackdrop(),
                _ => null
            };
        }

        private void CenterWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

                if (displayArea != null)
                {
                    var centeredPos = appWindow.Position;

                    centeredPos.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                    centeredPos.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;

                    appWindow.Move(centeredPos);
                }
            }
        }
    }
}