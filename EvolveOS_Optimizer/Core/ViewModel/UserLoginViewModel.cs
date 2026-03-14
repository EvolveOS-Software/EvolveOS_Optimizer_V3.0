// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class UserLoginViewModel : ObservableObject
    {
        #region Fields & Constants
        private readonly WeatherService _weatherService;
        private readonly UserDataAccess _userDataAccess;
        private readonly DispatcherTimer _loginTimer;
        private readonly Action _closeWindowAction;

        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _passwordVisible = string.Empty;
        private bool _isPasswordShown;
        private bool _isSignInEnabled;
        private int _loginAttempts = 1;
        private int _intervalCount = 0;
        private double _progressValue = 100.0;

        private const int MaxCount = 60;

        private static string GetPhysicalPath()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        }

        private static readonly string BaseDir = GetPhysicalPath();
        private readonly string fullMdfPath = Path.Combine(BaseDir, "EvolveOS_OptimizerDb.mdf");

        private static string GetConnectionString()
        {
            string dbPath = Path.Combine(GetPhysicalPath(), "EvolveOS_OptimizerDb.mdf");
            return $"Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename={dbPath};Initial Catalog=EvolveOS_OptimizerDb_Main;Integrated Security=True;Connect Timeout=30;MultipleActiveResultSets=True;";
        }
        #endregion

        #region Events & Commands
        public event Action? SwitchToCreateAccountRequested;

        public ICommand SignInCommand { get; private set; }
        public ICommand? ToggleRunOnStartupCommand { get; private set; }
        public ICommand OpenSignUpCommand { get; private set; }
        #endregion

        #region Properties
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    UserSession.Username = value;
                    UpdateSignInButtonState();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    PasswordVisible = value;
                    UpdateSignInButtonState();
                }
            }
        }

        public string PasswordVisible
        {
            get => _passwordVisible;
            set => SetProperty(ref _passwordVisible, value);
        }

        public bool IsPasswordShown
        {
            get => _isPasswordShown;
            set => SetProperty(ref _isPasswordShown, value);
        }

        public bool IsSignInEnabled
        {
            get => _isSignInEnabled;
            set => SetProperty(ref _isSignInEnabled, value);
        }

        public bool IsRunOnStartUp
        {
            get => SettingsEngine.IsRunOnStartUp;
            set
            {
                if (SettingsEngine.IsRunOnStartUp != value)
                {
                    SettingsEngine.IsRunOnStartUp = value;
                    OnPropertyChanged(nameof(IsRunOnStartUp));
                }
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }
        #endregion

        #region Constructors
        public UserLoginViewModel() : this(null!, () => { })
        {
        }

        public UserLoginViewModel(WeatherService weatherService, Action closeWindowAction)
        {
            _weatherService = weatherService;
            _closeWindowAction = closeWindowAction;

            _userDataAccess = new UserDataAccess(GetConnectionString());

            SignInCommand = new RelayCommand(async _ => await ExecuteSignInAsync(), CanExecuteSignIn);
            OpenSignUpCommand = new RelayCommand(_ => ExecuteOpenSignUp());

            _loginTimer = new DispatcherTimer();
            _loginTimer.Interval = TimeSpan.FromSeconds(1);
            _loginTimer.Tick += Timer_Tick;

            Task.Run(async () =>
            {
                try
                {
                    using var weatherCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    string savedLocation = SettingsEngine.LastLocation;
                    if (string.IsNullOrEmpty(savedLocation)) savedLocation = "Paris";

                    var data = await _weatherService.GetWeatherAsync(savedLocation, weatherCts.Token);
                    if (data != null)
                    {
                        GlobalAppData.PreloadedWeather = data;
                        Debug.WriteLine("[Weather] Preloaded successfully during Login screen.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Weather] Failed to preload during Login: {ex.Message}");
                }
            });
        }
        #endregion

        #region Timer Management
        public void StopTimer()
        {
            _loginTimer.Stop();
        }

        public void RestartTimer()
        {
            if (!_loginTimer.IsEnabled)
            {
                _intervalCount = 0;
                ProgressValue = 100.0;
                _loginTimer.Start();
            }
        }
        #endregion

        #region Command Execution & Authentication Logic
        private void UpdateSignInButtonState()
        {
            IsSignInEnabled = !string.IsNullOrEmpty(Username) && Username.Length >= 2 &&
                              !string.IsNullOrEmpty(Password) && Password.Length >= 7;

            if (SignInCommand is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }
        }

        private bool CanExecuteSignIn(object? parameter)
        {
            return _isSignInEnabled;
        }

        private async Task ExecuteSignInAsync()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                string blankFieldsMsg = ResourceString.GetString("notif_blank_fields") ?? "Username and password cannot be empty.";
                NativeToastHelper.SendNativeToast("Warning", blankFieldsMsg);
                return;
            }

            if (_loginAttempts >= 4)
            {
                string maxAttemptsMsg = ResourceString.GetString("notif_max_login_attempts") ?? "Maximum login attempts reached. Closing application.";
                NativeToastHelper.SendNativeToast("Security Alert", maxAttemptsMsg);
                CloseApplication();
                return;
            }

            try
            {
                var loginData = await _userDataAccess.GetPasswordAndImageAsync(Username);

                if (loginData.PasswordHash != null && BCrypt.Net.BCrypt.Verify(Password, loginData.PasswordHash))
                {
                    UserSession.Username = Username;
                    UserSession.IsAuthenticated = true;
                    UserSession.UserType = loginData.UserType;

                    if (loginData.ProfileImageBytes != null && loginData.ProfileImageBytes.Length > 0)
                    {
                        UserSession.ProfileImage = await ImageHelper.LoadFromBytesAsync(loginData.ProfileImageBytes);
                    }

                    _loginTimer.Stop();

                    var mainDash = new global::EvolveOS_Optimizer.MainWindow();

                    mainDash.Closed += (s, e) => { Application.Current.Exit(); };

                    if (Application.Current is App)
                    {
                        App.MainWindow = mainDash;
                    }

                    bool shouldStartHidden = Environment.GetCommandLineArgs().Any(arg => arg.Equals("-hidden", StringComparison.OrdinalIgnoreCase));

                    if (shouldStartHidden)
                    {
                        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(mainDash);
                        var appWin = mainDash.AppWindow;

                        appWin.Hide();

                        _closeWindowAction();
                    }
                    else
                    {
                        UIHelper.ApplyBackdrop(mainDash, SettingsEngine.Backdrop);

                        mainDash.Activate();

                        App.UIThreadDispatcher?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                        {
                            mainDash.ForceToForeground();
                            _closeWindowAction();
                        });
                    }

                    return;
                }

                string wrongCredentialsMsg = ResourceString.GetString("notif_wrong_username_password") ?? "Invalid username or password.";
                NativeToastHelper.SendNativeToast("Warning", wrongCredentialsMsg);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                string connectionErrorMsg = ResourceString.GetString("notif_connection_error") ?? "Connection error occurred.";
                NativeToastHelper.SendNativeToast($"Error: {ex.Message}", connectionErrorMsg);
            }

            _loginAttempts++;
        }

        private void ExecuteOpenSignUp()
        {
            _loginTimer.Stop();
            SwitchToCreateAccountRequested?.Invoke();
        }
        #endregion

        #region Database Validation & Dialogs
        public async Task InitialDatabaseCheckAsync(Microsoft.UI.Xaml.XamlRoot xamlRoot)
        {
            if (!File.Exists(fullMdfPath))
            {
                await ShowWelcomeMessageAsync(xamlRoot);
                return;
            }

            int maxRetries = 3;
            int delayMilliseconds = 1000;
            bool assumeEmptyDb = false;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (_userDataAccess.IsDatabaseEmpty())
                    {
                        assumeEmptyDb = true;
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Database check successful: User data found.");
                        _loginTimer.Start();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database connection attempt {i + 1} failed: {ex.Message}");

                    if (i == maxRetries - 1)
                    {
                        Console.WriteLine("Final attempt failed. Assuming fresh/empty database.");
                        assumeEmptyDb = true;
                    }
                    else
                    {
                        await Task.Delay(delayMilliseconds);
                    }
                }
            }

            if (assumeEmptyDb)
            {
                await ShowWelcomeMessageAsync(xamlRoot);
            }
        }

        private async Task ShowWelcomeMessageAsync(Microsoft.UI.Xaml.XamlRoot xamlRoot)
        {
            if (xamlRoot == null)
            {
                Debug.WriteLine("[Welcome Dialog] XamlRoot is null. Forcing Create Account View.");
                SwitchToCreateAccountRequested?.Invoke();
                return;
            }

            try
            {
                await Task.Delay(100);

                var textWelcomeMessage = ResourceString.GetString("msgbox_welcome_first_user") ?? "Welcome to EvolveOS Optimizer! Let's get your secure profile set up.";
                var btnGetStarted = ResourceString.GetString("msgbox_btn_get_started") ?? "Get Started";
                var btnCancel = ResourceString.GetString("msgbox_btn_cancel") ?? "Cancel";

                var dialog = new ContentDialog
                {
                    Title = "Welcome!",
                    Content = textWelcomeMessage,
                    PrimaryButtonText = btnGetStarted,
                    CloseButtonText = btnCancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    SwitchToCreateAccountRequested?.Invoke();
                }
                else
                {
                    CloseApplication();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Welcome Dialog Error]: {ex.Message}");
                SwitchToCreateAccountRequested?.Invoke();
            }
        }
        #endregion

        private void Timer_Tick(object? sender, object e)
        {
            _intervalCount++;

            ProgressValue = 100.0 - ((double)_intervalCount / MaxCount * 100.0);

            if (_intervalCount >= MaxCount)
            {
                string timeExpiredMsg = ResourceString.GetString("notif_login_time_expired") ?? "Login time expired.";

                NativeToastHelper.SendNativeToast("Warning", timeExpiredMsg);
                _loginTimer.Stop();
                CloseApplication();
            }
        }

        #region Utility Methods
        private void CloseApplication()
        {
            Application.Current.Exit();
        }
        #endregion
    }
}