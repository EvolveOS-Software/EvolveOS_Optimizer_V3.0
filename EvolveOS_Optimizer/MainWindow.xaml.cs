using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;

namespace EvolveOS_Optimizer
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static MainWindow? Instance { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        private static Frame? _permanentFrameReference;

        private AppWindow? _appWindow;
        private IntPtr _hWnd;

        private DispatcherTimer? _sessionTimer;
        private DateTime _sessionExpiryTime;

        public string GetText(string key) => LocalizationService.Instance[key];

        public MainWindow()
        {
            Instance = this;

            this.InitializeComponent();
            this.Title = "EvolveOS Optimizer";

            DisplayProfileName.Text = UserSession.Username;
            ProfileImage.ImageSource = UserSession.ProfileImage;

            _permanentFrameReference = this.ContentFrame;

            NotificationManager.Initialize(this);
            _hWnd = WindowNative.GetWindowHandle(this);

            if (App.IsStartedHidden)
            {
                Win32Helper.ShowWindow(_hWnd, 0);
            }

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            ConfigureWindow();

            Win32Helper.LogProcessIntegrityLevel();
            Win32Helper.InitializeAdminDragDrop(_hWnd, RouteFilesToPage);

            WindowHelper.RegisterMinWidthHeight(_hWnd, 850, 400);
            UIHelper.RegisterPageTransition(ContentFrame, RootGrid);

            this.Activated += MainWindow_Activated;

            this.AppWindow.Closing += AppWindow_Closing;
            this.SizeChanged += MainWindow_SizeChanged;

            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]") OnPropertyChanged(string.Empty);
            };

            this.RootGrid.Loaded += MainWindow_Loaded;
        }

        #region Window Configuration
        private void ConfigureWindow()
        {
            try
            {
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
                _appWindow = AppWindow.GetFromWindowId(windowId);

                if (_appWindow != null)
                {
                    _appWindow.SetIcon("Assets/EvolveOS_Optimizer.ico");
                    _appWindow.Resize(new Windows.Graphics.SizeInt32(1575, 870));

                    var titleBar = _appWindow.TitleBar;
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                    CenterWindow();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigureWindow Error] {ex.Message}");
            }
        }
        #endregion

        #region File Handling & Routing
        private void RouteFilesToPage(string[] paths)
        {
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                if (ContentFrame.Content is Pages.ScriptsPage scriptsPage)
                {
                    await scriptsPage.ViewModel.HandleDropAsync(paths);
                }
            });
        }
        #endregion

        #region Window Lifecycle Events
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (App.IsStartedHidden) return;

            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                ForceToForeground();
            }
        }

        public void ForceToForeground()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero) return;

            uint foregroundThreadId = Win32Helper.GetWindowThreadProcessId(Win32Helper.GetForegroundWindow(), IntPtr.Zero);
            uint appThreadId = Win32Helper.GetCurrentThreadId();

            Win32Helper.ShowWindow(hwnd, 5);
            Win32Helper.ShowWindow(hwnd, 9);

            if (foregroundThreadId != appThreadId)
            {
                Win32Helper.AttachThreadInput(appThreadId, foregroundThreadId, true);
                Win32Helper.SetForegroundWindow(hwnd);
                Win32Helper.AttachThreadInput(appThreadId, foregroundThreadId, false);
            }
            else
            {
                Win32Helper.SetForegroundWindow(hwnd);
            }
        }

        public void RefreshTrayIconLanguage()
        {
            if (TrayIcon.ContextFlyout is MenuFlyout flyout)
            {
                TrayIcon.ToolTipText = ResourceString.GetString("systray_click_show");

                foreach (var item in flyout.Items)
                {
                    if (item is MenuFlyoutItem menuItem)
                    {
                        if (menuItem.Name == "TrayMenu_Show")
                            menuItem.Text = ResourceString.GetString("systray_show_window");

                        else if (menuItem.Name == "TrayMenu_Hide")
                            menuItem.Text = ResourceString.GetString("systray_hide_window");

                        else if (menuItem.Name == "TrayMenu_RunStartup")
                            menuItem.Text = ResourceString.GetString("systray_run_startup");

                        else if (menuItem.Name == "TrayMenu_Close")
                            menuItem.Text = ResourceString.GetString("systray_close_window");
                    }
                }
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupNavigationObserver();

            if (!string.IsNullOrEmpty(UserSession.Username))
            {
                await InitializeUserPermissionsAsync(UserSession.Username);
            }

            if (SystemDiagnostics.IsNeedUpdate && SettingsEngine.IsUpdateCheckRequired)
            {
                this.DispatcherQueue.TryEnqueue(() => AnimateUpdateBanner(true));
            }

            if (AuthSessionManager.IsSessionValid(out string? sessionUser, out DateTime expiry))
            {
                StartLiveSessionTimer(expiry);
            }
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (SettingsEngine.IsCloseToTrayEnabled)
            {
                args.Cancel = true;

                if (RootGrid.DataContext is MainWinViewModel vm)
                {
                    vm.MinimizeCommand.Execute(null);
                }
            }
            else
            {
                App.ExitApp();
            }
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Minimized)
                {
                    if (RootGrid.DataContext is MainWinViewModel vm)
                    {
                        vm.MinimizeCommand.Execute(null);
                    }
                }
            }
        }

        private void LogoutMenu_Click(object sender, RoutedEventArgs e)
        {
            UserSession.IsAuthenticated = false;
            UserSession.Username = string.Empty;
            UserSession.UserType = string.Empty;
            UserSession.ProfileImage = null;

            TokenManager.DeleteToken();

            SettingsEngine.SelfReboot();
        }
        #endregion

        #region Global Auto-Login Session Monitoring
        public void StartLiveSessionTimer(DateTime expiry)
        {
            _sessionExpiryTime = expiry;

            if (_sessionTimer == null)
            {
                _sessionTimer = new DispatcherTimer();
                _sessionTimer.Interval = TimeSpan.FromSeconds(1);
                _sessionTimer.Tick += (s, e) => CheckSessionExpiry();
            }

            _sessionTimer.Start();
            Debug.WriteLine($"[Session] Global monitor started. Expires at: {_sessionExpiryTime}");
        }

        public void StopLiveSessionTimer()
        {
            _sessionTimer?.Stop();
            _sessionTimer = null;
            Debug.WriteLine("[Session] Global monitor stopped.");
        }

        private void CheckSessionExpiry()
        {
            if ((_sessionExpiryTime - DateTime.Now).TotalSeconds <= 0)
            {
                StopLiveSessionTimer();

                if (LocalMachineSettingsEngine.IsDeveloperMode)
                {
                    string devMsg = ResourceString.GetString("msgbox_dev_mode_logout_prompt") ?? "Developer Mode is still active. Would you like to disable it before logging out?";
                    string devTitle = ResourceString.GetString("msgbox_dev_mode_active_title") ?? "Developer Mode Active";

                    var devResult = Win32Helper.MessageBox(_hWnd, devMsg, devTitle, Win32Helper.MB_YESNO | Win32Helper.MB_ICONWARNING);

                    if (devResult == Win32Helper.IDYES)
                    {
                        LocalMachineSettingsEngine.IsDeveloperMode = false;
                    }
                    else
                    {
                        LocalMachineSettingsEngine.KeepDevModeOnExit = true;
                    }
                }

                UserSession.Clear();
                TokenManager.DeleteToken();

                string msg = ResourceString.GetString("msg_session_expired_login") ?? "Your session has expired. Please log in again.";
                NativeToastHelper.SendNativeToast("Session Expired", msg);

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    var weatherService = new WeatherService();
                    var loginWin = new Views.UserLoginWindow(weatherService, msg);

                    if (App.Current.MainWindow != null)
                    {
                        App.Current.MainWindow.Close();
                    }

                    App.Current.MainWindow = loginWin;
                    loginWin.Activate();

                    this.Close();
                });
            }
        }
        #endregion

        #region Navigation Logic
        private void SetupNavigationObserver()
        {
            if (this.RootGrid.DataContext is MainWinViewModel vm)
            {
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainWinViewModel.CurrentViewTag))
                    {
                        NavigateByTag(vm.CurrentViewTag);
                    }

                    if (e.PropertyName == nameof(MainWinViewModel.IsOverlayVisible))
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (vm.IsOverlayVisible)
                            {
                                WindowDimOverlay.IsHitTestVisible = true;
                                ShowDimOverlay.Begin();
                            }
                            else
                            {
                                WindowDimOverlay.IsHitTestVisible = false;
                                HideDimOverlay.Begin();
                            }

                            Debug.WriteLine($"[DEBUG] Overlay Toggled: {vm.IsOverlayVisible}");
                        });
                    }
                };

                NavigateByTag(vm.CurrentViewTag);
            }
        }

        public void NavigateByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || ContentFrame == null) return;

            Type pageType = tag switch
            {
                "Home" => typeof(Pages.HomePage),
                "Security" => typeof(Pages.SecurityPage),
                "Confidentiality" => typeof(Pages.PrivacyPage),
                "GroupPolicy" => typeof(Pages.GroupPolicyPage),
                "Interface" => typeof(Pages.InterfacePage),
                "ServiceTweaks" => typeof(Pages.ServicesPage),
                "SystemManager" => typeof(Pages.SystemManagerPage),
                "Software" => typeof(Pages.SoftwareCenterPage),
                "Utilities" => typeof(Pages.UtilitiesPage),
                "Scripts" => typeof(Pages.ScriptsPage),
                "System" => typeof(Pages.SystemPage),
                "Maintenance" => typeof(Pages.MaintenancePage),
                "Settings" => typeof(Pages.SettingsPage),
                _ => typeof(Pages.HomePage)
            };

            if (ContentFrame.Content?.GetType() == pageType) return;

            try
            {
                var oldPage = ContentFrame.Content as Page;

                var newPage = Activator.CreateInstance(pageType) as Page;

                ContentFrame.Content = newPage;
                //Debug.WriteLine($"[Navigation] Manually swapped to {tag}.");

                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (oldPage != null)
                    {
                        NavigationHelper.PurgePage(oldPage);

                        oldPage = null;
                    }
                });

                _ = NavigationHelper.TriggerDeepCleanupAsync(this.DispatcherQueue);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Critical] Swap Failed: {ex.Message}");
            }
        }

        public void SwitchPage(string tag)
        {
            if (this.RootGrid.DataContext is MainWinViewModel vm)
            {
                vm.CurrentViewTag = tag;
            }
            else
            {
                NavigateByTag(tag);
            }

            if (tag == "Utilities")
            {
                BtnNavUtilities.IsChecked = true;
            }
            else if (tag == "Maintenance") { BtnNavMaintenance.IsChecked = true; }
            else if (tag == "Security") { BtnNavSecurity.IsChecked = true; }
        }
        #endregion

        #region UI & Window Management
        public void SetBackdrop(SystemBackdrop backdrop) => this.SystemBackdrop = backdrop;

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

                Color color = Microsoft.UI.ColorHelper.FromArgb(a, r, g, b);

                if (App.Current.Resources.TryGetValue("MyDynamicAccentBrush", out object brushObj)
                    && brushObj is SolidColorBrush dynamicBrush)
                {
                    dynamicBrush.Color = color;
                }
                else
                {
                    App.Current.Resources["MyDynamicAccentBrush"] = new SolidColorBrush(color);
                }
                Debug.WriteLine($"[Accent] Applied color: {hexColor}");
            }
            catch (Exception ex) { Debug.WriteLine($"[Accent] Error: {ex.Message}"); }
        }
        #endregion

        #region Update Management
        public void AnimateUpdateBanner(bool show)
        {
            if (show)
            {
                UpdateBanner.Visibility = Visibility.Visible;
                UpdateBanner.UpdateLayout();
            }

            var visual = ElementCompositionPreview.GetElementVisual(UpdateBanner);
            var compositor = visual.Compositor;

            if (show)
            {
                visual.Opacity = 0f;
                visual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(0, 250f, 0));
            }

            var easeOut = compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.3f, 0.3f), new System.Numerics.Vector2(0.0f, 1.0f));
            var batch = compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);

            var moveAnim = compositor.CreateScalarKeyFrameAnimation();
            moveAnim.InsertKeyFrame(0.0f, show ? 200f : 0f);
            moveAnim.InsertKeyFrame(1.0f, show ? 0f : 200f, easeOut);
            moveAnim.Duration = TimeSpan.FromMilliseconds(500);

            var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(1.0f, show ? 1.0f : 0.0f);
            fadeAnim.Duration = TimeSpan.FromMilliseconds(400);

            visual.StartAnimation("Translation.Y", moveAnim);
            visual.StartAnimation("Opacity", fadeAnim);

            batch.Completed += (s, e) =>
            {
                if (!show)
                {
                    UpdateBanner.Visibility = Visibility.Collapsed;
                    Task.Delay(50).ContinueWith(_ => {
                        this.DispatcherQueue.TryEnqueue(() => NotificationManager.ProcessQueue());
                    });
                }
            };
            batch.End();
        }

        private void DismissBanner_Click(object sender, RoutedEventArgs e) => AnimateUpdateBanner(false);

        private async void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn) btn.IsEnabled = false;
                DownloadProgressArea.Visibility = Visibility.Visible;
                string downloadUrl = PathLocator.Links.GitHubLatest;
                string tempPath = Path.Combine(Path.GetTempPath(), $"EvolveOS_Update_{Guid.NewGuid():N}.exe");

                PulseAnimation.Begin();
                await DownloadUpdateAsync(downloadUrl, tempPath);
                PulseAnimation.Stop();

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
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            var sw = Stopwatch.StartNew();

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                totalRead += read;

                if (totalBytes.HasValue)
                {
                    if (sw.ElapsedMilliseconds > 100 || totalRead == totalBytes.Value)
                    {
                        double progress = (double)totalRead / totalBytes.Value * 100;
                        string sizeText = $"{Math.Round(totalRead / 1024.0 / 1024.0, 2)} MB / {Math.Round(totalBytes.Value / 1024.0 / 1024.0, 2)} MB";

                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ProgressDownload != null)
                            {
                                ProgressDownload.Value = progress;
                                SizeByte.Text = sizeText;
                            }
                        });
                        sw.Restart();
                    }
                }
            }
        }
        #endregion

        #region User Permissions & Access Control
        private async Task InitializeUserPermissionsAsync(string username)
        {
            try
            {
                string userType = "Guest";

                await Task.Run(() =>
                {
                    try
                    {
                        using (var conn = new Microsoft.Data.SqlClient.SqlConnection(SqlConnectionHelper.connectReturn()))
                        {
                            string sql = "SELECT usertype FROM admin WHERE username = @user";
                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@user", username);
                                conn.Open();
                                var result = cmd.ExecuteScalar();
                                userType = result?.ToString() ?? "Guest";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Permissions] DB Query Failed: {ex.Message}");
                    }
                });

                _dispatcherQueue.TryEnqueue(() =>
                {
                    ApplyUserPermissions(userType);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Permissions] Critical Failure: {ex.Message}");
                ApplyUserPermissions("Guest");
            }
        }

        private void ApplyUserPermissions(string type)
        {
            bool isAdmin = string.Equals(UserSession.UserType, "Admin", StringComparison.OrdinalIgnoreCase);

            Visibility adminVisibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            //if (BtnNavUserAccounts != null) BtnNavUserAccounts.Visibility = adminVisibility;

            UserSession.UserType = type;

            Debug.WriteLine($"[Permissions] Applied logic for type: {type}");
        }

        #endregion

        #region Events & Overrides
        private void Banner_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => NotificationManager.SetPaused(true);
        private void Banner_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => NotificationManager.SetPaused(false);
        private void DismissNotification_Click(object sender, RoutedEventArgs e) => NotificationManager.HideBanner();

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}