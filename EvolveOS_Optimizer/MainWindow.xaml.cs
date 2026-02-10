using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;

namespace EvolveOS_Optimizer
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private AppWindow? _appWindow;
        private IntPtr _hWnd;

        public string GetText(string key) => LocalizationService.Instance[key];

        private bool _isBackdropInitialized = false;

        public MainWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            if (_appWindow != null)
            {
                _appWindow.SetIcon("Assets/EvolveOS_Optimizer.ico");
                var titleBar = _appWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                _appWindow.Resize(new Windows.Graphics.SizeInt32(1575, 870));
            }

            WindowHelper.RegisterMinWidthHeight(_hWnd, 700, 400);
            UIHelper.RegisterPageTransition(RootContentControl, RootGrid);

            CenterWindow();

            this.Activated += (s, e) =>
            {
                if (!_isBackdropInitialized && e.WindowActivationState != WindowActivationState.Deactivated)
                {
                    _isBackdropInitialized = true;

                    this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
                    {
                        await Task.Delay(400);

                        UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);

                        Debug.WriteLine("[Startup] Backdrop initialization complete.");
                    });
                }
            };

            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    OnPropertyChanged(string.Empty); 
                }
            };

            this.RootGrid.Loaded += MainWindow_Loaded;
        }

        public void SetBackdrop(SystemBackdrop backdrop)
        {
            this.SystemBackdrop = backdrop;
        }

        public void SetBackdropByName(string name)
        {
            this.SystemBackdrop = name switch
            {
                "Mica" => new MicaBackdrop(),
                "MicaAlt" => new MicaBackdrop() { Kind = MicaKind.BaseAlt },
                "Acrylic" => new DesktopAcrylicBackdrop(),
                _ => null
            };
        }

        private void CenterWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

                if (displayArea != null)
                {
                    var centeredPos = appWindow.Position;

                    centeredPos.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                    centeredPos.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;

                    appWindow.Move(centeredPos);
                }
            }
        }

        public static void ApplyAccentColor(string hexColor)
        {
            try
            {
                hexColor = hexColor.Replace("#", string.Empty);
                byte a = (byte)uint.Parse(hexColor.Substring(0, 2), NumberStyles.HexNumber);
                byte r = (byte)uint.Parse(hexColor.Substring(2, 2), NumberStyles.HexNumber);
                byte g = (byte)uint.Parse(hexColor.Substring(4, 2), NumberStyles.HexNumber);
                byte b = (byte)uint.Parse(hexColor.Substring(6, 2), NumberStyles.HexNumber);

                global::Windows.UI.Color color = global::Windows.UI.Color.FromArgb(a, r, g, b);

                if (App.Current.Resources.TryGetValue("MyDynamicAccentBrush", out object brushObj)
                    && brushObj is SolidColorBrush dynamicBrush)
                {
                    dynamicBrush.Color = color;
                }
                else
                {
                    App.Current.Resources["MyDynamicAccentBrush"] = new SolidColorBrush(color);
                }

                Debug.WriteLine($"[Accent] Successfully applied color: {hexColor}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Accent] Error parsing/applying color: {ex.Message}");
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (SystemDiagnostics.IsNeedUpdate && SettingsEngine.IsUpdateCheckRequired)
            {
                await Task.Delay(500);
                //AnimateUpdateBanner(true);
            }
        }

        public void AnimateUpdateBanner(bool show)
        {
            if (show)
            {
                // 1. Hide the standard title bar
                AppTitleBar.Visibility = Visibility.Collapsed;

                // 2. Show the update banner
                UpdateBanner.Visibility = Visibility.Visible;

                // Optional: Simple fade-in for the banner
                var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(UpdateBanner);
                var compositor = visual.Compositor;
                var fade = compositor.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0.0f, 0.0f);
                fade.InsertKeyFrame(1.0f, 1.0f);
                fade.Duration = TimeSpan.FromMilliseconds(300);
                visual.StartAnimation("Opacity", fade);
            }
            else
            {
                // Reverse: Show title bar and hide banner
                UpdateBanner.Visibility = Visibility.Collapsed;
                AppTitleBar.Visibility = Visibility.Visible;
            }
        }

        private void DismissBanner_Click(object sender, RoutedEventArgs e) => AnimateUpdateBanner(false);

        private async void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn) btn.IsEnabled = false;


                DownloadProgressArea.Visibility = Visibility.Visible;

                string downloadUrl = PathLocator.Links.GitHubLatest;
                string tempPath = Path.Combine(Path.GetTempPath(), $"EvolveOS_Update_{Guid.NewGuid().ToString("N").Substring(0, 8)}.exe");

                await DownloadUpdateAsync(downloadUrl, tempPath);

                string currentExe = Environment.ProcessPath ?? AppContext.BaseDirectory;
                string exeName = Path.GetFileName(currentExe) ?? "EvolveOS_Optimizer.exe";
                string cmdScript = $"/c timeout /t 1 & taskkill /f /im \"{exeName}\" & timeout /t 2 & del /f /q \"{currentExe}\" & move /y \"{tempPath}\" \"{currentExe}\" & start \"\" \"{currentExe}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdScript,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update Error] {ex.Message}");
                DownloadProgressArea.Visibility = Visibility.Collapsed;
                if (sender is Button btn) btn.IsEnabled = true;
            }
        }

        private async Task DownloadUpdateAsync(string url, string destinationPath)
        {
            using HttpClient client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                if (totalBytes.HasValue)
                {
                    double progress = (double)totalRead / totalBytes.Value * 100;

                    // DispatcherQueue handles the thread-safe UI update
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        ProgressDownload.Value = progress;
                        SizeByte.Text = $"{Math.Round(totalRead / 1024.0)} KB / {Math.Round(totalBytes.Value / 1024.0)} KB";
                    });
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}