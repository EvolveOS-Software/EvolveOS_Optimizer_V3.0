using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Shapes;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged, IPurgeable
    {
        #region Fields
        private bool _isInitialized;
        private string _pendingHexColor = "#FF0078D4";

        private PropertyChangedEventHandler? _localizationHandler;

        private DispatcherTimer? _sessionTimer;
        private DateTime _sessionExpiryTime;
        private bool _isPulsing = false;
        #endregion

        #region Events & Properties
        public event PropertyChangedEventHandler? PropertyChanged;
        public LocalizationService Localizer => LocalizationService.Instance;

        public bool IsUpdateCheckRequired
        {
            get => SettingsEngine.IsUpdateCheckRequired;
            set => SettingsEngine.IsUpdateCheckRequired = value;
        }

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
        #endregion

        #region Localization Helper
        public string GetText(string key) => Localizer[key];
        #endregion

        #region Constructor
        public SettingsPage()
        {
            InitializeComponent();

            _localizationHandler = (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100);

                        if (this.XamlRoot != null)
                        {
                            OnPropertyChanged(string.Empty);
                            UpdateComboBoxLocalization();
                        }
                    });
                }
            };

            LocalizationService.Instance.PropertyChanged += _localizationHandler;

            InitializeSelections();
            UpdateComboBoxLocalization();
            SetSelectedByTag(ThemeSelector, SettingsEngine.AppTheme);

            this.Loaded += SettingsPage_Loaded;
            this.Unloaded += SettingsPage_Unloaded;
        }
        #endregion

        #region Page Lifecycle
        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            TintOpacitySlider.Value = SettingsEngine.AcrylicOpacity;
            LuminositySlider.Value = SettingsEngine.AcrylicLuminosity;

            var savedColor = UIHelper.ToColor(SettingsEngine.AcrylicTintColor);
            AcrylicColorPicker.Color = savedColor;
            ColorPreview.Background = new SolidColorBrush(savedColor);

            _isInitialized = true;

            ApplyUIPermissions();

            string currentBackdrop = SettingsEngine.Backdrop;
            foreach (ComboBoxItem item in BackdropSelector.Items)
            {
                if (item.Tag?.ToString() == currentBackdrop)
                {
                    BackdropSelector.SelectedItem = item;
                    break;
                }
            }

            if (currentBackdrop == "AcrylicThin")
            {
                AcrylicOptionsPanel.Visibility = Visibility.Visible;
                AcrylicOptionsPanel.Opacity = 1.0;
                PanelTransform.Y = 0;
            }

            if (SliderSessionHours != null)
            {
                SliderSessionHours.Value = SettingsEngine.AutoLoginSessionHours;
            }

            if (AuthSessionManager.IsSessionValid(out string? sessionUser, out DateTime expiry))
            {
                _isInitialized = false;

                if (CbEnableAutoLogin != null) CbEnableAutoLogin.IsOn = true;
                if (AutoLoginSettings != null) AutoLoginSettings.Visibility = Visibility.Visible;

                StartLiveSessionTimer(expiry);

                _isInitialized = true;
            }

            if (BtnDeveloperMode != null)
            {
                BtnDeveloperMode.IsOn = LocalMachineSettingsEngine.IsDeveloperMode;
            }
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }
        #endregion

        #region Initialization Helpers
        private void ApplyUIPermissions()
        {
            IsAdmin = string.Equals(UserSession.UserType, "Admin", StringComparison.OrdinalIgnoreCase);

            SecurityPrivacy.Visibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            Debug.WriteLine($"[Settings] Permissions Applied. UserType: {UserSession.UserType}, IsAdmin: {IsAdmin}");
        }

        private void InitializeSelections()
        {
            SetSelectedByTag(LanguageSelector, SettingsEngine.Language);
            SetSelectedByTag(BackdropSelector, SettingsEngine.Backdrop);

            var savedColor = SettingsEngine.AccentColor;
            ColorPalette.SelectedItem = ColorPalette.Items
                .FirstOrDefault(i => i is Rectangle r && r.Tag?.ToString() == savedColor);

            try
            {
                var hex = savedColor.Replace("#", string.Empty);
                if (hex.Length == 6) hex = "FF" + hex;
                var a = (byte)uint.Parse(hex[..2], NumberStyles.HexNumber);
                var r = (byte)uint.Parse(hex[2..4], NumberStyles.HexNumber);
                var g = (byte)uint.Parse(hex[4..6], NumberStyles.HexNumber);
                var b = (byte)uint.Parse(hex[6..8], NumberStyles.HexNumber);

                AdvancedColorPicker.Color = Microsoft.UI.ColorHelper.FromArgb(a, r, g, b);
            }
            catch
            {
                AdvancedColorPicker.Color = Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212);
            }
        }

        private void UpdateComboBoxLocalization()
        {
            foreach (var item in BackdropSelector.Items.Cast<ComboBoxItem>())
            {
                var tag = item.Tag?.ToString() ?? "";
                item.Content = Localizer[$"Settings_Backdrop_{tag}"];
            }
            foreach (var item in ThemeSelector.Items.Cast<ComboBoxItem>())
            {
                var tag = item.Tag?.ToString() ?? "";
                item.Content = Localizer[$"Settings_Theme_{tag}"];
            }
        }

        private void SetSelectedByTag(ComboBox comboBox, string tag) =>
            comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == tag);
        #endregion

        #region Event Handlers - Selection Changes
        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && LanguageSelector.SelectedItem is ComboBoxItem item)
            {
                SettingsEngine.Language = item.Tag?.ToString() ?? "en-us";

                if (App.Current.MainWindow is EvolveOS_Optimizer.MainWindow mainWindow)
                {
                    mainWindow.RefreshTrayIconLanguage();
                }
            }
        }

        private void BackdropSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || BackdropSelector.SelectedItem is not ComboBoxItem item) return;

            string selected = item.Tag?.ToString() ?? "None";
            SettingsEngine.Backdrop = selected;

            bool showOptions = (selected == "AcrylicThin");

            if (showOptions)
            {
                AcrylicOptionsPanel.Visibility = Visibility.Visible;
                ShowPanelAnimation.Begin();
            }
            else
            {
                AcrylicOptionsPanel.Visibility = Visibility.Collapsed;
                AcrylicOptionsPanel.Opacity = 0;
                PanelTransform.Y = -20;
            }

            if (App.Current.MainWindow is Window mainWindow)
            {
                UIHelper.ApplyBackdrop(mainWindow, selected);
            }
        }
        #endregion

        #region Event Handlers - Sliders & Colors
        private void AcrylicSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is Slider slider)
            {
                if (slider == TintOpacitySlider)
                    SettingsEngine.AcrylicOpacity = e.NewValue;
                else if (slider == LuminositySlider)
                    SettingsEngine.AcrylicLuminosity = e.NewValue;

                if (App.Current.MainWindow is Window window)
                {
                    UIHelper.ApplyBackdrop(window, "AcrylicThin");
                }
            }
        }

        private void AcrylicColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (!_isInitialized) return;

            string hex = args.NewColor.ToString();
            SettingsEngine.AcrylicTintColor = hex;

            ColorPreview.Background = new SolidColorBrush(args.NewColor);

            if (App.Current.MainWindow is Window mainWindow)
            {
                UIHelper.ApplyBackdrop(mainWindow, SettingsEngine.Backdrop);
            }
        }

        private void ResetAcrylicColor_Click(object sender, RoutedEventArgs e)
        {
            AcrylicColorPicker.Color = Microsoft.UI.Colors.Black;
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && ThemeSelector.SelectedItem is ComboBoxItem item)
            {
                SettingsEngine.AppTheme = item.Tag?.ToString() ?? "Default";
            }
        }

        private void ColorPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && ColorPalette.SelectedItem is Rectangle rect && rect.Tag != null)
            {
                string newColor = rect.Tag.ToString()!;
                SettingsEngine.AccentColor = newColor;

                ((App)Application.Current).UpdateGlobalAccentColor(newColor);
            }
        }

        private void AdvancedColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args) =>
            _pendingHexColor = $"#{args.NewColor.A:X2}{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}";

        private void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPalette.SelectedItem = null;
            SettingsEngine.AccentColor = _pendingHexColor;

            ((App)Application.Current).UpdateGlobalAccentColor(_pendingHexColor);
        }
        #endregion

        #region Event Handlers - Updates & Toggles
        private async void ManualUpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            btn.IsEnabled = false;
            btn.Content = ResourceString.GetString("Settings_Update_Checking");

            bool updateFound = SystemDiagnostics.IsNeedUpdate;

            if (updateFound)
            {
                if (App.Current.MainWindow is EvolveOS_Optimizer.MainWindow mainWin)
                {
                    mainWin.AnimateUpdateBanner(true);
                }
            }
            else
            {
                btn.Content = ResourceString.GetString("Settings_Update_UpToDate");
                await Task.Delay(2000);
            }

            btn.IsEnabled = true;
            btn.Content = ResourceString.GetString("Settings_Update_CheckButton");
        }

        private void BtnStartMinimized_ChangedState(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.ToggleSwitch toggleSwitch)
            {
                SettingsEngine.IsStartMinimized = toggleSwitch.IsOn;
            }
        }

        private void BtnRunOnStartUp_ChangedState(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.ToggleSwitch toggleSwitch)
            {
                SettingsEngine.IsRunOnStartUp = toggleSwitch.IsOn;
            }
        }

        private void BtnDeveloperMode_ChangedState(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is ToggleSwitch ts)
            {
                LocalMachineSettingsEngine.IsDeveloperMode = ts.IsOn;
            }
        }
        #endregion

        #region Bound Properties
        public bool IsRunOnStartUp
        {
            get => SettingsEngine.IsRunOnStartUp;
            set
            {
                if (SettingsEngine.IsRunOnStartUp != value)
                {
                    SettingsEngine.IsRunOnStartUp = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCloseToTray
        {
            get => SettingsEngine.IsCloseToTrayEnabled;
            set
            {
                if (SettingsEngine.IsCloseToTrayEnabled != value)
                {
                    SettingsEngine.IsCloseToTrayEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsStartMinimized
        {
            get => SettingsEngine.IsStartMinimized;
            set
            {
                if (SettingsEngine.IsStartMinimized != value)
                {
                    SettingsEngine.IsStartMinimized = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion

        #region Auto-Login Session Logic
        private async void BtnAutoLogin_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            if (this.XamlRoot == null) return;

            if (CbEnableAutoLogin != null && !CbEnableAutoLogin.IsOn)
            {
                if (AuthSessionManager.IsSessionValid(out _, out _))
                {
                    var confirmDialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("title_end_session") ?? "End Session?",
                        Content = ResourceString.GetString("msg_end_session_confirm") ?? "An Auto-Login session is currently active. Are you sure you want to end it and remove your saved credentials?",
                        PrimaryButtonText = ResourceString.GetString("btn_yes_end_session") ?? "Yes, End Session",
                        CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await confirmDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        _sessionTimer?.Stop();
                        AuthSessionManager.ClearSession();

                        if (AutoLoginSettings != null && LblSessionExpiry != null)
                        {
                            LblSessionExpiry.Text = ResourceString.GetString("lbl_session_expire_default") ?? "Session will expire at: --:--";
                            AutoLoginSettings.Visibility = Visibility.Collapsed;
                        }

                        NativeToastHelper.SendNativeToast(
                            ResourceString.GetString("toast_title_autologin") ?? "Auto-Login",
                            ResourceString.GetString("toast_msg_session_ended") ?? "Session ended and credentials removed."
                        );
                    }
                    else
                    {
                        _isInitialized = false;
                        CbEnableAutoLogin.IsOn = true;
                        _isInitialized = true;
                    }
                }
                else
                {
                    if (AutoLoginSettings != null) AutoLoginSettings.Visibility = Visibility.Collapsed;
                }

                return;
            }

            var passwordBox = new PasswordBox
            {
                PlaceholderText = ResourceString.GetString("txt_enter_password") ?? "Enter your password...",
                Width = 300,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var errorTextBlock = new TextBlock
            {
                Text = ResourceString.GetString("msg_invalid_password") ?? "Invalid password. Please try again.",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 12
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = ResourceString.GetString("msg_verify_identity") ?? "Please verify your identity to enable Auto-Login.", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(passwordBox);
            panel.Children.Add(errorTextBlock);

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("title_authorize_autologin") ?? "Authorize Auto-Login",
                Content = panel,
                PrimaryButtonText = ResourceString.GetString("btn_verify") ?? "Verify",
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                args.Cancel = true;

                string plainPassword = passwordBox.Password;
                passwordBox.IsEnabled = false;

                try
                {
                    await Task.Run(() =>
                    {
                        var psi = new ProcessStartInfo("sqllocaldb", "start MSSQLLocalDB")
                        {
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(psi)?.WaitForExit();
                    });

                    var userDataAccess = new UserDataAccess(SqlConnectionHelper.connectReturn());
                    var loginData = await userDataAccess.GetPasswordAndImageAsync(UserSession.Username!);

                    bool isVerified = loginData.PasswordHash != null && BCrypt.Net.BCrypt.Verify(plainPassword, loginData.PasswordHash);

                    passwordBox.IsEnabled = true;

                    if (isVerified)
                    {
                        using (SecureString machineKey = TokenManager.GetMachineKey())
                        {
                            string encryptedToken = AesHelper.Encrypt(plainPassword, machineKey);
                            TokenManager.SaveToken(UserSession.Username!, encryptedToken);
                        }

                        int hours = SettingsEngine.AutoLoginSessionHours;
                        AuthSessionManager.CreateAutoLoginSession(UserSession.Username!, hours);

                        UpdateExpiryLabel();
                        if (AutoLoginSettings != null) AutoLoginSettings.Visibility = Visibility.Visible;

                        NativeToastHelper.SendNativeToast(
                            ResourceString.GetString("toast_success") ?? "Success",
                            ResourceString.GetString("toast_autologin_authorized") ?? "Auto-Login Authorized!"
                        );

                        dialog.Hide();
                    }
                    else
                    {
                        FactoryAnimation.AnimateErrorShake(passwordBox);

                        errorTextBlock.Visibility = Visibility.Visible;
                        passwordBox.Password = string.Empty;
                        passwordBox.Focus(FocusState.Programmatic);
                    }
                }
                catch (Exception ex)
                {
                    passwordBox.IsEnabled = true;
                    NativeToastHelper.SendNativeToast(
                        ResourceString.GetString("toast_token_error") ?? "Database Error",
                        ex.Message
                    );
                    dialog.Hide();
                }
            };

            dialog.Closed += (s, args) =>
            {
                if (!AuthSessionManager.IsSessionValid(out _, out _))
                {
                    _isInitialized = false;
                    if (CbEnableAutoLogin != null) CbEnableAutoLogin.IsOn = false;
                    _isInitialized = true;
                }
            };

            await dialog.ShowAsync();
        }

        private void BtnAutoLogin_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            //StopCriticalAnimation();
            _sessionTimer?.Stop();

            AuthSessionManager.ClearSession();
            if (AutoLoginSettings != null && LblSessionExpiry != null)
            {
                LblSessionExpiry.Text = "Session will expire at: --:--";
                AutoLoginSettings.Visibility = Visibility.Collapsed;
            }
        }

        private void SliderSessionHours_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }
            SettingsEngine.AutoLoginSessionHours = (int)e.NewValue;
            UpdateExpiryLabel();
        }

        private void UpdateExpiryLabel()
        {
            if (LblSessionExpiry == null || SliderSessionHours == null)
            {
                return;
            }

            DateTime expiryTime = DateTime.Now.AddHours((int)SliderSessionHours.Value);
            string timeString = expiryTime.ToString("hh:mm tt");
            LblSessionExpiry.Text = $"Session will expire at: {timeString}";
        }
        #endregion

        #region Session Timer Management
        private void StartLiveSessionTimer(DateTime expiry)
        {
            _sessionExpiryTime = expiry;

            if (_sessionTimer == null)
            {
                _sessionTimer = new DispatcherTimer();
                _sessionTimer.Interval = TimeSpan.FromSeconds(1);
                _sessionTimer.Tick += (s, e) => UpdateLiveDisplay();
            }

            _sessionTimer.Start();
        }

        private async void UpdateLiveDisplay()
        {
            TimeSpan remaining = _sessionExpiryTime - DateTime.Now;
            double totalSeconds = remaining.TotalSeconds;

            if (totalSeconds <= 0)
            {
                _sessionTimer?.Stop();
                //StopCriticalAnimation();

                if (LblSessionExpiry != null)
                {
                    LblSessionExpiry.Text = ResourceString.GetString("lbl_session_expired") ?? "Session Expired";
                    LblSessionExpiry.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }

                if (CbEnableAutoLogin != null) CbEnableAutoLogin.IsOn = false;

                if (LocalMachineSettingsEngine.IsDeveloperMode)
                {
                    string devMsg = ResourceString.GetString("msgbox_dev_mode_logout_prompt") ?? "Developer Mode is still active. Would you like to disable it before logging out?";
                    string devTitle = ResourceString.GetString("msgbox_dev_mode_active_title") ?? "Developer Mode Active";

                    var devResult = Win32Helper.MessageBox(IntPtr.Zero, devMsg, devTitle, Win32Helper.MB_YESNO);

                    if (devResult == Win32Helper.IDYES)
                    {
                        LocalMachineSettingsEngine.IsDeveloperMode = false;
                    }
                }

                UserSession.Clear();
                TokenManager.DeleteToken();

                string msg = ResourceString.GetString("msg_session_expired_login") ?? "Your session has expired. Please log in again.";

                DispatcherQueue.TryEnqueue(() =>
                {
                    var loginWin = new EvolveOS_Optimizer.Views.UserLoginWindow(new WeatherService(), msg);
                    if (App.Current.MainWindow != null)
                    {
                        App.Current.MainWindow.Close();
                    }
                    App.Current.MainWindow = loginWin;
                    loginWin.Activate();
                });

                return;
            }

            if (totalSeconds <= 350 && !_isPulsing)
            {
                //StartCriticalAnimation();
            }
            else if (totalSeconds > 350 && _isPulsing)
            {
                //StopCriticalAnimation();
            }

            if (LblSessionExpiry != null)
            {
                string prefix = ResourceString.GetString("lbl_session_expires_in") ?? "Expires in: ";
                LblSessionExpiry.Text = $"{prefix}{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            }
        }

        /*private void StartCriticalAnimation()
        {
            _isPulsing = true;
            if (this.Resources.TryGetValue("PulseCriticalTimer", out object? resource) && resource is Storyboard sb)
            {
                if (LblSessionExpiry != null) LblSessionExpiry.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                sb.Begin();
            }
        }

        private void StopCriticalAnimation()
        {
            _isPulsing = false;
            if (this.Resources.TryGetValue("PulseCriticalTimer", out object? resource) && resource is Storyboard sb)
            {
                sb.Stop();
            }

            if (LblSessionExpiry != null && App.Current.Resources.TryGetValue("Brush_Highlighted_Inverted", out object? brushObj) && brushObj is Brush brush)
            {
                LblSessionExpiry.Foreground = brush;
            }
        }*/
        #endregion

        #region Purge Page
        public void Purge()
        {
            Debug.WriteLine("[SettingsPage] Purge initiated...");

            if (_sessionTimer != null)
            {
                _sessionTimer.Stop();
                _sessionTimer = null;
            }

            if (_localizationHandler != null)
            {
                LocalizationService.Instance.PropertyChanged -= _localizationHandler;
                _localizationHandler = null;
            }

            this.Loaded -= SettingsPage_Loaded;
            this.Unloaded -= SettingsPage_Unloaded;

            PropertyChanged = null;

            this.DataContext = null;
            this.Content = null;

            _isInitialized = false;

            Debug.WriteLine("[SettingsPage] Purge complete.");
        }
        #endregion
    }
}