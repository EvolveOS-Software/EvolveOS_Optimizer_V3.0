// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class TaskbarPinsPage : Page
    {
        private bool _isInitialized = false;

        public TaskbarPinsPage()
        {
            this.InitializeComponent();
            LoadSettings();
            _isInitialized = true;
        }

        private void LoadSettings()
        {
            ToggleSubmenus.IsOn = SettingsEngine.Taskbar_ShowFoldersAsSubmenus;
        }

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
                    this.Frame.Navigate(typeof(ShellCustomizationPage));
                }
            }
        }

        private async void BtnPinFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = App.MainWindow;
                if (mainWindow == null) return;

                string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(mainWindow, "Select a document to pin", "All Files", "*.*");

                if (!string.IsNullOrEmpty(filePath))
                {
                    string encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(filePath));
                    await ShellEnhancerController.SendCommandAsync($"Taskbar_PinItem:{encodedPath}");
                    Debug.WriteLine($"[Optimizer] Pinned file: {filePath}");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Optimizer] Error: {ex.Message}"); }
        }

        private async void BtnPinFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = App.MainWindow;
                if (mainWindow == null) return;

                string? folderPath = Win32FileDialogHelper.ShowFolderPicker(mainWindow, "Select a folder to pin");

                if (!string.IsNullOrEmpty(folderPath))
                {
                    string encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(folderPath));
                    await ShellEnhancerController.SendCommandAsync($"Taskbar_PinItem:{encodedPath}");
                    Debug.WriteLine($"[Optimizer] Pinned folder: {folderPath}");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Optimizer] Error: {ex.Message}"); }
        }

        private void BtnPinFolderMenu_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(Pages.ManageFilteredFoldersPage));
            }
        }

        private async void ToggleSubmenus_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is ToggleSwitch toggleSwitch)
            {
                bool isOn = toggleSwitch.IsOn;
                SettingsEngine.Taskbar_ShowFoldersAsSubmenus = isOn;
                await ShellEnhancerController.SendCommandAsync($"Taskbar_FolderSubmenus:{isOn}");
            }
        }

        private void BtnManageFolders_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(Pages.ManageFilteredFoldersPage));
            }
        }
    }
}