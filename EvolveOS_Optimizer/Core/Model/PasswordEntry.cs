// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.Model
{
    public class PasswordEntry : INotifyPropertyChanged
    {
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public string? Id { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }

        public string EncryptedPassword { get; set; } = string.Empty;

        private SecureString? _masterPassword;
        private KeyDerivationConfig? _localKeyDerivationConfig;

        private string _displayedPassword = "••••••••";
        private bool _isPasswordRevealed = false;

        public string DisplayedPassword
        {
            get => _displayedPassword;
            set { _displayedPassword = value; OnPropertyChanged(); }
        }

        public bool IsPasswordRevealed
        {
            get => _isPasswordRevealed;
            set { _isPasswordRevealed = value; OnPropertyChanged(); }
        }

        public void SetMasterPassword(SecureString masterPassword, KeyDerivationConfig config)
        {
            _masterPassword = masterPassword;
            _localKeyDerivationConfig = config;

            if (IsPasswordRevealed)
            {
                TogglePasswordVisibility();
            }
        }

        public void SetHiddenState()
        {
            _displayedPassword = "••••••••";
            _isPasswordRevealed = false;

            OnPropertyChanged(nameof(DisplayedPassword));
            OnPropertyChanged(nameof(IsPasswordRevealed));
        }

        public void TogglePasswordVisibility()
        {
            if (_masterPassword == null || _localKeyDerivationConfig == null)
            {
                DisplayedPassword = "Error: Key Missing";
                IsPasswordRevealed = false;
                return;
            }

            if (IsPasswordRevealed)
            {
                DisplayedPassword = "••••••••";
                IsPasswordRevealed = false;
            }
            else
            {
                try
                {
                    DisplayedPassword = AesHelper.Decrypt(
                        EncryptedPassword,
                        _masterPassword,
                        _localKeyDerivationConfig
                    );

                    IsPasswordRevealed = (DisplayedPassword != "Error: Decryption Failed");
                }
                catch (CryptographicException)
                {
                    DisplayedPassword = "Error: Decryption Failed";
                    IsPasswordRevealed = true;
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Toggle Decryption General Error: {ex.Message}");
                    DisplayedPassword = "Error: Decryption Failed";
                    IsPasswordRevealed = true;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}