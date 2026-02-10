using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace EvolveOS_Optimizer.Views
{
    public partial class NotificationWindow : Window
    {
        private NotificationManager.NoticeAction _action;
        private bool _isHovered = false;
        private DispatcherTimer? _autoCloseTimer;
        private DateTime _startTime;
        private double _totalDuration = 4000;
        private double _elapsedAtPause = 0;

        public NotificationWindow(string title, string text, NotificationManager.NoticeAction action, NotificationManager.NoticeSeverity severity, int durationMs)
        {
            this.InitializeComponent();
            HeaderLabel.Text = title;
            MessageLabel.Text = text;
            _action = action;
            _totalDuration = durationMs;

            SetSeverityUI(severity);
            InitializeWindowLayout();
            StartTimer();
        }

        private void SetSeverityUI(NotificationManager.NoticeSeverity severity)
        {
            var infoBrush = Application.Current.Resources["Color_Info"] as Brush;
            var errorBrush = Application.Current.Resources["Color_Error"] as Brush;
            var warningBrush = Application.Current.Resources["Color_Warning"] as Brush;
            var successBrush = Application.Current.Resources["Color_Success"] as Brush;
            var accentBrush = Application.Current.Resources["MyDynamicAccentBrush"] as Brush;

            switch (severity)
            {
                case NotificationManager.NoticeSeverity.Info:
                    SeverityIcon.Glyph = "\uE946";
                    ApplyColors(infoBrush ?? accentBrush);
                    break;
                case NotificationManager.NoticeSeverity.Warning:
                    SeverityIcon.Glyph = "\uE7BA";
                    ApplyColors(warningBrush ?? accentBrush);
                    break;
                case NotificationManager.NoticeSeverity.Error:
                    SeverityIcon.Glyph = "\uEA39";
                    ApplyColors(errorBrush ?? accentBrush);
                    break;
                case NotificationManager.NoticeSeverity.Success:
                    SeverityIcon.Glyph = "\uE73E";
                    ApplyColors(successBrush ?? accentBrush);
                    break;
            }
        }

        private void ApplyColors(Brush? brush)
        {
            if (brush == null) return;
            SeverityIcon.Foreground = brush;
            ProgressTimer.Foreground = brush;
        }

        private void StartTimer()
        {
            _startTime = DateTime.Now;
            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromMilliseconds(10);

            _autoCloseTimer.Tick += AutoCloseTimer_Tick;

            _autoCloseTimer.Start();
        }

        private void AutoCloseTimer_Tick(object? sender, object e)
        {
            if (_isHovered) return;

            var elapsed = (DateTime.Now - _startTime).TotalMilliseconds + _elapsedAtPause;
            double percentage = (elapsed / _totalDuration) * 100;

            if (percentage >= 100)
            {
                ProgressTimer.Value = 100;
                _autoCloseTimer?.Stop();
                this.Close();
            }
            else
            {
                ProgressTimer.Value = percentage;
            }
        }

        private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _isHovered = true;
            _elapsedAtPause += (DateTime.Now - _startTime).TotalMilliseconds;
        }

        private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _isHovered = false;
            _startTime = DateTime.Now;
            _autoCloseTimer?.Start();
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText($"{HeaderLabel.Text}\n{MessageLabel.Text}");
            Clipboard.SetContent(dataPackage);

            string copiedText = ResourceString.GetString("copied_to_clipboard_noty");
            string originalToolTip = ResourceString.GetString("click_to_copy_noty");

            CopyIcon.Glyph = "\uE73E";
            ToolTipService.SetToolTip(CopyButton, copiedText);

            await Task.Delay(1500);

            CopyIcon.Glyph = "\uE8C8";
            ToolTipService.SetToolTip(CopyButton, originalToolTip);
        }


        private void RootGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_action == NotificationManager.NoticeAction.Restart)
                Process.Start("shutdown.exe", "-r -t 0");
            else if (_action == NotificationManager.NoticeAction.Logout)
                Process.Start("shutdown.exe", "-l");
        }

        private void InitializeWindowLayout()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

                var presenter = appWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.IsResizable = false;
                    presenter.IsAlwaysOnTop = true;
                    presenter.SetBorderAndTitleBar(false, false);
                }

                int width = 380;
                int height = 130;
                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var posX = displayArea.WorkArea.Width - width - 20;
                    var posY = displayArea.WorkArea.Height - height - 20;
                    appWindow.Move(new Windows.Graphics.PointInt32(posX, posY));
                }
            }

            int style = Win32Helper.GetWindowLong(hWnd, Win32Helper.GWL_STYLE);
            Win32Helper.SetWindowLong(hWnd, Win32Helper.GWL_STYLE, style & ~0x00C00000 & ~0x00040000);
            Win32Helper.SetWindowPos(hWnd, new IntPtr(-1), 0, 0, 0, 0,
                0x0020 | 0x0002 | 0x0001 | 0x0040 | 0x0010);

            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}