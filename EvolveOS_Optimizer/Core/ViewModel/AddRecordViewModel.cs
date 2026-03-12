// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Security;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class RecordTypeItem
    {
        public RecordType Value { get; set; }

        public string DisplayName { get; set; } = string.Empty;
    }

    public class AddRecordViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private readonly SecureString _masterSecurePassword;
        private KeyDerivationConfig _activeKeyDerivationConfig;

        private PasswordEntry? _currentEntry;

        public string CurrentUserId { get; set; } = "1";

        public Action? RecordSavedAction { get; set; }
        public Action? CloseRequestedAction { get; set; }

        private RecordTypeItem? _selectedType;
        public RecordTypeItem? SelectedType
        {
            get => _selectedType;
            set { _selectedType = value; OnPropertyChanged(); }
        }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); UpdateCanExecute(); }
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        private string _emailAddress = string.Empty;
        public string EmailAddress
        {
            get => _emailAddress;
            set { _emailAddress = value; OnPropertyChanged(); }
        }

        private string _websiteLink = string.Empty;
        public string WebsiteLink
        {
            get => _websiteLink;
            set { _websiteLink = value; OnPropertyChanged(); }
        }

        private string _mobileNumber = string.Empty;
        public string MobileNumber
        {
            get => _mobileNumber;
            set { _mobileNumber = value; OnPropertyChanged(); }
        }

        private string _recordPassword = string.Empty;
        public string RecordPassword
        {
            get => _recordPassword;
            set { _recordPassword = value; OnPropertyChanged(); UpdateCanExecute(); }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RecordTypeItem> RecordTypes { get; }

        private ICommand? _saveCommand;
        public ICommand SaveCommand => _saveCommand ??= new RelayCommand(ExecuteSave, CanExecuteSave);

        private ICommand? _cancelCommand;
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(ExecuteCancel);

        public AddRecordViewModel(SecureString masterSecurePassword, KeyDerivationConfig initialConfig)
        {
            _masterSecurePassword = masterSecurePassword;
            _activeKeyDerivationConfig = initialConfig ?? throw new ArgumentNullException(nameof(initialConfig));

            _dataService = new DataService();

            RecordTypes = new ObservableCollection<RecordTypeItem>(
                Enum.GetValues(typeof(RecordType))
                    .Cast<RecordType>()
                    .Where(type => type != RecordType.All)
                    .Select(type => new RecordTypeItem
                    {
                        Value = type,
                        DisplayName = type.GetDescription()
                    })
            );

            SelectedType = RecordTypes.FirstOrDefault(rt => rt.Value == RecordType.Website)
                               ?? RecordTypes.FirstOrDefault();

            UpdateCanExecute();
        }

        public void UpdateKeyDerivationConfig(KeyDerivationConfig newConfig)
        {
            _activeKeyDerivationConfig = newConfig ?? throw new ArgumentNullException(nameof(newConfig));
        }

        public void Initialize(PasswordEntry? entryToEdit = null)
        {
            ResetForm();

            if (entryToEdit != null)
            {
                _currentEntry = entryToEdit;

                try
                {
                    RecordPassword = entryToEdit.DisplayedPassword;
                }
                catch
                {
                    RecordPassword = ResourceString.GetString("status_error_decrypt_edit");
                }

                Title = entryToEdit.Name ?? string.Empty;
                Username = entryToEdit.Username ?? string.Empty;
                EmailAddress = entryToEdit.Email ?? string.Empty;
                WebsiteLink = entryToEdit.Website ?? string.Empty;
                MobileNumber = entryToEdit.MobileNumber ?? string.Empty;
                Description = entryToEdit.Description ?? string.Empty;

                RecordType mappedType = RecordType.Other;

                if (entryToEdit.Type == ResourceString.GetString("lbl_category_email_db")) mappedType = RecordType.Email;
                else if (entryToEdit.Type == ResourceString.GetString("lbl_category_website_db")) mappedType = RecordType.Website;
                else if (entryToEdit.Type == ResourceString.GetString("lbl_category_bank_db")) mappedType = RecordType.Bank;
                else if (entryToEdit.Type == ResourceString.GetString("lbl_category_official_db")) mappedType = RecordType.Official;
                else if (entryToEdit.Type == ResourceString.GetString("lbl_category_mobile_db")) mappedType = RecordType.Mobile;

                SelectedType = RecordTypes.FirstOrDefault(rt => rt.Value == mappedType) ?? RecordTypes.FirstOrDefault();

                StatusMessage = string.Format(ResourceString.GetString("status_ready_to_edit"), entryToEdit.Name);
            }
            else
            {
                _currentEntry = null;
                StatusMessage = ResourceString.GetString("status_ready_to_add_new");

                SelectedType = RecordTypes.FirstOrDefault(rt => rt.Value == RecordType.Website)
                                 ?? RecordTypes.FirstOrDefault();
            }
        }

        private bool CanExecuteSave(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(Title) &&
                   !string.IsNullOrEmpty(RecordPassword);
        }

        private async void ExecuteSave(object? parameter)
        {
            StatusMessage = ResourceString.GetString("status_processing_record");

            try
            {
                SecureString secureMasterPwd = _masterSecurePassword;

                string encryptedRecordPassword = AesHelper.Encrypt(
                    RecordPassword,
                    secureMasterPwd,
                    _activeKeyDerivationConfig
                );

                var entryToSave = _currentEntry ?? new PasswordEntry();

                entryToSave.UserId = CurrentUserId;
                entryToSave.Name = this.Title;
                entryToSave.Username = this.Username;
                entryToSave.Email = this.EmailAddress;
                entryToSave.Website = this.WebsiteLink;
                entryToSave.MobileNumber = this.MobileNumber;

                entryToSave.Type = this.SelectedType?.Value switch
                {
                    RecordType.Email => ResourceString.GetString("lbl_category_email_db"),
                    RecordType.Website => ResourceString.GetString("lbl_category_website_db"),
                    RecordType.Bank => ResourceString.GetString("lbl_category_bank_db"),
                    RecordType.Official => ResourceString.GetString("lbl_category_official_db"),
                    RecordType.Mobile => ResourceString.GetString("lbl_category_mobile_db"),
                    RecordType.Other => ResourceString.GetString("lbl_category_other_db"),
                    _ => ResourceString.GetString("lbl_category_other_db")
                };

                entryToSave.Description = this.Description;
                entryToSave.EncryptedPassword = encryptedRecordPassword;

                bool success = _dataService.SavePasswordEntry(entryToSave, encryptedRecordPassword);

                if (!success)
                {
                    throw new Exception(ResourceString.GetString("status_error_db_failed"));
                }

                string successMessage = ResourceString.GetString("status_success_saved");
                StatusMessage = successMessage;

                NotificationManager.Show(
                    ResourceString.GetString("msg_success_title") ?? "Success",
                    successMessage)
                    .WithSeverity(NotificationManager.NoticeSeverity.Success)
                    .WithDuration(3000)
                    .Create();

                RecordSavedAction?.Invoke();

                ResetForm();
                CloseRequestedAction?.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(ResourceString.GetString("status_error_failed_to_save"), ex.Message);
                Console.WriteLine($"Error during save: {ex.Message}");

                NotificationManager.Show(
                    ResourceString.GetString("msg_error_title") ?? "Error",
                    ex.Message)
                    .WithSeverity(NotificationManager.NoticeSeverity.Error)
                    .WithDuration(5000)
                    .Create();
            }
        }

        private void ExecuteCancel(object? parameter)
        {
            ResetForm();
            CloseRequestedAction?.Invoke();
        }

        private void UpdateCanExecute()
        {
            (_saveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ResetForm()
        {
            _currentEntry = null;
            Title = string.Empty;
            Username = string.Empty;
            EmailAddress = string.Empty;
            WebsiteLink = string.Empty;
            MobileNumber = string.Empty;
            RecordPassword = string.Empty;
            Description = string.Empty;
            SelectedType = RecordTypes.FirstOrDefault(rt => rt.Value == RecordType.Website)
                               ?? RecordTypes.FirstOrDefault();
            StatusMessage = string.Empty;
        }
    }

    public static class EnumExtensions
    {
        public static string GetDescription<T>(this T enumValue) where T : Enum
        {
            return enumValue.GetType()
                .GetMember(enumValue.ToString())
                .FirstOrDefault()
                ?.GetCustomAttribute<DescriptionAttribute>(false)
                ?.Description ?? enumValue.ToString();
        }
    }
}