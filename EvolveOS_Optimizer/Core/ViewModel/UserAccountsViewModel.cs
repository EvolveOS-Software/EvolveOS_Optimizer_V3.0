// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Data.SqlClient;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class UserAccountsViewModel : ViewModelBase
    {
        private readonly string _connectionString = SqlConnectionHelper.connectReturn();
        private DispatcherTimer _timer;

        private readonly PasswordValidator _passwordValidator = new PasswordValidator();
        public PasswordViewModel PasswordValidation { get; } = new PasswordViewModel();

        #region Properties - General

        private ObservableCollection<UserAccount> _userList = new();
        public ObservableCollection<UserAccount> UserList
        {
            get => _userList;
            set { if (_userList != value) { _userList = value; OnPropertyChanged(); } }
        }

        private UserAccount? _selectedUser;
        public UserAccount? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (_selectedUser != value)
                {
                    _selectedUser = value;
                    OnPropertyChanged();
                    UpdateSelectedUserDetails(value);

                    (DeleteUserCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (EditUserCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _selectedUserId = string.Empty;
        public string SelectedUserId
        {
            get => _selectedUserId;
            set { if (_selectedUserId != value) { _selectedUserId = value; OnPropertyChanged(); } }
        }

        private string _selectedUserName = string.Empty;
        public string SelectedUserName
        {
            get => _selectedUserName;
            set { if (_selectedUserName != value) { _selectedUserName = value; OnPropertyChanged(); } }
        }

        private BitmapImage? _selectedUserImageSource;
        public BitmapImage? SelectedUserImageSource
        {
            get => _selectedUserImageSource;
            set { if (_selectedUserImageSource != value) { _selectedUserImageSource = value; OnPropertyChanged(); } }
        }

        private Visibility _isUserIdVisible = Visibility.Collapsed;
        public Visibility IsUserIdVisible
        {
            get => _isUserIdVisible;
            set { if (_isUserIdVisible != value) { _isUserIdVisible = value; OnPropertyChanged(); } }
        }

        private string _currentTime = string.Empty;
        public string CurrentTime
        {
            get => _currentTime;
            set { if (_currentTime != value) { _currentTime = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Properties - Side Panel Form

        private bool _isPanelOpen;
        public bool IsPanelOpen
        {
            get => _isPanelOpen;
            set
            {
                if (_isPanelOpen != value)
                {
                    _isPanelOpen = value;
                    OnPropertyChanged();
                    UIHelper.SetOverlay(value);
                }
            }
        }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (_isEditMode != value)
                {
                    _isEditMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormTitle));
                }
            }
        }

        public string FormTitle => IsEditMode ? "Edit User Account" : "Create New Account";

        private string _formUsername = string.Empty;
        public string FormUsername
        {
            get => _formUsername;
            set { if (_formUsername != value) { _formUsername = value; OnPropertyChanged(); } }
        }

        private string _formEmail = string.Empty;
        public string FormEmail
        {
            get => _formEmail;
            set
            {
                if (_formEmail != value)
                {
                    _formEmail = value;
                    OnPropertyChanged();
                    ValidateEmail();
                }
            }
        }

        private string _emailValidationContent = string.Empty;
        public string EmailValidationContent
        {
            get => _emailValidationContent;
            set { _emailValidationContent = value; OnPropertyChanged(); }
        }

        private Brush _emailValidationForeground = new SolidColorBrush(Colors.Transparent);
        public Brush EmailValidationForeground
        {
            get => _emailValidationForeground;
            set { _emailValidationForeground = value; OnPropertyChanged(); }
        }

        private Visibility _emailValidationVisibility = Visibility.Collapsed;
        public Visibility EmailValidationVisibility
        {
            get => _emailValidationVisibility;
            set { _emailValidationVisibility = value; OnPropertyChanged(); }
        }

        private string _formPassword = string.Empty;
        public string FormPassword
        {
            get => _formPassword;
            set
            {
                if (_formPassword != value)
                {
                    _formPassword = value;
                    OnPropertyChanged();
                    UpdateValidation();
                    UpdateMatchValidation();
                }
            }
        }

        private string _formConfirmPassword = string.Empty;
        public string FormConfirmPassword
        {
            get => _formConfirmPassword;
            set
            {
                if (_formConfirmPassword != value)
                {
                    _formConfirmPassword = value;
                    OnPropertyChanged();
                    UpdateMatchValidation();
                }
            }
        }

        private string _formFirstName = string.Empty;
        public string FormFirstName
        {
            get => _formFirstName;
            set { if (_formFirstName != value) { _formFirstName = value; OnPropertyChanged(); } }
        }

        private string _formLastName = string.Empty;
        public string FormLastName
        {
            get => _formLastName;
            set { if (_formLastName != value) { _formLastName = value; OnPropertyChanged(); } }
        }

        private string _formUserType = "Standard";
        public string FormUserType
        {
            get => _formUserType;
            set { if (_formUserType != value) { _formUserType = value; OnPropertyChanged(); } }
        }

        private BitmapImage? _formImageSource;
        public BitmapImage? FormImageSource
        {
            get => _formImageSource;
            set
            {
                if (_formImageSource != value)
                {
                    _formImageSource = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormInitials));
                }
            }
        }

        private byte[]? _formImageData;

        public string FormInitials => FormImageSource != null ? " " : "";

        #endregion

        #region Properties - Password Validation Indicators

        private int _metRulesCount = 0;
        public double StrengthPiece1Opacity => _metRulesCount >= 1 ? 1.0 : 0.2;
        public double StrengthPiece2Opacity => _metRulesCount >= 2 ? 1.0 : 0.2;
        public double StrengthPiece3Opacity => _metRulesCount >= 3 ? 1.0 : 0.2;
        public double StrengthPiece4Opacity => _metRulesCount >= 4 ? 1.0 : 0.2;
        public double StrengthPiece5Opacity => _metRulesCount >= 5 ? 1.0 : 0.2;
        public double StrengthPiece6Opacity => _metRulesCount >= 6 ? 1.0 : 0.2;

        private double _matchPieceOpacity = 0.2;
        public double MatchPieceOpacity
        {
            get => _matchPieceOpacity;
            set { _matchPieceOpacity = value; OnPropertyChanged(); }
        }

        private string _matchText = string.Empty;
        public string MatchText
        {
            get => _matchText;
            set { _matchText = value; OnPropertyChanged(); }
        }

        private Visibility _matchTextVisibility = Visibility.Collapsed;
        public Visibility MatchTextVisibility
        {
            get => _matchTextVisibility;
            set { _matchTextVisibility = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        private ICommand? _loadUsersCommand;
        private ICommand? _clearSelectionCommand;
        private ICommand? _deleteUserCommand;
        private ICommand? _createUserCommand;
        private ICommand? _editUserCommand;
        private ICommand? _saveUserCommand;
        private ICommand? _cancelFormCommand;
        private ICommand? _browseImageCommand;

        public ICommand LoadUsersCommand => _loadUsersCommand ??= new RelayCommand(async (_) => await ExecuteLoadUsers());
        public ICommand ClearSelectionCommand => _clearSelectionCommand ??= new RelayCommand((_) => SelectedUser = null);
        public ICommand DeleteUserCommand => _deleteUserCommand ??= new RelayCommand(async (_) => await ExecuteDeleteUser(), (_) => SelectedUser != null);

        public ICommand CreateUserCommand => _createUserCommand ??= new RelayCommand((_) => ExecuteCreateUser());
        public ICommand EditUserCommand => _editUserCommand ??= new RelayCommand((_) => ExecuteUpdateUser(), (_) => SelectedUser != null);

        public ICommand SaveUserCommand => _saveUserCommand ??= new RelayCommand(async (_) => await ExecuteSaveUser());
        public ICommand CancelFormCommand => _cancelFormCommand ??= new RelayCommand((_) => IsPanelOpen = false);
        public ICommand BrowseImageCommand => _browseImageCommand ??= new RelayCommand(async (_) => await ExecuteBrowseImage());

        #endregion

        #region Constructor
        public UserAccountsViewModel()
        {
            _currentTime = DateTime.Now.ToString("MM-dd-yyyy HH:mm:ss");

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("MM-dd-yyyy HH:mm:ss");
            _timer.Start();

            _ = ExecuteLoadUsers();
        }
        #endregion

        #region Validation Logic
        private void ValidateEmail()
        {
            if (string.IsNullOrWhiteSpace(FormEmail))
            {
                EmailValidationVisibility = Visibility.Collapsed;
                return;
            }

            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            bool isValid = Regex.IsMatch(FormEmail, pattern);

            if (isValid)
            {
                EmailValidationContent = "Valid";
                EmailValidationForeground = new SolidColorBrush(ColorHelper.FromArgb(255, 52, 205, 105));
                EmailValidationVisibility = Visibility.Visible;
            }
            else
            {
                EmailValidationContent = "Invalid";
                EmailValidationForeground = new SolidColorBrush(Colors.Red);
                EmailValidationVisibility = Visibility.Visible;
            }
        }

        private void UpdateValidation()
        {
            PasswordValidation.Password = FormPassword;
            var rules = _passwordValidator.Validate(FormPassword);
            _metRulesCount = rules.Count(r => r.IsMet);

            OnPropertyChanged(nameof(StrengthPiece1Opacity));
            OnPropertyChanged(nameof(StrengthPiece2Opacity));
            OnPropertyChanged(nameof(StrengthPiece3Opacity));
            OnPropertyChanged(nameof(StrengthPiece4Opacity));
            OnPropertyChanged(nameof(StrengthPiece5Opacity));
            OnPropertyChanged(nameof(StrengthPiece6Opacity));
        }

        private void UpdateMatchValidation()
        {
            if (string.IsNullOrEmpty(FormPassword) && string.IsNullOrEmpty(FormConfirmPassword))
            {
                MatchPieceOpacity = 0.2;
                MatchTextVisibility = Visibility.Collapsed;
                return;
            }

            if (FormPassword == FormConfirmPassword)
            {
                MatchPieceOpacity = 1.0;
                MatchText = "Match";
                MatchTextVisibility = Visibility.Visible;
            }
            else
            {
                MatchPieceOpacity = 0.2;
                MatchTextVisibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Database Operations
        private async Task ExecuteLoadUsers()
        {
            try
            {
                using var connect = new SqlConnection(_connectionString);
                await connect.OpenAsync();

                var cmd = new SqlCommand("SELECT id, username, email, firstname, lastname, date_created, usertype, image FROM admin", connect);
                using var reader = await cmd.ExecuteReaderAsync();

                var tempGrid = new ObservableCollection<UserAccount>();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var account = new UserAccount
                        {
                            Id = reader["id"].ToString() ?? "",
                            Username = reader["username"].ToString() ?? "",
                            Email = reader["email"].ToString() ?? "",
                            FirstName = reader["firstname"].ToString() ?? "",
                            LastName = reader["lastname"].ToString() ?? "",
                            DateCreated = reader["date_created"].ToString() ?? "",
                            UserType = reader["usertype"].ToString() ?? "",
                            RawImage = reader["image"] as byte[]
                        };

                        if (account.RawImage != null && account.RawImage.Length > 0)
                        {
                            using var ms = new MemoryStream(account.RawImage);
                            var bi = new BitmapImage();
                            await bi.SetSourceAsync(ms.AsRandomAccessStream());
                            account.ProfileImage = bi;
                        }
                        else
                        {
                            account.ProfileImage = new BitmapImage(new Uri("ms-appx:///Resources/EvolveOSLogo.png"));
                        }

                        tempGrid.Add(account);
                    }
                    catch (Exception imgEx)
                    {
                        Debug.WriteLine($"Failed to load image for a user: {imgEx.Message}");
                    }
                }

                App.UIThreadDispatcher?.TryEnqueue(() =>
                {
                    UserList = tempGrid;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading users: {ex.Message}");
                NativeToastHelper.SendNativeToast("SQL Error", $"Failed to load accounts: {ex.Message}");
            }
        }

        public async Task LoadProfileImage(byte[]? data)
        {
            if (data == null || data.Length == 0)
            {
                SelectedUserImageSource = new BitmapImage(new Uri("ms-appx:///Resources/EvolveOSLogo.png"));
                return;
            }

            using var ms = new MemoryStream(data);
            var bi = new BitmapImage();
            await bi.SetSourceAsync(ms.AsRandomAccessStream());
            SelectedUserImageSource = bi;
        }

        private void UpdateSelectedUserDetails(UserAccount? user)
        {
            if (user != null)
            {
                SelectedUserId = user.Id ?? string.Empty;
                SelectedUserName = user.Username ?? string.Empty;
                IsUserIdVisible = Visibility.Visible;
                _ = LoadProfileImage(user.RawImage);
            }
            else
            {
                SelectedUserId = string.Empty;
                SelectedUserName = string.Empty;
                IsUserIdVisible = Visibility.Collapsed;
                SelectedUserImageSource = null;
            }
        }

        private async Task ExecuteDeleteUser()
        {
            if (SelectedUser == null) return;

            try
            {
                using var connect = new SqlConnection(_connectionString);
                await connect.OpenAsync();
                var cmd = new SqlCommand("DELETE FROM admin WHERE id = @id", connect);
                cmd.Parameters.AddWithValue("@id", SelectedUser.Id);
                await cmd.ExecuteNonQueryAsync();

                NativeToastHelper.SendNativeToast("Account Deleted", $"User {SelectedUser.Username} has been removed.");
                SelectedUser = null;
                await ExecuteLoadUsers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Delete error: {ex.Message}");
            }
        }

        private async Task ExecuteBrowseImage()
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".bmp");

                var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();

                if (file != null)
                {
                    _formImageData = await File.ReadAllBytesAsync(file.Path);
                    var loadedImage = await ImageHelper.LoadFromBytesAsync(_formImageData);

                    App.UIThreadDispatcher?.TryEnqueue(() => FormImageSource = loadedImage);
                }
            }
            catch (Exception ex)
            {
                NativeToastHelper.SendNativeToast("Error", $"Could not load image: {ex.Message}");
            }
        }

        private void ExecuteCreateUser()
        {
            IsEditMode = false;
            FormUsername = string.Empty;
            FormEmail = string.Empty;
            EmailValidationVisibility = Visibility.Collapsed;
            FormFirstName = string.Empty;
            FormLastName = string.Empty;
            FormPassword = string.Empty;
            FormConfirmPassword = string.Empty;
            FormUserType = "Standard";

            _formImageData = null;

            FormImageSource = new BitmapImage(new Uri("ms-appx:///Resources/EvolveOSLogo.png"));

            IsPanelOpen = true;
        }

        private void ExecuteUpdateUser()
        {
            if (SelectedUser != null)
            {
                IsEditMode = true;

                FormUsername = SelectedUser.Username ?? string.Empty;
                FormEmail = SelectedUser.Email ?? string.Empty;
                ValidateEmail();
                FormFirstName = SelectedUser.FirstName ?? string.Empty;
                FormLastName = SelectedUser.LastName ?? string.Empty;
                FormPassword = string.Empty;
                FormConfirmPassword = string.Empty;
                FormUserType = SelectedUser.UserType ?? "Standard";

                _formImageData = SelectedUser.RawImage;
                FormImageSource = SelectedUserImageSource;

                IsPanelOpen = true;
            }
        }

        private async Task ExecuteSaveUser()
        {
            if (string.IsNullOrWhiteSpace(FormUsername) || string.IsNullOrWhiteSpace(FormEmail))
            {
                NativeToastHelper.SendNativeToast("Validation Error", "Username and Email are required.");
                return;
            }

            if (FormUsername.Contains(" "))
            {
                NativeToastHelper.SendNativeToast("Validation Error", "Username cannot contain spaces.");
                return;
            }

            if (EmailValidationContent == "Invalid")
            {
                NativeToastHelper.SendNativeToast("Validation Error", "Please enter a valid email address.");
                return;
            }

            if (!string.IsNullOrEmpty(FormPassword) || !string.IsNullOrEmpty(FormConfirmPassword))
            {
                if (FormPassword != FormConfirmPassword)
                {
                    NativeToastHelper.SendNativeToast("Validation Error", "Passwords do not match. Please try again.");
                    return;
                }
            }

            try
            {
                using var connect = new SqlConnection(_connectionString);
                await connect.OpenAsync();

                string sql;
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = connect;

                cmd.Parameters.AddWithValue("@username", FormUsername.Trim());
                cmd.Parameters.AddWithValue("@email", FormEmail.Trim());
                cmd.Parameters.AddWithValue("@firstname", FormFirstName.Trim());
                cmd.Parameters.AddWithValue("@lastname", FormLastName.Trim());
                cmd.Parameters.AddWithValue("@usertype", FormUserType.Trim());

                var imageParam = new SqlParameter("@image", System.Data.SqlDbType.VarBinary);
                if (_formImageData != null && _formImageData.Length > 0)
                {
                    imageParam.Value = _formImageData;
                }
                else
                {
                    imageParam.Value = DBNull.Value;
                }
                cmd.Parameters.Add(imageParam);

                if (IsEditMode)
                {
                    if (SelectedUser == null) return;
                    cmd.Parameters.AddWithValue("@id", SelectedUser.Id);

                    if (string.IsNullOrEmpty(FormPassword))
                    {
                        sql = "UPDATE admin SET username=@username, email=@email, firstname=@firstname, lastname=@lastname, usertype=@usertype, image=@image WHERE id=@id";
                    }
                    else
                    {
                        string hash = BCrypt.Net.BCrypt.HashPassword(FormPassword);
                        cmd.Parameters.AddWithValue("@password", hash);
                        sql = "UPDATE admin SET username=@username, email=@email, firstname=@firstname, lastname=@lastname, password=@password, usertype=@usertype, image=@image WHERE id=@id";
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(FormPassword))
                    {
                        NativeToastHelper.SendNativeToast("Validation Error", "A password is required for new accounts.");
                        return;
                    }

                    string hash = BCrypt.Net.BCrypt.HashPassword(FormPassword);
                    cmd.Parameters.AddWithValue("@password", hash);
                    string currentDate = DateTime.Now.ToString("MM-dd-yyyy HH:mm:ss");
                    cmd.Parameters.AddWithValue("@date", currentDate);

                    sql = "INSERT INTO admin (username, email, firstname, lastname, password, usertype, image, date_created) VALUES (@username, @email, @firstname, @lastname, @password, @usertype, @image, @date)";
                }

                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();

                IsPanelOpen = false;
                await ExecuteLoadUsers();

                string action = IsEditMode ? "updated" : "created";
                NativeToastHelper.SendNativeToast("Success", $"Account '{FormUsername}' has been {action}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveUser Error] {ex.Message}");
                NativeToastHelper.SendNativeToast("Database Error", $"Failed to save: {ex.Message}");
            }
        }
        #endregion
    }
}