// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class FileEncryptionPage : Page
    {
        private string? _username;
        private SecureString? _masterPassword;

        private const string EncryptedExtension = ".evo";

        public FileEncryptionPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ValueTuple<string, SecureString> navParams)
            {
                _username = navParams.Item1;
                _masterPassword = navParams.Item2;
            }
        }

        #region Core Encryption/Decryption Logic (Runs on Background Threads)

        private async Task ProcessFileEncryptionAsync(string sourceFilePath, string destinationFilePath)
        {
            byte[] fileData = await File.ReadAllBytesAsync(sourceFilePath);

            byte[] encryptedData = await Task.Run(() => AesHelper.EncryptBytes(fileData, _masterPassword!));

            await File.WriteAllBytesAsync(destinationFilePath, encryptedData);
        }

        private async Task ProcessFileDecryptionAsync(string encryptedFilePath, string destinationFilePath)
        {
            byte[] encryptedData = await File.ReadAllBytesAsync(encryptedFilePath);

            byte[] decryptedData = await Task.Run(() => AesHelper.DecryptBytes(encryptedData, _masterPassword!));

            await File.WriteAllBytesAsync(destinationFilePath, decryptedData);
        }

        private async Task ProcessFolderEncryptionAsync(string sourceFolderPath, string destinationFilePath)
        {
            string tempZipPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");

            try
            {
                await Task.Run(() => ZipFile.CreateFromDirectory(sourceFolderPath, tempZipPath, CompressionLevel.Optimal, false));

                byte[] zipData = await File.ReadAllBytesAsync(tempZipPath);

                byte[] encryptedData = await Task.Run(() => AesHelper.EncryptBytes(zipData, _masterPassword!));

                await File.WriteAllBytesAsync(destinationFilePath, encryptedData);
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
        }

        private async Task ProcessFolderDecryptionAsync(string encryptedFilePath, string destinationFolderPath)
        {
            string tempZipPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");

            try
            {
                byte[] encryptedData = await File.ReadAllBytesAsync(encryptedFilePath);

                byte[] decryptedZipData = await Task.Run(() => AesHelper.DecryptBytes(encryptedData, _masterPassword!));

                await File.WriteAllBytesAsync(tempZipPath, decryptedZipData);

                await Task.Run(() => ZipFile.ExtractToDirectory(tempZipPath, destinationFolderPath, true));
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
        }

        #endregion

        #region UI Button Handlers (Pickers & Execution)

        private async void BtnEncryptFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

                var openPicker = new FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
                openPicker.FileTypeFilter.Add("*");

                var fileToEncrypt = await openPicker.PickSingleFileAsync();
                if (fileToEncrypt == null) return;

                var savePicker = new FileSavePicker();
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

                string encryptedFileType = ResourceString.GetString("FileEncryptor_FileType_Encrypted");
                savePicker.FileTypeChoices.Add(string.IsNullOrEmpty(encryptedFileType) ? "EvolveOS Encrypted File" : encryptedFileType, new[] { EncryptedExtension });
                savePicker.SuggestedFileName = fileToEncrypt.Name + EncryptedExtension;

                var destinationFile = await savePicker.PickSaveFileAsync();
                if (destinationFile == null) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await ProcessFileEncryptionAsync(fileToEncrypt.Path, destinationFile.Path);

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileEncryptor_Toast_FileEncryptSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "File encrypted successfully." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();
            }
            catch (Exception ex)
            {
                string errorTitle = ResourceString.GetString("FileEncryptor_Toast_EncryptionErrorTitle");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Encryption Error" : errorTitle, ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UIHelper.SetOverlay(false);

                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            }
        }

        private async void BtnDecrypt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

                var openPicker = new FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
                openPicker.FileTypeFilter.Add(EncryptedExtension);

                var fileToDecrypt = await openPicker.PickSingleFileAsync();
                if (fileToDecrypt == null) return;

                string originalName = fileToDecrypt.Name.Replace(EncryptedExtension, "");
                bool isFolderArchive = !Path.HasExtension(originalName);

                UIHelper.SetOverlay(true);

                if (isFolderArchive)
                {
                    var folderPicker = new FolderPicker();
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                    folderPicker.FileTypeFilter.Add("*");

                    var destFolder = await folderPicker.PickSingleFolderAsync();

                    if (destFolder == null)
                    {
                        UIHelper.SetOverlay(false);
                        return;
                    }

                    EfficiencyModeHelper.IsUIWakeLockActive = true;
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                    LoadingOverlay.Visibility = Visibility.Visible;
                    await ProcessFolderDecryptionAsync(fileToDecrypt.Path, destFolder.Path);
                }
                else
                {
                    var savePicker = new FileSavePicker();
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                    string originalExtension = Path.GetExtension(originalName);

                    string originalFileType = ResourceString.GetString("FileEncryptor_FileType_Original");
                    savePicker.FileTypeChoices.Add(string.IsNullOrEmpty(originalFileType) ? "Original File" : originalFileType, new[] { originalExtension });
                    savePicker.SuggestedFileName = originalName;

                    var destFile = await savePicker.PickSaveFileAsync();

                    if (destFile == null)
                    {
                        UIHelper.SetOverlay(false);
                        return;
                    }

                    LoadingOverlay.Visibility = Visibility.Visible;
                    await ProcessFileDecryptionAsync(fileToDecrypt.Path, destFile.Path);
                }

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileEncryptor_Toast_DecryptSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "Decrypted successfully." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();
            }
            catch (CryptographicException)
            {
                string failTitle = ResourceString.GetString("FileEncryptor_Toast_DecryptFailTitle");
                string failMsg = ResourceString.GetString("FileEncryptor_Toast_DecryptFailMsg");

                NotificationManager.Show(string.IsNullOrEmpty(failTitle) ? "Decryption Failed" : failTitle,
                                         string.IsNullOrEmpty(failMsg) ? "The password is incorrect or the file has been tampered with." : failMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            catch (Exception ex)
            {
                string errorTitle = ResourceString.GetString("Toast_Error_Title");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Error" : errorTitle, ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UIHelper.SetOverlay(false);

                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            }
        }

        private async void BtnEncryptFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

                var folderPicker = new FolderPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                folderPicker.FileTypeFilter.Add("*");

                var folderToEncrypt = await folderPicker.PickSingleFolderAsync();
                if (folderToEncrypt == null) return;

                var savePicker = new FileSavePicker();
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                string encryptedFolderType = ResourceString.GetString("FileEncryptor_FileType_EncryptedFolder");
                savePicker.FileTypeChoices.Add(string.IsNullOrEmpty(encryptedFolderType) ? "EvolveOS Encrypted Folder" : encryptedFolderType, new[] { EncryptedExtension });
                savePicker.SuggestedFileName = folderToEncrypt.Name + "_Archive" + EncryptedExtension;

                var destinationFile = await savePicker.PickSaveFileAsync();
                if (destinationFile == null) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await ProcessFolderEncryptionAsync(folderToEncrypt.Path, destinationFile.Path);

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileEncryptor_Toast_FolderEncryptSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "Folder encrypted successfully." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();
            }
            catch (Exception ex)
            {
                string errorTitle = ResourceString.GetString("FileEncryptor_Toast_EncryptionErrorTitle");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Encryption Error" : errorTitle, ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UIHelper.SetOverlay(false);

                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            }
        }

        #region Navigation

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null && this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        #endregion

        #endregion

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _masterPassword?.Dispose();
        }
    }
}