// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;
using WinPoint = global::Windows.Graphics.PointInt32;
using WinSize = global::Windows.Graphics.SizeInt32;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class LoadingWindow : Window
    {
        private readonly LoadingWindowViewModel _viewModel;
        private readonly DispatcherQueue _dispatcherQueue;

        #region Constructor & Initialization
        public LoadingWindow(bool autoLoginSuccessful = false, bool isShutdownMode = false)
        {
            this.InitializeComponent();

            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _viewModel = new LoadingWindowViewModel(autoLoginSuccessful, isShutdownMode);

            _viewModel.StatusUpdateRequested += ViewModel_StatusUpdateRequested;
            _viewModel.CriticalErrorRequested += ViewModel_CriticalErrorRequested;
            _viewModel.UserDataLoaded += ViewModel_UserDataLoaded;
            _viewModel.TransitionReady += ViewModel_TransitionReady;

            ApplyUserAccentColor();

            try
            {
                string fallbackPath = Path.Combine(AppContext.BaseDirectory, "Resources", "EvolveOSLogo.png");
                if (File.Exists(fallbackPath))
                {
                    DisplayProfileAvatar.Source = new BitmapImage(new Uri(fallbackPath));
                }

                if (StatusLoading != null)
                {
                    StatusLoading.Text = "";
                }
            }
            catch { }

            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
            ConfigureWindow();

            if (isShutdownMode)
            {
                DisplayProfileAvatar.Visibility = Visibility.Collapsed;
                AvatarGradientOverlay.Visibility = Visibility.Collapsed;
                if (AutoLoginBadge != null) AutoLoginBadge.Visibility = Visibility.Collapsed;
                ShutdownProgressRing.Visibility = Visibility.Visible;
            }

            this.Activated += LoadingWindow_Activated;
            this.Closed += LoadingWindow_Closed;
        }
        #endregion

        #region Window LifeCycle
        private void LoadingWindow_Closed(object sender, WindowEventArgs args)
        {
            _viewModel.Dispose();

            EfficiencyModeHelper.IsUIWakeLockActive = false;

            if (LocalMachineSettingsEngine.RunOnPriority == Priority.Low)
            {
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
            }

            if (RootGrid != null) RootGrid.DataContext = null;

            Debug.WriteLine("[LoadingWindow] Cleaned up background tasks and disposed scanners.");
        }

        private async void LoadingWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            this.Activated -= LoadingWindow_Activated;

            if (RootGrid.Resources.TryGetValue("DotAnimation", out object? da) && da is Storyboard s1) s1.Begin();
            if (RootGrid.Resources.TryGetValue("LoadingEllipses", out object? la) && la is Storyboard s2) s2.Begin();

            await _viewModel.InitializeAsync();
        }
        #endregion

        #region ViewModel Event Handlers
        private void ViewModel_StatusUpdateRequested(string text)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (StatusLoading == null) return;

                var duration = TimeSpan.FromMilliseconds(text.Length * 30);
                TypewriterAnimation.Create(text, StatusLoading, duration);
            });
        }

        private void ViewModel_CriticalErrorRequested(string title, string message)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                NativeToastHelper.SendNativeToast(title, message);
                App.ExitApp();
            });
        }

        private void ViewModel_UserDataLoaded(string avatarPath, string targetName, bool showBadge)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    DisplayProfileAvatar.Source = new BitmapImage(new Uri(avatarPath));
                }
                catch { }

                if (showBadge && AutoLoginBadge != null)
                {
                    AutoLoginBadge.Visibility = Visibility.Visible;
                    AutoLoginBadge.Opacity = 1;

                    RunUsername.Text = targetName;
                    RunUsername.UpdateLayout();
                    AutoLoginBadge.UpdateLayout();
                }
            });
        }

        private void ViewModel_TransitionReady(bool goToMain, byte[]? profileBytes)
        {
            _dispatcherQueue.TryEnqueue(async () =>
            {
                if (SystemDiagnostics.IsNeedUpdate && SettingsEngine.IsUpdateCheckRequired)
                {
                    if (Application.Current is App && App.MainWindow is MainWindow mainWinObj)
                    {
                        mainWinObj.DispatcherQueue.TryEnqueue(async () =>
                        {
                            await Task.Delay(500);
                            mainWinObj.AnimateUpdateBanner(true);
                        });
                    }
                }

                if (profileBytes != null)
                {
                    try
                    {
                        UserSession.ProfileImage = await ImageHelper.LoadFromBytesAsync(profileBytes);
                    }
                    catch { }
                }

                FinalizeTransition(goToMain);
            });
        }
        #endregion

        #region Public Methods
        public void UpdateShutdownText(string text)
        {
            ViewModel_StatusUpdateRequested(text);
        }

        public void SetHeaderTitle(string title)
        {
            if (LoadingTextRun != null)
            {
                LoadingTextRun.Text = title;
            }
        }
        #endregion

        #region Theming And Accent
        private void ApplyUserAccentColor()
        {
            try
            {
                string hexColor = SettingsEngine.AccentColor;
                Color userColor = ColorFromHex(hexColor);

                if (RootGrid.Resources.TryGetValue("Brush_Accent", out object? brushObj) && brushObj is SolidColorBrush accentBrush)
                {
                    accentBrush.Color = userColor;
                }

                RootGrid.Resources["SystemAccentColor"] = userColor;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "ApplyUserAccentColor_Fail");
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

        #region Window Configuration
        private void ConfigureWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            int style = Win32Helper.GetWindowLong(hWnd, Win32Helper.GWL_STYLE);
            Win32Helper.SetWindowLong(hWnd, Win32Helper.GWL_STYLE, style & ~Win32Helper.WS_CAPTION & ~Win32Helper.WS_THICKFRAME);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Resize(new WinSize(400, 160));

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centeredX = (displayArea.WorkArea.Width - 400) / 2;
                    var centeredY = (displayArea.WorkArea.Height - 160) / 2;
                    appWindow.Move(new WinPoint(centeredX, centeredY));
                }

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsAlwaysOnTop = true;
                    presenter.SetBorderAndTitleBar(false, false);

                    if (appWindow.TitleBar != null)
                    {
                        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    }
                }
            }
        }
        #endregion

        #region Transition Logic
        private void FinalizeTransition(bool goToMain)
        {
            try
            {
                Window nextWindow;

                if (goToMain)
                {
                    nextWindow = new MainWindow();
                }
                else
                {
                    var weatherService = new WeatherService();
                    nextWindow = new UserLoginWindow(weatherService);
                }

                nextWindow.Closed += (s, e) => { App.ExitApp(); };

                if (Application.Current is App)
                {
                    App.MainWindow = nextWindow;
                }

                bool isStartedHidden = Environment.GetCommandLineArgs().Any(a => a.Equals("-hidden", StringComparison.OrdinalIgnoreCase));

                if (isStartedHidden)
                {
                    IntPtr hWnd = WindowNative.GetWindowHandle(nextWindow);
                    var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWin = AppWindow.GetFromWindowId(windowId);

                    Win32Helper.ShowWindow(hWnd, 0);
                    appWin.Hide();

                    this.Close();
                    Debug.WriteLine("[LoadingWindow] Target Window initialized silently in the tray.");
                }
                else
                {
                    UIHelper.ApplyBackdrop(nextWindow, SettingsEngine.Backdrop);

                    if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                        presenter.IsAlwaysOnTop = false;

                    nextWindow.Activate();

                    _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    {
                        IntPtr hWnd = WindowNative.GetWindowHandle(nextWindow);
                        Win32Helper.SetForegroundWindow(hWnd);

                        this.Close();
                    });
                }

                _viewModel.Cancel();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "Transition_Fail");

                var fallbackWeather = new WeatherService();
                var fallback = new global::EvolveOS_Optimizer.Views.UserLoginWindow(fallbackWeather);

                if (Application.Current is App)
                {
                    App.MainWindow = fallback;
                    SettingsEngine.UpdateTheme(SettingsEngine.AppTheme);
                }

                bool isStartedHidden = Environment.GetCommandLineArgs().Any(arg => arg.Equals("-hidden", StringComparison.OrdinalIgnoreCase));

                if (!isStartedHidden)
                {
                    fallback.Activate();
                }
                else
                {
                    IntPtr hWnd = global::WinRT.Interop.WindowNative.GetWindowHandle(fallback);
                    var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWin = AppWindow.GetFromWindowId(windowId);
                    appWin.Hide();
                }
                this.Close();
            }
        }
        #endregion
    }
}