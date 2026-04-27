using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Pages;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;

namespace EvolveOS_Optimizer
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static MainWindow? Instance { get; private set; }

        public DiagnosticsPageViewModel DiagnosticsVM => DiagnosticsPageViewModel.Current;

        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        private Pages.DiagnosticsPage? _cachedDiagnosticsPage;

        private static Frame? _permanentFrameReference;

        private AppWindow? _appWindow;
        private IntPtr _hWnd;

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "EvolveOS_Optimizer.ico");

        private DispatcherTimer? _sessionTimer;
        private DateTime _sessionExpiryTime;

        public string GetText(string key) => LocalizationService.Instance[key];

        public MainWindow()
        {
            Instance = this;

            this.InitializeComponent();

            if (this.Content is FrameworkElement rootElement)
            {
                string savedTheme = SettingsEngine.AppTheme;

                if (savedTheme == "Dark")
                    rootElement.RequestedTheme = ElementTheme.Dark;
                else if (savedTheme == "Light")
                    rootElement.RequestedTheme = ElementTheme.Light;
                else
                    rootElement.RequestedTheme = ElementTheme.Default;
            }

            string savedBackdrop = SettingsEngine.Backdrop;
            UIHelper.ApplyBackdrop(this, savedBackdrop);

            this.Title = "EvolveOS Optimizer";

            if (File.Exists(iconPath))
            {
                _appWindow?.SetIcon(iconPath);
            }
            else
            {
                Debug.WriteLine($"[Icon Warning] Could not find icon at: {iconPath}");
            }

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

            DiagnosticsVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DiagnosticsVM.IsBusy))
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (GlobalOptimizationOverlay != null)
                        {
                            AnimateOptimizationOverlay(DiagnosticsVM.IsBusy);
                        }
                    });
                }
            };
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

            bool isMinimized = false;
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow?.Presenter is OverlappedPresenter presenter)
            {
                isMinimized = presenter.State == OverlappedPresenterState.Minimized;
            }

            uint foregroundThreadId = Win32Helper.GetWindowThreadProcessId(Win32Helper.GetForegroundWindow(), IntPtr.Zero);
            uint appThreadId = Win32Helper.GetCurrentThreadId();

            if (isMinimized)
            {
                Win32Helper.ShowWindow(hwnd, 9);
            }
            else
            {
                Win32Helper.ShowWindow(hwnd, 5);
            }

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

            _ = Task.Run(async () =>
            {
                if (SettingsEngine.IsUpdateCheckRequired)
                {
                    await SystemDiagnostics.ValidateVersionUpdatesAsync();

                    if (SystemDiagnostics.IsNeedUpdate)
                    {
                        await Task.Delay(1500);

                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            Debug.WriteLine("[UPDATE] Auto-check found update. Triggering Banner UI.");

                            UpdateBanner.Visibility = Visibility.Visible;
                            UpdateBanner.Opacity = 1.0;
                            UpdateBanner.UpdateLayout();

                            AnimateUpdateBanner(true);
                        });
                    }
                }
            });

            if (AuthSessionManager.IsSessionValid(out string? sessionUser, out DateTime expiry))
            {
                StartLiveSessionTimer(expiry);
            }

            bool isWindowsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            bool isAppAdmin = UserSession.UserType == "Admin";

            if (LocalMachineSettingsEngine.IsFirstRun && isAppAdmin && isWindowsAdmin)
            {
                this.DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(1000);
                    await ShowRestorePointDialogAsync();
                });
            }
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
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

                    if (App.MainWindow != null)
                    {
                        App.MainWindow.Close();
                    }

                    App.MainWindow = loginWin;
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
                "Diagnostics" => typeof(Pages.DiagnosticsPage),
                "SystemManager" => typeof(Pages.SystemManagerPage),
                "Software" => typeof(Pages.SoftwareCenterPage),
                "GroupPolicy" => typeof(Pages.GroupPolicyPage),
                "Tweaks" => typeof(Pages.TweaksPage),
                "Utilities" => typeof(Pages.UtilitiesPage),
                "Scripts" => typeof(Pages.ScriptsPage),
                "Settings" => typeof(Pages.SettingsPage),
                "UserAccounts" => typeof(Pages.UserAccountsPage),
                _ => typeof(Pages.HomePage)
            };

            if (ContentFrame.Content?.GetType() == pageType) return;

            try
            {
                var oldPage = ContentFrame.Content as Page;
                Page? newPage = null;

                if (pageType == typeof(Pages.DiagnosticsPage))
                {
                    if (_cachedDiagnosticsPage == null)
                    {
                        _cachedDiagnosticsPage = new Pages.DiagnosticsPage();
                    }
                    newPage = _cachedDiagnosticsPage;
                }
                else
                {
                    newPage = Activator.CreateInstance(pageType) as Page;
                }

                ContentFrame.Content = newPage;

                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (oldPage != null && !(oldPage is Pages.DiagnosticsPage))
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

        public void SwitchPage(string tag, string requestedPane = "")
        {
            if (tag == "Diagnostics" && !string.IsNullOrEmpty(requestedPane))
            {
                if (DiagnosticsPage.ExternalPaneRequest != null)
                {
                    DiagnosticsPage.ExternalPaneRequest?.Invoke(requestedPane);
                }
                else
                {
                    DiagnosticsPage.RequestedPaneOnLoad = requestedPane;
                }
            }

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
            else if (tag == "Diagnostics")
            {
                BtnNavDiagnostics.IsChecked = true;
            }
        }

        private void SidebarContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            SidebarContainer.Width = 300;
        }

        private void SidebarContainer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            SidebarContainer.Width = 75;
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

        private async Task ShowRestorePointDialogAsync()
        {
            var neverShowAgain = new CheckBox
            {
                Content = ResourceString.GetString("chkbox_do_not_show") ?? "Do not show this again",
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("title_restore_point") ?? "Create Restore Point",
                Content = new StackPanel
                {
                    Children =
            {
                new TextBlock
                {
                    Text = ResourceString.GetString("txt_restore_point_dialog") ?? "It is highly recommended to create a system restore point before using this or other optimization tools.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                },
                neverShowAgain
            }
                },
                PrimaryButtonText = ResourceString.GetString("btn_continue") ?? "Continue",
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Close",
                XamlRoot = this.Content.XamlRoot,
                PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                BorderBrush = Application.Current.Resources["MyDynamicAccentBrush"] as SolidColorBrush
            };

            try
            {
                var result = await dialog.ShowAsync();

                if (neverShowAgain.IsChecked == true)
                {
                    LocalMachineSettingsEngine.IsFirstRun = false;
                }

                if (result == ContentDialogResult.Primary)
                {
                    Debug.WriteLine("[RestorePoint] Opening SystemPropertiesProtection");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "SystemPropertiesProtection.exe",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RestorePoint Error] {ex.Message}");
            }
        }

        public void AnimateOptimizationOverlay(bool show)
        {
            if (show)
            {
                GlobalOptimizationOverlay.Visibility = Visibility.Visible;

                OverlayHeartbeatStoryboard.Begin();
            }

            var visual = ElementCompositionPreview.GetElementVisual(GlobalOptimizationOverlay);
            var compositor = visual.Compositor;

            var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(1.0f, show ? 1.0f : 0.0f);
            fadeAnim.Duration = TimeSpan.FromMilliseconds(450);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            visual.StartAnimation("Opacity", fadeAnim);

            batch.Completed += (s, e) =>
            {
                if (!show)
                {
                    GlobalOptimizationOverlay.Visibility = Visibility.Collapsed;

                    OverlayHeartbeatStoryboard.Stop();
                }
            };
            batch.End();
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
                visual.Properties.InsertVector3("Translation", new Vector3(0, 250f, 0));
            }

            var easeOut = compositor.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0.3f), new Vector2(0.0f, 1.0f));
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

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
                    Task.Delay(50).ContinueWith(_ =>
                    {
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
                string cmdScript = $"/c timeout /t 2 & taskkill /f /im \"{exeName}\" 2>NUL & timeout /t 1 & move /y \"{tempPath}\" \"{currentExe}\" & start \"\" \"{currentExe}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdScript,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update Error] {ex.Message}");
                DownloadProgressArea.Visibility = Visibility.Collapsed;
                if (sender is Button btn) btn.IsEnabled = true;

                App.ShowNotification(
                    GetText("title_error") ?? "Update Failed",
                    GetText("msg_check_internet") ?? "Could not download the update. Please check your connection.",
                    InfoBarSeverity.Error,
                    5000);
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
        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                if (_isAdmin != value)
                {
                    _isAdmin = value;
                    OnPropertyChanged();
                }
            }
        }

        private async Task InitializeUserPermissionsAsync(string username)
        {
            try
            {
                string userType = "Guest";

                await Task.Run(() =>
                {
                    try
                    {
                        using (var conn = new SqlConnection(SqlConnectionHelper.connectReturn()))
                        {
                            string sql = "SELECT usertype FROM admin WHERE username = @user";
                            using (var cmd = new SqlCommand(sql, conn))
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
            UserSession.UserType = type;

            IsAdmin = string.Equals(UserSession.UserType, "Admin", StringComparison.OrdinalIgnoreCase);

            Debug.WriteLine($"[Permissions] Applied logic for type: {type}, IsAdmin: {IsAdmin}");
        }

        #endregion

        #region Events & Overrides
        private void Banner_PointerEntered(object sender, PointerRoutedEventArgs e) => NotificationManager.SetPaused(true);
        private void Banner_PointerExited(object sender, PointerRoutedEventArgs e) => NotificationManager.SetPaused(false);
        private void DismissNotification_Click(object sender, RoutedEventArgs e) => NotificationManager.HideBanner();

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}