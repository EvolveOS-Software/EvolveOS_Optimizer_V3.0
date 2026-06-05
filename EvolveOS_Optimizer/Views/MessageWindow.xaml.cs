// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using WinRT;
using WinRT.Interop;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class MessageWindow : Window
    {
        private TimerControlManager? _timer = default;
        private MicaController? _micaController;
        private SystemBackdropConfiguration? _configurationSource;

        public MessageWindow(MessageWindowState windowState = MessageWindowState.Warning)
        {
            this.InitializeComponent();

            SettingsEngine.CheckingParameters();

            ConfigureWindow();

            this.SystemBackdrop = null;

            if (this.Content is FrameworkElement rootElement)
            {
                if (rootElement is Panel rootPanel)
                {
                    rootPanel.Background = new SolidColorBrush(Colors.Transparent);
                }

                string savedTheme = SettingsEngine.AppTheme;
                rootElement.RequestedTheme = savedTheme == "Dark" ? ElementTheme.Dark :
                                            savedTheme == "Light" ? ElementTheme.Light : ElementTheme.Default;
            }

            this.Activated += (s, e) =>
            {
                if (_micaController == null)
                {
                    ApplyForcedBackdrop();
                }
            };

            WarningContent.Visibility = windowState == MessageWindowState.Warning ? Visibility.Visible : Visibility.Collapsed;
            NotSupportContent.Visibility = windowState == MessageWindowState.NotSupported ? Visibility.Visible : Visibility.Collapsed;
            AlreadyRunningContent.Visibility = windowState == MessageWindowState.AlreadyRunning ? Visibility.Visible : Visibility.Collapsed;

            this.Closed += (s, e) =>
            {
                _timer?.Stop();
                _micaController?.Dispose();
            };

            _timer = new TimerControlManager(TimeSpan.FromSeconds(4), TimerControlManager.TimerMode.CountDown, time =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    string currentContent = BtnAccept.Content?.ToString() ?? "";
                    BtnAccept.Content = $"{new Regex("[(05)(04)(03)(02)]").Replace(currentContent, "")}({time:ss})";
                });
            }, () =>
            {
                this.DispatcherQueue.TryEnqueue(() => App.ExitApp());
            });

            _timer.Start();
        }

        private void ApplyForcedBackdrop()
        {
            string backdropName = SettingsEngine.Backdrop;

            if (backdropName != "Mica" && backdropName != "MicaAlt")
            {
                UIHelper.ApplyBackdrop(this, backdropName);
                return;
            }

            if (!MicaController.IsSupported()) return;

            _micaController?.Dispose();
            _micaController = new MicaController();

            _micaController.Kind = backdropName == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base;

            _configurationSource = new SystemBackdropConfiguration();
            _configurationSource.IsInputActive = true;

            string savedTheme = SettingsEngine.AppTheme;
            _configurationSource.Theme = savedTheme == "Dark" ? SystemBackdropTheme.Dark :
                                         savedTheme == "Light" ? SystemBackdropTheme.Light : SystemBackdropTheme.Default;

            try
            {
                var target = this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();
                _micaController.AddSystemBackdropTarget(target);
                _micaController.SetSystemBackdropConfiguration(_configurationSource);
            }
            catch { }
        }

        private void ConfigureWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            int style = Win32Helper.GetWindowLong(hwnd, Win32Helper.GWL_STYLE);
            Win32Helper.SetWindowLong(hwnd, Win32Helper.GWL_STYLE, style & ~Win32Helper.WS_CAPTION & ~Win32Helper.WS_THICKFRAME);

            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
                if (appWindow.TitleBar != null) appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            }

            int width = 400; int height = 200;
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredX, centeredY, width, height));
            }
        }

        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                Win32Helper.SendMessage(hwnd, 0x00A1, (IntPtr)2, IntPtr.Zero);
            }
        }

        private void BtnAccept_Click(object sender, RoutedEventArgs e) => App.ExitApp();
    }
}