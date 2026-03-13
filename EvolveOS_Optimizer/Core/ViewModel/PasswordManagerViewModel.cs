// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Security;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Data.SqlClient;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class PasswordManagerViewModel : ViewModelBase
    {
        #region Fields & Properties
        private static readonly Dictionary<string, List<string>> EnumTypeToDatabaseMap =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Email", new List<string> { ResourceString.GetString("lbl_category_email_db") } },
            { "Website", new List<string> { ResourceString.GetString("lbl_category_website_db") } },
            { "Bank", new List<string> { ResourceString.GetString("lbl_category_bank_db") } },
            { "Official", new List<string> { ResourceString.GetString("lbl_category_official_db") } },
            { "Mobile", new List<string> { ResourceString.GetString("lbl_category_mobile_db") } },
            { "Other", new List<string> { ResourceString.GetString("lbl_category_other_db") } },

            { "All", new List<string> { ResourceString.GetString("lbl_category_all_db") } }
        };

        public Action<PasswordEntry>? RequestOpenRecordModal { get; set; }
        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();

        private readonly SqlConnection connect = new SqlConnection(SqlConnectionHelper.connectReturnMARS());
        private readonly DataService _dataService = new DataService();
        private readonly string _username;
        private readonly SecureString _masterPassword;
        public ImageSource? DisplayProfileAvatar => _systemDiagnostics.GetProfileImage();

        public string CurrentUserId => _username;
        public SecureString MasterSecurePassword => _masterPassword;

        private List<PasswordEntry> _allEntries = new List<PasswordEntry>();

        private AddRecordViewModel _addRecordVM = null!;
        public AddRecordViewModel AddRecordVM
        {
            get => _addRecordVM;
            private set { _addRecordVM = value; OnPropertyChanged(); }
        }

        private ObservableCollection<CategoryDisplayItem> _categoryDisplayItems = new ObservableCollection<CategoryDisplayItem>();

        public ObservableCollection<CategoryDisplayItem> CategoryDisplayItems
        {
            get => _categoryDisplayItems;
            set { _categoryDisplayItems = value; OnPropertyChanged(); }
        }

        private CategoryDisplayItem? _selectedCategoryDisplayItem;

        public CategoryDisplayItem? SelectedCategoryDisplayItem
        {
            get => _selectedCategoryDisplayItem;
            set
            {
                if (_selectedCategoryDisplayItem != value)
                {
                    _selectedCategoryDisplayItem = value;
                    OnPropertyChanged();

                    FilterEntries();
                }
            }
        }

        private ObservableCollection<PasswordEntry> _filteredEntries = new ObservableCollection<PasswordEntry>();
        public ObservableCollection<PasswordEntry> FilteredEntries
        {
            get => _filteredEntries;
            set { _filteredEntries = value; OnPropertyChanged(); }
        }

        private KeyDerivationConfig _keyDerivationConfig = null!;
        private KeyDerivationMode _selectedEncryptionMode = KeyDerivationMode.Balanced;

        public ObservableCollection<KeyDerivationMode> EncryptionModes { get; }

        public KeyDerivationMode SelectedEncryptionMode
        {
            get => _selectedEncryptionMode;
            set
            {
                if (_selectedEncryptionMode != value)
                {
                    KeyDerivationMode oldMode = _selectedEncryptionMode;

                    _selectedEncryptionMode = value;
                    OnPropertyChanged();

                    _keyDerivationConfig = KeyDerivationConfig.Create(value);

                    AddRecordVM.UpdateKeyDerivationConfig(_keyDerivationConfig);

                    _ = ProcessEncryptionModeChangeAsync(oldMode, value);
                }
            }
        }
        #endregion

        #region Property Helpers
        private async Task ProcessEncryptionModeChangeAsync(KeyDerivationMode oldMode, KeyDerivationMode newMode)
        {
            bool success = await ReEncryptAllEntriesAsync(oldMode);
            if (success)
            {
                SettingsEngine.EncryptionMode = newMode.ToString();
            }
            else
            {
                _selectedEncryptionMode = oldMode;
                _keyDerivationConfig = KeyDerivationConfig.Create(oldMode);
                OnPropertyChanged(nameof(SelectedEncryptionMode));

                AddRecordVM.UpdateKeyDerivationConfig(_keyDerivationConfig);
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    FilterEntries();
                }
            }
        }

        public ICommand LoadDataCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }
        public ICommand SaveEntryCommand { get; }
        public ICommand CopyPasswordCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        #endregion

        #region Constructor
        public PasswordManagerViewModel(string username, SecureString masterPassword)
        {
            _username = username;
            _masterPassword = masterPassword ?? throw new ArgumentNullException(nameof(masterPassword), ResourceHelper.GetResourceString("err_master_password_null"));

            FilteredEntries = new ObservableCollection<PasswordEntry>();

            EncryptionModes = new ObservableCollection<KeyDerivationMode>(
                Enum.GetValues(typeof(KeyDerivationMode)).Cast<KeyDerivationMode>());

            if (Enum.TryParse(SettingsEngine.EncryptionMode, out KeyDerivationMode savedMode))
            {
                _selectedEncryptionMode = savedMode;
            }
            else
            {
                _selectedEncryptionMode = KeyDerivationMode.Balanced;
            }

            _keyDerivationConfig = KeyDerivationConfig.Create(_selectedEncryptionMode);


            InitializeCategoryDisplayItems();

            CategoryDisplayItem? initialSelection = CategoryDisplayItems.FirstOrDefault(c => c.Type == RecordType.All);
            if (initialSelection != null)
            {
                initialSelection.IsSelected = true;
                SelectedCategoryDisplayItem = initialSelection;
            }

            AddRecordVM = new AddRecordViewModel(_masterPassword, _keyDerivationConfig);
            AddRecordVM.CurrentUserId = _username;

            AddRecordVM.RecordSavedAction += async () =>
            {
                await LoadDataAsync(null);
            };

            LoadDataCommand = new RelayCommand(async (obj) => await LoadDataAsync(obj));
            EditCommand = new RelayCommand(ExecuteEdit);
            DeleteCommand = new RelayCommand(async (obj) => await ExecuteDeleteAsync(obj));
            TogglePasswordVisibilityCommand = new RelayCommand(ExecuteTogglePasswordVisibility);
            SaveEntryCommand = new RelayCommand(async (obj) => await ExecuteSaveEntryAsync(obj));
            CopyPasswordCommand = new RelayCommand(async (obj) => await ExecuteCopyPasswordAsync(obj));

            SelectCategoryCommand = new RelayCommand(ExecuteSelectCategory);

            _ = LoadDataAsync(null);
        }
        #endregion

        #region Methods & Command Implementations
        private void InitializeCategoryDisplayItems()
        {
            CategoryDisplayItems = new ObservableCollection<CategoryDisplayItem>
            {
                new CategoryDisplayItem(RecordType.All, ResourceString.GetString("lbl_category_all_display"), "\uE8B3"),
                new CategoryDisplayItem(RecordType.Email, ResourceString.GetString("lbl_category_emails_display"), "\uE715"),
                new CategoryDisplayItem(RecordType.Website, ResourceString.GetString("lbl_category_websites_display"), "\uE774"),
                new CategoryDisplayItem(RecordType.Bank, ResourceString.GetString("lbl_category_banking_display"), "\uE8AF"),
                new CategoryDisplayItem(RecordType.Official, ResourceString.GetString("lbl_category_official_display"), "\uE8A5"),
                new CategoryDisplayItem(RecordType.Mobile, ResourceString.GetString("lbl_category_mobile_display"), "\uE8EA"),
                new CategoryDisplayItem(RecordType.Other, ResourceString.GetString("lbl_category_other_display"), "\uE718")
            };
        }

        private void ExecuteSelectCategory(object? parameter)
        {
            if (parameter is CategoryDisplayItem newCategory)
            {
                if (SelectedCategoryDisplayItem != null)
                {
                    SelectedCategoryDisplayItem.IsSelected = false;
                }

                newCategory.IsSelected = true;

                SelectedCategoryDisplayItem = newCategory;
            }
        }

        private async Task ExecuteCopyPasswordAsync(object? parameter)
        {
            if (parameter is PasswordEntry entry)
            {
                try
                {
                    if (string.IsNullOrEmpty(entry.EncryptedPassword))
                    {
                        NotificationManager.Show(
                            ResourceString.GetString("msg_copy_failed_title") ?? "Failed",
                            ResourceString.GetString("msg_copy_missing_data"))
                            .WithSeverity(NotificationManager.NoticeSeverity.Warning)
                            .Create();
                        return;
                    }

                    string decryptedPassword = AesHelper.Decrypt(entry.EncryptedPassword, _masterPassword, _keyDerivationConfig);

                    if (!string.IsNullOrEmpty(decryptedPassword))
                    {
                        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                        dataPackage.SetText(decryptedPassword);
                        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                        NotificationManager.Show(
                            ResourceString.GetString("msg_success_title") ?? "Success",
                            string.Format(ResourceString.GetString("msg_copy_success_body"), entry.Name ?? "Unknown"))
                            .WithSeverity(NotificationManager.NoticeSeverity.Success)
                            .WithDuration(3000)
                            .Create();
                    }
                    else
                    {
                        NotificationManager.Show(
                            ResourceString.GetString("msg_decrypt_error_title") ?? "Error",
                            ResourceString.GetString("msg_decrypt_error_body"))
                            .WithSeverity(NotificationManager.NoticeSeverity.Error)
                            .Create();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FATAL: Error copying password: {ex.Message}");
                    NotificationManager.Show(
                        ResourceString.GetString("msg_critical_error_title") ?? "Critical Error",
                        string.Format(ResourceString.GetString("msg_copy_critical_error_body"), ex.Message))
                        .WithSeverity(NotificationManager.NoticeSeverity.Error)
                        .WithDuration(5000)
                        .Create();
                }
            }
        }

        private void FilterEntries()
        {
            if (_allEntries == null)
            {
                FilteredEntries = new ObservableCollection<PasswordEntry>();
                return;
            }

            IEnumerable<PasswordEntry> query = _allEntries;

            if (SelectedCategoryDisplayItem != null && SelectedCategoryDisplayItem.Type != RecordType.All)
            {
                string selectedTypeString = SelectedCategoryDisplayItem.Type.ToString();

                if (EnumTypeToDatabaseMap.TryGetValue(selectedTypeString, out List<string>? acceptableDbTypes) && acceptableDbTypes != null)
                {
                    query = query.Where(e =>
                        !string.IsNullOrWhiteSpace(e.Type) &&
                        acceptableDbTypes.Any(dbType => string.Equals(dbType, e.Type, StringComparison.OrdinalIgnoreCase))
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string searchLower = SearchText.ToLowerInvariant();

                query = query.Where(e =>
                    (e.Name != null && e.Name.ToLowerInvariant().Contains(searchLower)) ||
                    (e.Username != null && e.Username.ToLowerInvariant().Contains(searchLower)) ||
                    (e.Website != null && e.Website.ToLowerInvariant().Contains(searchLower))
                );
            }

            FilteredEntries = new ObservableCollection<PasswordEntry>(query.ToList());
        }

        private async Task LoadDataAsync(object? obj)
        {
            try
            {
                _allEntries = _dataService.GetAllPasswordEntries(_username);

                foreach (var entry in _allEntries)
                {
                    entry.SetMasterPassword(_masterPassword, _keyDerivationConfig);
                    entry.SetHiddenState();
                }

                FilterEntries();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: Error loading password data: {ex.Message}");
                NotificationManager.Show(
                    ResourceString.GetString("msg_data_load_error_title") ?? "Error",
                    string.Format(ResourceString.GetString("msg_load_data_error_body"), ex.Message))
                    .WithSeverity(NotificationManager.NoticeSeverity.Error)
                    .WithDuration(6000)
                    .Create();

                _allEntries.Clear();
                FilteredEntries.Clear();
            }
        }

        private async Task ExecuteSaveEntryAsync(object? parameter)
        {
            await LoadDataAsync(null);
        }

        private void ExecuteEdit(object? parameter)
        {
            if (parameter is PasswordEntry entry)
            {
                AddRecordVM.Initialize(entry);
                RequestOpenRecordModal?.Invoke(entry);
            }
        }

        private async Task ExecuteDeleteAsync(object? parameter)
        {
            if (parameter is PasswordEntry entry)
            {
                var dialog = new ContentDialog
                {
                    Title = ResourceString.GetString("msg_delete_confirm_title"),
                    Content = string.Format(ResourceString.GetString("msg_delete_confirm_body"), entry.Name ?? "Unknown"),
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = App.MainWindow!.Content!.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    if (_dataService.DeletePasswordEntry(entry.Name ?? string.Empty, _username))
                    {
                        _allEntries.Remove(entry);
                        FilteredEntries.Clear();
                        FilteredEntries = new ObservableCollection<PasswordEntry>(_allEntries);

                        NotificationManager.Show(
                            ResourceString.GetString("msg_success_title") ?? "Success",
                            string.Format(ResourceString.GetString("msg_delete_success_body"), entry.Name ?? "Unknown"))
                            .WithSeverity(NotificationManager.NoticeSeverity.Success)
                            .WithDuration(3000)
                            .Create();
                    }
                    else
                    {
                        NotificationManager.Show(
                            ResourceString.GetString("msg_error_title") ?? "Error",
                            ResourceString.GetString("msg_delete_db_error_body"))
                            .WithSeverity(NotificationManager.NoticeSeverity.Error)
                            .Create();
                    }
                }
            }
        }

        private void ExecuteTogglePasswordVisibility(object? parameter)
        {
            if (parameter is PasswordEntry entry)
            {
                entry.TogglePasswordVisibility();
            }
        }
        #endregion

        #region Re-Encryption Logic
        private async Task<bool> ReEncryptAllEntriesAsync(KeyDerivationMode oldMode)
        {
            if (_allEntries == null || !_allEntries.Any())
            {
                return true;
            }

            KeyDerivationConfig oldConfig = KeyDerivationConfig.Create(oldMode);
            KeyDerivationConfig newConfig = KeyDerivationConfig.Create(_selectedEncryptionMode);

            if (oldConfig.Equals(newConfig))
            {
                NotificationManager.Show(
                    ResourceString.GetString("msg_no_change_title") ?? "Info",
                    string.Format(ResourceString.GetString("msg_reencrypt_no_change_body"), _selectedEncryptionMode))
                    .WithSeverity(NotificationManager.NoticeSeverity.Info)
                    .Create();
                return true;
            }

            int reEncryptedCount = 0;
            List<string> failedEntries = new List<string>();

            NotificationManager.Show(
                ResourceString.GetString("msg_reencryption_title") ?? "Processing",
                string.Format(ResourceString.GetString("msg_reencrypt_start_body"), oldMode, _selectedEncryptionMode))
                .WithSeverity(NotificationManager.NoticeSeverity.Info)
                .Create();

            try
            {
                for (int i = 0; i < _allEntries.Count; i++)
                {
                    var entry = _allEntries[i];

                    if (string.IsNullOrEmpty(entry.EncryptedPassword))
                    {
                        continue;
                    }

                    string decryptedPassword = string.Empty;

                    try
                    {
                        decryptedPassword = AesHelper.Decrypt(
                            entry.EncryptedPassword,
                            _masterPassword,
                            oldConfig
                        );
                    }
                    catch (Exception decryptEx)
                    {
                        Console.WriteLine($"FATAL DECRYPTION WARNING: Failed to decrypt entry '{entry.Name}' (ID: {entry.Id}). Error: {decryptEx.Message}");
                        failedEntries.Add(entry.Name ?? "Unknown");
                        continue;
                    }

                    if (string.IsNullOrEmpty(decryptedPassword) || decryptedPassword == "[Decryption Failed]")
                    {
                        Console.WriteLine($"FATAL DECRYPTION WARNING: Decryption of entry '{entry.Name}' failed using old config. Skipping re-encryption for this entry.");
                        failedEntries.Add(entry.Name ?? "Unknown");
                        continue;
                    }

                    string newEncryptedPwd = AesHelper.Encrypt(
                        decryptedPassword,
                        _masterPassword,
                        newConfig
                    );

                    bool success = _dataService.UpdateEncryptedPassword(entry.Name ?? string.Empty, newEncryptedPwd, _username);

                    if (success)
                    {
                        entry.EncryptedPassword = newEncryptedPwd;
                        reEncryptedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: Failed to save re-encrypted entry '{entry.Name}' to the database.");
                    }
                }

                if (reEncryptedCount > 0)
                {
                    NotificationManager.Show(
                        ResourceString.GetString("msg_encryption_success_title") ?? "Success",
                        string.Format(ResourceString.GetString("msg_reencrypt_success_body"), reEncryptedCount, _selectedEncryptionMode))
                        .WithSeverity(NotificationManager.NoticeSeverity.Success)
                        .WithDuration(4000)
                        .Create();
                }

                if (failedEntries.Any())
                {
                    NotificationManager.Show(
                        ResourceString.GetString("msg_partial_reencrypt_title") ?? "Warning",
                        string.Format(ResourceString.GetString("msg_reencrypt_partial_failure_body"), failedEntries.Count, failedEntries.First(), oldMode))
                        .WithSeverity(NotificationManager.NoticeSeverity.Warning)
                        .WithDuration(6000)
                        .Create();
                }
                else if (reEncryptedCount == 0 && _allEntries.Any())
                {
                    NotificationManager.Show(
                        ResourceString.GetString("msg_reencrypt_finished_title") ?? "Completed",
                        ResourceString.GetString("msg_reencrypt_finished_body"))
                        .WithSeverity(NotificationManager.NoticeSeverity.Info)
                        .Create();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: System error during re-encryption: {ex.Message}");
                NotificationManager.Show(
                    ResourceString.GetString("msg_critical_error_title") ?? "Critical Error",
                    string.Format(ResourceString.GetString("msg_reencrypt_critical_error_body"), oldMode, ex.Message))
                    .WithSeverity(NotificationManager.NoticeSeverity.Error)
                    .WithDuration(6000)
                    .Create();
                return false;
            }
            finally
            {
                await LoadDataAsync(null);
            }
        }
        #endregion
    }
}