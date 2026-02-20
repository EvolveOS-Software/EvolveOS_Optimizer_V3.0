using System.IO;
using System.Security.Principal;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Utilities.Tweaks.DefenderManager;
using EvolveOS_Optimizer.Views;

namespace EvolveOS_Optimizer
{
    public partial class App : Application
    {
        public const string Name = "EvolveOS Optimizer";

        public Window? MainWindow { get; set; }
        private static Mutex? _mutex;

        private static IHotkeyService? _hotkeyService;
        public static event EventHandler? HotkeySettingsChanged;

        public static new App Current => (App)Application.Current;

        public App()
        {
            InitializeComponent();

            UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _mutex = new Mutex(true, "EvolveOS_Optimizer_SingleInstance", out bool isNewInstance);
            if (!isNewInstance) { Environment.Exit(0); return; }

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
                    _ =  WindowsDefender.Recovery();

                    return;
                }
            }

            SetPriority(LocalMachineSettingsEngine.RunOnPriority);

            _hotkeyService = new EvolveOS_Optimizer.Utilities.Services.HotkeyService();

            СheckingGlobalParameters.Initialize();
            App.Current.UpdateGlobalAccentColor(SettingsEngine.AccentColor);

            var loadingWindow = new LoadingWindow();
            MainWindow = loadingWindow;

            SettingsEngine.UpdateTheme(SettingsEngine.AppTheme);
            MainWindow.Activate();

            MainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            {
                await Task.Delay(500);
                UIHelper.ApplyBackdrop(MainWindow, SettingsEngine.Backdrop);

                NotifyHotkeySettingsChanged();

                _ = StartBackgroundServices();
            });
        }

        #region System & Process Utilities

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

            if (LocalMachineSettingsEngine.UseHotkey)
            {
                var hotkey = new EvolveOS_Optimizer.Core.Model.Hotkey(
                    LocalMachineSettingsEngine.OptimizationModifiers,
                    LocalMachineSettingsEngine.OptimizationKey
                );

                bool success = service.Register(hotkey, () =>
                {
                    Task.Run(() => RunGlobalOptimization());
                });

                if (!success)
                {
                    ShowNotification("Hotkey Warning", $"Hotkey {hotkey} is in use by another app.", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning, 5000);
                }

                return success;
            }

            return true;
        }

        private static void RunGlobalOptimization()
        {
            var computerService = new ComputerService();
            _ = computerService.Optimize(Enums.Memory.Optimization.Reason.Manual, LocalMachineSettingsEngine.MemoryAreas);

            if (Current.MainWindow?.DispatcherQueue != null)
            {
                Current.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    ShowNotification("Optimizer", "Memory successfully cleaned via Global Hotkey!", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, 3000);
                });
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
                ProcessStartInfo proc = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                try { Process.Start(proc); }
                catch (Exception ex) { Debug.WriteLine($"[App] Elevation failed: {ex.Message}"); }
            }
            Environment.Exit(0);
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

        public static void ExitApp()
        {
            try
            {
                Debug.WriteLine("[App] Shutting down...");

                _hotkeyService?.Dispose();

                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }

                ReleaseMemory();

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