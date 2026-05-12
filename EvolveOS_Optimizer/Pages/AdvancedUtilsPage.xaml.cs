// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Security;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class AdvancedUtilsPage : Page, IPurgeable
    {
        public AdvancedUtilsViewModel ViewModel { get; }

        public AdvancedUtilsPage()
        {
            this.InitializeComponent();

            // BLUEPRINT: Adjustable Cache Mode
            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            ViewModel = new AdvancedUtilsViewModel();
            this.DataContext = ViewModel;

            this.Unloaded += AdvancedUtilsPage_Unloaded;
        }

        private void AdvancedUtilsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }

        private async void BtnOpenPasswordManager_Click(object sender, RoutedEventArgs e)
        {
            if (this.XamlRoot == null) return;

            var passwordBox = new PasswordBox
            {
                PlaceholderText = ResourceString.GetString("tag_password") ?? "Enter your master password...",
                Width = 300,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var errorTextBlock = new TextBlock
            {
                Text = ResourceString.GetString("notif_wrong_password") ?? "The password is incorrect. Please try again.",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 12
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = ResourceString.GetString("lbl_login_subtitle") ?? "Enter your credentials to continue.",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(passwordBox);
            panel.Children.Add(errorTextBlock);

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("lbl_login_title") ?? "Password Manager Login",
                Content = panel,
                PrimaryButtonText = ResourceString.GetString("btn_login_securely") ?? "Login Securely",
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                args.Cancel = true;

                string plainPassword = passwordBox.Password;

                if (string.IsNullOrWhiteSpace(plainPassword))
                {
                    FactoryAnimation.AnimateErrorShake(passwordBox);
                    errorTextBlock.Visibility = Visibility.Visible;
                    return;
                }

                passwordBox.IsEnabled = false;

                try
                {
                    var userDataAccess = new UserDataAccess(SqlConnectionHelper.connectReturn());
                    var loginData = await userDataAccess.GetPasswordAndImageAsync(UserSession.Username!);

                    bool isVerified = loginData.PasswordHash != null && BCrypt.Net.BCrypt.Verify(plainPassword, loginData.PasswordHash);

                    passwordBox.IsEnabled = true;

                    if (isVerified)
                    {
                        dialog.Hide();

                        SecureString masterSecurePassword = new System.Net.NetworkCredential("", plainPassword).SecurePassword;

                        var navParams = (Username: UserSession.Username!, MasterPassword: masterSecurePassword);

                        this.Frame.Navigate(typeof(PasswordManagerPage), navParams);
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
                    dialog.Hide();
                    NativeToastHelper.SendNativeToast("Application Error", ex.Message);
                }
            };

            await dialog.ShowAsync();
        }

        private async void BtnOpenFileEncryptor_Click(object sender, RoutedEventArgs e)
        {
            if (this.XamlRoot == null) return;

            var passwordBox = new PasswordBox
            {
                PlaceholderText = ResourceString.GetString("tag_password") ?? "Enter your master password...",
                Width = 300,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var errorTextBlock = new TextBlock
            {
                Text = ResourceString.GetString("notif_wrong_password") ?? "The password is incorrect. Please try again.",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 12
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = ResourceString.GetString("lbl_login_encryptor") ?? "Enter your credentials to access the File Encryptor.",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(passwordBox);
            panel.Children.Add(errorTextBlock);

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("lbl_encryptor_title") ?? "File Encryptor Login",
                Content = panel,
                PrimaryButtonText = ResourceString.GetString("btn_login_securely") ?? "Login Securely",
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                args.Cancel = true;

                string plainPassword = passwordBox.Password;

                if (string.IsNullOrWhiteSpace(plainPassword))
                {
                    FactoryAnimation.AnimateErrorShake(passwordBox);
                    errorTextBlock.Visibility = Visibility.Visible;
                    return;
                }

                passwordBox.IsEnabled = false;

                try
                {
                    var userDataAccess = new UserDataAccess(SqlConnectionHelper.connectReturn());
                    var loginData = await userDataAccess.GetPasswordAndImageAsync(UserSession.Username!);

                    bool isVerified = loginData.PasswordHash != null && BCrypt.Net.BCrypt.Verify(plainPassword, loginData.PasswordHash);

                    passwordBox.IsEnabled = true;

                    if (isVerified)
                    {
                        dialog.Hide();

                        SecureString masterSecurePassword = new System.Net.NetworkCredential("", plainPassword).SecurePassword;

                        var navParams = (Username: UserSession.Username!, MasterPassword: masterSecurePassword);

                        this.Frame.Navigate(typeof(FileEncryptionPage), navParams);
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
                    dialog.Hide();
                    NativeToastHelper.SendNativeToast("Application Error", ex.Message);
                }
            };

            await dialog.ShowAsync();
        }

        #region Purge Page
        public void Purge()
        {
            Debug.WriteLine("[AdvancedUtilsPage] Caching Purge requested. Pausing UI...");

            Debug.WriteLine("[AdvancedUtilsPage] Page preserved in cache.");
        }
        #endregion
    }
}