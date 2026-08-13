// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class LoadingWindowViewModel : ObservableObject, IDisposable
    {
        public static DiagnosticsPageViewModel? GlobalDiagnosticsVM { get; private set; }

        #region Private Fields & Constants
        private const string PlainDb = "EvolveOS_OptimizerDb.mdf";
        private const string SecureDb = "EvolveOS_OptimizerDb.dat";
        private const string PlainLdf = "EvolveOS_OptimizerDb_log.ldf";
        private const string SecureLdf = "EvolveOS_OptimizerDb_log.dat";

        private SystemDiagnostics _systemDiagnostics = null!;
        private UninstallingPackages _uninstallingPakages = null!;

        private readonly CancellationTokenSource _cts = new();
        private int _lastReportedStep = -1;

        private bool _isSystemBusy = false;
        private bool _isFreshBoot = false;

        private readonly bool _isAutoLoginSuccessful;
        private readonly bool _isShutdownMode;

        public LocalizationService Localizer => LocalizationService.Instance;
        #endregion

        #region Events for View Communication
        public event Action<string>? StatusUpdateRequested;
        public event Action<string, string>? CriticalErrorRequested;
        public event Action<string, string, bool>? UserDataLoaded;
        public event Action<bool, byte[]?>? TransitionReady;
        #endregion

        public LoadingWindowViewModel(bool autoLoginSuccessful, bool isShutdownMode)
        {
            _isAutoLoginSuccessful = autoLoginSuccessful;
            _isShutdownMode = isShutdownMode;
        }

        public async Task InitializeAsync()
        {
            if (_isShutdownMode) return;

            await Task.Run(() =>
            {
                _systemDiagnostics = new SystemDiagnostics();
                _uninstallingPakages = new UninstallingPackages();
                CheckSystemUptimeBackground();
            });

            await LoadUserDisplayDataAsync();

            string startText = Localizer["status_starting_services"];
            if (string.IsNullOrEmpty(startText) || startText == "status_starting_services")
                startText = "Starting core services...";

            StatusUpdateRequested?.Invoke(startText);

            await Task.Delay(1500, _cts.Token);

            ScheduledCleanService.Instance.Start();
            RegistryMonitorService.Instance.StartMonitoring();

            await StartProcessingAsync();
        }

        public void Cancel()
        {
            _cts.Cancel();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();

            if (_systemDiagnostics is IDisposable d1) d1.Dispose();
            if (_uninstallingPakages is IDisposable d2) d2.Dispose();
        }

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

            bool showBadge = _isAutoLoginSuccessful || isSessionValid;
            string targetName = string.Empty;

            if (showBadge)
            {
                targetName = !string.IsNullOrEmpty(UserSession.Username)
                    ? UserSession.Username
                    : (!string.IsNullOrEmpty(validUser) ? validUser : "Authorized User");
            }

            UserDataLoaded?.Invoke(avatarPath, targetName, showBadge);
        }
        #endregion

        #region Main Processing Engine
        private async Task StartProcessingAsync()
        {
            var token = _cts.Token;

            // Step 1: Background services initializing
            ReportStep(1);

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

            await App.HostInitializationSource.Task;
            await Task.Delay(1500, token);

            if (GlobalDiagnosticsVM == null)
            {
                GlobalDiagnosticsVM = DiagnosticsPageViewModel.Current;
            }

            if (_isSystemBusy)
            {
                string waitText = LocalizationService.Instance["status_waiting_system"] ?? "Waiting for system to initialize...";
                StatusUpdateRequested?.Invoke(waitText);
                await Task.Delay(3500, token);
            }

            // Step 2: Preparing system database
            ReportStep(2);
            bool isDbReady = await Task.Run(async () =>
            {
                bool isEngineInstalled = await EnsureDatabaseEngineInstalledAsync(token);
                if (!isEngineInstalled) return false;

                return await PerformDatabaseBootSequenceAsync(token);
            }, token);

            await Task.Delay(1000, token);

            if (!isDbReady || token.IsCancellationRequested) return;

            try
            {
                // Step 3: Gathering hardware diagnostics
                ReportStep(3);
                await Task.Delay(1500, token);

                Task diagnosticsScanTask = Task.Run(async () =>
                {
                    if (GlobalDiagnosticsVM != null)
                    {
                        await GlobalDiagnosticsVM.ExecuteFullScanAsync();
                    }
                });

                // Step 4: Checking Windows License
                ReportStep(4);
                var t1 = Task.Run(() => ExecuteWithLogging(WindowsLicense.LicenseStatus, nameof(WindowsLicense.LicenseStatus)));
                await Task.Delay(1500, token);

                // Step 5: Validating version updates
                ReportStep(5);
                var t2 = ExecuteAsyncWithLogging(() => SystemDiagnostics.ValidateVersionUpdatesAsync(token), nameof(SystemDiagnostics.ValidateVersionUpdatesAsync));
                await Task.Delay(1500, token);

                // Step 6: Scanning installed packages
                ReportStep(6);
                var t3 = Task.Run(() => ExecuteWithLogging(_uninstallingPakages.GetInstalledPackages, nameof(_uninstallingPakages.GetInstalledPackages)));
                await Task.Delay(1500, token);

                // Step 7: Verifying local account & Bluetooth
                ReportStep(7);
                var t4 = Task.Run(() =>
                {
                    ExecuteWithLogging(UninstallingPackages.CheckingForLocalAccount, nameof(UninstallingPackages.CheckingForLocalAccount));
                    ExecuteWithLogging(BluetoothManager.Initialize, nameof(BluetoothManager.Initialize));
                });
                await Task.Delay(1500, token);

                // Step 8: Initializing temperature sensors
                ReportStep(8);
                var t5 = Task.Run(() => ExecuteWithLogging(HardwareTemperatureService.Instance.Initialize, nameof(HardwareTemperatureService.Initialize)));
                await Task.Delay(1500, token);

                // Step 9: Analyzing active processes & services
                ReportStep(9);
                var t6 = Task.Run(() => ExecuteWithLogging(_systemDiagnostics.GetHardwareData, nameof(_systemDiagnostics.GetHardwareData)));

                // Wait for all the parallel background tasks to securely finish
                await Task.WhenAll(t1, t2, t3, t4, t5, t6, diagnosticsScanTask);

                // Fetch final stats
                HardwareData.RunningProcessesCount = await _systemDiagnostics.GetProcessCount();
                HardwareData.RunningServicesCount = await _systemDiagnostics.GetServicesCount();
                await _systemDiagnostics.GetTotalProcessorUsage();
                await _systemDiagnostics.GetPhysicalAvailableMemory();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "TelemetryGathering_Fail");
            }

            if (token.IsCancellationRequested) return;

            try
            {
                // Step 10: All systems ready!
                ReportStep(10);
                await Task.Delay(1500, token);

                await Task.WhenAny(weatherTask, Task.Delay(1500, token));

                if (token.IsCancellationRequested) return;

                string? sessionUser = string.Empty;
                bool isSessionValid = false;

                await Task.Run(() =>
                {
                    isSessionValid = AuthSessionManager.IsSessionValid(out sessionUser, out _);
                }, token);

                bool goToMain = _isAutoLoginSuccessful || isSessionValid;
                byte[]? profileBytes = null;

                if (goToMain)
                {
                    string targetUser = !string.IsNullOrEmpty(UserSession.Username)
                        ? UserSession.Username!
                        : sessionUser ?? "DefaultUser";

                    if (string.IsNullOrEmpty(targetUser))
                    {
                        targetUser = "DefaultUser";
                    }

                    UserSession.Username = targetUser;

                    try
                    {
                        var loginData = await Task.Run(async () =>
                        {
                            var userDataAccess = new UserDataAccess(SqlConnectionHelper.connectReturn());
                            return await userDataAccess.GetPasswordAndImageAsync(targetUser);
                        }, token);

                        if (loginData.ProfileImageBytes != null && loginData.ProfileImageBytes.Length > 0)
                        {
                            profileBytes = loginData.ProfileImageBytes;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AutoLogin] Failed to load profile image: {ex.Message}");
                    }
                }

                TransitionReady?.Invoke(goToMain, profileBytes);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "LoadingProcessing_Transition_Fail");
                TransitionReady?.Invoke(false, null); // Fallback to login
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

                StatusUpdateRequested?.Invoke(LocalizationService.Instance["status_installing_engine"] ?? "Installing required database engine...");

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
                    CriticalErrorRequested?.Invoke("Dependency Missing", "Database engine is not installed and the installer archive could not be found.");
                    return false;
                }

                await Task.Run(() => ArchiveManager.Unarchive(msiPath, archiveBytes), token);

                StatusUpdateRequested?.Invoke(LocalizationService.Instance["status_configuring_engine"] ?? "Configuring database engine...");

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
                    CriticalErrorRequested?.Invoke("Installation Failed", "Could not install the required SQL engine. Please run the app as Administrator.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(new Exception("Dependency Check Failed", ex), "Engine_Install_Sequence");
                CriticalErrorRequested?.Invoke("Startup Error", $"Engine installation failed: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Database Boot Sequence
        private async Task<bool> PerformDatabaseBootSequenceAsync(CancellationToken token)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
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
                    CriticalErrorRequested?.Invoke("Permission Denied", "App cannot write to the EXE folder. Please run as Administrator or move the app out of Program Files.");
                }

                if (!hasSecure && !hasPlain)
                {
                    StatusUpdateRequested?.Invoke(LocalizationService.Instance["status_extracting_resources"] ?? "Extracting resources...");
                    await Task.Delay(800, token);
                    await Task.Run(() => DatabaseSecurityService.RestoreDatabase(mdfPath, ldfPath), token);
                    hasPlain = true;
                }

                if (hasSecure)
                {
                    StatusUpdateRequested?.Invoke(LocalizationService.Instance["status_decrypting_db"] ?? "Decrypting database...");
                    await Task.Delay(800, token);

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
                        CriticalErrorRequested?.Invoke("Critical Error", "Database is busy or corrupted. Please wait a moment and try again.");
                        return false;
                    }
                }

                DatabaseSecurityService.QueueForDeleteOnReboot(mdfPath);
                DatabaseSecurityService.QueueForDeleteOnReboot(ldfPath);

                if (!CanOpenFile(mdfPath))
                {
                    StatusUpdateRequested?.Invoke(LocalizationService.Instance["status_starting_sql"] ?? "Starting SQL engine...");
                    await Task.Delay(800, token);

                    string sqlExePath = GetSqlLocalDbAbsolutePath();
                    await Task.Run(() =>
                    {
                        CommandExecutor.ExecuteCommand(sqlExePath, "stop MSSQLLocalDB -i");
                        CommandExecutor.ExecuteCommand(sqlExePath, "start MSSQLLocalDB");
                    }, token);

                    await WaitForFileReadyAsync(mdfPath, 10000, token);
                }

                if (!File.Exists(mdfPath) || !CanOpenFile(mdfPath))
                {
                    throw new Exception("Database files remain locked or missing.");
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(new Exception("Database Access Failed", ex), "Database_Boot_Sequence");
                CriticalErrorRequested?.Invoke("Startup Error", $"Database initialization failed: {ex.Message}");
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
        #endregion

        #region Reporting And Logging
        private void ReportStep(int stepNumber)
        {
            if (_cts.Token.IsCancellationRequested) return;

            if (stepNumber != _lastReportedStep && stepNumber <= 10)
            {
                _lastReportedStep = stepNumber;
                string resourceKey = $"step{stepNumber}_load";
                string message = LocalizationService.Instance[resourceKey];

                if (!string.IsNullOrEmpty(message))
                {
                    StatusUpdateRequested?.Invoke(message);
                }
            }
        }

        private void ExecuteWithLogging(Action action, string member)
        {
            try { action(); }
            catch (Exception ex) { ErrorLogging.LogWritingFile(ex, member); }
        }

        private async Task ExecuteAsyncWithLogging(Func<Task> action, string member)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, member);
            }
        }
        #endregion
    }
}