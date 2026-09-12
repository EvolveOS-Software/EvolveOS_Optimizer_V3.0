// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class FileEncryptionPage : Page, IPurgeable
    {
        private string? _username;
        private SecureString? _masterPassword;

        private const string EncryptedExtension = ".evo";

        private readonly Stopwatch _overlayStopwatch = new Stopwatch();
        private const int MinOverlayDurationMs = 750;

        public FileEncryptionPage()
        {
            this.InitializeComponent();

            this.Unloaded += FileEncryptionPage_Unloaded;
        }

        private void FileEncryptionPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _ = Purge();
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

        #region Dialog Helper (Premium UX Wizard)

        private async Task<bool> ShowStepDialogAsync(string titleKey, string defaultTitle, string messageKey, string defaultMessage, string primaryBtnKey, string defaultPrimaryBtn)
        {
            if (this.XamlRoot == null) return false;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString(titleKey) ?? defaultTitle,
                Content = new TextBlock
                {
                    Text = ResourceString.GetString(messageKey) ?? defaultMessage,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = ResourceString.GetString(primaryBtnKey) ?? defaultPrimaryBtn,
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async Task ShowOpenFolderDialogAsync(string filePath)
        {
            if (this.XamlRoot == null) return;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("Encryptor_SuccessOpen_Title") ?? "Process Complete",
                Content = new TextBlock
                {
                    Text = ResourceString.GetString("Encryptor_SuccessOpen_Desc") ?? "The operation was successful. Would you like to view the output in File Explorer?",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = ResourceString.GetString("Encryptor_SuccessOpen_Btn") ?? "Open Folder",
                CloseButtonText = ResourceString.GetString("btn_close") ?? "Close",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    string argument = File.Exists(filePath) ? $"/select,\"{filePath}\"" : $"\"{filePath}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = argument,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open explorer: {ex.Message}");
                }
            }
        }

        #endregion

        #region Core Encryption/Decryption Logic (Runs on Background Threads)

        private KeyDerivationConfig GetSelectedSecurityConfig()
        {
            var mode = KeyDerivationMode.Balanced;

            if (CmbSecurityLevel.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (Enum.TryParse<KeyDerivationMode>(tag, out var parsedMode))
                {
                    mode = parsedMode;
                }
            }

            return KeyDerivationConfig.Create(mode);
        }

        private async Task ProcessFileEncryptionAsync(string sourceFilePath, string destinationFilePath, KeyDerivationConfig config)
        {
            byte[] fileData = await File.ReadAllBytesAsync(sourceFilePath);

            byte[] encryptedData = await Task.Run(() => AesHelper.EncryptBytes(fileData, _masterPassword!, config));

            await File.WriteAllBytesAsync(destinationFilePath, encryptedData);
        }

        private async Task ProcessFileDecryptionAsync(string encryptedFilePath, string destinationFilePath, KeyDerivationConfig config)
        {
            byte[] encryptedData = await File.ReadAllBytesAsync(encryptedFilePath);

            byte[] decryptedData = await Task.Run(() => AesHelper.DecryptBytes(encryptedData, _masterPassword!, config));

            await File.WriteAllBytesAsync(destinationFilePath, decryptedData);
        }

        private async Task ProcessFolderEncryptionAsync(string sourceFolderPath, string destinationFilePath, KeyDerivationConfig config)
        {
            string tempZipPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");

            try
            {
                await Task.Run(() => ZipFile.CreateFromDirectory(sourceFolderPath, tempZipPath, CompressionLevel.Optimal, false));

                byte[] zipData = await File.ReadAllBytesAsync(tempZipPath);

                byte[] encryptedData = await Task.Run(() => AesHelper.EncryptBytes(zipData, _masterPassword!, config));

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

        private async Task ProcessFolderDecryptionAsync(string encryptedFilePath, string destinationFolderPath, KeyDerivationConfig config)
        {
            string tempZipPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");

            try
            {
                byte[] encryptedData = await File.ReadAllBytesAsync(encryptedFilePath);

                byte[] decryptedZipData = await Task.Run(() => AesHelper.DecryptBytes(encryptedData, _masterPassword!, config));

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

        #region Overlay Management

        private async Task ShowLoadingOverlayAsync()
        {
            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            if (LoadingOverlayPanel.Parent is Panel parentPanel)
            {
                parentPanel.Children.Remove(LoadingOverlayPanel);
            }
            UIHelper.SetOverlay(true);
            UIHelper.ShowPopupOverlay(LoadingOverlayPanel);

            _overlayStopwatch.Restart();

            await Task.Delay(50);
        }

        private async Task HideLoadingOverlayAsync()
        {
            if (_overlayStopwatch.IsRunning)
            {
                long elapsed = _overlayStopwatch.ElapsedMilliseconds;
                if (elapsed < MinOverlayDurationMs)
                {
                    await Task.Delay(MinOverlayDurationMs - (int)elapsed);
                }
                _overlayStopwatch.Stop();
            }

            UIHelper.HidePopupOverlay();
            UIHelper.SetOverlay(false);
            if (LoadingOverlayPanel.Parent == null)
            {
                OverlayHost.Children.Add(LoadingOverlayPanel);
            }
        }

        #endregion

        #region UI Button Handlers (Pickers & Execution)

        private async void BtnEncryptFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep1 = await ShowStepDialogAsync(
                    "Encryptor_Wizard_EncFileTitle", "Step 1: Select File",
                    "Encryptor_Wizard_EncFileDesc", "Choose the file you want to securely encrypt with your Master Password.",
                    "Encryptor_Wizard_EncFileBtn", "Select File");
                if (!continueStep1) return;

                string openTitle = ResourceString.GetString("FileEncryptor_OpenFile_Title") ?? "Select a File to Encrypt";
                string? fileToEncryptPath = Win32FileDialogHelper.ShowOpenFilePicker(App.MainWindow!, openTitle, "All Files", "*.*");
                if (string.IsNullOrEmpty(fileToEncryptPath)) return;

                bool continueStep2 = await ShowStepDialogAsync(
                    "Encryptor_Wizard_EncSaveTitle", "Step 2: Save Encrypted File",
                    "Encryptor_Wizard_EncSaveDesc", "Choose where to save your newly encrypted .evo file.",
                    "Encryptor_Wizard_EncSaveBtn", "Choose Save Location");
                if (!continueStep2) return;

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

                await ShowLoadingOverlayAsync();

                KeyDerivationConfig cryptoConfig = GetSelectedSecurityConfig();

                await ProcessFileEncryptionAsync(fileToEncryptPath, destinationFilePath, cryptoConfig);

                await HideLoadingOverlayAsync();

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileEncryptor_Toast_FileEncryptSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "File encrypted successfully." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();

                await ShowOpenFolderDialogAsync(destinationFilePath);
            }
            catch (Exception ex)
            {
                await HideLoadingOverlayAsync();

                string errorTitle = ResourceString.GetString("FileEncryptor_Toast_EncryptionErrorTitle");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Encryption Error" : errorTitle, ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            finally
            {
                UIHelper.HidePopupOverlay();
                UIHelper.SetOverlay(false);
                if (LoadingOverlayPanel.Parent == null) OverlayHost.Children.Add(LoadingOverlayPanel);

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
                bool continueStep1 = await ShowStepDialogAsync(
                    "Encryptor_Wizard_EncFolderTitle", "Step 1: Select Folder",
                    "Encryptor_Wizard_EncFolderDesc", "Choose the folder you want to compress and securely encrypt.",
                    "Encryptor_Wizard_EncFolderBtn", "Select Folder");
                if (!continueStep1) return;

                string folderTitle = ResourceString.GetString("FileEncryptor_SelectFolder_Title") ?? "Select Folder to Encrypt";
                string? folderToEncryptPath = Win32FileDialogHelper.ShowFolderPicker(App.MainWindow!, folderTitle);
                if (string.IsNullOrEmpty(folderToEncryptPath)) return;

                bool continueStep2 = await ShowStepDialogAsync(
                    "Encryptor_Wizard_EncFolderSaveTitle", "Step 2: Save Encrypted Archive",
                    "Encryptor_Wizard_EncFolderSaveDesc", "Choose where to save your encrypted .evo folder archive.",
                    "Encryptor_Wizard_EncFolderSaveBtn", "Choose Save Location");
                if (!continueStep2) return;

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

                await ShowLoadingOverlayAsync();

                KeyDerivationConfig cryptoConfig = GetSelectedSecurityConfig();

                await ProcessFolderEncryptionAsync(folderToEncryptPath, destinationFilePath, cryptoConfig);

                await HideLoadingOverlayAsync();

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileEncryptor_Toast_FolderEncryptSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "Folder encrypted successfully." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();

                await ShowOpenFolderDialogAsync(destinationFilePath);
            }
            catch (Exception ex)
            {
                await HideLoadingOverlayAsync();

                string errorTitle = ResourceString.GetString("FileEncryptor_Toast_EncryptionErrorTitle");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Encryption Error" : errorTitle, ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            finally
            {
                UIHelper.HidePopupOverlay();
                UIHelper.SetOverlay(false);
                if (LoadingOverlayPanel.Parent == null) OverlayHost.Children.Add(LoadingOverlayPanel);

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
                bool continueStep1 = await ShowStepDialogAsync(
                    "Encryptor_Wizard_DecTitle", "Step 1: Select Encrypted File",
                    "Encryptor_Wizard_DecDesc", "Choose the .evo file you want to decrypt.",
                    "Encryptor_Wizard_DecBtn", "Select Encrypted File");
                if (!continueStep1) return;

                string openTitle = ResourceString.GetString("FileEncryptor_OpenDecrypt_Title") ?? "Select Encrypted File";
                string? fileToDecryptPath = Win32FileDialogHelper.ShowOpenFilePicker(App.MainWindow!, openTitle, "EvolveOS Encrypted File", "*" + EncryptedExtension);
                if (string.IsNullOrEmpty(fileToDecryptPath)) return;

                string originalName = Path.GetFileName(fileToDecryptPath).Replace(EncryptedExtension, "");
                bool isFolderArchive = !Path.HasExtension(originalName);

                string outputFilePath = string.Empty;
                KeyDerivationConfig cryptoConfig = GetSelectedSecurityConfig();

                if (isFolderArchive)
                {
                    bool continueStep2 = await ShowStepDialogAsync(
                        "Encryptor_Wizard_DecFolderTitle", "Step 2: Select Extraction Folder",
                        "Encryptor_Wizard_DecFolderDesc", "Choose where you want to extract the decrypted folder contents.",
                        "Encryptor_Wizard_DecFolderBtn", "Select Destination");
                    if (!continueStep2) return;

                    string folderTitle = ResourceString.GetString("FileEncryptor_SelectDestFolder_Title") ?? "Select Destination Folder";
                    string? destFolderPath = Win32FileDialogHelper.ShowFolderPicker(App.MainWindow!, folderTitle);

                    if (string.IsNullOrEmpty(destFolderPath)) return;

                    outputFilePath = destFolderPath;

                    await ShowLoadingOverlayAsync();

                    await ProcessFolderDecryptionAsync(fileToDecryptPath, destFolderPath, cryptoConfig);
                }
                else
                {
                    bool continueStep2 = await ShowStepDialogAsync(
                        "Encryptor_Wizard_DecSaveTitle", "Step 2: Save Decrypted File",
                        "Encryptor_Wizard_DecSaveDesc", "Choose where you want to save the original, decrypted file.",
                        "Encryptor_Wizard_DecSaveBtn", "Choose Save Location");
                    if (!continueStep2) return;

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

                    if (string.IsNullOrEmpty(destFilePath)) return;

                    outputFilePath = destFilePath;

                    await ShowLoadingOverlayAsync();

                    await ProcessFileDecryptionAsync(fileToDecryptPath, destFilePath, cryptoConfig);
                }

                await HideLoadingOverlayAsync();

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileEncryptor_Toast_DecryptSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "Decrypted successfully." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();

                await ShowOpenFolderDialogAsync(outputFilePath);
            }
            catch (CryptographicException)
            {
                await HideLoadingOverlayAsync();

                string failTitle = ResourceString.GetString("FileEncryptor_Toast_DecryptFailTitle");
                string failMsg = ResourceString.GetString("FileEncryptor_Toast_DecryptFailMsg");

                NotificationManager.Show(string.IsNullOrEmpty(failTitle) ? "Decryption Failed" : failTitle,
                                         string.IsNullOrEmpty(failMsg) ? "The password or security level is incorrect, or the file has been tampered with." : failMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            catch (Exception ex)
            {
                await HideLoadingOverlayAsync();

                string errorTitle = ResourceString.GetString("Toast_Error_Title");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Error" : errorTitle, ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
            finally
            {
                UIHelper.HidePopupOverlay();
                UIHelper.SetOverlay(false);
                if (LoadingOverlayPanel.Parent == null) OverlayHost.Children.Add(LoadingOverlayPanel);

                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            }
        }

        #endregion

        #region Navigation

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                if (this.Frame.CanGoBack)
                {
                    this.Frame.GoBack();
                }
                else
                {
                    this.Frame.Navigate(typeof(AdvancedUtilsPage));
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _masterPassword?.Dispose();
        }

        #endregion

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            _overlayStopwatch.Stop();

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Secure Credentials...");

                this.Unloaded -= FileEncryptionPage_Unloaded;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);

                    var tcs = new TaskCompletionSource();
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (this.DataContext is IDisposable disposableVm) disposableVm.Dispose();

                        _username = null;
                        if (_masterPassword != null)
                        {
                            _masterPassword.Dispose();
                            _masterPassword = null;
                        }

                        // this.Bindings?.StopTracking();
                        this.DataContext = null;
                        this.Content = null;

                        tcs.SetResult();
                    });

                    await tcs.Task;

                    DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();

                    App.MemoryGuardian?.ForcePageTransitionCleanup();
                });
            }
            else
            {
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
            }

            return Task.CompletedTask;
        }
        #endregion
    }
}