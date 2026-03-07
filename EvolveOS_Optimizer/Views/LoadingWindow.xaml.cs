// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Utilities.Tweaks;
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
        #region Private Fields & Constants
        private const string PlainDb = "EvolveOS_OptimizerDb.mdf";
        private const string SecureDb = "EvolveOS_OptimizerDb.dat";
        private const string PlainLdf = "EvolveOS_OptimizerDb_log.ldf";
        private const string SecureLdf = "EvolveOS_OptimizerDb_log.dat";

        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();
        private readonly UninstallingPackages _uninstallingPakages = new UninstallingPackages();
        private readonly bool _isAutoLoginSuccessful;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly CancellationTokenSource _cts = new();
        private int _lastReportedStep = -1;

        private bool _isSystemBusy = false;
        private bool _isFreshBoot = false;

        public LocalizationService Localizer => LocalizationService.Instance;
        public string GetText(string key) => Localizer[key];
        #endregion

        #region Constructor & Initialization
        public LoadingWindow(bool autoLoginSuccessful = false)
        {
            this.InitializeComponent();
            _isAutoLoginSuccessful = autoLoginSuccessful;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            ApplyUserAccentColor();

            if (RootGrid != null) RootGrid.Opacity = 0;

            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
            ConfigureWindow();

            CheckSystemUptime();
            LoadUserDisplayData();

            this.Activated += LoadingWindow_Activated;
            this.Closed += LoadingWindow_Closed;
        }
        #endregion

        #region Startup Checks
        private void CheckSystemUptime()
        {
            try
            {
                double totalUptimeMinutes = TimeSpan.FromMilliseconds(Environment.TickCount & Int32.MaxValue).TotalMinutes;
                _isFreshBoot = totalUptimeMinutes < 5;
                bool isNewSession = false;

                var shellProcess = Process.GetProcessesByName("explorer").FirstOrDefault();
                if (shellProcess != null)
                {
                    isNewSession = (DateTime.Now - shellProcess.StartTime).TotalMinutes < 2;
                }

                _isSystemBusy = _isFreshBoot || isNewSession;

                Debug.WriteLine($"[Startup] Boot={_isFreshBoot}, Session={isNewSession}, Busy={_isSystemBusy}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Startup] Uptime Check Failed: {ex.Message}");
            }
        }
        #endregion

        #region Window LifeCycle
        private void LoadingWindow_Closed(object sender, WindowEventArgs args)
        {
            _cts.Cancel();
            _cts.Dispose();

            if (RootGrid != null) RootGrid.DataContext = null;

            if (_systemDiagnostics is IDisposable d1) d1.Dispose();
            if (_uninstallingPakages is IDisposable d2) d2.Dispose();

            Debug.WriteLine("[LoadingWindow] Cleaned up background tasks and disposed scanners.");
        }
        #endregion

        #region User Display Data
        private void LoadUserDisplayData()
        {
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

            string? validUser = string.Empty;
            bool isSessionValid = AuthSessionManager.IsSessionValid(out validUser, out _);

            if (_isAutoLoginSuccessful || isSessionValid)
            {
                AutoLoginBadge.Visibility = Visibility.Visible;
                AutoLoginBadge.Opacity = 1;

                string targetName = !string.IsNullOrEmpty(UserSession.Username)
                    ? UserSession.Username
                    : (!string.IsNullOrEmpty(validUser) ? validUser : "Authorized User");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    RunUsername.Text = targetName;

                    RunUsername.UpdateLayout();
                    AutoLoginBadge.UpdateLayout();
                });
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
        #endregion

        #region Iinitial Activation
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
        #endregion

        #region Main Processing Engine
        private async Task StartProcessingAsync()
        {
            UpdateStatus(1);
            var token = _cts.Token;

            if (_isSystemBusy)
            {
                UpdateStatusDirect("Waiting for system to initialize...");
                await Task.Delay(5000, token);
            }

            bool isEngineInstalled = await EnsureDatabaseEngineInstalledAsync(token);
            if (!isEngineInstalled)
            {
                return;
            }

            bool dbBootSuccessful = await PerformDatabaseBootSequenceAsync(token);

            if (!dbBootSuccessful)
            {
                return;
            }

            await Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    Report(10);
                    await Task.Delay(400, token);

                    Report(20);

                    Task weatherTask = Task.Run(async () =>
                    {
                        try
                        {
                            var weatherService = new WeatherService();
                            using var weatherCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                            string savedLocation = SettingsEngine.LastLocation;
                            if (string.IsNullOrEmpty(savedLocation)) savedLocation = "London";

                            var data = await weatherService.GetWeatherAsync(savedLocation, weatherCts.Token);
                            if (data != null)
                            {
                                GlobalAppData.PreloadedWeather = data;
                            }
                        }
                        catch { }
                    });

                    Parallel.Invoke(
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

                    await Task.WhenAny(weatherTask, Task.Delay(1500, token));

                    if (token.IsCancellationRequested) return;

                    string? sessionUser = string.Empty;
                    bool isSessionValid = AuthSessionManager.IsSessionValid(out sessionUser, out _);

                    if (_isAutoLoginSuccessful || isSessionValid)
                    {
                        string? targetUser = !string.IsNullOrEmpty(UserSession.Username)
                            ? UserSession.Username!
                            : sessionUser;

                        if (string.IsNullOrEmpty(targetUser))
                        {
                            targetUser = "DefaultUser";
                        }

                        UserSession.Username = targetUser;

                        try
                        {
                            var userDataAccess = new EvolveOS_Optimizer.Core.Model.UserDataAccess(SqlConnectionHelper.connectReturn());

                            var loginData = await userDataAccess.GetPasswordAndImageAsync(targetUser);

                            if (loginData.ProfileImageBytes != null && loginData.ProfileImageBytes.Length > 0)
                            {
                                var tcs = new TaskCompletionSource<bool>();
                                _dispatcherQueue.TryEnqueue(async () =>
                                {
                                    try
                                    {
                                        UserSession.ProfileImage = await ImageHelper.LoadFromBytesAsync(loginData.ProfileImageBytes);
                                    }
                                    catch { }
                                    finally
                                    {
                                        tcs.SetResult(true);
                                    }
                                });
                                await tcs.Task;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[AutoLogin] Failed to load profile image: {ex.Message}");
                        }
                    }

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        FinalizeTransition();

                        if (SystemDiagnostics.IsNeedUpdate && SettingsEngine.IsUpdateCheckRequired)
                        {
                            if (Application.Current is App myApp && myApp.MainWindow is MainWindow mainWin)
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
        #endregion

        #region LocalDB Dependency Check
        private async Task<bool> EnsureDatabaseEngineInstalledAsync(CancellationToken token)
        {
            try
            {
                string checkCommand = "sqllocaldb info";
                string output = await CommandExecutor.GetCommandOutput(checkCommand, false);

                if (!string.IsNullOrEmpty(output) && output.Contains("MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                UpdateStatusDirect(ResourceString.GetString("status_installing_engine") ?? "Installing required database engine...");

                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
                string baseDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
                string archivePath = Path.Combine(baseDir, "Resources", "DatabaseEngine.gz");
                string msiPath = Path.Combine(baseDir, "SqlLocalDB.msi");

                byte[] archiveBytes = Array.Empty<byte>();

                if (File.Exists(archivePath))
                {
                    archiveBytes = await File.ReadAllBytesAsync(archivePath, token);
                }
                else if (File.Exists(Path.Combine(baseDir, "DatabaseEngine.gz")))
                {
                    archiveBytes = await File.ReadAllBytesAsync(Path.Combine(baseDir, "DatabaseEngine.gz"), token);
                }
                else
                {
                    archiveBytes = ArchiveManager.GetResourceBytes("DatabaseEngine.gz");
                }

                if (archiveBytes.Length == 0)
                {
                    _dispatcherQueue.TryEnqueue(() => {
                        NativeToastHelper.SendNativeToast("Dependency Missing", "Database engine is not installed and the installer archive could not be found.");
                        Application.Current.Exit();
                    });
                    return false;
                }

                await Task.Run(() => ArchiveManager.Unarchive(msiPath, archiveBytes), token);

                UpdateStatusDirect(ResourceString.GetString("status_configuring_engine") ?? "Configuring database engine...");
                string installCommand = $"msiexec /i \"{msiPath}\" /qn /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES";

                await CommandExecutor.StartInCmd(installCommand);

                if (File.Exists(msiPath))
                {
                    File.Delete(msiPath);
                }

                await CommandExecutor.StartInCmd("sqllocaldb create MSSQLLocalDB");

                string verifyOutput = await CommandExecutor.GetCommandOutput("sqllocaldb info", false);
                if (string.IsNullOrEmpty(verifyOutput) || verifyOutput.Contains("not recognized", StringComparison.OrdinalIgnoreCase))
                {
                    _dispatcherQueue.TryEnqueue(() => {
                        NativeToastHelper.SendNativeToast("Installation Failed", "Could not install the required SQL engine. Please run the app as Administrator.");
                        Application.Current.Exit();
                    });
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(new Exception("Dependency Check Failed", ex), "Engine_Install_Sequence");
                _dispatcherQueue.TryEnqueue(() => {
                    NativeToastHelper.SendNativeToast("Startup Error", $"Engine installation failed: {ex.Message}");
                    Application.Current.Exit();
                });
                return false;
            }
        }
        #endregion

        #region Database Boot Sequence
        private async Task<bool> PerformDatabaseBootSequenceAsync(CancellationToken token)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                             ?? AppContext.BaseDirectory;

            string baseDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            string mdfPath = Path.Combine(baseDir, PlainDb);
            string ldfPath = Path.Combine(baseDir, PlainLdf);
            string securePath = Path.Combine(baseDir, SecureDb);
            string secureLdfPath = Path.Combine(baseDir, SecureLdf);

            bool hasSecure = File.Exists(securePath);
            bool hasPlain = File.Exists(mdfPath);

            try
            {
                try
                {
                    string testFile = Path.Combine(baseDir, "write_test.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch (UnauthorizedAccessException)
                {
                    _dispatcherQueue.TryEnqueue(() => {
                        NativeToastHelper.SendNativeToast("Permission Denied", "App cannot write to the EXE folder. Please run as Administrator or move the app out of Program Files.");
                    });
                }

                if (!hasSecure && !hasPlain)
                {
                    UpdateStatusDirect(ResourceString.GetString("status_extracting_resources") ?? "Extracting resources...");
                    await Task.Run(() => DatabaseSecurityService.RestoreDatabase(mdfPath, ldfPath), token);
                    hasPlain = true;
                }

                if (!File.Exists(securePath))
                {
                    UpdateStatusDirect(ResourceString.GetString("status_initializing_db") ?? "Initializing database...");
                }

                UpdateStatusDirect(ResourceString.GetString("status_checking_db") ?? "Checking database availability...");

                await WaitForFileReadyAsync(securePath, _isSystemBusy ? 15000 : 5000, token);

                UpdateStatusDirect(ResourceString.GetString("status_decrypting_db") ?? "Decrypting database...");

                bool decryptionSuccessful = false;
                int retryCount = 0;
                int maxRetries = 3;

                while (!decryptionSuccessful && retryCount < maxRetries)
                {
                    decryptionSuccessful = await Task.Run(() =>
                    {
                        try
                        {
                            UnlockHandleHelper.UnlockDirectory(baseDir, "sqlservr");
                            DatabaseSecurityService.DecryptDatabase(securePath, mdfPath);

                            if (File.Exists(secureLdfPath))
                            {
                                try { DatabaseSecurityService.DecryptDatabase(secureLdfPath, ldfPath); }
                                catch { }
                            }
                            return true;
                        }
                        catch (Exception decryptEx)
                        {
                            Debug.WriteLine($"[Database] Decryption attempt {retryCount + 1} failed: {decryptEx.Message}");
                            return false;
                        }
                    }, token);

                    if (!decryptionSuccessful)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await Task.Delay(1000, token);
                        }
                    }
                }

                if (!decryptionSuccessful)
                {
                    _dispatcherQueue.TryEnqueue(() => {
                        NativeToastHelper.SendNativeToast("Critical Error", "Database is busy or corrupted. Please wait a moment and try again.");
                        Application.Current.Exit();
                    });
                    return false;
                }

                UpdateStatusDirect(ResourceString.GetString("status_starting_sql") ?? "Starting SQL engine...");
                await Task.Run(() =>
                {
                    CommandExecutor.ExecuteCommand("sqllocaldb", "stop MSSQLLocalDB -i");
                    CommandExecutor.ExecuteCommand("sqllocaldb", "start MSSQLLocalDB");
                }, token);

                UpdateStatusDirect(ResourceString.GetString("status_finalizing_access") ?? "Finalizing access...");
                await WaitForFileReadyAsync(mdfPath, 10000, token);

                if (!File.Exists(mdfPath) || !CanOpenFile(mdfPath))
                {
                    throw new Exception("Database files remain locked or missing.");
                }

                UpdateStatusDirect(ResourceString.GetString("status_db_ready") ?? "Database Ready");
                await Task.Delay(500, token);

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(new Exception("Database Access Failed", ex), "Database_Boot_Sequence");
                _dispatcherQueue.TryEnqueue(() => {
                    NativeToastHelper.SendNativeToast("Startup Error", $"Database initialization failed: {ex.Message}");
                    Application.Current.Exit();
                });
                return false;
            }
        }

        private async Task<bool> WaitForFileReadyAsync(string filename, int timeoutMilliseconds, CancellationToken token)
        {
            int elapsed = 0;
            int delay = 500;

            while (elapsed < timeoutMilliseconds)
            {
                if (token.IsCancellationRequested) return false;

                if (CanOpenFile(filename))
                    return true;

                await Task.Delay(delay, token);
                elapsed += delay;
            }
            return false;
        }

        private bool CanOpenFile(string filename)
        {
            try
            {
                if (!File.Exists(filename)) return false;

                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return inputStream.Length > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void UpdateStatusDirect(string text)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (_cts.Token.IsCancellationRequested || StatusLoading == null) return;
                TypewriterAnimation.Create(text, StatusLoading, TimeSpan.FromMilliseconds(20));
            });
        }
        #endregion

        #region Transition Logic
        private void FinalizeTransition()
        {
            try
            {
                Window nextWindow;

                if (_isAutoLoginSuccessful || AuthSessionManager.IsSessionValid(out _, out _))
                {
                    nextWindow = new MainWindow();
                }
                else
                {
                    var weatherService = new WeatherService();
                    nextWindow = new UserLoginWindow(weatherService);
                }

                nextWindow.Closed += (s, e) => { Application.Current.Exit(); };

                if (Application.Current is App myApp)
                {
                    myApp.MainWindow = nextWindow;
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

                _cts.Cancel();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "Transition_Fail");

                var fallbackWeather = new WeatherService();
                var fallback = new global::EvolveOS_Optimizer.Views.UserLoginWindow(fallbackWeather);

                if (Application.Current is App a)
                {
                    a.MainWindow = fallback;
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

        #region Reporting And Logging
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
        #endregion
    }
}