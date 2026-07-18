// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
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
        public static DiagnosticsPageViewModel? GlobalDiagnosticsVM { get; private set; }

        #region Private Fields & Constants
        private const string PlainDb = "EvolveOS_OptimizerDb.mdf";
        private const string SecureDb = "EvolveOS_OptimizerDb.dat";
        private const string PlainLdf = "EvolveOS_OptimizerDb_log.ldf";
        private const string SecureLdf = "EvolveOS_OptimizerDb_log.dat";

        private SystemDiagnostics _systemDiagnostics = null!;
        private UninstallingPackages _uninstallingPakages = null!;

        private readonly bool _isAutoLoginSuccessful;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly CancellationTokenSource _cts = new();
        private int _lastReportedStep = -1;

        private bool _isSystemBusy = false;
        private bool _isFreshBoot = false;

        private readonly bool _isShutdownMode;

        public LocalizationService Localizer => LocalizationService.Instance;
        public string GetText(string key) => Localizer[key];
        #endregion

        #region Constructor & Initialization
        public LoadingWindow(bool autoLoginSuccessful = false, bool isShutdownMode = false)
        {
            this.InitializeComponent();

            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            _isAutoLoginSuccessful = autoLoginSuccessful;
            _isShutdownMode = isShutdownMode;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            ApplyUserAccentColor();

            if (RootGrid != null) RootGrid.Opacity = 0;

            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
            ConfigureWindow();

            if (_isShutdownMode)
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
            _cts.Cancel();
            _cts.Dispose();

            EfficiencyModeHelper.IsUIWakeLockActive = false;

            if (LocalMachineSettingsEngine.RunOnPriority == Priority.Low)
            {
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
            }

            if (RootGrid != null) RootGrid.DataContext = null;
            if (_systemDiagnostics is IDisposable d1) d1.Dispose();
            if (_uninstallingPakages is IDisposable d2) d2.Dispose();

            Debug.WriteLine("[LoadingWindow] Cleaned up background tasks and disposed scanners.");
        }
        #endregion

        #region Iinitial Activation (FIXED STARTUP SEQUENCE)
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

            if (!_isShutdownMode)
            {
                await Task.Run(() =>
                {
                    _systemDiagnostics = new SystemDiagnostics();
                    _uninstallingPakages = new UninstallingPackages();
                    CheckSystemUptimeBackground();
                });

                await LoadUserDisplayDataAsync();

                ScheduledCleanService.Instance.Start();
                RegistryMonitorService.Instance.StartMonitoring();

                await StartProcessingAsync();
            }
        }

        public void UpdateShutdownText(string text)
        {
            UpdateStatusDirect(text);
        }
        #endregion

        #region Background Startup Checks
        private void CheckSystemUptimeBackground()
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

        #region User Display Data
        private async Task LoadUserDisplayDataAsync()
        {
            string avatarPath = string.Empty;
            bool isSessionValid = false;
            string? validUser = string.Empty;

            await Task.Run(() =>
            {
                if (_cts.Token.IsCancellationRequested) return;

                avatarPath = _systemDiagnostics.GetProfileAvatarPath() ?? string.Empty;
                if (string.IsNullOrEmpty(avatarPath) || !File.Exists(avatarPath))
                {
                    avatarPath = Path.Combine(AppContext.BaseDirectory, "Resources", "EvolveOSLogo.png");
                }

                isSessionValid = AuthSessionManager.IsSessionValid(out validUser, out _);
            }, _cts.Token);

            if (_cts.Token.IsCancellationRequested) return;

            try { DisplayProfileAvatar.Source = new BitmapImage(new Uri(avatarPath)); }
            catch { }

            if (_isAutoLoginSuccessful || isSessionValid)
            {
                AutoLoginBadge.Visibility = Visibility.Visible;
                AutoLoginBadge.Opacity = 1;

                string targetName = !string.IsNullOrEmpty(UserSession.Username)
                    ? UserSession.Username
                    : (!string.IsNullOrEmpty(validUser) ? validUser : "Authorized User");

                RunUsername.Text = targetName;
                RunUsername.UpdateLayout();
                AutoLoginBadge.UpdateLayout();
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

        #region Main Processing Engine
        private async Task StartProcessingAsync()
        {
            UpdateStatus(1);
            var token = _cts.Token;

            Task weatherTask = Task.Run(async () =>
            {
                try
                {
                    var weatherService = new WeatherService();
                    using var weatherCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    string savedLocation = SettingsEngine.LastLocation;
                    if (string.IsNullOrEmpty(savedLocation)) savedLocation = "London";

                    var data = await weatherService.GetWeatherAsync(savedLocation, weatherCts.Token);
                    if (data != null)
                    {
                        GlobalAppData.PreloadedWeather = data;
                        Debug.WriteLine("[Weather] Preloaded successfully in background.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Weather] Failed to preload: {ex.Message}");
                }
            });

            if (_isSystemBusy)
            {
                UpdateStatusDirect(ResourceString.GetString("status_waiting_system") ?? "Waiting for system to initialize...");
                await Task.Delay(5000, token);
            }

            Task<bool> dbTask = Task.Run(async () =>
            {
                bool isEngineInstalled = await EnsureDatabaseEngineInstalledAsync(token);
                if (!isEngineInstalled) return false;

                return await PerformDatabaseBootSequenceAsync(token);
            }, token);

            var diagnosticsInstance = DiagnosticsPageViewModel.Current;

            Task telemetryTask = Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    Report(10);
                    await Task.Delay(400, token);

                    UpdateStatusDirect(ResourceString.GetString("status_hardware_gather") ?? "Interrogating hardware and telemetry...");

                    Task diagnosticsScanTask = diagnosticsInstance.ExecuteFullScanAsync();

                    Report(20);

                    Parallel.Invoke(
                        () => ExecuteWithLogging(WindowsLicense.LicenseStatus, nameof(WindowsLicense.LicenseStatus)),
                        () => ExecuteWithLogging(_systemDiagnostics.GetHardwareData, nameof(_systemDiagnostics.GetHardwareData)),
                        () => ExecuteAsyncWithLogging(() => SystemDiagnostics.ValidateVersionUpdatesAsync(token), nameof(SystemDiagnostics.ValidateVersionUpdatesAsync)),
                        () => ExecuteWithLogging(_uninstallingPakages.GetInstalledPackages, nameof(_uninstallingPakages.GetInstalledPackages)),
                        () => ExecuteAsyncWithLogging(RunGuard.CheckingDefenderExclusions, nameof(RunGuard.CheckingDefenderExclusions)),
                        () =>
                        {
                            ExecuteWithLogging(UninstallingPackages.CheckingForLocalAccount, nameof(UninstallingPackages.CheckingForLocalAccount));
                            ExecuteWithLogging(BluetoothManager.Initialize, nameof(BluetoothManager.Initialize));
                        },

                        () => ExecuteWithLogging(HardwareTemperatureService.Instance.Initialize, nameof(HardwareTemperatureService.Initialize))
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
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(ex, "TelemetryGathering_Fail");
                }
            }, token);

            await Task.WhenAll(dbTask, telemetryTask);

            if (!await dbTask)
            {
                return;
            }

            if (token.IsCancellationRequested) return;

            try
            {
                Report(100);
                await Task.Delay(500, token);
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
                        var userDataAccess = new UserDataAccess(SqlConnectionHelper.connectReturn());

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
                    if (GlobalDiagnosticsVM == null)
                    {
                        GlobalDiagnosticsVM = DiagnosticsPageViewModel.Current;
                    }

                    FinalizeTransition();

                    if (SystemDiagnostics.IsNeedUpdate && SettingsEngine.IsUpdateCheckRequired)
                    {
                        if (Application.Current is App && App.MainWindow is MainWindow mainWin)
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
                ErrorLogging.LogWritingFile(ex, "LoadingProcessing_Transition_Fail");
            }
        }
        #endregion

        #region LocalDB Dependency Check
        private string GetSqlLocalDbAbsolutePath()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string sqlDir = Path.Combine(programFiles, "Microsoft SQL Server");

                if (Directory.Exists(sqlDir))
                {
                    var files = Directory.GetFiles(sqlDir, "sqllocaldb.exe", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
            }
            catch { }

            return "sqllocaldb";
        }

        private async Task<bool> EnsureDatabaseEngineInstalledAsync(CancellationToken token)
        {
            try
            {
                string sqlExePath = GetSqlLocalDbAbsolutePath();
                string checkCommand = $"\"{sqlExePath}\" info";
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
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        NativeToastHelper.SendNativeToast("Dependency Missing", "Database engine is not installed and the installer archive could not be found.");
                        App.ExitApp();
                    });
                    return false;
                }

                await Task.Run(() => ArchiveManager.Unarchive(msiPath, archiveBytes), token);

                UpdateStatusDirect(ResourceString.GetString("status_configuring_engine") ?? "Configuring database engine...");

                var psi = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{msiPath}\" /qn /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync(token);
                    }
                }

                if (File.Exists(msiPath))
                {
                    File.Delete(msiPath);
                }

                await Task.Delay(3000, token);

                sqlExePath = GetSqlLocalDbAbsolutePath();

                await CommandExecutor.StartInCmd($"\"{sqlExePath}\" create MSSQLLocalDB");
                await Task.Delay(1500, token);

                string verifyOutput = await CommandExecutor.GetCommandOutput($"\"{sqlExePath}\" info", false);
                if (string.IsNullOrEmpty(verifyOutput) || verifyOutput.Contains("not recognized", StringComparison.OrdinalIgnoreCase))
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        NativeToastHelper.SendNativeToast("Installation Failed", "Could not install the required SQL engine. Please run the app as Administrator.");
                        App.ExitApp();
                    });
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(new Exception("Dependency Check Failed", ex), "Engine_Install_Sequence");
                _dispatcherQueue.TryEnqueue(() =>
                {
                    NativeToastHelper.SendNativeToast("Startup Error", $"Engine installation failed: {ex.Message}");
                    App.ExitApp();
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
                    _dispatcherQueue.TryEnqueue(() =>
                    {
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
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        NativeToastHelper.SendNativeToast("Critical Error", "Database is busy or corrupted. Please wait a moment and try again.");
                        App.ExitApp();
                    });
                    return false;
                }

                UpdateStatusDirect(ResourceString.GetString("status_starting_sql") ?? "Starting SQL engine...");

                string sqlExePath = GetSqlLocalDbAbsolutePath();
                await Task.Run(() =>
                {
                    CommandExecutor.ExecuteCommand(sqlExePath, "stop MSSQLLocalDB -i");
                    CommandExecutor.ExecuteCommand(sqlExePath, "start MSSQLLocalDB");
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
                _dispatcherQueue.TryEnqueue(() =>
                {
                    NativeToastHelper.SendNativeToast("Startup Error", $"Database initialization failed: {ex.Message}");
                    App.ExitApp();
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

        public void SetHeaderTitle(string title)
        {
            if (LoadingTextRun != null)
            {
                LoadingTextRun.Text = title;
            }
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

                _cts.Cancel();
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