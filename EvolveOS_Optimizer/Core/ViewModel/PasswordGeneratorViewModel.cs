// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using System.Text;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Utilities.Helpers;
using Windows.ApplicationModel.DataTransfer;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class PasswordGeneratorViewModel : ObservableObject
    {
        private readonly PasswordValidator _passwordValidator = new PasswordValidator();
        public PasswordViewModel PasswordValidation { get; }

        #region Properties
        private int _passwordLength = 16;
        public int PasswordLength
        {
            get => _passwordLength;
            set => SetProperty(ref _passwordLength, value);
        }

        private bool _includeLowercase = true;
        public bool IncludeLowercase
        {
            get => _includeLowercase;
            set => SetProperty(ref _includeLowercase, value);
        }

        private bool _includeUppercase = true;
        public bool IncludeUppercase
        {
            get => _includeUppercase;
            set => SetProperty(ref _includeUppercase, value);
        }

        private bool _includeDigits = true;
        public bool IncludeDigits
        {
            get => _includeDigits;
            set => SetProperty(ref _includeDigits, value);
        }

        private bool _includeSymbols = true;
        public bool IncludeSymbols
        {
            get => _includeSymbols;
            set => SetProperty(ref _includeSymbols, value);
        }

        private string _generatedPassword = string.Empty;
        public string GeneratedPassword
        {
            get => _generatedPassword;
            set
            {
                if (SetProperty(ref _generatedPassword, value))
                {
                    UpdateValidation();
                }
            }
        }

        private bool _isCustomPasswordEnabled = false;
        public bool IsCustomPasswordEnabled
        {
            get => _isCustomPasswordEnabled;
            set
            {
                if (SetProperty(ref _isCustomPasswordEnabled, value))
                {
                    OnPropertyChanged(nameof(IsPasswordReadOnly));
                    OnPropertyChanged(nameof(IsGenerationEnabled));

                    if (value)
                    {
                        GeneratedPassword = string.Empty;
                    }
                }
            }
        }

        public bool IsPasswordReadOnly => !IsCustomPasswordEnabled;
        public bool IsGenerationEnabled => !IsCustomPasswordEnabled;

        private int _metRulesCount = 0;
        public double StrengthPiece1Opacity => _metRulesCount >= 1 ? 1.0 : 0.2;
        public double StrengthPiece2Opacity => _metRulesCount >= 2 ? 1.0 : 0.2;
        public double StrengthPiece3Opacity => _metRulesCount >= 3 ? 1.0 : 0.2;
        public double StrengthPiece4Opacity => _metRulesCount >= 4 ? 1.0 : 0.2;
        public double StrengthPiece5Opacity => _metRulesCount >= 5 ? 1.0 : 0.2;
        public double StrengthPiece6Opacity => _metRulesCount >= 6 ? 1.0 : 0.2;
        #endregion

        #region Commands
        public ICommand GenerateCommand { get; }
        public ICommand CopyCommand { get; }
        #endregion

        public PasswordGeneratorViewModel()
        {
            PasswordValidation = new PasswordViewModel();
            GenerateCommand = new RelayCommand(_ => GeneratePassword());
            CopyCommand = new RelayCommand(_ => CopyToClipboard());

            GeneratePassword();
        }

        private void GeneratePassword()
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string symbols = "!@#$%^&*()_-+=[{]};:<>|./?";

            StringBuilder characterPool = new StringBuilder();
            StringBuilder password = new StringBuilder();
            Random random = new Random();

            if (IncludeLowercase) characterPool.Append(lowercase);
            if (IncludeUppercase) characterPool.Append(uppercase);
            if (IncludeDigits) characterPool.Append(digits);
            if (IncludeSymbols) characterPool.Append(symbols);

            if (characterPool.Length == 0)
            {
                GeneratedPassword = string.Empty;
                return;
            }

            string pool = characterPool.ToString();

            if (IncludeLowercase) password.Append(lowercase[random.Next(lowercase.Length)]);
            if (IncludeUppercase) password.Append(uppercase[random.Next(uppercase.Length)]);
            if (IncludeDigits) password.Append(digits[random.Next(digits.Length)]);
            if (IncludeSymbols) password.Append(symbols[random.Next(symbols.Length)]);

            while (password.Length < PasswordLength)
            {
                password.Append(pool[random.Next(pool.Length)]);
            }

            GeneratedPassword = new string(password.ToString().OrderBy(s => random.Next()).ToArray());
        }

        private void UpdateValidation()
        {
            PasswordValidation.Password = GeneratedPassword;
            var rules = _passwordValidator.Validate(GeneratedPassword);
            _metRulesCount = rules.Count(r => r.IsMet);

            OnPropertyChanged(nameof(StrengthPiece1Opacity));
            OnPropertyChanged(nameof(StrengthPiece2Opacity));
            OnPropertyChanged(nameof(StrengthPiece3Opacity));
            OnPropertyChanged(nameof(StrengthPiece4Opacity));
            OnPropertyChanged(nameof(StrengthPiece5Opacity));
            OnPropertyChanged(nameof(StrengthPiece6Opacity));
        }

        private void CopyToClipboard()
        {
            if (string.IsNullOrEmpty(GeneratedPassword)) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(GeneratedPassword);
            Clipboard.SetContent(dataPackage);

            NativeToastHelper.SendNativeToast("Success", "Password copied to clipboard!");
        }
    }
}