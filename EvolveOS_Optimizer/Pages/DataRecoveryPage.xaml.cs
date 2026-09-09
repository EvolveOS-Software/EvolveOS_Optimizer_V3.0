// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using Windows.Storage.Streams;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class DataRecoveryPage : Page
    {
        #region Fields & State

        private readonly List<RecoveredItem> _allScannedFiles = new();
        private readonly ObservableCollection<RecoveredItem> _discoveredFiles = new();

        private List<DriveTarget> _availableDrives = new();
        private DriveTarget? _selectedDrive;
        private CancellationTokenSource? _actionCts;
        private bool _isHandlingSelectAllEvent = false;

        #endregion

        #region Initialization

        public DataRecoveryPage()
        {
            this.InitializeComponent();
            ListDiscoveredFiles.ItemsSource = _discoveredFiles;
            LoadSystemDrives();
        }

        private void LoadSystemDrives()
        {
            _availableDrives = DataRecoveryEngine.GetAvailableDrives();
            CmbDrives.Items.Clear();

            foreach (var drive in _availableDrives)
            {
                CmbDrives.Items.Add(drive.DisplayName);
            }

            if (CmbDrives.Items.Count > 0)
            {
                CmbDrives.SelectedIndex = 0;
            }
        }

        #endregion

        #region UI Handlers & Drive Evaluation

        private async void CmbDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbDrives.SelectedIndex < 0 || CmbDrives.SelectedIndex >= _availableDrives.Count) return;

            _selectedDrive = _availableDrives[CmbDrives.SelectedIndex];
            SsdAdvisoryBanner.IsOpen = _selectedDrive.IsSsd;

            CmbVssSnapshots.Items.Clear();
            CmbVssSnapshots.Items.Add($"Live Physical Drive ({_selectedDrive.DriveLetter})");
            CmbVssSnapshots.SelectedIndex = 0;

            CmbVssSnapshots.PlaceholderText = ResourceString.GetString("DataRecovery_VssPlaceholder") ?? "Searching for VSS Snapshots...";

            var snapshots = await Task.Run(() => DataRecoveryEngine.GetVssSnapshots(_selectedDrive.DriveLetter));

            foreach (var snap in snapshots)
            {
                CmbVssSnapshots.Items.Add($"Shadow Copy: {snap}");
            }

            CmbVssSnapshots.PlaceholderText = string.Empty;
        }

        private async void BtnStartScan_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDrive == null) return;

            ScanMode scanMode = (CmbScanMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "MftQuickScan"
                                ? ScanMode.MftQuickScan : ScanMode.DeepScanSectorCarving;

            FileCategory? selectedCategory = null;
            if (CmbFileTypes.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag && tag != "All")
            {
                if (Enum.TryParse<FileCategory>(tag, out var parsedCat))
                {
                    selectedCategory = parsedCat;
                }
            }

            string volumePathOverride = string.Empty;
            if (CmbVssSnapshots.SelectedIndex > 0)
            {
                string? selection = CmbVssSnapshots.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selection))
                {
                    volumePathOverride = selection.Replace("Shadow Copy: ", "");
                }
            }

            _allScannedFiles.Clear();
            _discoveredFiles.Clear();

            BtnRecoverSelected.IsEnabled = false;
            BtnShredSelected.IsEnabled = false;
            BtnSaveSession.IsEnabled = false;
            TxtFilter.IsEnabled = false;
            CmbSort.IsEnabled = false;

            _isHandlingSelectAllEvent = true;
            ChkSelectAll.IsChecked = true;
            ChkSelectAll.IsEnabled = false;
            _isHandlingSelectAllEvent = false;

            _actionCts = new CancellationTokenSource();

            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            TxtOverlayTitle.Text = scanMode == ScanMode.MftQuickScan
                ? (ResourceString.GetString("DataRecovery_ParsingMft") ?? "Parsing Master File Table...")
                : (ResourceString.GetString("DataRecovery_AnalyzingSectors") ?? "Analyzing Raw Sectors...");

            ScanProgressBar.IsIndeterminate = false;
            BtnCancelScan.Visibility = Visibility.Visible;
            BtnCancelScan.Content = ResourceString.GetString("DataRecovery_CancelScan") ?? "Cancel Scan";

            if (ScanProgressPanel.Parent is Panel parentPanel)
            {
                parentPanel.Children.Remove(ScanProgressPanel);
            }

            UIHelper.SetOverlay(true);
            UIHelper.ShowPopupOverlay(ScanProgressPanel);

            int totalFound = 0;

            try
            {
                await DataRecoveryEngine.ScanDriveAsync(
                    _selectedDrive,
                    volumePathOverride,
                    scanMode,
                    selectedCategory,
                    onItemsDiscoveredBatch: batch =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            bool shouldSelect = (ChkSelectAll.IsChecked == true);

                            foreach (var item in batch)
                            {
                                item.IsSelected = shouldSelect;
                                _allScannedFiles.Add(item);
                                _discoveredFiles.Add(item);
                            }

                            totalFound += batch.Count;
                        });
                    },
                    onProgressChanged: progress =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            ScanProgressBar.Value = progress;
                            TxtScanTelemetry.Text = string.Format(ResourceString.GetString("DataRecovery_ScanTelemetry") ?? "Discovered: {0} files | Scanned: {1:0.0}%", totalFound, progress);
                        });
                    },
                    _actionCts.Token);

                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_ScanCompleteTitle") ?? "Scan Complete",
                    string.Format(ResourceString.GetString("DataRecovery_ScanCompleteMsg") ?? "Discovered {0} files.", totalFound))
                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                   .Create();
            }
            catch (OperationCanceledException)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_ScanStoppedTitle") ?? "Scan Stopped",
                    ResourceString.GetString("DataRecovery_ScanStoppedMsg") ?? "The recovery scan was cancelled by user.")
                   .WithSeverity(NotificationManager.NoticeSeverity.Info)
                   .Create();
            }
            catch (Exception ex)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_ScanErrorTitle") ?? "Scan Error",
                    ex.Message)
                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                   .Create();
            }
            finally
            {
                UIHelper.HidePopupOverlay();
                UIHelper.SetOverlay(false);

                if (ScanProgressPanel.Parent == null)
                {
                    OverlayHost.Children.Add(ScanProgressPanel);
                }

                EfficiencyModeHelper.IsUIWakeLockActive = false;

                if (_allScannedFiles.Any())
                {
                    BtnRecoverSelected.IsEnabled = true;
                    BtnShredSelected.IsEnabled = true;
                    BtnSaveSession.IsEnabled = true;
                    ChkSelectAll.IsEnabled = true;
                    TxtFilter.IsEnabled = true;
                    CmbSort.IsEnabled = true;
                    UpdateDisplayedFiles();
                }
            }
        }

        private void BtnCancelScan_Click(object sender, RoutedEventArgs e)
        {
            _actionCts?.Cancel();
        }

        #endregion

        #region Session Save & Load

        private async void BtnSaveSession_Click(object sender, RoutedEventArgs e)
        {
            if (!_allScannedFiles.Any()) return;

            string fileName = $"EvolveOS_RecoverySession_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string title = ResourceString.GetString("DataRecovery_SaveSessionTitle") ?? "Save Recovery Session";

            string? path = Win32FileDialogHelper.ShowSaveFilePicker(
                App.MainWindow!,
                title,
                "JSON Session File",
                "*.json",
                fileName,
                "json");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = JsonSerializer.Serialize(_allScannedFiles);
                await File.WriteAllTextAsync(path, json);

                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_SessionSaved") ?? "Session Saved",
                    $"Saved to {Path.GetFileName(path)}")
                    .WithSeverity(NotificationManager.NoticeSeverity.Success)
                    .Create();
            }
            catch (Exception ex)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_SaveError") ?? "Save Error",
                    ex.Message)
                    .WithSeverity(NotificationManager.NoticeSeverity.Error)
                    .Create();
            }
        }

        private async void BtnLoadSession_Click(object sender, RoutedEventArgs e)
        {
            string title = ResourceString.GetString("DataRecovery_LoadSessionTitle") ?? "Load Recovery Session";

            string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
                App.MainWindow!,
                title,
                "JSON Session File",
                "*.json");

            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var loadedSession = JsonSerializer.Deserialize<List<RecoveredItem>>(json);

                if (loadedSession != null && loadedSession.Any())
                {
                    _allScannedFiles.Clear();
                    _allScannedFiles.AddRange(loadedSession);

                    BtnRecoverSelected.IsEnabled = true;
                    BtnShredSelected.IsEnabled = true;
                    BtnSaveSession.IsEnabled = true;
                    ChkSelectAll.IsEnabled = true;
                    TxtFilter.IsEnabled = true;
                    CmbSort.IsEnabled = true;

                    UpdateDisplayedFiles();

                    NotificationManager.Show(
                        ResourceString.GetString("DataRecovery_SessionLoaded") ?? "Session Loaded",
                        $"Successfully loaded {loadedSession.Count} items.")
                        .WithSeverity(NotificationManager.NoticeSeverity.Success)
                        .Create();
                }
                else
                {
                    throw new InvalidOperationException("The session file appears to be empty or corrupted.");
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_LoadError") ?? "Load Error",
                    "The selected file is not a valid Recovery Session: " + ex.Message)
                    .WithSeverity(NotificationManager.NoticeSeverity.Error)
                    .Create();
            }
        }

        #endregion

        #region Searching & Sorting Engine

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDisplayedFiles();
        }

        private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allScannedFiles.Any())
            {
                UpdateDisplayedFiles();
            }
        }

        private void UpdateDisplayedFiles()
        {
            var query = _allScannedFiles.AsEnumerable();

            string filterText = TxtFilter.Text?.Trim().ToLower() ?? string.Empty;
            if (!string.IsNullOrEmpty(filterText))
            {
                query = query.Where(f => f.Name.ToLower().Contains(filterText) || f.Extension.ToLower().Contains(filterText));
            }

            if (CmbSort.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                switch (tag)
                {
                    case "Name":
                        query = query.OrderBy(f => f.Name);
                        break;
                    case "Extension":
                        query = query.OrderBy(f => f.Extension).ThenBy(f => f.Name);
                        break;
                    case "SizeDesc":
                        query = query.OrderByDescending(f => f.SizeBytes);
                        break;
                    case "SizeAsc":
                        query = query.OrderBy(f => f.SizeBytes);
                        break;
                    case "Integrity":
                        query = query.OrderBy(f => f.Status).ThenByDescending(f => f.SizeBytes);
                        break;
                }
            }

            var results = query.ToList();

            ListDiscoveredFiles.ItemsSource = null;
            _discoveredFiles.Clear();

            foreach (var f in results)
            {
                _discoveredFiles.Add(f);
            }

            ListDiscoveredFiles.ItemsSource = _discoveredFiles;

            _isHandlingSelectAllEvent = true;
            ChkSelectAll.IsChecked = results.Any() && results.All(f => f.IsSelected);
            _isHandlingSelectAllEvent = false;
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_isHandlingSelectAllEvent) return;

            foreach (var item in _discoveredFiles)
            {
                item.IsSelected = true;
            }
        }

        private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isHandlingSelectAllEvent) return;

            foreach (var item in _discoveredFiles)
            {
                item.IsSelected = false;
            }
        }

        #endregion

        #region Previewer Engine

        private void FileRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is RecoveredItem item)
            {
                ShowPreviewForItem(item);
            }
        }

        private void BtnPreviewFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RecoveredItem item)
            {
                ShowPreviewForItem(item);
            }
        }

        private async void ShowPreviewForItem(RecoveredItem item)
        {
            string volumePath = string.Empty;
            if (CmbVssSnapshots.SelectedIndex > 0)
            {
                volumePath = CmbVssSnapshots.SelectedItem.ToString()!.Replace("Shadow Copy: ", "");
            }
            else
            {
                volumePath = $@"\\.\{_selectedDrive?.DriveLetter}";
            }

            PreviewOverlay.Visibility = Visibility.Visible;
            TxtPreviewFileName.Text = item.Name + item.Extension;
            ImgPreview.Visibility = Visibility.Collapsed;
            HexPreviewScroller.Visibility = Visibility.Collapsed;
            PreviewLoadingRing.IsActive = true;

            AnimShowPreview.Begin();

            try
            {
                byte[] rawBytes = await DataRecoveryEngine.GetFilePreviewBytesAsync(volumePath, item);

                if (item.Category == FileCategory.Picture && rawBytes.Length > 0)
                {
                    try
                    {
                        using var stream = new InMemoryRandomAccessStream();
                        using var writer = new DataWriter(stream);
                        writer.WriteBytes(rawBytes);
                        await writer.StoreAsync();
                        stream.Seek(0);

                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);

                        ImgPreview.Source = bitmap;
                        ImgPreview.Visibility = Visibility.Visible;
                    }
                    catch
                    {
                        if (item.Extension == ".heic" || item.Extension == ".dng")
                        {
                            TxtHexPreview.Text = $"--- {item.Extension.ToUpper()} PREVIEW ---\n\nNative rendering failed. Ensure 'HEIF Image Extensions' is installed from the Microsoft Store to view this format natively in Windows.";
                        }
                        else
                        {
                            TxtHexPreview.Text = "--- CORRUPTED IMAGE ---\n\nThe headers were found, but the file data is corrupted or incomplete and cannot be rendered.";
                        }
                        HexPreviewScroller.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    string advancedPreview = DataRecoveryEngine.GenerateAdvancedPreview(rawBytes, item);

                    if (advancedPreview == "HEX_FALLBACK")
                    {
                        TxtHexPreview.Text = BitConverter.ToString(rawBytes.Take(512).ToArray()).Replace("-", " ");
                    }
                    else
                    {
                        TxtHexPreview.Text = advancedPreview;
                    }
                    HexPreviewScroller.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                TxtHexPreview.Text = (ResourceString.GetString("DataRecovery_PreviewError") ?? "Failed to generate preview: ") + ex.Message;
                HexPreviewScroller.Visibility = Visibility.Visible;
            }
            finally
            {
                PreviewLoadingRing.IsActive = false;
            }
        }

        private void BtnClosePreview_Click(object sender, RoutedEventArgs e)
        {
            PreviewOverlay.Visibility = Visibility.Collapsed;
            ImgPreview.Source = null;
            TxtHexPreview.Text = string.Empty;
        }

        #endregion

        #region Safe Extraction, Shredding & Disk Space Verification

        private async void BtnRecoverSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _allScannedFiles.Where(f => f.IsSelected).ToList();
            if (!selectedItems.Any() || _selectedDrive == null) return;

            string? destination = Win32FileDialogHelper.ShowFolderPicker(App.MainWindow!, ResourceString.GetString("DataRecovery_PickerTitle") ?? "Select Destination Folder for Recovered Files");
            if (string.IsNullOrEmpty(destination)) return;

            string destRoot = Path.GetPathRoot(destination)?.TrimEnd('\\') ?? string.Empty;

            if (string.Equals(destRoot, _selectedDrive.DriveLetter, StringComparison.OrdinalIgnoreCase) && CmbVssSnapshots.SelectedIndex == 0)
            {
                var dialog = new ContentDialog
                {
                    Title = ResourceString.GetString("DataRecovery_OverwriteTitle") ?? "Sector Overwrite Prevention",
                    Content = new TextBlock
                    {
                        Text = ResourceString.GetString("DataRecovery_OverwriteMessage") ?? "Writing recovered files to the same live drive you are recovering from will permanently overwrite and destroy unallocated sectors! Please select a different drive or a USB flash drive.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = ResourceString.GetString("msgbox_btn_ok") ?? "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            long totalNeededBytes = selectedItems.Sum(i => i.SizeBytes);
            try
            {
                DriveInfo destDrive = new DriveInfo(destRoot);
                if (destDrive.AvailableFreeSpace < totalNeededBytes)
                {
                    string spaceMsgFormat = ResourceString.GetString("DataRecovery_SpaceMessage") ?? "The selected files require {0} of free space, but the destination drive ({1}) only has {2} available. Please choose a different drive or select fewer files.";

                    var dialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("DataRecovery_SpaceTitle") ?? "Insufficient Disk Space",
                        Content = new TextBlock
                        {
                            Text = string.Format(spaceMsgFormat, FormatBytes(totalNeededBytes), destRoot, FormatBytes(destDrive.AvailableFreeSpace)),
                            TextWrapping = TextWrapping.Wrap
                        },
                        CloseButtonText = ResourceString.GetString("msgbox_btn_ok") ?? "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return;
                }
            }
            catch { }

            string volumePathOverride = string.Empty;
            if (CmbVssSnapshots.SelectedIndex > 0)
            {
                volumePathOverride = CmbVssSnapshots.SelectedItem.ToString()!.Replace("Shadow Copy: ", "");
            }
            string extractTarget = string.IsNullOrEmpty(volumePathOverride) ? $@"\\.\{_selectedDrive.DriveLetter}" : volumePathOverride;

            _actionCts = new CancellationTokenSource();

            TxtOverlayTitle.Text = ResourceString.GetString("DataRecovery_ExtractingFiles") ?? "Extracting Files...";
            ScanProgressBar.IsIndeterminate = true;
            BtnCancelScan.Visibility = Visibility.Visible;
            BtnCancelScan.Content = ResourceString.GetString("DataRecovery_CancelRecovery") ?? "Cancel Recovery";

            if (ScanProgressPanel.Parent is Panel parentPanel)
            {
                parentPanel.Children.Remove(ScanProgressPanel);
            }

            UIHelper.SetOverlay(true);
            UIHelper.ShowPopupOverlay(ScanProgressPanel);

            int recoveredCount = 0;

            try
            {
                foreach (var item in selectedItems)
                {
                    string carvingFormat = ResourceString.GetString("DataRecovery_CarvingProgress") ?? "Carving file {0} of {1}...";
                    TxtScanTelemetry.Text = string.Format(carvingFormat, recoveredCount + 1, selectedItems.Count);

                    await DataRecoveryEngine.ExtractFileAsync(extractTarget, item, destination, _actionCts.Token);
                    recoveredCount++;
                }

                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_SuccessTitle") ?? "Recovery Succeeded",
                    string.Format(ResourceString.GetString("DataRecovery_SuccessMsg") ?? "Successfully saved {0} files to {1}.", recoveredCount, destination))
                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                   .Create();
            }
            catch (OperationCanceledException)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_StoppedTitle") ?? "Recovery Stopped",
                    string.Format(ResourceString.GetString("DataRecovery_StoppedMsg") ?? "Extraction cancelled. {0} files were successfully saved before cancellation.", recoveredCount))
                   .WithSeverity(NotificationManager.NoticeSeverity.Warning)
                   .Create();
            }
            catch (Exception ex)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_ErrorTitle") ?? "Extraction Error",
                    ex.Message)
                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                   .Create();
            }
            finally
            {
                UIHelper.HidePopupOverlay();
                UIHelper.SetOverlay(false);
                ScanProgressBar.IsIndeterminate = false;

                if (ScanProgressPanel.Parent == null)
                {
                    OverlayHost.Children.Add(ScanProgressPanel);
                }
            }
        }

        private async void BtnShredSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _allScannedFiles.Where(f => f.IsSelected).ToList();
            if (!selectedItems.Any() || _selectedDrive == null) return;

            if (CmbVssSnapshots.SelectedIndex > 0)
            {
                var vssDialog = new ContentDialog
                {
                    Title = "Read-Only Volume",
                    Content = new TextBlock
                    {
                        Text = "You cannot securely shred sectors on a VSS Shadow Copy because it is a historical read-only snapshot. Please select the 'Live Physical Drive' from the dropdown and rescan to perform shredding.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = ResourceString.GetString("msgbox_btn_ok") ?? "OK",
                    XamlRoot = this.XamlRoot
                };
                await vssDialog.ShowAsync();
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = ResourceString.GetString("DataRecovery_ShredWarningTitle") ?? "Permanent Destruction Warning",
                Content = new TextBlock
                {
                    Text = ResourceString.GetString("DataRecovery_ShredWarningMsg") ?? "This action will permanently overwrite the physical sectors for the selected files with zeroes, making them completely unrecoverable by any software. Are you sure you want to proceed?",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = ResourceString.GetString("DataRecovery_ShredConfirmBtn") ?? "Shred Permanently",
                CloseButtonText = ResourceString.GetString("msgbox_btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary) return;

            string extractTarget = $@"\\.\{_selectedDrive.DriveLetter}";

            _actionCts = new CancellationTokenSource();

            TxtOverlayTitle.Text = ResourceString.GetString("DataRecovery_ShreddingFiles") ?? "Securely Shredding Sectors...";
            ScanProgressBar.IsIndeterminate = true;
            BtnCancelScan.Visibility = Visibility.Visible;
            BtnCancelScan.Content = ResourceString.GetString("DataRecovery_CancelShred") ?? "Abort Shredding";

            if (ScanProgressPanel.Parent is Panel parentPanel)
            {
                parentPanel.Children.Remove(ScanProgressPanel);
            }

            UIHelper.SetOverlay(true);
            UIHelper.ShowPopupOverlay(ScanProgressPanel);

            int shreddedCount = 0;

            try
            {
                foreach (var item in selectedItems)
                {
                    string shredFormat = ResourceString.GetString("DataRecovery_ShreddingProgress") ?? "Nuking file {0} of {1}...";
                    TxtScanTelemetry.Text = string.Format(shredFormat, shreddedCount + 1, selectedItems.Count);

                    await DataRecoveryEngine.ShredFileAsync(extractTarget, item, _actionCts.Token);

                    item.IsSelected = false;
                    _allScannedFiles.Remove(item);

                    shreddedCount++;
                }

                UpdateDisplayedFiles();

                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_ShredSuccessTitle") ?? "Shredding Complete",
                    string.Format(ResourceString.GetString("DataRecovery_ShredSuccessMsg") ?? "Successfully destroyed {0} files.", shreddedCount))
                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                   .Create();
            }
            catch (OperationCanceledException)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_ShredStoppedTitle") ?? "Shredding Aborted",
                    string.Format(ResourceString.GetString("DataRecovery_ShredStoppedMsg") ?? "{0} files were permanently destroyed before aborting.", shreddedCount))
                   .WithSeverity(NotificationManager.NoticeSeverity.Warning)
                   .Create();
            }
            catch (Exception ex)
            {
                NotificationManager.Show(
                    ResourceString.GetString("DataRecovery_ShredErrorTitle") ?? "Shredding Error",
                    ex.Message)
                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                   .Create();
            }
            finally
            {
                UIHelper.HidePopupOverlay();
                UIHelper.SetOverlay(false);
                ScanProgressBar.IsIndeterminate = false;

                if (ScanProgressPanel.Parent == null)
                {
                    OverlayHost.Children.Add(ScanProgressPanel);
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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

        #endregion
    }
}