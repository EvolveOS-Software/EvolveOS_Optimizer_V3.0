// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Security;
using System.Security.Cryptography;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SecureFileShredderPage : Page
    {
        private string? _username;
        private SecureString? _masterPassword;

        private const int DefaultShredPasses = 3;

        public SecureFileShredderPage()
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

        #endregion

        #region Core Shredding Logic (Runs on Background Threads)

        private async Task ProcessFileShreddingAsync(string filePath, int passes)
        {
            if (!File.Exists(filePath)) return;

            var fileInfo = new FileInfo(filePath);
            long length = fileInfo.Length;

            await Task.Run(() =>
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[4096];

                    for (int pass = 0; pass < passes; pass++)
                    {
                        fs.Position = 0;
                        long bytesWritten = 0;

                        while (bytesWritten < length)
                        {
                            RandomNumberGenerator.Fill(buffer);
                            int toWrite = (int)Math.Min(buffer.Length, length - bytesWritten);
                            fs.Write(buffer, 0, toWrite);
                            bytesWritten += toWrite;
                        }
                        fs.Flush();
                    }
                }

                string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                string scrambledName = Path.Combine(directory, Guid.NewGuid().ToString() + ".tmp");
                File.Move(filePath, scrambledName, true);

                File.Delete(scrambledName);
            });
        }

        private async Task ProcessFolderShreddingAsync(string folderPath, int passes)
        {
            if (!Directory.Exists(folderPath)) return;

            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                await ProcessFileShreddingAsync(file, passes);
            }

            await Task.Run(() => Directory.Delete(folderPath, true));
        }

        #endregion

        #region UI Button Handlers

        private async void BtnShredFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep = await ShowStepDialogAsync(
                    "Shredder_Wizard_FileTitle", "Step 1: Select File to Destroy",
                    "Shredder_Wizard_FileDesc", "Choose the individual file you want to permanently wipe. WARNING: This action cannot be undone.",
                    "Shredder_Wizard_FileBtn", "Select File");
                if (!continueStep) return;

                string title = ResourceString.GetString("FileShredder_FilePicker_Title") ?? "Select a File to Shred";

                string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
                    App.MainWindow!,
                    title,
                    "All Files",
                    "*.*"
                );

                if (string.IsNullOrEmpty(filePath)) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await ProcessFileShreddingAsync(filePath, DefaultShredPasses);

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileShredder_Toast_FileShredSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "File securely shredded and destroyed." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();
            }
            catch (Exception ex)
            {
                string errorTitle = ResourceString.GetString("Toast_Error_Title");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Shredding Error" : errorTitle, ex.Message)
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

        private async void BtnShredFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep = await ShowStepDialogAsync(
                    "Shredder_Wizard_FolderTitle", "Step 1: Select Folder to Destroy",
                    "Shredder_Wizard_FolderDesc", "Choose the directory you want to completely wipe. All files and subfolders inside will be permanently destroyed.",
                    "Shredder_Wizard_FolderBtn", "Select Folder");
                if (!continueStep) return;

                string title = ResourceString.GetString("FileShredder_FolderPicker_Title") ?? "Select a Folder to Shred";

                string? folderPath = Win32FileDialogHelper.ShowFolderPicker(
                    App.MainWindow!,
                    title
                );

                if (string.IsNullOrEmpty(folderPath)) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await ProcessFolderShreddingAsync(folderPath, DefaultShredPasses);

                string successTitle = ResourceString.GetString("Toast_Success_Title");
                string successMsg = ResourceString.GetString("FileShredder_Toast_FolderShredSuccess");

                NotificationManager.Show(string.IsNullOrEmpty(successTitle) ? "Success" : successTitle,
                                         string.IsNullOrEmpty(successMsg) ? "Folder securely shredded and destroyed." : successMsg)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();
            }
            catch (Exception ex)
            {
                string errorTitle = ResourceString.GetString("Toast_Error_Title");
                NotificationManager.Show(string.IsNullOrEmpty(errorTitle) ? "Shredding Error" : errorTitle, ex.Message)
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

        #endregion

        #region Navigation

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(AdvancedUtilsPage));

                this.Frame.BackStack.Clear();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _masterPassword?.Dispose();
        }

        #endregion
    }
}