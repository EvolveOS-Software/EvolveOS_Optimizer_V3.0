// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SteganographyPage : Page
    {
        private string? _username;
        private SecureString? _masterPassword;

        public SteganographyPage()
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

        private async Task ShowOpenFolderDialogAsync(string filePath)
        {
            if (this.XamlRoot == null) return;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("Stego_SuccessOpen_Title") ?? "Process Complete",
                Content = new TextBlock
                {
                    Text = ResourceString.GetString("Stego_SuccessOpen_Desc") ?? "The vault file has been successfully generated and secured. Would you like to view it in File Explorer?",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = ResourceString.GetString("Stego_SuccessOpen_Btn") ?? "Open Folder",
                CloseButtonText = ResourceString.GetString("btn_close") ?? "Close",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{filePath}\"",
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

        #region Core Steganography Logic (LSB + AES Encryption + Hardware Optimization)

        private KeyDerivationConfig GetSelectedSecurityConfig()
        {
            var mode = KeyDerivationMode.Balanced; // Default

            if (CmbSecurityLevel.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (Enum.TryParse<KeyDerivationMode>(tag, out var parsedMode))
                {
                    mode = parsedMode;
                }
            }

            return KeyDerivationConfig.Create(mode);
        }

        private async Task ProcessEmbedAsync(string hostImagePath, string secretFilePath, string destImagePath, KeyDerivationConfig cryptoConfig, IProgress<string> progress)
        {
            progress.Report(ResourceString.GetString("Stego_Status_Prep") ?? "Preparing payload headers...");
            string fileName = Path.GetFileName(secretFilePath);
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);
            byte[] fileBytes = await File.ReadAllBytesAsync(secretFilePath);

            byte[] rawData;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(nameBytes.Length);
                bw.Write(nameBytes);
                bw.Write(fileBytes);
                rawData = ms.ToArray();
            }

            Array.Clear(fileBytes, 0, fileBytes.Length);
            Array.Clear(nameBytes, 0, nameBytes.Length);

            progress.Report(ResourceString.GetString("Stego_Status_Encrypt") ?? "Encrypting payload (AES-256)...");

            byte[] encryptedData = await Task.Run(() => AesHelper.EncryptBytes(rawData, _masterPassword!, cryptoConfig));

            Array.Clear(rawData, 0, rawData.Length);

            progress.Report(ResourceString.GetString("Stego_Status_Analyze") ?? "Analyzing host image capacity...");
            StorageFile hostFile = await StorageFile.GetFileFromPathAsync(hostImagePath);
            using IRandomAccessStream stream = await hostFile.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            var pixelDataProvider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            byte[] pixels = pixelDataProvider.DetachPixelData();

            int requiredBytes = 32 + (encryptedData.Length * 8);
            if (requiredBytes > pixels.Length)
            {
                double maxKb = (pixels.Length / 8.0) / 1024.0;
                double reqKb = encryptedData.Length / 1024.0;
                Array.Clear(encryptedData, 0, encryptedData.Length);
                Array.Clear(pixels, 0, pixels.Length);

                throw new Exception($"Host image capacity exceeded.\n\nAvailable: {maxKb:F2} KB\nRequired: {reqKb:F2} KB\n\nPlease select a higher-resolution image or a smaller secret file.");
            }

            await Task.Run(() =>
            {
                int length = encryptedData.Length;
                int pixelIndex = 0;
                int lastReportedPercent = -1;
                int reportInterval = Math.Max(1, length / 100);

                for (int i = 0; i < 32; i++)
                {
                    int bit = (length >> i) & 1;
                    pixels[pixelIndex] = (byte)((pixels[pixelIndex] & 254) | bit);
                    pixelIndex++;
                }

                for (int i = 0; i < length; i++)
                {
                    byte b = encryptedData[i];

                    pixels[pixelIndex] = (byte)((pixels[pixelIndex] & 254) | (b & 1));
                    pixels[pixelIndex + 1] = (byte)((pixels[pixelIndex + 1] & 254) | ((b >> 1) & 1));
                    pixels[pixelIndex + 2] = (byte)((pixels[pixelIndex + 2] & 254) | ((b >> 2) & 1));
                    pixels[pixelIndex + 3] = (byte)((pixels[pixelIndex + 3] & 254) | ((b >> 3) & 1));
                    pixels[pixelIndex + 4] = (byte)((pixels[pixelIndex + 4] & 254) | ((b >> 4) & 1));
                    pixels[pixelIndex + 5] = (byte)((pixels[pixelIndex + 5] & 254) | ((b >> 5) & 1));
                    pixels[pixelIndex + 6] = (byte)((pixels[pixelIndex + 6] & 254) | ((b >> 6) & 1));
                    pixels[pixelIndex + 7] = (byte)((pixels[pixelIndex + 7] & 254) | ((b >> 7) & 1));

                    pixelIndex += 8;

                    if (i % reportInterval == 0)
                    {
                        int currentPercent = (int)((double)i / length * 100);
                        if (currentPercent != lastReportedPercent)
                        {
                            lastReportedPercent = currentPercent;
                            string localizedInject = ResourceString.GetString("Stego_Status_Inject") ?? "Injecting cryptographic payload";
                            progress.Report($"{localizedInject}... {currentPercent}%");
                        }
                    }
                }
            });

            progress.Report(ResourceString.GetString("Stego_Status_Encode") ?? "Rendering lossless vault image...");
            using var memStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memStream);

            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                decoder.PixelWidth,
                decoder.PixelHeight,
                decoder.DpiX,
                decoder.DpiY,
                pixels);

            await encoder.FlushAsync();

            using var dataReader = new DataReader(memStream.GetInputStreamAt(0));
            await dataReader.LoadAsync((uint)memStream.Size);
            byte[] outBytes = new byte[memStream.Size];
            dataReader.ReadBytes(outBytes);

            progress.Report(ResourceString.GetString("Stego_Status_Save") ?? "Writing to disk...");
            await File.WriteAllBytesAsync(destImagePath, outBytes);

            Array.Clear(encryptedData, 0, encryptedData.Length);
            Array.Clear(pixels, 0, pixels.Length);
            Array.Clear(outBytes, 0, outBytes.Length);
        }

        private async Task<(string OriginalFileName, byte[] FileBytes)> DecodeEncryptedPayloadAsync(string stegoImagePath, KeyDerivationConfig cryptoConfig, IProgress<string> progress)
        {
            progress.Report(ResourceString.GetString("Stego_Status_ReadImg") ?? "Reading image matrix...");
            StorageFile stegoFile = await StorageFile.GetFileFromPathAsync(stegoImagePath);
            using IRandomAccessStream stream = await stegoFile.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            var pixelDataProvider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            byte[] pixels = pixelDataProvider.DetachPixelData();

            return await Task.Run(() =>
            {
                int pixelIndex = 0;
                int length = 0;

                progress.Report(ResourceString.GetString("Stego_Status_Header") ?? "Locating payload header...");

                for (int i = 0; i < 32; i++)
                {
                    int bit = pixels[pixelIndex] & 1;
                    length |= (bit << i);
                    pixelIndex++;
                }

                if (length <= 0 || length > (pixels.Length - 32) / 8)
                {
                    Array.Clear(pixels, 0, pixels.Length);
                    throw new Exception("No valid hidden data found in this image, or the image has been compressed/corrupted.");
                }

                byte[] encryptedData = new byte[length];
                int lastReportedPercent = -1;
                int reportInterval = Math.Max(1, length / 100);

                for (int i = 0; i < length; i++)
                {
                    int b = (pixels[pixelIndex] & 1) |
                            ((pixels[pixelIndex + 1] & 1) << 1) |
                            ((pixels[pixelIndex + 2] & 1) << 2) |
                            ((pixels[pixelIndex + 3] & 1) << 3) |
                            ((pixels[pixelIndex + 4] & 1) << 4) |
                            ((pixels[pixelIndex + 5] & 1) << 5) |
                            ((pixels[pixelIndex + 6] & 1) << 6) |
                            ((pixels[pixelIndex + 7] & 1) << 7);

                    encryptedData[i] = (byte)b;
                    pixelIndex += 8;

                    if (i % reportInterval == 0)
                    {
                        int currentPercent = (int)((double)i / length * 100);
                        if (currentPercent != lastReportedPercent)
                        {
                            lastReportedPercent = currentPercent;
                            string localizedExtract = ResourceString.GetString("Stego_Status_Extract") ?? "Extracting encrypted blocks";
                            progress.Report($"{localizedExtract}... {currentPercent}%");
                        }
                    }
                }

                Array.Clear(pixels, 0, pixels.Length);

                progress.Report(ResourceString.GetString("Stego_Status_Decrypting") ?? "Decrypting payload (AES-256)...");

                byte[] decryptedData = AesHelper.DecryptBytes(encryptedData, _masterPassword!, cryptoConfig);

                Array.Clear(encryptedData, 0, encryptedData.Length);

                progress.Report(ResourceString.GetString("Stego_Status_Reconstructing") ?? "Reconstructing file structure...");
                using (var ms = new MemoryStream(decryptedData))
                using (var br = new BinaryReader(ms))
                {
                    int nameLen = br.ReadInt32();
                    byte[] nameBytes = br.ReadBytes(nameLen);
                    string fileName = Encoding.UTF8.GetString(nameBytes);

                    int remainingBytes = (int)(ms.Length - ms.Position);
                    byte[] fileBytes = br.ReadBytes(remainingBytes);

                    Array.Clear(decryptedData, 0, decryptedData.Length);

                    return (fileName, fileBytes);
                }
            });
        }

        #endregion

        #region UI Button Handlers

        private async void BtnEmbed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep1 = await ShowStepDialogAsync(
                    "Stego_Wizard_HostTitle", "Step 1: Select Host Image",
                    "Stego_Wizard_HostDesc", "Choose a normal-looking photo (PNG or JPG). This image needs to be relatively large in resolution to hold your secret data without visual distortion.",
                    "Stego_Wizard_HostBtn", "Select Host Image");
                if (!continueStep1) return;

                string hostTitle = ResourceString.GetString("Stego_PickHost_Title") ?? "Select Host Image (PNG/JPG)";
                string? hostPath = Win32FileDialogHelper.ShowOpenFilePicker(App.MainWindow!, hostTitle, "Images", "*.png;*.jpg;*.jpeg;*.bmp");
                if (string.IsNullOrEmpty(hostPath)) return;

                bool continueStep2 = await ShowStepDialogAsync(
                    "Stego_Wizard_SecretTitle", "Step 2: Select Secret File",
                    "Stego_Wizard_SecretDesc", "Choose the sensitive file you want to hide. It will be AES-256 encrypted using your Master Password before being embedded into the host image.",
                    "Stego_Wizard_SecretBtn", "Select Secret File");
                if (!continueStep2) return;

                string secretTitle = ResourceString.GetString("Stego_PickSecret_Title") ?? "Select Secret File to Hide";
                string? secretPath = Win32FileDialogHelper.ShowOpenFilePicker(App.MainWindow!, secretTitle, "All Files", "*.*");
                if (string.IsNullOrEmpty(secretPath)) return;

                bool continueStep3 = await ShowStepDialogAsync(
                    "Stego_Wizard_SaveTitle", "Step 3: Save Vault Image",
                    "Stego_Wizard_SaveDesc", "Choose a location to save your new hidden vault. The file MUST be saved as a PNG to prevent image compression from destroying the hidden data.",
                    "Stego_Wizard_SaveBtn", "Choose Save Location");
                if (!continueStep3) return;

                string saveTitle = ResourceString.GetString("Stego_SaveStego_Title") ?? "Save Hidden Vault Image";
                string? destPath = Win32FileDialogHelper.ShowSaveFilePicker(App.MainWindow!, saveTitle, "PNG Image", "*.png", "HiddenImage.png", ".png");
                if (string.IsNullOrEmpty(destPath)) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                KeyDerivationConfig cryptoConfig = GetSelectedSecurityConfig();

                var progress = new Progress<string>(status =>
                {
                    LoadingTitleText.Text = status;
                });

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await ProcessEmbedAsync(hostPath, secretPath, destPath, cryptoConfig, progress);

                LoadingOverlay.Visibility = Visibility.Collapsed;
                UIHelper.SetOverlay(false);

                NotificationManager.Show(
                    ResourceString.GetString("Toast_Success_Title") ?? "Success",
                    ResourceString.GetString("Stego_Toast_EmbedSuccess") ?? "Data securely hidden inside image.")
                    .WithSeverity(NotificationManager.NoticeSeverity.Success)
                    .Create();

                await ShowOpenFolderDialogAsync(destPath);
            }
            catch (Exception ex)
            {
                NotificationManager.Show(
                    ResourceString.GetString("Toast_Error_Title") ?? "Steganography Error",
                    ex.Message)
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

        private async void BtnExtract_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep1 = await ShowStepDialogAsync(
                    "Stego_Wizard_ExtractTitle", "Step 1: Select Vault Image",
                    "Stego_Wizard_ExtractDesc", "Choose the PNG image that contains your hidden, encrypted data.",
                    "Stego_Wizard_ExtractBtn", "Select Vault Image");
                if (!continueStep1) return;

                string hostTitle = ResourceString.GetString("Stego_PickExtract_Title") ?? "Select Image with Hidden Data";
                string? stegoPath = Win32FileDialogHelper.ShowOpenFilePicker(App.MainWindow!, hostTitle, "PNG Images", "*.png");
                if (string.IsNullOrEmpty(stegoPath)) return;

                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

                KeyDerivationConfig cryptoConfig = GetSelectedSecurityConfig();

                var progress = new Progress<string>(status =>
                {
                    LoadingTitleText.Text = status;
                });

                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                var result = await DecodeEncryptedPayloadAsync(stegoPath, cryptoConfig, progress);

                LoadingOverlay.Visibility = Visibility.Collapsed;
                UIHelper.SetOverlay(false);

                string payloadFoundDesc = ResourceString.GetString("Stego_Wizard_FoundDesc") ?? "We successfully decrypted: {0}\n\nChoose where you would like to save this extracted file.";

                bool continueStep2 = await ShowStepDialogAsync(
                    "Stego_Wizard_FoundTitle", "Payload Decrypted Successfully",
                    string.Empty, string.Format(payloadFoundDesc, result.OriginalFileName),
                    "Stego_Wizard_FoundBtn", "Save File");
                if (!continueStep2) return;

                string ext = Path.GetExtension(result.OriginalFileName);
                if (string.IsNullOrEmpty(ext)) ext = ".*";

                string saveTitle = ResourceString.GetString("Stego_SaveRevealed_Title") ?? "Save Revealed File";
                string? savePath = Win32FileDialogHelper.ShowSaveFilePicker(App.MainWindow!, saveTitle, "Original File", "*" + ext, result.OriginalFileName, ext);

                if (string.IsNullOrEmpty(savePath))
                {
                    Array.Clear(result.FileBytes, 0, result.FileBytes.Length);
                    return;
                }

                LoadingTitleText.Text = ResourceString.GetString("Stego_OverlayExtractTitle") ?? "Saving Secure File to Disk...";
                UIHelper.SetOverlay(true);
                LoadingOverlay.Visibility = Visibility.Visible;

                await File.WriteAllBytesAsync(savePath, result.FileBytes);

                Array.Clear(result.FileBytes, 0, result.FileBytes.Length);

                LoadingOverlay.Visibility = Visibility.Collapsed;
                UIHelper.SetOverlay(false);

                NotificationManager.Show(
                    ResourceString.GetString("Toast_Success_Title") ?? "Success",
                    ResourceString.GetString("Stego_Toast_ExtractSuccess") ?? "File successfully extracted and decrypted.")
                    .WithSeverity(NotificationManager.NoticeSeverity.Success)
                    .Create();

                await ShowOpenFolderDialogAsync(savePath);
            }
            catch (CryptographicException)
            {
                NotificationManager.Show(
                    ResourceString.GetString("Stego_Toast_DecryptFailTitle") ?? "Decryption Failed",
                    ResourceString.GetString("Stego_Toast_DecryptFailMsg") ?? "The master password is incorrect o il file è stato manomesso.")
                    .WithSeverity(NotificationManager.NoticeSeverity.Error)
                    .Create();
            }
            catch (Exception ex)
            {
                NotificationManager.Show(
                    ResourceString.GetString("Toast_Error_Title") ?? "Extraction Error",
                    ex.Message)
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
    }
}