using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Animation;
using System.Threading;
using WinPoint = global::Windows.Graphics.PointInt32;
using WinSize = global::Windows.Graphics.SizeInt32;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class LoadingWindow : Window
    {
        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();
        private readonly UninstallingPackages _uninstallingPakages = new UninstallingPackages();
        private readonly bool _isAutoLoginSuccessful;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly CancellationTokenSource _cts = new();
        private int _lastReportedStep = -1;

        public LocalizationService Localizer => LocalizationService.Instance;
        public string GetText(string key) => Localizer[key];

        public LoadingWindow(bool autoLoginSuccessful = false)
        {
            this.InitializeComponent();
            _isAutoLoginSuccessful = autoLoginSuccessful;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            ApplyUserAccentColor();

            if (RootGrid != null) RootGrid.Opacity = 0;

            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
            ConfigureWindow();
            LoadUserDisplayData();

            this.Activated += LoadingWindow_Activated;
            this.Closed += LoadingWindow_Closed;
        }

        private void LoadingWindow_Closed(object sender, WindowEventArgs args)
        {
            _cts.Cancel();
            _cts.Dispose();

            if (RootGrid != null) RootGrid.DataContext = null;

            if (_systemDiagnostics is IDisposable d1) d1.Dispose();
            if (_uninstallingPakages is IDisposable d2) d2.Dispose();

            Debug.WriteLine("[LoadingWindow] Cleaned up background tasks and disposed scanners.");
        }

        private void LoadUserDisplayData()
        {
            RunUsername.Text = Environment.UserName;

            Task.Run(() =>
            {
                if (_cts.Token.IsCancellationRequested) return;

                string? avatarPath = _systemDiagnostics.GetProfileAvatarPath();
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (_cts.Token.IsCancellationRequested) return;
                        try { DisplayProfileAvatar.Source = new BitmapImage(new Uri(avatarPath)); }
                        catch { }
                    });
                }
            }, _cts.Token);

            if (_isAutoLoginSuccessful)
            {
                AutoLoginBadge.Visibility = Visibility.Visible;
                AutoLoginBadge.Opacity = 1;
            }
        }

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

            return ColorHelper.FromArgb(a, r, g, b);
        }

        private void ConfigureWindow()
        {
            IntPtr hWnd = global::WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            int style = Win32Helper.GetWindowLong(hWnd, Win32Helper.GWL_STYLE);
            Win32Helper.SetWindowLong(hWnd, Win32Helper.GWL_STYLE, style & ~Win32Helper.WS_CAPTION & ~Win32Helper.WS_THICKFRAME);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Resize(new WinSize(350, 150));

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centeredX = (displayArea.WorkArea.Width - 350) / 2;
                    var centeredY = (displayArea.WorkArea.Height - 150) / 2;
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

        private async void LoadingWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            this.Activated -= LoadingWindow_Activated;

            if (RootGrid.Resources.TryGetValue("DotAnimation", out object? da) && da is Storyboard s1) s1.Begin();
            if (RootGrid.Resources.TryGetValue("LoadingEllipses", out object? la) && la is Storyboard s2) s2.Begin();

            Storyboard fadeIn = new Storyboard();
            DoubleAnimation anim = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(400) };
            Storyboard.SetTarget(anim, RootGrid);
            Storyboard.SetTargetProperty(anim, "Opacity");
            fadeIn.Children.Add(anim);
            fadeIn.Begin();

            await StartProcessingAsync();
        }

        private async Task StartProcessingAsync()
        {
            UpdateStatus(1);
            var token = _cts.Token;

            await Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    Report(10);
                    await Task.Delay(400, token);

                    Report(20);
                    Parallel.Invoke(
                        () => ExecuteWithLogging(TrustedInstaller.StartTrustedInstallerService, nameof(TrustedInstaller.StartTrustedInstallerService)),
                        () => ExecuteWithLogging(WindowsLicense.LicenseStatus, nameof(WindowsLicense.LicenseStatus)),
                        () => ExecuteWithLogging(_systemDiagnostics.GetHardwareData, nameof(_systemDiagnostics.GetHardwareData)),
                        () => ExecuteAsyncWithLogging(() => _systemDiagnostics.ValidateVersionUpdatesAsync(token), nameof(_systemDiagnostics.ValidateVersionUpdatesAsync)),
                        () => ExecuteWithLogging(_uninstallingPakages.GetInstalledPackages, nameof(_uninstallingPakages.GetInstalledPackages)),
                        () => ExecuteAsyncWithLogging(RunGuard.CheckingDefenderExclusions, nameof(RunGuard.CheckingDefenderExclusions)),
                        () =>
                        {
                            ExecuteWithLogging(UninstallingPackages.CheckingForLocalAccount, nameof(UninstallingPackages.CheckingForLocalAccount));
                            ExecuteWithLogging(SystemTweaks.ViewNetshState, nameof(SystemTweaks.ViewNetshState));
                            ExecuteWithLogging(SystemTweaks.ViewBluetoothStatus, nameof(SystemTweaks.ViewBluetoothStatus));
                            ExecuteWithLogging(SystemTweaks.ViewConfigTick, nameof(SystemTweaks.ViewConfigTick));
                        }
                    );

                    for (int p = 30; p <= 70; p += 10)
                    {
                        if (token.IsCancellationRequested) return;
                        Report(p);
                        await Task.Delay(350, token);
                    }

                    if (token.IsCancellationRequested) return;

                    Report(80);
                    HardwareData.RunningProcessesCount = await _systemDiagnostics.GetProcessCount();
                    HardwareData.RunningServicesCount = await _systemDiagnostics.GetServicesCount();

                    Report(90);
                    await _systemDiagnostics.GetTotalProcessorUsage();
                    await _systemDiagnostics.GetPhysicalAvailableMemory();

                    Report(100);
                    await Task.Delay(1000, token);

                    if (token.IsCancellationRequested) return;

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        FinalizeTransition();

                        if (SystemDiagnostics.IsNeedUpdate && SettingsEngine.IsUpdateCheckRequired)
                        {
                            if (App.Current.MainWindow is MainWindow mainWin)
                            {
                                mainWin.DispatcherQueue.TryEnqueue(async () =>
                                {
                                    await Task.Delay(500);
                                    mainWin.AnimateUpdateBanner(true);
                                });
                            }
                        }
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(ex, "LoadingProcessing_Fail");
                }
            }, token);
        }

        private void FinalizeTransition()
        {
            try
            {
                var mainDash = new global::EvolveOS_Optimizer.MainWindow();

                if (Application.Current is App myApp)
                {
                    myApp.MainWindow = mainDash;
                    SettingsEngine.UpdateTheme(SettingsEngine.AppTheme);
                    myApp.UpdateGlobalAccentColor(SettingsEngine.AccentColor);
                }

                UIHelper.ApplyBackdrop(mainDash, SettingsEngine.Backdrop);
                mainDash.Activate();

                _cts.Cancel();
                this.Close();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "Transition_Fail");
                var fallback = new global::EvolveOS_Optimizer.MainWindow();
                if (Application.Current is App a)
                {
                    a.MainWindow = fallback;
                    SettingsEngine.UpdateTheme(SettingsEngine.AppTheme);
                }
                fallback.Activate();
                this.Close();
            }
        }

        private void Report(int percentage)
        {
            if (_cts.Token.IsCancellationRequested) return;

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (_cts.Token.IsCancellationRequested) return;

                int stepNumber = (percentage / 10) + 1;
                if (stepNumber != _lastReportedStep && stepNumber <= 10)
                {
                    _lastReportedStep = stepNumber;
                    UpdateStatus(stepNumber);
                }
            });
        }

        private void UpdateStatus(int step)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (_cts.Token.IsCancellationRequested || StatusLoading == null) return;

                string resourceKey = $"step{step}_load";
                string message = LocalizationService.Instance[resourceKey];

                if (!string.IsNullOrEmpty(message))
                {
                    TypewriterAnimation.Create(message, StatusLoading, TimeSpan.FromMilliseconds(50));
                }
            });
        }

        private void ExecuteWithLogging(Action action, string member)
        {
            try { action(); }
            catch (Exception ex) { ErrorLogging.LogWritingFile(ex, member); }
        }

        private void ExecuteAsyncWithLogging(Func<Task> action, string member)
        {
            try
            {
                Task.Run(async () => await action()).GetAwaiter().GetResult();
            }
            catch (Exception ex) { ErrorLogging.LogWritingFile(ex, member); }
        }
    }
}