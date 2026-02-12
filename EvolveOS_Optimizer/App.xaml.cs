using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Views;
using System.IO;
using System.Security.Principal;
using System.Threading;

namespace EvolveOS_Optimizer
{
    public partial class App : Application
    {
        public Window? MainWindow { get; set; }
        private static Mutex? _mutex;

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

            SetPriority(ProcessPriorityClass.High);

            SettingsEngine.CheckingParameters();
            App.Current.UpdateGlobalAccentColor(SettingsEngine.AccentColor);

            var loadingWindow = new LoadingWindow();
            MainWindow = loadingWindow;

            SettingsEngine.UpdateTheme(SettingsEngine.AppTheme);
            MainWindow.Activate();

            MainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            {
                await Task.Delay(500);
                UIHelper.ApplyBackdrop(MainWindow, SettingsEngine.Backdrop);

                _ = StartBackgroundServices();
            });
        }

        #region System & Process Utilities

        public static void SetPriority(ProcessPriorityClass priorityClass)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                process.PriorityClass = priorityClass;
                Debug.WriteLine($"[App] Priority set to: {priorityClass}");
            }
            catch (Exception ex) { Debug.WriteLine($"[App] Priority Error: {ex.Message}"); }
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
    }
}
