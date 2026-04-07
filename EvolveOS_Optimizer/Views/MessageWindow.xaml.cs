using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Views
{
    public enum MessageWindowState
    {
        Warning,
        NotSupported,
        AlreadyRunning
    }

    public sealed partial class MessageWindow : Window
    {
        private TimerControlManager? _timer = default;

        public MessageWindow(MessageWindowState windowState = MessageWindowState.Warning)
        {
            this.InitializeComponent();

            ConfigureWindow();

            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);

            WarningContent.Visibility = windowState == MessageWindowState.Warning ? Visibility.Visible : Visibility.Collapsed;
            NotSupportContent.Visibility = windowState == MessageWindowState.NotSupported ? Visibility.Visible : Visibility.Collapsed;
            AlreadyRunningContent.Visibility = windowState == MessageWindowState.AlreadyRunning ? Visibility.Visible : Visibility.Collapsed;

            this.Closed += delegate { _timer?.Stop(); };

            _timer = new TimerControlManager(TimeSpan.FromSeconds(4), TimerControlManager.TimerMode.CountDown, time =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    string currentContent = BtnAccept.Content?.ToString() ?? "";
                    BtnAccept.Content = $"{new Regex("[(05)(04)(03)(02)]").Replace(currentContent, "")}({time:ss})";
                });
            }, () =>
            {
                this.DispatcherQueue.TryEnqueue(() => Application.Current.Exit());
            });

            _timer.Start();
        }

        private void ConfigureWindow()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            int width = 400;
            int height = 250;

            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;

                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredX, centeredY, width, height));
            }
            else
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            }
        }

        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Win32Helper.SendMessage(hwnd, 0x00A1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
            }
        }

        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }
    }
}