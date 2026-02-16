using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class TerminalOutputWindow : Window
    {
        private bool _errorDetected = false;
        private bool _isBackdropInitialized = false;

        private readonly IntPtr _hWnd;

        public TerminalOutputWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(720, 540));

            Win32Helper.HideFromTaskbar(_hWnd);

            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            Win32Helper.SetWindowPos(_hWnd, Win32Helper.HWND_TOPMOST, 0, 0, 0, 0,
            Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOSIZE);

            this.Activated += TerminalOutputWindow_Activated;

            /*this.AppWindow.Changed += (s, e) =>
            {
                if (e.DidPositionChange && this.AppWindow.IsVisible)
                {
                    // Not in use.... (If something forces a move, centered logic could go here)
                }
            };*/

            this.Closed += (s, e) =>
            {
                UIHelper.SetOverlay(false);
            };
        }

        private void TerminalOutputWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (!_isBackdropInitialized && args.WindowActivationState != WindowActivationState.Deactivated)
            {
                _isBackdropInitialized = true;
                this.DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(500);
                    try
                    {
                        UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Backdrop] Exception: {ex.Message}"); }
                });
            }
            else if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    if (this.AppWindow != null && _hWnd != IntPtr.Zero)
                    {
                        try
                        {
                            Win32Helper.SetForegroundWindow(_hWnd);

                            this.AppWindow.MoveInZOrderAtTop();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FocusTrap] Failed to re-focus: {ex.Message}");
                        }
                    }
                });
            }
        }

        public void AppendOutput(string text)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (string.IsNullOrEmpty(text)) return;

                RTBOutput.IsReadOnly = false;

                try
                {
                    var document = RTBOutput.Document;

                    document.Selection.SetRange(document.Selection.EndPosition, document.Selection.EndPosition);

                    if (text.StartsWith("[ERROR]"))
                    {
                        _errorDetected = true;
                        StatusSpinner.Visibility = Visibility.Collapsed;
                        StatusWarning.Visibility = Visibility.Visible;

                        document.Selection.CharacterFormat.ForegroundColor = Colors.Red;
                        document.Selection.TypeText(text + Environment.NewLine);
                    }
                    else if (text.StartsWith("[CRITICAL ERROR]"))
                    {
                        _errorDetected = true;
                        document.Selection.CharacterFormat.ForegroundColor = Colors.DarkRed;
                        document.Selection.CharacterFormat.Bold = FormatEffect.On;
                        document.Selection.TypeText(text + Environment.NewLine);
                    }
                    else
                    {
                        document.Selection.CharacterFormat.ForegroundColor = ColorHelper.ToColor("#DCDCDC");
                        document.Selection.CharacterFormat.Bold = FormatEffect.Off;
                        document.Selection.TypeText(text + Environment.NewLine);
                    }

                    var endRange = document.GetRange(document.Selection.EndPosition, document.Selection.EndPosition);
                    endRange.ScrollIntoView(PointOptions.None);
                }
                finally
                {
                    RTBOutput.IsReadOnly = true;
                }
            });
        }

        private void RTBOutput_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            IntPtr arrowCursor = Win32Helper.LoadCursor(IntPtr.Zero, Win32Helper.IDC_ARROW);
            Win32Helper.SetCursor(arrowCursor);
        }

        public void MarkAsFinished()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                BtnClose.IsEnabled = true;
                StatusSpinner.Visibility = Visibility.Collapsed;

                if (!_errorDetected)
                {
                    StatusCheck.Visibility = Visibility.Visible;
                    StatusWarning.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            RTBOutput.Document.GetText(TextGetOptions.None, out string text);
            if (string.IsNullOrWhiteSpace(text)) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);

            CopyIcon.Symbol = Symbol.Accept;
            _ = ResetCopyIcon();
        }

        private async Task ResetCopyIcon()
        {
            await Task.Delay(2000);
            CopyIcon.Symbol = Symbol.Copy;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            UIHelper.SetOverlay(false);
            this.Close();
        }
    }

    public static class ColorHelper
    {
        public static Windows.UI.Color ToColor(string hex)
        {
            hex = hex.Replace("#", "");
            byte a = 255;
            int pos = 0;
            if (hex.Length == 8)
            {
                a = (byte)Convert.ToUInt32(hex.Substring(pos, 2), 16);
                pos = 2;
            }
            byte r = (byte)Convert.ToUInt32(hex.Substring(pos, 2), 16);
            byte g = (byte)Convert.ToUInt32(hex.Substring(pos + 2, 2), 16);
            byte b = (byte)Convert.ToUInt32(hex.Substring(pos + 4, 2), 16);

            return Color.FromArgb(a, r, g, b);
        }
    }
}