using System.IO;
using System.Security.Principal;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks.DefenderManager;
using EvolveOS_Optimizer.Views;
using Microsoft.Windows.AppNotifications;
using EvolveOS_Optimizer.Utilities.Services;

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

        public static Microsoft.UI.Dispatching.DispatcherQueue? UIThreadDispatcher { get; private set; }

        private static Mutex? _mutex;

        private static IHotkeyService? _hotkeyService;
        public static event EventHandler? HotkeySettingsChanged;
        private static PasswordGeneratorWindow? _passwordGeneratorWindow;

        public static new App Current => (App)Application.Current;

        public App()
        {
            InitializeComponent();
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            UnhandledException += OnUnhandledException;

            AppDomain.CurrentDomain.ProcessExit += (s, ev) => HandleCleanup();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            string aumid = "EvolveOS.Optimizer.App";
            SetCurrentProcessAppId(aumid);

            UIThreadDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            try
            {
                var dummy = Windows.ApplicationModel.Package.Current.Id;
            }
            catch (InvalidOperationException)
            {
                AppNotificationManager.Default.Register();
            }

            EnsureShortcutWithAumid();

            _mutex = new Mutex(true, "EvolveOS_Optimizer_SingleInstance", out bool isNewInstance);

            if (!isNewInstance)
            {
                IntPtr hwnd = Win32Helper.FindWindow("WinUIDesktopWin32WindowClass", "EvolveOS Optimizer");

                if (hwnd == IntPtr.Zero) hwnd = Win32Helper.FindWindow(null!, "EvolveOS Optimizer");

                if (hwnd != IntPtr.Zero)
                {
                    Win32Helper.ShowWindow(hwnd, 5);
                    Win32Helper.ShowWindow(hwnd, 9);
                    Win32Helper.SetForegroundWindow(hwnd);
                }
                else
                {
                    Win32Helper.MessageBox(IntPtr.Zero, "Failed to find the hidden window. Check the Title!", "Debug", 0);
                }

                Environment.Exit(0);
                return;
            }

            bool startHidden = Environment.CommandLine.Contains("-hidden", StringComparison.OrdinalIgnoreCase);

            IsStartedHidden = startHidden;

            if (!IsRunningAsAdmin())
            {
                ElevateToAdmin();
                return;
            }

            if (CheckIfSafebootIsActive())
            {
                string title = ResourceString.GetString("title_recovery");
                string message = ResourceString.GetString("msg_safemode_detected");

                if (string.IsNullOrEmpty(title)) title = "Recovery Mode";
                if (string.IsNullOrEmpty(message)) message = "Safe Mode detected! Would you like to attempt a recovery?";

                int result = Win32Helper.MessageBox(IntPtr.Zero, message, title, Win32Helper.MB_YESNO | Win32Helper.MB_ICONWARNING | Win32Helper.MB_DEFBUTTON1);

                if (result == Win32Helper.IDYES)
                {
                    _ = WindowsDefender.Recovery();
                    return;
                }
            }

            SetPriority(LocalMachineSettingsEngine.RunOnPriority);

            _hotkeyService = new EvolveOS_Optimizer.Utilities.Services.HotkeyService();

            СheckingGlobalParameters.Initialize();
            App.Current.UpdateGlobalAccentColor(SettingsEngine.AccentColor);

            _ = Core.ViewModel.MaintenanceViewModel.Current;

            SettingsEngine.UpdateTheme(SettingsEngine.AppTheme);

            if (IsStartedHidden)
            {
                MainWindow = new LoadingWindow();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                Win32Helper.ShowWindow(hwnd, 0);

                MainWindow.Activate();
            }
            else
            {
                MainWindow = new LoadingWindow();
                MainWindow.Activate();
            }

            MainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            {
                await Task.Delay(500);
                UIHelper.ApplyBackdrop(MainWindow, SettingsEngine.Backdrop);

                NotifyHotkeySettingsChanged();

                _ = StartBackgroundServices();
            });
        }

        #region System & Process Utilities
        private static void SetCurrentProcessAppId(string appId)
        {
            Win32Helper.SetCurrentProcessExplicitAppUserModelID(appId);
        }

        public static void SetPriority(Enums.Priority priority)
        {
            var (boost, procClass, threadPri, threadLevel) = priority switch
            {
                Enums.Priority.Low => (false, ProcessPriorityClass.Idle, ThreadPriority.Lowest, ThreadPriorityLevel.Idle),
                Enums.Priority.Normal => (true, ProcessPriorityClass.Normal, ThreadPriority.Normal, ThreadPriorityLevel.Normal),
                Enums.Priority.High => (true, ProcessPriorityClass.High, ThreadPriority.Highest, ThreadPriorityLevel.Highest),
                _ => throw new NotImplementedException()
            };

            try
            {
                Thread.CurrentThread.Priority = threadPri;
                var process = Process.GetCurrentProcess();
                process.PriorityBoostEnabled = boost;
                process.PriorityClass = procClass;

                Task.Run(() =>
                {
                    foreach (ProcessThread thread in process.Threads)
                    {
                        try
                        {
                            thread.PriorityBoostEnabled = boost;
                            thread.PriorityLevel = threadLevel;
                        }
                        catch { }
                    }
                });
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

        public static bool NotifyHotkeySettingsChanged()
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

                bool success = service.Register(hotkey, () =>
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
                var pwHotkey = new EvolveOS_Optimizer.Core.Model.Hotkey(
                    (Windows.System.VirtualKeyModifiers)SettingsEngine.PasswordGenHotkeyModifier,
                    (Windows.System.VirtualKey)SettingsEngine.PasswordGenHotkeyKey
                );

                bool success = service.Register(pwHotkey, () =>
                {
                    UIThreadDispatcher?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
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
            var sharedViewModel = MaintenanceViewModel.Current;

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
                await RunGuard.CheckingDefenderExclusions();

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

        private static void HandleCleanup()
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
                var stopInfo = new ProcessStartInfo("sqllocaldb", "stop MSSQLLocalDB -i")
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(stopInfo)?.WaitForExit();

                Thread.Sleep(500);
            }
            catch { /* Log error */ }

            // Note: _systemDiagnostics is handled correctly via IDisposable in LoadingWindow
            // When implemented the static BackupScheduler in this project, uncomment the next line:
            // BackupScheduler?.StopScheduler(); 

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

        public static void ExitApp()
        {
            try
            {
                Debug.WriteLine("[App] Shutting down...");

                HandleCleanup();

                Environment.Exit(0);
            }
            catch
            {
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