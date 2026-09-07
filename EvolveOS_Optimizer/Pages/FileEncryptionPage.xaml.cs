// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;

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
                string openTitle = ResourceString.GetString("FileEncryptor_OpenFile_Title") ?? "Select a File to Encrypt";
                string? fileToEncryptPath = Win32FileDialogHelper.ShowOpenFilePicker(App.MainWindow!, openTitle, "All Files", "*.*");

                if (string.IsNullOrEmpty(fileToEncryptPath)) return;

                string encryptedFileType = ResourceString.GetString("FileEncryptor_FileType_Encrypted") ?? "EvolveOS Encrypted File";
                string saveTitle = ResourceString.GetString("FileEncryptor_SaveFile_Title") ?? "Save Encrypted File";
                string suggestedName = Path.GetFileName(fileToEncryptPath) + EncryptedExtension;

                string? destinationFilePath = Win32FileDialogHelper.ShowSaveFilePicker(
                    App.MainWindow!,
                    saveTitle,
                    encryptedFileType,
                    "*" + EncryptedExtension,
                    suggestedName,
                    EncryptedExtension);

                if (string.IsNullOrEmpty(destinationFilePath)) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await ProcessFileEncryptionAsync(fileToEncryptPath, destinationFilePath);

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
                string openTitle = ResourceString.GetString("FileEncryptor_OpenDecrypt_Title") ?? "Select Encrypted File";
                string? fileToDecryptPath = Win32FileDialogHelper.ShowOpenFilePicker(App.MainWindow!, openTitle, "EvolveOS Encrypted File", "*" + EncryptedExtension);

                if (string.IsNullOrEmpty(fileToDecryptPath)) return;

                string originalName = Path.GetFileName(fileToDecryptPath).Replace(EncryptedExtension, "");
                bool isFolderArchive = !Path.HasExtension(originalName);

                UIHelper.SetOverlay(true);

                if (isFolderArchive)
                {
                    string folderTitle = ResourceString.GetString("FileEncryptor_SelectDestFolder_Title") ?? "Select Destination Folder";
                    string? destFolderPath = Win32FileDialogHelper.ShowFolderPicker(App.MainWindow!, folderTitle);

                    if (string.IsNullOrEmpty(destFolderPath))
                    {
                        UIHelper.SetOverlay(false);
                        return;
                    }

                    EfficiencyModeHelper.IsUIWakeLockActive = true;
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                    LoadingOverlay.Visibility = Visibility.Visible;
                    await ProcessFolderDecryptionAsync(fileToDecryptPath, destFolderPath);
                }
                else
                {
                    string originalExtension = Path.GetExtension(originalName);
                    if (string.IsNullOrEmpty(originalExtension)) originalExtension = ".*";

                    string originalFileType = ResourceString.GetString("FileEncryptor_FileType_Original") ?? "Original File";
                    string saveTitle = ResourceString.GetString("FileEncryptor_SaveDecrypt_Title") ?? "Save Decrypted File";

                    string? destFilePath = Win32FileDialogHelper.ShowSaveFilePicker(
                        App.MainWindow!,
                        saveTitle,
                        originalFileType,
                        "*" + originalExtension,
                        originalName,
                        originalExtension);

                    if (string.IsNullOrEmpty(destFilePath))
                    {
                        UIHelper.SetOverlay(false);
                        return;
                    }

                    EfficiencyModeHelper.IsUIWakeLockActive = true;
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                    LoadingOverlay.Visibility = Visibility.Visible;
                    await ProcessFileDecryptionAsync(fileToDecryptPath, destFilePath);
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
                string folderTitle = ResourceString.GetString("FileEncryptor_SelectFolder_Title") ?? "Select Folder to Encrypt";
                string? folderToEncryptPath = Win32FileDialogHelper.ShowFolderPicker(App.MainWindow!, folderTitle);

                if (string.IsNullOrEmpty(folderToEncryptPath)) return;

                string folderName = new DirectoryInfo(folderToEncryptPath).Name;
                string encryptedFolderType = ResourceString.GetString("FileEncryptor_FileType_EncryptedFolder") ?? "EvolveOS Encrypted Folder";
                string saveTitle = ResourceString.GetString("FileEncryptor_SaveFolder_Title") ?? "Save Encrypted Folder Archive";
                string suggestedName = folderName + "_Archive" + EncryptedExtension;

                string? destinationFilePath = Win32FileDialogHelper.ShowSaveFilePicker(
                    App.MainWindow!,
                    saveTitle,
                    encryptedFolderType,
                    "*" + EncryptedExtension,
                    suggestedName,
                    EncryptedExtension);

                if (string.IsNullOrEmpty(destinationFilePath)) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await ProcessFolderEncryptionAsync(folderToEncryptPath, destinationFilePath);

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