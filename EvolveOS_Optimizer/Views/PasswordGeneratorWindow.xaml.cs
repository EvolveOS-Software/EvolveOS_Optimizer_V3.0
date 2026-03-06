// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class PasswordGeneratorWindow : Window
    {
        public PasswordGeneratorViewModel ViewModel { get; }

        private bool _isBackdropInitialized = false;
        private IntPtr _hWnd;

        public PasswordGeneratorWindow()
        {
            this.InitializeComponent();

            ViewModel = new PasswordGeneratorViewModel();

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = ViewModel;
            }

            ApplyUserAccentColor();

            UIHelper.SetOverlay(true);

            _hWnd = WindowNative.GetWindowHandle(this);

            CenterAndSizeWindow(450, 650);

            SetDragRegion();

            this.Activated += PasswordGeneratorWindow_Activated;

            this.Closed += (s, e) =>
            {
                UIHelper.SetOverlay(false);
            };
        }

        #region Theming & Accent Colors

        private void ApplyUserAccentColor()
        {
            try
            {
                string hexColor = SettingsEngine.AccentColor;
                Color userColor = ColorFromHex(hexColor);

                if (this.Content is FrameworkElement root && root.Resources != null)
                {
                    root.Resources["SystemAccentColor"] = userColor;

                    if (root.Resources.TryGetValue("Brush_Accent", out object? brushObj) && brushObj is SolidColorBrush accentBrush)
                    {
                        accentBrush.Color = userColor;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PasswordGenerator] ApplyUserAccentColor Failed: {ex.Message}");
            }
        }

        private Color ColorFromHex(string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte a = 255;
            int pos = 0;

            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(pos, 2), System.Globalization.NumberStyles.HexNumber);
                pos += 2;
            }

            byte r = byte.Parse(hex.Substring(pos, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(pos + 2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(pos + 4, 2), System.Globalization.NumberStyles.HexNumber);

            return Microsoft.UI.ColorHelper.FromArgb(a, r, g, b);
        }

        #endregion

        #region Window Lifecycle & Focus Trapping

        private void PasswordGeneratorWindow_Activated(object sender, WindowActivatedEventArgs args)
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
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Backdrop] Exception: {ex.Message}");
                    }
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
                            Debug.WriteLine($"[FocusTrap] Failed to re-focus: {ex.Message}");
                        }
                    }
                });
            }
        }

        private void SetDragRegion()
        {
            var appWindow = this.AppWindow;

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;

                uint dpi = Win32Helper.GetDpiForWindow(_hWnd);
                double scaleAdjustment = (double)dpi / 96.0;

                RectInt32[] dragRects = new RectInt32[]
                {
                    new RectInt32
                    {
                        X = 0,
                        Y = 0,
                        Width = (int)(450 * scaleAdjustment),
                        Height = (int)(80 * scaleAdjustment)
                    }
                };

                titleBar.SetDragRectangles(dragRects);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CenterAndSizeWindow(int width, int height)
        {
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            var appWindow = AppWindow.GetFromWindowId(myWndId);

            appWindow.Resize(new SizeInt32 { Width = width, Height = height });

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            DisplayArea displayArea = DisplayArea.GetFromWindowId(myWndId, DisplayAreaFallback.Nearest);

            if (displayArea != null)
            {
                var centeredPosition = new PointInt32
                {
                    X = ((displayArea.WorkArea.Width - width) / 2) + displayArea.WorkArea.X,
                    Y = ((displayArea.WorkArea.Height - height) / 2) + displayArea.WorkArea.Y
                };

                appWindow.Move(centeredPosition);
            }
        }

        #endregion
    }
}