// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Security.Principal;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Views;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;

namespace EvolveOS_Optimizer
{
    public partial class App : Application
    {
        public const string Name = "EvolveOS Optimizer";

        private const string PlainDb = "EvolveOS_OptimizerDb.mdf";
        private const string SecureDb = "EvolveOS_OptimizerDb.dat";
        private const string PlainLdf = "EvolveOS_OptimizerDb_log.ldf";
        private const string SecureLdf = "EvolveOS_OptimizerDb_log.dat";

        private static bool _isCleanupRunning = false;

        public static Window? MainWindow { get; set; }
        public static bool IsStartedHidden { get; private set; }

        public static bool IsPrimaryInstance { get; private set; }
        private static bool _isExiting = false;

        public static MemoryGuardian? MemoryGuardian { get; private set; }

        public static Microsoft.UI.Dispatching.DispatcherQueue? UIThreadDispatcher { get; private set; }

        private static Mutex? _mutex;
        private IHost? _host;

        private ILogService? _logService;

        private static IHotkeyService? _hotkeyService;
        public static event EventHandler? HotkeySettingsChanged;
        private static PasswordGeneratorWindow? _passwordGeneratorWindow;

        public static new App Current => (App)Application.Current;

        public static TaskCompletionSource<bool> HostInitializationSource { get; } = new TaskCompletionSource<bool>();

        public static IServiceProvider Services => (Current as App)?._host?.Services
            ?? throw new InvalidOperationException("Host not initialized");

        public static IntPtr WindowHandle { get; private set; }

        public App()
        {
            InitializeComponent();

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            UnhandledException += OnUnhandledException;

            this.UnhandledException += App_UnhandledException;

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            AppDomain.CurrentDomain.ProcessExit += (s, ev) => HandleCleanup();

            LocalizationService.Instance.LoadLanguage("en-us");
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            //string aumid = "EvolveOS.Optimizer.App";
            //SetCurrentProcessAppId(aumid);

            UIThreadDispatcher = DispatcherQueue.GetForCurrentThread();

            _mutex = new Mutex(true, "EvolveOS_Optimizer_SingleInstance", out bool isNewInstance);
            IsPrimaryInstance = isNewInstance;

            if (!isNewInstance)
            {
                string windowTitle = "EvolveOS_Optimizer";
                IntPtr hwnd = Win32Helper.FindWindow("WinUIDesktopWin32WindowClass", windowTitle);
                if (hwnd == IntPtr.Zero) hwnd = Win32Helper.FindWindow(null!, windowTitle);

                if (hwnd != IntPtr.Zero)
                {
                    Win32Helper.ShowWindow(hwnd, 5);
                    Win32Helper.ShowWindow(hwnd, 9);
                    Win32Helper.SetForegroundWindow(hwnd);
                    Environment.Exit(0);
                }
                else
                {
                    MainWindow = new MessageWindow(MessageWindowState.AlreadyRunning);
                    MainWindow.Activate();
                }
                return;
            }

            bool startHidden = Environment.CommandLine.Contains("-hidden", StringComparison.OrdinalIgnoreCase);
            IsStartedHidden = startHidden;

            if (!IsRunningAsAdmin())
            {
                ElevateToAdmin();
                return;
            }

            СheckingGlobalParameters.Initialize();
            App.Current.UpdateGlobalAccentColor(SettingsEngine.AccentColor);
            SettingsEngine.UpdateTheme(SettingsEngine.AppTheme);

            MainWindow = new LoadingWindow();

            if (IsStartedHidden)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                Win32Helper.ShowWindow(hwnd, 0);
            }

            MainWindow.Activate();

            Task.Run(async () =>
            {
                try { var dummy = Windows.ApplicationModel.Package.Current.Id; }
                catch (InvalidOperationException) { AppNotificationManager.Default.Register(); }

                await IdentityHelper.EnsureAppIdentityAsync();

                EnsureShortcutWithAumid();

                _host = CompositionRoot.CreateEvolveOSHost().Build();

                HostInitializationSource.SetResult(true);

                UIThreadDispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                {
                    _logService = Services.GetService<ILogService>();
                    InitializeLocalization();

                    var interactiveUserService = Services.GetService<IInteractiveUserService>();
                    if (_logService is LogService concreteLogService)
                    {
                        if (interactiveUserService != null) concreteLogService.SetInteractiveUserService(interactiveUserService);
                        var systemInfoProvider = Services.GetService<ISystemInfoProvider>();
                        if (systemInfoProvider != null) concreteLogService.SetSystemInfoProvider(systemInfoProvider);
                    }
                    _logService?.StartLog();
                    _logService?.LogInformation("EvolveOS_Optimizer application starting...");

                    SetPriority(LocalMachineSettingsEngine.RunOnPriority);
                    _hotkeyService = new EvolveOS_Optimizer.Utilities.Services.HotkeyService();

                    UIThreadDispatcher.TryEnqueue(DispatcherQueuePriority.Low, async () =>
                    {
                        await NotifyHotkeySettingsChanged();
                        _ = StartBackgroundServices();

                        if (LocalMachineSettingsEngine.EnableStartupMonitor)
                        {
                            StartupChangeMonitor.StartWatching();
                        }

                        MemoryGuardian = new MemoryGuardian((before, after) =>
                        {
                            long diff = (long)before - (long)after;
                            if (diff > 5 * 1024 * 1024)
                            {
                                Debug.WriteLine($"[Global] GC Trimmed: {diff / 1024 / 1024}MB");
                            }
                        });
                    });
                });
            });
        }

        private void InitializeLocalization()
        {
            try
            {
                var localizationService = Services.GetRequiredService<ILocalizationService>();

                var preferencesService = Services.GetRequiredService<IUserPreferencesService>();
                var savedLanguage = preferencesService.GetPreference("Language", "en");
                localizationService.SetLanguage(savedLanguage);
            }
            catch (Exception ex)
            {
                _logService?.LogDebug($"Failed to initialize localization: {ex.Message}");
            }
        }

        private async void App_UnhandledException(object? sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            string errorMessage = $"CRASH: {e.Exception.Message}\nStack: {e.Exception.StackTrace}";
            await ErrorLogging.LogInfo(errorMessage);
        }

        private async void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            string errorMessage = $"BACKGROUND CRASH: {e.Exception.Message}\nStack: {e.Exception.StackTrace}";
            await ErrorLogging.LogInfo(errorMessage);
        }

        private static async Task ShowMissingStringsDialogAsync()
        {
            if (MainWindow?.Content?.XamlRoot == null) return;

            var missingStrings = Loc.GetMissingStringsReport();

            var panel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 10 };

            if (missingStrings.Count == 0)
            {
                panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = "Good job! All strings on this page are fully translated.",
                    Foreground = new SolidColorBrush(Colors.LightGreen)
                });
            }
            else
            {
                var scrollViewer = new Microsoft.UI.Xaml.Controls.ScrollViewer { MaxHeight = 400 };
                var listPanel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 6 };

                foreach (var item in missingStrings.OrderByDescending(x => x.Status).ThenBy(x => x.Key))
                {
                    string statusText = item.Status == StringStatus.Missing ? "[MISSING]" : "[FALLBACK]";
                    var color = item.Status == StringStatus.Missing ? Colors.Red : Colors.Orange;

                    var tb = new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = $"{statusText} {item.Key}",
                        Foreground = new SolidColorBrush(color),
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new FontFamily("Consolas")
                    };
                    listPanel.Children.Add(tb);
                }
                scrollViewer.Content = listPanel;
                panel.Children.Add(scrollViewer);
            }

            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = $"Missing Strings on Page ({missingStrings.Count})",
                Content = panel,
                CloseButtonText = "Close",
                XamlRoot = MainWindow.Content.XamlRoot
            };

            try
            {
                await dialog.ShowAsync();
            }
            catch
            {
                // Suppress exception if another ContentDialog is already open
            }
        }

        #region System & Process Utilities
        private static void SetCurrentProcessAppId(string appId)
        {
            Win32Helper.SetCurrentProcessExplicitAppUserModelID(appId);
        }

        public static void SetPriority(Priority priority)
        {
            var (boost, procClass, isEfficiencyMode) = priority switch
            {
                Priority.Low => (false, ProcessPriorityClass.Idle, true),
                Priority.Normal => (true, ProcessPriorityClass.Normal, false),
                Priority.High => (true, ProcessPriorityClass.High, false),
                _ => throw new NotImplementedException()
            };

            try
            {
                var process = Process.GetCurrentProcess();
                process.PriorityBoostEnabled = boost;
                process.PriorityClass = procClass;

                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(isEfficiencyMode);
            }
            catch { }
        }

        private bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static async Task<bool> WaitForFileReady(string filePath, int timeoutMs = 5000)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException) { await Task.Delay(200); }
            }
            return false;
        }

        public static async Task<bool> NotifyHotkeySettingsChanged()
        {
            HotkeySettingsChanged?.Invoke(null, EventArgs.Empty);

            var service = GetService<IHotkeyService>();
            if (service == null) return false;

            service.UnregisterAll();

            bool allSuccess = true;

            if (LocalMachineSettingsEngine.UseHotkey)
            {
                var hotkey = new EvolveOS_Optimizer.Core.Model.Hotkey(
                    LocalMachineSettingsEngine.OptimizationModifiers,
                    LocalMachineSettingsEngine.OptimizationKey
                );

                bool success = await service.Register(hotkey, () =>
                {
                    Task.Run(() => RunGlobalOptimizationAsync());
                });

                if (!success)
                {
                    ShowNotification("Hotkey Warning", $"Optimization Hotkey {hotkey} is in use.", InfoBarSeverity.Warning, 5000);
                    allSuccess = false;
                }
            }

            if (SettingsEngine.IsPasswordGenHotkeyEnabled)
            {
                var pwHotkey = new Hotkey(
                    (Windows.System.VirtualKeyModifiers)SettingsEngine.PasswordGenHotkeyModifier,
                    (Windows.System.VirtualKey)SettingsEngine.PasswordGenHotkeyKey
                );

                bool success = await service.Register(pwHotkey, () =>
                {
                    UIThreadDispatcher?.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        OpenPasswordGeneratorWindow();
                    });
                });

                if (!success)
                {
                    NativeToastHelper.SendNativeToast(
                        "Hotkey Warning",
                        $"Password Gen Hotkey {pwHotkey} is in use by another app."
                    );
                    allSuccess = false;
                }
            }

            if (LocalMachineSettingsEngine.IsTranslationHotkeyEnabled)
            {
                var locHotkey = new Hotkey(
                    (Windows.System.VirtualKeyModifiers)LocalMachineSettingsEngine.TranslationHotkeyModifier,
                    (Windows.System.VirtualKey)LocalMachineSettingsEngine.TranslationHotkeyKey
                );

                bool success = await service.Register(locHotkey, () =>
                {
                    UIThreadDispatcher?.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                    {
                        await ShowMissingStringsDialogAsync();
                    });
                });

                if (!success)
                {
                    NativeToastHelper.SendNativeToast("Hotkey Warning", $"Translation Debug Hotkey {locHotkey} is in use by another app.");
                    allSuccess = false;
                }
            }

            if (LocalMachineSettingsEngine.IsFindHotkeyEnabled)
            {
                var findHotkey = new Hotkey(
                    (Windows.System.VirtualKeyModifiers)LocalMachineSettingsEngine.FindHotkeyModifier,
                    (Windows.System.VirtualKey)LocalMachineSettingsEngine.FindHotkeyKey
                );

                bool success = await service.Register(findHotkey, () =>
                {
                    UIThreadDispatcher?.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        WeakReferenceMessenger.Default.Send(new OpenFindDialogMessage());
                    });
                });

                if (!success)
                {
                    ShowNotification("Hotkey Conflict", $"The Find Hotkey {findHotkey} is already assigned or restricted.", InfoBarSeverity.Warning, 5000);
                    allSuccess = false;
                }
            }

            return allSuccess;
        }

        private static void OpenPasswordGeneratorWindow()
        {
            if (_passwordGeneratorWindow == null)
            {
                _passwordGeneratorWindow = new PasswordGeneratorWindow();

                _passwordGeneratorWindow.Closed += (s, e) => { _passwordGeneratorWindow = null; };

                _passwordGeneratorWindow.Activate();
            }
            else
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_passwordGeneratorWindow);
                Win32Helper.ShowWindow(hwnd, 9);
                Win32Helper.SetForegroundWindow(hwnd);
            }
        }

        private static async Task RunGlobalOptimizationAsync()
        {
            var sharedViewModel = DiagnosticsPageViewModel.Current;

            if (sharedViewModel != null)
            {
                await NotificationManager.ExecuteBackgroundOptimizationAsync(sharedViewModel);
            }
        }

        public static T? GetService<T>() where T : class
        {
            if (typeof(T) == typeof(IHotkeyService))
            {
                return _hotkeyService as T;
            }

            return null;
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[CRASH] {e.Message}");
            e.Handled = true;
        }

        public static void ShowNotification(string title, string message, Microsoft.UI.Xaml.Controls.InfoBarSeverity severity, int duration)
        {
            NotificationManager.Show(title, message)
                .WithSeverity(severity)
                .WithDuration(duration)
                .Perform();
        }

        #endregion

        #region App Initialization & Styling

        private void ElevateToAdmin()
        {
            string? exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                string args = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));

                ProcessStartInfo proc = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                try { Process.Start(proc); }
                catch (Exception ex) { Debug.WriteLine($"[App] Elevation failed: {ex.Message}"); }
            }
            Environment.Exit(0);
        }

        private void EnsureShortcutWithAumid()
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "EvolveOS Optimizer.lnk");

            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            ShortcutHelper.CreateShortcut();

            if (File.Exists(shortcutPath))
            {
                File.SetLastWriteTime(shortcutPath, DateTime.Now);
                Debug.WriteLine("[App] Shortcut recreated and timestamped in CommonPrograms.");
            }
        }

        private async Task StartBackgroundServices()
        {
            try
            {
                //await RunGuard.CheckingDefenderExclusions();

                if (LocalMachineSettingsEngine.RgbOverrideOem)
                {
                    await Task.Run(() => OemManager.OverrideOemSoftware(true));
                }

                Debug.WriteLine("[App] Background services completed successfully.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Background service error: {ex.Message}");
            }
        }

        private bool CheckIfSafebootIsActive() => Win32Helper.GetSystemMetrics(Win32Helper.SM_CLEANBOOT) > 0;

        public void UpdateGlobalAccentColor(string hexColor)
        {
            try
            {
                if (string.IsNullOrEmpty(hexColor)) return;

                string hex = hexColor.Replace("#", string.Empty);
                if (hex.Length == 6) hex = "FF" + hex;

                byte a = (byte)uint.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte r = (byte)uint.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = (byte)uint.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = (byte)uint.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

                Color color = ColorHelper.FromArgb(a, r, g, b);

                if (this.Resources.ContainsKey("MyDynamicAccentColor"))
                {
                    this.Resources["MyDynamicAccentColor"] = color;
                }
                else
                {
                    this.Resources.Add("MyDynamicAccentColor", color);
                }

                if (this.Resources.ContainsKey("MyDynamicAccentBrush"))
                {
                    if (this.Resources["MyDynamicAccentBrush"] is SolidColorBrush brush)
                    {
                        brush.Color = color;
                    }
                }
                else
                {
                    this.Resources.Add("MyDynamicAccentBrush", new SolidColorBrush(color));
                }

                Debug.WriteLine($"[App] Global accent color updated to: {hexColor}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Failed to load startup accent: {ex.Message}");
            }
        }

        #endregion

        #region Application Cleanup & Shutdown
        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            ExitApp();
        }

        #region Cleanup & Offline Database Backup Logic
        /*private static void HandleCleanup()
        {
            if (_isCleanupRunning)
            {
                return;
            }

            _isCleanupRunning = true;

            try
            {
                bool hasActiveAutoLogin = TokenManager.TokenExists();

                if (LocalMachineSettingsEngine.KeepDevModeOnExit || hasActiveAutoLogin)
                {
                    // Respect the explicit session choice or the auto-login state
                }
                else
                {
                    LocalMachineSettingsEngine.IsDeveloperMode = false;
                    LocalMachineSettingsEngine.IsTranslationHotkeyEnabled = false;
                }
            }
            catch { }

            try
            {
                SqlConnectionHelper.ReleaseDatabase();
            }
            catch { }

            try
            {
                //var stopInfo = new ProcessStartInfo("sqllocaldb", "stop MSSQLLocalDB -i") // -i -> Force
                var stopInfo = new ProcessStartInfo("sqllocaldb", "stop MSSQLLocalDB")
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(stopInfo)?.WaitForExit();

                Thread.Sleep(500);
            }
            catch { /* Log error */ /*}

            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            string baseDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            string mdfPath = Path.Combine(baseDir, PlainDb);
            string ldfPath = Path.Combine(baseDir, PlainLdf);
            string securePath = Path.Combine(baseDir, SecureDb);
            string secureLdfPath = Path.Combine(baseDir, SecureLdf);

            if (!File.Exists(mdfPath))
            {
                Debug.WriteLine("[App] No MDF file found. Skipping encryption.");
                ReleaseMemory();
                return;
            }

            bool isReady = false;
            for (int i = 0; i < 20; i++)
            {
                if (!DatabaseSecurityService.IsFileLocked(mdfPath))
                {
                    isReady = true;
                    break;
                }
                Thread.Sleep(500);
            }

            if (isReady)
            {
                try
                {
                    if (SettingsEngine.PerformDbBackup)
                    {
                        ExecuteRawDatabaseBackup(mdfPath, ldfPath);
                    }

                    DatabaseSecurityService.EncryptDatabase(mdfPath, securePath);

                    if (File.Exists(ldfPath))
                    {
                        DatabaseSecurityService.EncryptDatabase(ldfPath, secureLdfPath);
                    }

                    if (File.Exists(securePath))
                    {
                        File.Delete(mdfPath);
                    }
                    if (File.Exists(secureLdfPath))
                    {
                        File.Delete(ldfPath);
                    }

                    Debug.WriteLine("[App] Database successfully encrypted and plain files deleted.");
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(ex, "App_HandleCleanup_Fail");
                }
            }
            else
            {
                Debug.WriteLine("[App] Timeout waiting for SQL Server to release the database files.");
            }

            try
            {
                _hotkeyService?.Dispose();

                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
            catch { }

            ReleaseMemory();
        }

        private static void ExecuteRawDatabaseBackup(string mdfSource, string ldfSource)
        {
            try
            {
                string backupDir = SettingsEngine.DatabaseBackupPath;
                if (string.IsNullOrEmpty(backupDir) || !Directory.Exists(backupDir))
                {
                    return;
                }

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                bool encrypt = SettingsEngine.EncryptDbBackupCopies;

                string destMdf = Path.Combine(backupDir, $"EvolveOS_Backup_{stamp}{(encrypt ? ".dat" : ".mdf.bak")}");
                string destLdf = Path.Combine(backupDir, $"EvolveOS_Log_{stamp}{(encrypt ? "_log.dat" : ".ldf.bak")}");

                if (File.Exists(mdfSource))
                {
                    if (encrypt)
                    {
                        DatabaseSecurityService.EncryptDatabase(mdfSource, destMdf);
                    }
                    else
                    {
                        File.Copy(mdfSource, destMdf, true);
                    }
                }

                if (File.Exists(ldfSource))
                {
                    if (encrypt)
                    {
                        DatabaseSecurityService.EncryptDatabase(ldfSource, destLdf);
                    }
                    else
                    {
                        File.Copy(ldfSource, destLdf, true);
                    }
                }

                if (!SettingsEngine.KeepBackupEnabled)
                {
                    ResetBackupSettings();
                    Debug.WriteLine("[App] Backup settings reset as requested.");
                }
                else
                {
                    Debug.WriteLine("[App] Backup settings preserved for next session.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Failed to execute raw database backup: {ex.Message}");
            }
        }*/
        #endregion

        #region Cleanup & Online Database Backup Logic
        private static void HandleCleanup(Views.LoadingWindow? shutdownWindow = null)
        {
            if (_isCleanupRunning)
            {
                return;
            }

            _isCleanupRunning = true;

            if (!IsPrimaryInstance)
            {
                return;
            }

            try
            {
                FanControlEngine.Instance.Shutdown();

                try
                {
                    RgbControlEngine.Instance.DisposeAsync().AsTask().Wait();
                }
                catch { }

                MemoryGuardian?.Dispose();
                _hotkeyService?.Dispose();
            }
            catch { }

            try
            {
                bool hasActiveAutoLogin = TokenManager.TokenExists();

                if (LocalMachineSettingsEngine.KeepDevModeOnExit || hasActiveAutoLogin)
                {
                    // Respect the explicit session choice or the auto-login state
                }
                else
                {
                    LocalMachineSettingsEngine.IsDeveloperMode = false;
                    LocalMachineSettingsEngine.IsTranslationHotkeyEnabled = false;
                }
            }
            catch { }

            try
            {
                if (SettingsEngine.PerformDbBackup)
                {
                    shutdownWindow?.DispatcherQueue.TryEnqueue(() =>
                        shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_init_backup") ?? "Initializing database backup..."));
                    ExecuteOnlineDatabaseBackup(shutdownWindow);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "App_OnlineBackup_Fail");
            }

            try
            {
                SqlConnectionHelper.ReleaseDatabase();
            }
            catch { }

            try
            {
                shutdownWindow?.DispatcherQueue.TryEnqueue(() =>
                    shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_closing_db") ?? "Closing database engine..."));

                var stopInfo = new ProcessStartInfo("sqllocaldb", "stop MSSQLLocalDB")
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(stopInfo)?.WaitForExit();

                Thread.Sleep(500);
            }
            catch { /* Log error */ }

            shutdownWindow?.DispatcherQueue.TryEnqueue(() =>
                shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_securing_files") ?? "Securing files..."));

            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            string baseDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            string mdfPath = Path.Combine(baseDir, PlainDb);
            string ldfPath = Path.Combine(baseDir, PlainLdf);
            string securePath = Path.Combine(baseDir, SecureDb);
            string secureLdfPath = Path.Combine(baseDir, SecureLdf);

            if (!File.Exists(mdfPath))
            {
                Debug.WriteLine("[App] No MDF file found. Skipping encryption.");
                ReleaseMemory();
                return;
            }

            bool isReady = false;
            for (int i = 0; i < 20; i++)
            {
                if (!DatabaseSecurityService.IsFileLocked(mdfPath))
                {
                    isReady = true;
                    break;
                }
                Thread.Sleep(500);
            }

            if (isReady)
            {
                try
                {
                    DatabaseSecurityService.EncryptDatabase(mdfPath, securePath);

                    if (File.Exists(ldfPath))
                    {
                        DatabaseSecurityService.EncryptDatabase(ldfPath, secureLdfPath);
                    }

                    if (File.Exists(securePath))
                    {
                        File.Delete(mdfPath);
                    }
                    if (File.Exists(secureLdfPath))
                    {
                        File.Delete(ldfPath);
                    }

                    Debug.WriteLine("[App] Database successfully encrypted and plain files deleted.");
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(ex, "App_HandleCleanup_Fail");
                }
            }
            else
            {
                Debug.WriteLine("[App] Timeout waiting for SQL Server to release the database files.");
            }

            try
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
            catch { }

            ReleaseMemory();
        }


        private static void ExecuteOnlineDatabaseBackup(Views.LoadingWindow? shutdownWindow = null)
        {
            try
            {
                string backupDir = SettingsEngine.DatabaseBackupPath;
                if (string.IsNullOrEmpty(backupDir) || !Directory.Exists(backupDir))
                {
                    return;
                }

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                bool encrypt = SettingsEngine.EncryptDbBackupCopies;

                string plainBakPath = Path.Combine(backupDir, $"EvolveOS_Backup_{stamp}.bak");

                shutdownWindow?.DispatcherQueue.TryEnqueue(() =>
                    shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_writing_backup") ?? "Writing backup to disk..."));

                using (var connection = new SqlConnection(SqlConnectionHelper.connectReturn()))
                {
                    connection.Open();

                    string activeDbName = connection.Database;

                    string query = $"BACKUP DATABASE [{activeDbName}] TO DISK = '{plainBakPath}'";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                if (encrypt && File.Exists(plainBakPath))
                {
                    shutdownWindow?.DispatcherQueue.TryEnqueue(() =>
                        shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_encrypting_backup") ?? "Encrypting backup copy..."));
                    string encryptedBakPath = Path.Combine(backupDir, $"EvolveOS_Backup_{stamp}.dat");
                    DatabaseSecurityService.EncryptDatabase(plainBakPath, encryptedBakPath);

                    if (File.Exists(encryptedBakPath))
                    {
                        File.Delete(plainBakPath);
                    }
                }

                shutdownWindow?.DispatcherQueue.TryEnqueue(() =>
                    shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_finalizing_backup") ?? "Finalizing backup settings..."));

                if (!SettingsEngine.KeepBackupEnabled)
                {
                    ResetBackupSettings();
                    Debug.WriteLine("[App] Backup settings reset as requested.");
                }
                else
                {
                    Debug.WriteLine("[App] Backup settings preserved for next session.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Failed to execute online database backup: {ex.Message}");
                throw;
            }
        }
        #endregion

        private static void ResetBackupSettings()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(PathLocator.Registry.SubKey, true);

                if (key == null)
                {
                    return;
                }

                key.DeleteValue("DatabaseBackupPath", false);
                key.DeleteValue("PerformDbBackup", false);
                key.DeleteValue("EncryptDbBackupCopies", false);
                key.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Failed to reset backup registry settings: {ex.Message}");
            }
        }

        public static void ExitApp(string? customMessage = null)
        {
            if (_isExiting) return;
            _isExiting = true;

            string finalMessage = !string.IsNullOrEmpty(customMessage)
                ? customMessage
                : (ResourceString.GetString("closing_message") ?? "Closing EvolveOS Optimizer");

            if (UIThreadDispatcher != null && !UIThreadDispatcher.HasThreadAccess)
            {
                UIThreadDispatcher.TryEnqueue(async () => await ExecuteExitSequenceAsync(finalMessage));
            }
            else
            {
                _ = ExecuteExitSequenceAsync(finalMessage);
            }
        }

        private static async Task ExecuteExitSequenceAsync(string displayTitle)
        {
            try
            {
                Debug.WriteLine("[App] Shutting down...");

                var shutdownWindow = new Views.LoadingWindow(false, isShutdownMode: true);
                shutdownWindow.SetHeaderTitle(displayTitle);
                shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_prep_close") ?? "Preparing to close...");

                shutdownWindow.Activate();

                if (MainWindow != null)
                {
                    IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                    Win32Helper.ShowWindow(hwnd, 0);
                }

                if (shutdownWindow.Content is FrameworkElement rootElement)
                {
                    rootElement.Opacity = 1;
                }

                await Task.Delay(500);

                await Task.Run(() =>
                {
                    HandleCleanup(shutdownWindow);
                });

                shutdownWindow.UpdateShutdownText(ResourceString.GetString("status_goodbye") ?? "Goodbye!");

                await Task.Delay(750);

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExitApp Exception: {ex.Message}");
                Environment.Exit(1);
            }
        }

        public static void ReleaseMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Win32Helper.EmptyWorkingSet(Process.GetCurrentProcess().Handle);

                Debug.WriteLine("[App] Memory successfully released.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Failed to release memory: {ex.Message}");
            }
        }
        #endregion
    }
}