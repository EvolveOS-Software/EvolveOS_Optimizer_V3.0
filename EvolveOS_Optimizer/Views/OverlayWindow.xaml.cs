using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class OverlayWindow : Window
    {
        private AppWindow _appWindow;

        public OverlayWindow()
        {
            this.InitializeComponent();

            _appWindow = GetAppWindowForCurrentWindow();


            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

            var presenter = _appWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(myWndId);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}