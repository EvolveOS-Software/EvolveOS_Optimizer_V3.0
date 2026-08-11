// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class HardwareDriverHelper
    {
        #region Queries

        public static bool IsPawnIoInstalled()
        {
            try
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))
                {
                    if (key?.GetValue("DisplayVersion") != null) return true;
                }

                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey? wowKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))
                {
                    if (wowKey?.GetValue("DisplayVersion") != null) return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PawnIO] Registry check failed: {ex.Message}");
            }

            return false;
        }

        #endregion

        #region Installation Lifecycle

        public static async Task<bool> EnsurePawnIoInstalledAsync(XamlRoot xamlRoot, bool forcePrompt = false)
        {
            if (IsPawnIoInstalled())
            {
                return true;
            }

            if (!forcePrompt && LocalMachineSettingsEngine.HidePawnIoPrompt)
            {
                return false;
            }

            if (xamlRoot == null) return false;

            var checkBox = new CheckBox
            {
                Content = ResourceString.GetString("dialog_pawnio_dont_show_again") ?? "Do not show this message again",
                Margin = new Thickness(0, 15, 0, 0),
                FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Jura"] ?? new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI")
            };

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock
            {
                Text = ResourceString.GetString("dialog_pawnio_message") ?? "To read accurate, live temperatures for your CPU, Motherboard, and RAM, EvolveOS Optimizer requires the PawnIO hardware driver.\n\nThis is a secure, open-source driver that bypasses standard Windows kernel blocks to read your motherboard's thermal sensors directly. Would you like to install it now?",
                TextWrapping = TextWrapping.Wrap
            });
            stackPanel.Children.Add(checkBox);

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = ResourceString.GetString("dialog_pawnio_title") ?? "Advanced Hardware Monitoring",
                Content = stackPanel,
                PrimaryButtonText = ResourceString.GetString("dialog_pawnio_install_btn") ?? "Install Driver",
                CloseButtonText = ResourceString.GetString("dialog_pawnio_cancel_btn") ?? "Not Now",
                DefaultButton = ContentDialogButton.Primary
            };

            if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
            {
                dialog.Style = (Style)style;
            }

            ContentDialogResult result = await dialog.ShowAsync();

            if (checkBox.IsChecked == true)
            {
                LocalMachineSettingsEngine.HidePawnIoPrompt = true;
            }

            if (result != ContentDialogResult.Primary)
            {
                return false;
            }

            string tempExePath = Path.Combine(Path.GetTempPath(), "PawnIO_Setup.exe");

            try
            {
                byte[] archiveBytes = ArchiveManager.GetResourceBytes("PawnIO.gz");
                if (archiveBytes.Length == 0) return false;

                ArchiveManager.Unarchive(tempExePath, archiveBytes);

                int exitCode = await CommandExecutor.StartInCmd($"\"{tempExePath}\" -install");

                Debug.WriteLine($"[PawnIO Installer] Exit code returned: {exitCode}");

                return IsPawnIoInstalled();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PawnIO Install Error] {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempExePath)) File.Delete(tempExePath);
                }
                catch { }
            }
        }

        #endregion

        #region Uninstallation Lifecycle

        public static async Task<bool> UninstallPawnIoAsync()
        {
            if (!IsPawnIoInstalled()) return true;

            string uninstallExe = string.Empty;

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey? key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))
                {
                    if (key != null)
                    {
                        string? installLocation = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installLocation))
                        {
                            uninstallExe = Path.Combine(installLocation, "uninstall.exe");
                        }
                    }
                }

                if (string.IsNullOrEmpty(uninstallExe) || !File.Exists(uninstallExe))
                {
                    uninstallExe = @"C:\Program Files\PawnIO\uninstall.exe";
                }

                if (!File.Exists(uninstallExe))
                {
                    Debug.WriteLine("[PawnIO] Uninstall executable not found physically on disk.");
                    return false;
                }

                int exitCode = await CommandExecutor.StartInCmd($"\"{uninstallExe}\" -uninstall -silent");

                Debug.WriteLine($"[PawnIO Uninstaller] Exit code returned: {exitCode}");

                return !IsPawnIoInstalled();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PawnIO Uninstall Error] {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}