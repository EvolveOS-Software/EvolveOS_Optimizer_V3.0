// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ManageFilteredFoldersPage : Page
    {
        public ObservableCollection<FilteredFolderItem> FoldersList { get; } = new();

        public ManageFilteredFoldersPage()
        {
            this.InitializeComponent();
            LoadFolders();
        }

        private void LoadFolders()
        {
            try
            {
                FoldersList.Clear();
                string savedFolders = SettingsEngine.Taskbar_FilteredFolders;

                if (!string.IsNullOrEmpty(savedFolders))
                {
                    var paths = savedFolders.Split(';', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var folderPath in paths)
                    {
                        if (Directory.Exists(folderPath))
                        {
                            string folderName = Path.GetFileName(folderPath);

                            if (string.IsNullOrEmpty(folderName))
                            {
                                folderName = folderPath;
                            }

                            FoldersList.Add(new FilteredFolderItem
                            {
                                Name = folderName,
                                Path = folderPath
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Optimizer] Error loading filtered folders: {ex.Message}");
            }
            finally
            {
                UpdateEmptyState();
            }
        }

        private void SaveFolders()
        {
            try
            {
                var paths = FoldersList.Select(f => f.Path);
                string joinedPaths = string.Join(";", paths);

                SettingsEngine.Taskbar_FilteredFolders = joinedPaths;

                string encodedPaths = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(joinedPaths));
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_FilteredFolders:{encodedPaths}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Optimizer] Error saving filtered folders: {ex.Message}");
            }
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
                    this.Frame.Navigate(typeof(TaskbarPinsPage));
                }
            }
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = App.MainWindow;
                if (mainWindow == null) return;

                string? folderPath = Win32FileDialogHelper.ShowFolderPicker(
                    mainWindow,
                    "Select a folder to add as a filtered menu"
                );

                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    if (FoldersList.Any(f => f.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    string folderName = Path.GetFileName(folderPath);
                    if (string.IsNullOrEmpty(folderName)) folderName = folderPath;

                    FoldersList.Add(new FilteredFolderItem
                    {
                        Name = folderName,
                        Path = folderPath
                    });

                    SaveFolders();
                    UpdateEmptyState();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Optimizer] Error adding filtered folder: {ex.Message}");
            }
        }

        private void BtnRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string folderPath)
            {
                var itemToRemove = FoldersList.FirstOrDefault(f => f.Path == folderPath);
                if (itemToRemove != null)
                {
                    FoldersList.Remove(itemToRemove);
                    SaveFolders();
                    UpdateEmptyState();
                }
            }
        }

        private void UpdateEmptyState()
        {
            if (EmptyStatePanel != null)
            {
                EmptyStatePanel.Visibility = FoldersList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    public class FilteredFolderItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}