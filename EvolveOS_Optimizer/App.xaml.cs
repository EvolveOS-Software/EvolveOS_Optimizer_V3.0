using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace EvolveOS_Optimizer
{
    public partial class App : Application
    {
        public Window? MainWindow { get; private set; }
        private static Mutex? _mutex;

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
                string? exePath = Environment.ProcessPath;

                if (exePath != null)
                {
                    ProcessStartInfo proc = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    try
                    {
                        Process.Start(proc);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[App] Failed to restart as admin: {ex.Message}");
                    }
                }

                Environment.Exit(0);
                return;
            }

            SetPriority(ProcessPriorityClass.High);

            SettingsEngine.CheckingParameters();
            InitializeLocalization();

            MainWindow = new MainWindow();

            if (MainWindow is MainWindow main)
            {
                main.SetBackdropByName(SettingsEngine.Backdrop);

                UpdateGlobalAccentColor(SettingsEngine.AccentColor);
            }

            MainWindow.Activate();
        }

        #region Ported Logic from WPF Project

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

        #endregion

        private void InitializeLocalization()
        {
            try
            {
                string lang = SettingsEngine.Language;
                if (lang == "en") lang = "en-us";
                LocalizationService.Instance.LoadLanguage(lang);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Early Localizer Error: {ex.Message}");
            }
        }

        private void UpdateGlobalAccentColor(string hexColor)
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

                Windows.UI.Color color = Windows.UI.Color.FromArgb(a, r, g, b);

                if (Application.Current.Resources.ContainsKey("MyDynamicAccentBrush"))
                {
                    var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["MyDynamicAccentBrush"];
                    brush.Color = color;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load startup accent: {ex.Message}");
            }
        }
    }
}
