// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class UserCreateViewModel : ObservableObject
    {
        private static string GetConnectionString()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            string baseDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "EvolveOS_OptimizerDb.mdf");

            return $"Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename={dbPath};Initial Catalog=EvolveOS_OptimizerDb_Main;Integrated Security=True;Connect Timeout=30;MultipleActiveResultSets=True;";
        }

        private readonly string _connectionString = GetConnectionString();
        private readonly DispatcherTimer _timer;

        private readonly PasswordValidator _passwordValidator = new PasswordValidator();

        public PasswordViewModel PasswordValidation { get; }

        private byte[]? _defaultImageData;

        #region Properties
        private string _emailText = string.Empty;
        public string EmailText
        {
            get => _emailText;
            set
            {
                if (SetProperty(ref _emailText, value))
                {
                    ValidateEmail();
                    UpdateUsernamePasswordEnablement();
                    RefreshCommand();
                }
            }
        }

        private string _userNameText = string.Empty;
        public string UserNameText
        {
            get => _userNameText;
            set
            {
                if (SetProperty(ref _userNameText, value))
                {
                    UpdateUsernamePasswordEnablement();
                    RefreshCommand();
                }
            }
        }

        private string _passwordText = string.Empty;
        public string PasswordText
        {
            get => _passwordText;
            set
            {
                if (SetProperty(ref _passwordText, value))
                {
                    PasswordValidation.Password = value;

                    var rules = _passwordValidator.Validate(value);
                    _metRulesCount = rules.Count(r => r.IsMet);

                    UpdateConfirmPasswordEnablement();
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                    RefreshCommand();
                }
            }
        }

        private string _confirmPasswordText = string.Empty;
        public string ConfirmPasswordText
        {
            get => _confirmPasswordText;
            set
            {
                if (SetProperty(ref _confirmPasswordText, value))
                {
                    _confirmDebounceTimer.Stop();
                    _confirmDebounceTimer.Start();
                    RefreshCommand();
                }
            }
        }

        private int _metRulesCount = 0;

        public double StrengthPiece1Opacity => _metRulesCount >= 1 ? 1.0 : 0.2;
        public double StrengthPiece2Opacity => _metRulesCount >= 2 ? 1.0 : 0.2;
        public double StrengthPiece3Opacity => _metRulesCount >= 3 ? 1.0 : 0.2;
        public double StrengthPiece4Opacity => _metRulesCount >= 4 ? 1.0 : 0.2;
        public double StrengthPiece5Opacity => _metRulesCount >= 5 ? 1.0 : 0.2;
        public double StrengthPiece6Opacity => _metRulesCount >= 6 ? 1.0 : 0.2;

        public string MetRulesText => $"{_metRulesCount}/6";

        public bool IsPasswordMatch => !string.IsNullOrEmpty(PasswordText) && PasswordText == ConfirmPasswordText;
        public double MatchPieceOpacity => IsPasswordMatch && _metRulesCount == 6 ? 1.0 : 0.2;

        public string MatchText => IsPasswordMatch && _metRulesCount == 6 ? "Match" : "";
        public Visibility MatchTextVisibility => IsPasswordMatch && _metRulesCount == 6 ? Visibility.Visible : Visibility.Collapsed;

        private string _textTime = string.Empty;
        public string TextTime
        {
            get => _textTime;
            private set => SetProperty(ref _textTime, value);
        }

        private string _assignedUserType = "Guest";
        public string AssignedUserType
        {
            get => _assignedUserType;
            private set => SetProperty(ref _assignedUserType, value);
        }

        private ImageSource? _profileImageSource;
        public ImageSource? ProfileImageSource
        {
            get => _profileImageSource;
            private set => SetProperty(ref _profileImageSource, value);
        }

        private string? _profileImagePath;
        public string? ProfileImagePath
        {
            get => _profileImagePath;
            private set => SetProperty(ref _profileImagePath, value);
        }

        private string _emailValidationContent = string.Empty;
        public string EmailValidationContent
        {
            get => _emailValidationContent;
            private set => SetProperty(ref _emailValidationContent, value);
        }

        private Brush _emailValidationForeground = new SolidColorBrush(Colors.Red);
        public Brush EmailValidationForeground
        {
            get => _emailValidationForeground;
            private set => SetProperty(ref _emailValidationForeground, value);
        }

        private Visibility _emailValidationVisibility = Visibility.Collapsed;
        public Visibility EmailValidationVisibility
        {
            get => _emailValidationVisibility;
            private set => SetProperty(ref _emailValidationVisibility, value);
        }

        private bool _isUsernameEnabled = false;
        public bool IsUsernameEnabled
        {
            get => _isUsernameEnabled;
            private set => SetProperty(ref _isUsernameEnabled, value);
        }

        private bool _isPasswordEnabled = false;
        public bool IsPasswordEnabled
        {
            get => _isPasswordEnabled;
            private set => SetProperty(ref _isPasswordEnabled, value);
        }

        private bool _isConfirmPasswordEnabled = false;
        public bool IsConfirmPasswordEnabled
        {
            get => _isConfirmPasswordEnabled;
            private set => SetProperty(ref _isConfirmPasswordEnabled, value);
        }

        private bool _isPasswordValidationPopupOpen = false;
        public bool IsPasswordValidationPopupOpen
        {
            get => _isPasswordValidationPopupOpen;
            set => SetProperty(ref _isPasswordValidationPopupOpen, value);
        }

        private bool _isCreateAccountButtonEnabled = false;
        public bool IsCreateAccountButtonEnabled
        {
            get => _isCreateAccountButtonEnabled;
            private set => SetProperty(ref _isCreateAccountButtonEnabled, value);
        }

        private Visibility _alreadyAccountVisibility = Visibility.Visible;
        public Visibility AlreadyAccountVisibility
        {
            get => _alreadyAccountVisibility;
            private set => SetProperty(ref _alreadyAccountVisibility, value);
        }
        #endregion

        #region Commands & Events
        public ICommand CreateAccountCommand { get; }
        public ICommand LoginHereCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand SpaceBarPreventCommand { get; }

        private readonly DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _confirmDebounceTimer;
        public Action? CloseAction { get; set; }

        public event Action? SwitchToLoginRequested;

        #endregion

        public UserCreateViewModel()
        {
            PasswordValidation = new PasswordViewModel();
            PasswordValidation.PropertyChanged += PasswordValidation_PropertyChanged;

            CreateAccountCommand = new RelayCommand(OnCreateAccount, CanCreateAccount);
            LoginHereCommand = new RelayCommand(OnLoginHere);
            BrowseImageCommand = new RelayCommand(async _ => await OnBrowseImage());
            SpaceBarPreventCommand = new RelayCommand(OnSpaceBarPrevent);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            UpdateTime();

            _ = LoadDefaultDataAsync();

            CheckAssignedUserType();

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounceTimer.Tick += DebounceTimer_Tick;

            _confirmDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _confirmDebounceTimer.Tick += ConfirmDebounceTimer_Tick;

            UpdatePasswordValidationUI();
        }

        #region Helper Methods

        private void RefreshCommand()
        {
            if (CreateAccountCommand is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }

            OnPropertyChanged(nameof(StrengthPiece1Opacity));
            OnPropertyChanged(nameof(StrengthPiece2Opacity));
            OnPropertyChanged(nameof(StrengthPiece3Opacity));
            OnPropertyChanged(nameof(StrengthPiece4Opacity));
            OnPropertyChanged(nameof(StrengthPiece5Opacity));
            OnPropertyChanged(nameof(StrengthPiece6Opacity));
            OnPropertyChanged(nameof(MetRulesText));
            OnPropertyChanged(nameof(MatchPieceOpacity));
            OnPropertyChanged(nameof(MatchText));
            OnPropertyChanged(nameof(MatchTextVisibility));
        }

        private void PasswordValidation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PasswordValidation.IsPasswordStrong))
            {
                UpdateConfirmPasswordEnablement();

                if (!PasswordValidation.IsPasswordStrong)
                {
                    DebounceTimer_Tick(null, null!);
                }
                RefreshCommand();
            }
        }

        private void CheckAssignedUserType()
        {
            string cmdText = @"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'admin')
                    SELECT COUNT(*) FROM admin
                ELSE
                    SELECT 0";

            try
            {
                using (SqlConnection connect = new SqlConnection(_connectionString))
                {
                    if (connect.State != ConnectionState.Open) connect.Open();

                    using (SqlCommand cmd = new SqlCommand(cmdText, connect))
                    {
                        int count = (int)cmd.ExecuteScalar();

                        if (count > 0)
                        {
                            AssignedUserType = "Guest";
                            AlreadyAccountVisibility = Visibility.Visible;
                        }
                        else
                        {
                            AssignedUserType = "Admin";
                            AlreadyAccountVisibility = Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Database check error in CreateUser: " + ex.Message);
                AssignedUserType = "Admin";
                AlreadyAccountVisibility = Visibility.Collapsed;
            }
        }

        private void Timer_Tick(object? sender, object e) => UpdateTime();

        private void UpdateTime()
        {
            DateTime time = DateTime.Now;
            string format = "MM-dd-yyyy HH:mm:ss";
            TextTime = time.ToString(format);
        }

        private void ValidateEmail()
        {
            Regex regex = new Regex("^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$");

            if (regex.IsMatch(EmailText))
            {
                IsUsernameEnabled = true;
                EmailValidationForeground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 52, 205, 105));
                EmailValidationContent = "Valid";
                EmailValidationVisibility = Visibility.Visible;
            }
            else
            {
                IsUsernameEnabled = false;
                EmailValidationForeground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                EmailValidationContent = "Invalid";
                EmailValidationVisibility = EmailText.Length < 1 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateUsernamePasswordEnablement()
        {
            IsPasswordEnabled = IsUsernameEnabled && UserNameText.Length >= 3;
        }

        private void UpdateConfirmPasswordEnablement()
        {
            IsConfirmPasswordEnabled = _metRulesCount == 6;
        }

        private void DebounceTimer_Tick(object? sender, object e)
        {
            _debounceTimer.Stop();
            UpdatePasswordValidationUI();
            RefreshCommand();
        }

        private void ConfirmDebounceTimer_Tick(object? sender, object e)
        {
            _confirmDebounceTimer.Stop();
            RefreshCommand();
        }

        private async Task LoadDefaultDataAsync()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string defaultImagePath = Path.Combine(baseDir, "Resources", "EvolveOSLogo.png");

                if (File.Exists(defaultImagePath))
                {
                    _defaultImageData = await File.ReadAllBytesAsync(defaultImagePath);
                    var loadedImage = await ImageHelper.LoadFromBytesAsync(_defaultImageData);

                    App.UIThreadDispatcher?.TryEnqueue(() =>
                    {
                        ProfileImageSource = loadedImage;
                    });
                }
                else
                {
                    Debug.WriteLine($"[Warning] Default image not found at: {defaultImagePath}");
                    _defaultImageData = null;

                    App.UIThreadDispatcher?.TryEnqueue(() =>
                    {
                        ProfileImageSource = new BitmapImage(new Uri("ms-appx:///Resources/EvolveOSLogo.png"));
                    });
                }

                ProfileImagePath = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error loading default image resource: " + ex.Message);
                _defaultImageData = null;
            }
        }

        #endregion

        #region Native Win32 File Dialog

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class OpenFileName
        {
            public int structSize = 0;
            public IntPtr hwnd = IntPtr.Zero;
            public IntPtr hinst = IntPtr.Zero;
            public string? filter = null;
            public string? custFilter = null;
            public int custFilterMax = 0;
            public int filterIndex = 0;
            public string? file = null;
            public int maxFile = 0;
            public string? fileTitle = null;
            public int maxFileTitle = 0;
            public string? initialDir = null;
            public string? title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string? defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string? templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        #endregion

        #region Command Logic

        private void OnLoginHere(object? parameter)
        {
            SwitchToLoginRequested?.Invoke();
        }

        private void OnCreateAccount(object? parameter)
        {
            Debug.WriteLine("Register Button Triggered.");

            if (PasswordText != ConfirmPasswordText)
            {
                NativeToastHelper.SendNativeToast("Warning", ResourceString.GetString("notif_pass_not_match") ?? "Passwords do not match");
                return;
            }
            if (string.IsNullOrEmpty(EmailText) || string.IsNullOrEmpty(UserNameText) || string.IsNullOrEmpty(PasswordText))
            {
                NativeToastHelper.SendNativeToast("Warning", ResourceString.GetString("notif_blank_fields") ?? "Fields cannot be blank");
                return;
            }

            PerformCreateAccount();
        }

        private bool CanCreateAccount(object? parameter)
        {
            bool passwordsMatch = !string.IsNullOrEmpty(PasswordText) && PasswordText == ConfirmPasswordText;
            bool hasContent = !string.IsNullOrEmpty(EmailText) && !string.IsNullOrEmpty(UserNameText) && !string.IsNullOrEmpty(PasswordText);

            bool isEnabled = hasContent && passwordsMatch && _metRulesCount == 6 && IsUsernameEnabled;

            IsCreateAccountButtonEnabled = isEnabled;
            return IsCreateAccountButtonEnabled;
        }

        private async void PerformCreateAccount()
        {
            await Task.Run(() =>
            {
                try
                {
                    using (SqlConnection dbConnect = new SqlConnection(_connectionString))
                    {
                        dbConnect.Open();

                        string checkUsername = "SELECT COUNT(*) FROM admin WHERE username = @user";
                        using (SqlCommand checkUser = new SqlCommand(checkUsername, dbConnect))
                        {
                            checkUser.Parameters.AddWithValue("@user", UserNameText.Trim());
                            int existingCount = (int)checkUser.ExecuteScalar();

                            if (existingCount >= 1)
                            {
                                App.UIThreadDispatcher?.TryEnqueue(() =>
                                {
                                    NativeToastHelper.SendNativeToast("Warning", UserNameText + " " + (ResourceString.GetString("notif_exist") ?? "already exists"));
                                });
                                return;
                            }
                        }

                        string insertData = "INSERT INTO admin (username, password, email, date_created, usertype, image) " +
                                            "VALUES(@username, @pass, @email, @date, @usertype, @image)";

                        byte[] imageData = _defaultImageData ?? new byte[0];
                        if (!string.IsNullOrEmpty(ProfileImagePath) && File.Exists(ProfileImagePath))
                        {
                            imageData = File.ReadAllBytes(ProfileImagePath);
                        }

                        string hash = BCrypt.Net.BCrypt.HashPassword(PasswordText);

                        using (SqlCommand cmd = new SqlCommand(insertData, dbConnect))
                        {
                            cmd.Parameters.AddWithValue("@username", UserNameText.Trim());
                            cmd.Parameters.AddWithValue("@pass", hash);
                            cmd.Parameters.AddWithValue("@email", EmailText.Trim());
                            cmd.Parameters.AddWithValue("@date", TextTime.Trim());
                            cmd.Parameters.AddWithValue("@usertype", AssignedUserType.Trim());
                            cmd.Parameters.AddWithValue("@image", imageData);

                            cmd.ExecuteNonQuery();
                        }

                        App.UIThreadDispatcher?.TryEnqueue(() =>
                        {
                            NativeToastHelper.SendNativeToast("Success", ResourceString.GetString("notif_registered_succesful") ?? "Successfully registered");
                            SwitchToLoginRequested?.Invoke();
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Database Error: " + ex.Message);
                    App.UIThreadDispatcher?.TryEnqueue(() =>
                    {
                        NativeToastHelper.SendNativeToast("Error", (ResourceString.GetString("notif_error_database") ?? "Database Error: ") + ex.Message);
                    });
                }
            });
        }

        private async Task OnBrowseImage()
        {
            try
            {
                var ofn = new OpenFileName();
                ofn.structSize = Marshal.SizeOf(ofn);
                ofn.hwnd = WindowNative.GetWindowHandle(App.MainWindow);

                ofn.filter = "Image Files (*.jpg; *.jpeg; *.png; *.bmp)\0*.jpg;*.jpeg;*.png;*.bmp\0All Files (*.*)\0*.*\0";
                ofn.filterIndex = 1;

                ofn.file = new string(new char[256]);
                ofn.maxFile = ofn.file.Length;
                ofn.fileTitle = new string(new char[64]);
                ofn.maxFileTitle = ofn.fileTitle.Length;

                ofn.title = "Select Profile Picture";
                ofn.initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                if (GetOpenFileName(ofn))
                {
                    string filePath = ofn.file;

                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 2 * 1024 * 1024)
                    {
                        NativeToastHelper.SendNativeToast("File Too Large", "Please select an image smaller than 2 MB.");
                        return;
                    }

                    ProfileImagePath = filePath;
                    var loadedImage = await ImageHelper.LoadFromBytesAsync(await File.ReadAllBytesAsync(filePath));

                    App.UIThreadDispatcher?.TryEnqueue(() =>
                    {
                        ProfileImageSource = loadedImage;
                    });

                    NativeToastHelper.SendNativeToast("Success", ResourceString.GetString("msgbox_load_profile_image_success") ?? "Profile picture loaded.");
                }
                else
                {
                    ProfileImagePath = null;
                    await LoadDefaultDataAsync();
                }
            }
            catch (Exception ex)
            {
                NativeToastHelper.SendNativeToast("Error", ex.Message);
            }
        }

        private void OnSpaceBarPrevent(object? parameter)
        {
            if (parameter is KeyRoutedEventArgs e)
            {
                if (e.Key == VirtualKey.Space)
                {
                    e.Handled = true;
                }
            }
        }

        private void UpdatePasswordValidationUI()
        {
            bool hasContent = !string.IsNullOrEmpty(PasswordText);
            bool isStrong = _metRulesCount == 6;

            IsPasswordValidationPopupOpen = hasContent && !isStrong;
        }
        #endregion
    }
}