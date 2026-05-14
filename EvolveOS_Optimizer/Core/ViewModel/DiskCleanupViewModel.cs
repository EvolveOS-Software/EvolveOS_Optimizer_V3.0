// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class DiskCleanupViewModel : ObservableObject
    {
        #region Services & Fields
        private readonly Winapp2Parser _parser = new();
        private readonly DetectionService _detection = new();
        private readonly CleaningService _cleaner = new();

        private readonly string _historyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS", "cleanup_history.json");

        private const string Winapp2Url = "https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/raw/master-net10.0/EvolveOS_Optimizer/Assets/Winapp2.ini";
        private static string Winapp2LocalPath => Path.Combine(AppContext.BaseDirectory, "Winapp2.ini");

        private readonly List<ScanResult> _lastScan = [];
        private List<CleanerEntry> _loadedEntries = [];
        private List<string> _lastPaths = [];
        private bool _suppressSave;
        private CancellationTokenSource? _analyzerCts;
        #endregion

        #region Observable State (Cleaner)
        [ObservableProperty] public partial ObservableCollection<DiskCleanupCategoryViewModel> Categories { get; set; } = [];
        [ObservableProperty] public partial ObservableCollection<ScanResultLine> ResultLines { get; set; } = [];
        [ObservableProperty] public partial ObservableCollection<DetailLine> DetailLines { get; set; } = [];
        [ObservableProperty] public partial ScanResultLine? SelectedResultLine { get; set; }
        [ObservableProperty] public partial string SearchText { get; set; } = "";
        [ObservableProperty] public partial string StatusText { get; set; } = ResourceString.GetString("cleanup_status_loading") ?? "Loading...";
        [ObservableProperty] public partial string TotalSize { get; set; } = "";
        [ObservableProperty] public partial string TotalSpaceSaved { get; set; } = "0 B";
        [ObservableProperty] public partial bool IsBusy { get; set; }

        [ObservableProperty] public partial bool IsPostCleanEnabled { get; set; }
        [ObservableProperty] public partial string PostCleanCommands { get; set; } = "";
        [ObservableProperty] public partial string CustomPath { get; set; } = "";
        [ObservableProperty] public partial bool IsCustomSource { get; set; }
        [ObservableProperty] public partial string Winapp2Info { get; set; } = "";
        [ObservableProperty] public partial bool IsWinapp2NotAvailable { get; set; }
        [ObservableProperty] public partial bool IsScheduledCleanEnabled { get; set; }
        [ObservableProperty] public partial int ScheduledCleanDayIndex { get; set; } = 0;
        [ObservableProperty] public partial TimeSpan ScheduledCleanTime { get; set; } = new TimeSpan(12, 0, 0);
        [ObservableProperty] public partial bool IsCategoryPaneOpen { get; set; } = true;

        public ObservableCollection<StorageInsight> AnalyzerInsights { get; } = new();
        public ObservableCollection<FileCategoryInsight> FileCategories { get; } = new();

        private List<StorageNode> _unfilteredRootNodes = new();

        private readonly Dictionary<string, (string Name, string Color, string Icon)> _categoryMap;

        public IReadOnlyList<string> ScheduleDayOptions { get; } = new List<string>
        {
            ResourceString.GetString("cleanup_schedule_every_day") ?? "Every Day",
            ResourceString.GetString("cleanup_schedule_monday") ?? "Monday",
            ResourceString.GetString("cleanup_schedule_tuesday") ?? "Tuesday",
            ResourceString.GetString("cleanup_schedule_wednesday") ?? "Wednesday",
            ResourceString.GetString("cleanup_schedule_thursday") ?? "Thursday",
            ResourceString.GetString("cleanup_schedule_friday") ?? "Friday",
            ResourceString.GetString("cleanup_schedule_saturday") ?? "Saturday",
            ResourceString.GetString("cleanup_schedule_sunday") ?? "Sunday"
        };

        public ObservableCollection<StorageInsight> CategoryInsights { get; } = new();
        public ObservableCollection<HistoryBarItem> HistoryChart { get; } = new();

        public bool IsEmpty => Categories.Count == 0;
        public bool IsNotEmpty => Categories.Count > 0;
        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
        public bool IsShowingDetail => SelectedResultLine is not null;
        public bool IsShowingList => SelectedResultLine is null;
        public string SelectedAppName => SelectedResultLine?.AppName ?? "";
        public bool CanRunCleaner => !IsBusy && Categories.Count > 0;
        #endregion

        #region Observable State (Storage Analyzer)
        [ObservableProperty] public partial ObservableCollection<StorageNode> AnalyzedNodes { get; set; } = [];
        [ObservableProperty] public partial bool IsAnalyzerViewActive { get; set; }
        [ObservableProperty] public partial string AnalyzerStatusText { get; set; } = "";
        #endregion

        #region Constructor
        public DiskCleanupViewModel()
        {
            string locVideos = ResourceString.GetString("analyzer_cat_videos") ?? "Videos";
            string locAudio = ResourceString.GetString("analyzer_cat_audio") ?? "Audio";
            string locImages = ResourceString.GetString("analyzer_cat_images") ?? "Images";
            string locArchives = ResourceString.GetString("analyzer_cat_archives") ?? "Archives";
            string locInstallers = ResourceString.GetString("analyzer_cat_installers") ?? "Installers";
            string locDocs = ResourceString.GetString("analyzer_cat_documents") ?? "Documents";

            _categoryMap = new Dictionary<string, (string Name, string Color, string Icon)>(StringComparer.OrdinalIgnoreCase)
            {
                { ".mp4", (locVideos, "#FF3B30", "\uE714") }, { ".mkv", (locVideos, "#FF3B30", "\uE714") }, { ".mov", (locVideos, "#FF3B30", "\uE714") },
                { ".mp3", (locAudio, "#FF9500", "\uE8D6") }, { ".wav", (locAudio, "#FF9500", "\uE8D6") }, { ".flac", (locAudio, "#FF9500", "\uE8D6") },
                { ".jpg", (locImages, "#FFCC00", "\uEB9F") }, { ".png", (locImages, "#FFCC00", "\uEB9F") }, { ".gif", (locImages, "#FFCC00", "\uEB9F") },
                { ".zip", (locArchives, "#4CD964", "\uE7B8") }, { ".rar", (locArchives, "#4CD964", "\uE7B8") }, { ".7z", (locArchives, "#4CD964", "\uE7B8") },
                { ".exe", (locInstallers, "#5AC8FA", "\uE896") }, { ".msi", (locInstallers, "#5AC8FA", "\uE896") }, { ".iso", (locInstallers, "#5AC8FA", "\uE896") },
                { ".doc", (locDocs, "#007AFF", "\uE8A5") }, { ".pdf", (locDocs, "#007AFF", "\uE8A5") }, { ".txt", (locDocs, "#007AFF", "\uE8A5") }
            };
        }
        #endregion

        #region Property Change Hooks
        partial void OnIsPostCleanEnabledChanged(bool value) => SettingsEngine.IsPostCleanEnabled = value;
        partial void OnPostCleanCommandsChanged(string value) => SettingsEngine.PostCleanCommands = value;

        partial void OnSelectedResultLineChanged(ScanResultLine? value)
        {
            OnPropertyChanged(nameof(IsShowingDetail));
            OnPropertyChanged(nameof(IsShowingList));
            OnPropertyChanged(nameof(SelectedAppName));
            RebuildDetailLines(value);
        }

        partial void OnSearchTextChanged(string value)
        {
            RebuildVisibleCategories();
            RefreshCategoryState();
            StatusText = Categories.Count > 0
                ? string.Format(ResourceString.GetString("cleanup_status_search_found") ?? "Found {0} items", CountVisibleEntries())
                : ResourceString.GetString("cleanup_status_search_empty") ?? "No results";
            OnPropertyChanged(nameof(HasSearchText));
        }

        partial void OnIsBusyChanged(bool value)
        {
            AnalyzeCommand.NotifyCanExecuteChanged();
            RunCleanerCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            DownloadLatestCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanRunCleaner));
        }

        partial void OnIsScheduledCleanEnabledChanged(bool value)
        {
            SettingsEngine.IsScheduledCleanEnabled = value;
        }

        partial void OnScheduledCleanDayIndexChanged(int value)
        {
            SettingsEngine.ScheduledCleanDayIndex = value;
            if (IsScheduledCleanEnabled) { /* Trigger background service update */ }
        }

        partial void OnScheduledCleanTimeChanged(TimeSpan value)
        {
            SettingsEngine.ScheduledCleanTime = value;
            if (IsScheduledCleanEnabled) { /* Trigger background service update */ }
        }
        #endregion

        #region Commands (Storage Analyzer)

        public List<DriveOption> GetAvailableDrives()
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => new DriveOption
            {
                Name = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                Path = d.Name
            }).ToList();

            drives.Insert(0, new DriveOption { Name = ResourceString.GetString("analyzer_all_drives") ?? "All Drives", Path = "ALL" });
            return drives;
        }

        [RelayCommand]
        private async Task RunStorageAnalyzerAsync(string rootPath)
        {
            if (IsBusy) return;
            IsBusy = true;
            IsAnalyzerViewActive = true;
            AnalyzedNodes.Clear();

            AnalyzerInsights.Clear();

            _analyzerCts?.Cancel();
            _analyzerCts = new CancellationTokenSource();
            var token = _analyzerCts.Token;

            AnalyzerStatusText = string.Format(ResourceString.GetString("analyzer_status_scanning") ?? "Mapping storage on {0}...", rootPath);

            try
            {
                var drivesToScan = new List<string>();
                if (rootPath == "ALL")
                {
                    drivesToScan.AddRange(DriveInfo.GetDrives()
                        .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                        .Select(d => d.Name));
                }
                else
                {
                    drivesToScan.Add(rootPath);
                }

                var rootNodes = new List<StorageNode>();

                foreach (var drive in drivesToScan)
                {
                    var node = await Task.Run(() => BuildStorageTree(drive, token), token);
                    if (node != null)
                    {
                        rootNodes.Add(node);
                    }
                }

                long totalBytesScanned = rootNodes.Sum(n => n.SizeBytes);

                foreach (var node in rootNodes)
                {
                    node.Percentage = totalBytesScanned > 0 ? ((double)node.SizeBytes / totalBytesScanned) * 100 : 0;
                    AnalyzedNodes.Add(node);
                }

                if (rootPath == "ALL")
                {
                    var virtualRoot = new StorageNode
                    {
                        Name = "My Computer",
                        SizeBytes = totalBytesScanned
                    };

                    foreach (var drive in rootNodes)
                    {
                        virtualRoot.Children.Add(drive);
                    }

                    GenerateAnalyzerInsights(virtualRoot);
                }
                else if (rootNodes.Count > 0)
                {
                    GenerateAnalyzerInsights(rootNodes[0]);
                }

                GenerateCategoryInsights(rootNodes);

                AnalyzerStatusText = string.Format(ResourceString.GetString("analyzer_status_complete") ?? "Scan complete. Mapped {0}.", totalBytesScanned.FormatBytes());
            }
            catch (OperationCanceledException)
            {
                AnalyzerStatusText = ResourceString.GetString("analyzer_status_cancelled") ?? "Scan cancelled.";
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                AnalyzerStatusText = "Error during storage analysis.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void GenerateCategoryInsights(List<StorageNode> rootNodes)
        {
            _unfilteredRootNodes = rootNodes.ToList();

            string locAllFiles = ResourceString.GetString("analyzer_cat_all_files") ?? "All Files";
            string locOther = ResourceString.GetString("analyzer_cat_other") ?? "Other";

            FileCategories.Clear();
            var tally = new Dictionary<string, long>();

            void TallyNode(StorageNode node)
            {
                if (!node.IsFolder)
                {
                    string ext = Path.GetExtension(node.Name);
                    string catName = _categoryMap.TryGetValue(ext, out var cat) ? cat.Name : locOther;

                    if (!tally.ContainsKey(catName)) tally[catName] = 0;
                    tally[catName] += node.SizeBytes;
                }
                foreach (var child in node.Children) TallyNode(child);
            }

            foreach (var root in rootNodes) TallyNode(root);

            long totalBytes = tally.Values.Sum();

            FileCategories.Add(new FileCategoryInsight
            {
                CategoryName = locAllFiles,
                SizeBytes = totalBytes,
                Percentage = 100,
                ColorHex = "#FFFFFF",
                IconGlyph = "\uE81E"
            });

            foreach (var kvp in tally.OrderByDescending(x => x.Value))
            {
                var mapping = _categoryMap.Values.FirstOrDefault(v => v.Name == kvp.Key);
                FileCategories.Add(new FileCategoryInsight
                {
                    CategoryName = kvp.Key,
                    SizeBytes = kvp.Value,
                    Percentage = totalBytes > 0 ? ((double)kvp.Value / totalBytes) * 100 : 0,
                    ColorHex = mapping.Color ?? "#8E8E93",
                    IconGlyph = mapping.Icon ?? "\uE713"
                });
            }
        }

        public void FilterTreeByCategory(string categoryName)
        {
            AnalyzedNodes.Clear();

            string locAllFiles = ResourceString.GetString("analyzer_cat_all_files") ?? "All Files";
            string locOther = ResourceString.GetString("analyzer_cat_other") ?? "Other";

            if (categoryName == locAllFiles)
            {
                foreach (var node in _unfilteredRootNodes) AnalyzedNodes.Add(node);
                return;
            }

            StorageNode? CloneAndFilter(StorageNode node)
            {
                if (!node.IsFolder)
                {
                    string ext = Path.GetExtension(node.Name);
                    string cat = _categoryMap.TryGetValue(ext, out var c) ? c.Name : locOther;

                    if (cat == categoryName)
                    {
                        return new StorageNode
                        {
                            Name = node.Name,
                            Path = node.Path,
                            SizeBytes = node.SizeBytes,
                            IsFolder = false
                        };
                    }
                    return null;
                }

                var clonedFolder = new StorageNode
                {
                    Name = node.Name,
                    Path = node.Path,
                    IsFolder = true,
                    IsExpanded = true
                };
                long newSize = 0;

                foreach (var child in node.Children)
                {
                    var filteredChild = CloneAndFilter(child);
                    if (filteredChild != null)
                    {
                        clonedFolder.Children.Add(filteredChild);
                        newSize += filteredChild.SizeBytes;
                    }
                }

                if (clonedFolder.Children.Count > 0)
                {
                    clonedFolder.SizeBytes = newSize;
                    clonedFolder.Percentage = 100;

                    return clonedFolder;
                }

                return null;
            }

            foreach (var root in _unfilteredRootNodes)
            {
                var filteredRoot = CloneAndFilter(root);
                if (filteredRoot != null) AnalyzedNodes.Add(filteredRoot);
            }
        }

        [RelayCommand]
        private async Task BrowseAndAnalyzeFolderAsync()
        {
            if (IsBusy) return;

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
            {
                await RunStorageAnalyzerAsync(folder.Path);
            }
        }

        [RelayCommand]
        private void CancelStorageAnalysis()
        {
            _analyzerCts?.Cancel();
        }

        [RelayCommand]
        private void ShowInExplorer(StorageNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Path)) return;
            try
            {
                Process.Start("explorer.exe", $"/select,\"{node.Path}\"");
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }

        [RelayCommand]
        private async Task UnlockStorageItemAsync(StorageNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Path)) return;

            AnalyzerStatusText = ResourceString.GetString("analyzer_status_unlocking") ?? "Granting access and checking for locks...";
            IsBusy = true;

            try
            {
                List<string> terminatedProcesses = await Task.Run(() =>
                {
                    TakingOwnership.GrantAdministratorsAccess(
                        node.Path,
                        TakingOwnership.SE_OBJECT_TYPE.SE_FILE_OBJECT);

                    var lockingNames = UnlockHandleHelper.GetLockingProcessNames(node.Path);

                    UnlockHandleHelper.UnlockDirectory(node.Path);

                    return lockingNames;
                });

                if (terminatedProcesses.Count > 0)
                {
                    string names = string.Join(", ", terminatedProcesses.Take(3));
                    if (terminatedProcesses.Count > 3) names += " and others";

                    AnalyzerStatusText = string.Format(ResourceString.GetString("analyzer_status_unlocked_procs") ?? "Access granted & unlocked. Terminated: {0}", names);
                }
                else
                {
                    AnalyzerStatusText = ResourceString.GetString("analyzer_status_unlocked_nolocks") ?? "Access granted. No active file locks found.";
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                AnalyzerStatusText = "Failed to unlock item.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteStorageItemAsync(StorageNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Path)) return;
            IsBusy = true;
            try
            {
                await Task.Run(() =>
                {
                    if (node.IsFolder)
                        Directory.Delete(node.Path, true);
                    else
                        File.Delete(node.Path);
                });

                RemoveNodeFromTree(AnalyzedNodes, node);
                AnalyzerStatusText = string.Format(ResourceString.GetString("analyzer_status_deleted") ?? "Deleted {0}", node.Name);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                AnalyzerStatusText = $"Delete failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private StorageNode BuildStorageTree(string path, CancellationToken token)
        {
            var node = new StorageNode
            {
                Name = Path.GetFileName(path),
                Path = path,
                IsFolder = true
            };

            if (string.IsNullOrEmpty(node.Name)) node.Name = path;

            try
            {
                var dirInfo = new DirectoryInfo(path);
                node.LastModified = dirInfo.LastWriteTime;

                node.IsHidden = (dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                        (dirInfo.Attributes & FileAttributes.System) == FileAttributes.System;

                foreach (var file in dirInfo.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.None }))
                {
                    if (token.IsCancellationRequested) return node;

                    long fileSize = file.Length;

                    long allocatedSize = ((fileSize + 4095) / 4096) * 4096;

                    node.SizeBytes += fileSize;
                    node.AllocatedSizeBytes += allocatedSize;
                    node.FilesCount++;

                    node.Children.Add(new StorageNode
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        IsFolder = false,
                        SizeBytes = fileSize,
                        AllocatedSizeBytes = allocatedSize,
                        FilesCount = 0,
                        FoldersCount = 0,
                        LastModified = file.LastWriteTime
                    });
                }

                foreach (var dir in dirInfo.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.None }))
                {
                    if (token.IsCancellationRequested) return node;

                    if ((dir.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;

                    var childNode = BuildStorageTree(dir.FullName, token);
                    if (childNode != null && childNode.SizeBytes > 0)
                    {
                        childNode.Depth = node.Depth + 1;

                        childNode.IsHidden = (dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                                     (dir.Attributes & FileAttributes.System) == FileAttributes.System;

                        node.SizeBytes += childNode.SizeBytes;
                        node.AllocatedSizeBytes += childNode.AllocatedSizeBytes;
                        node.FilesCount += childNode.FilesCount;
                        node.FoldersCount += (childNode.FoldersCount + 1);

                        node.Children.Add(childNode);
                    }
                }

                var sortedChildren = node.Children.OrderByDescending(c => c.SizeBytes).ToList();
                node.Children.Clear();
                foreach (var child in sortedChildren)
                {
                    child.Percentage = node.SizeBytes > 0 ? ((double)child.SizeBytes / node.SizeBytes) * 100 : 0;
                    node.Children.Add(child);
                }
            }
            catch (UnauthorizedAccessException) { /* System protected folder, ignore */ }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }

            return node;
        }

        private bool RemoveNodeFromTree(ObservableCollection<StorageNode> nodes, StorageNode targetNode)
        {
            if (nodes.Contains(targetNode))
            {
                nodes.Remove(targetNode);
                return true;
            }
            foreach (var node in nodes)
            {
                if (RemoveNodeFromTree(node.Children, targetNode))
                    return true;
            }
            return false;
        }

        public void GenerateAnalyzerInsights(StorageNode rootNode)
        {
            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                AnalyzerInsights.Clear();

                if (rootNode == null || rootNode.Children.Count == 0 || rootNode.SizeBytes == 0)
                    return;

                string[] colors = { "#FF3B30", "#FF9500", "#FFCC00", "#4CD964", "#5AC8FA", "#007AFF", "#5856D6" };

                double totalVisualWidth = 1000.0;

                var topNodes = rootNode.Children.OrderByDescending(n => n.SizeBytes).ToList();

                var largestNodes = topNodes.Take(5).ToList();

                int colorIndex = 0;
                foreach (var node in largestNodes)
                {
                    double percentage = (double)node.SizeBytes / rootNode.SizeBytes;

                    if (percentage > 0.01)
                    {
                        AnalyzerInsights.Add(new StorageInsight
                        {
                            ColorHex = colors[colorIndex % colors.Length],
                            DynamicWidth = percentage * totalVisualWidth,
                            TooltipText = $"{node.Name} - {node.FormattedSize} ({percentage:P1})",
                            TargetNode = node
                        });
                        colorIndex++;
                    }
                }

                var otherNodes = topNodes.Skip(5).ToList();
                long otherSizeBytes = otherNodes.Sum(n => n.SizeBytes);

                if (otherSizeBytes > 0)
                {
                    double otherPercentage = (double)otherSizeBytes / rootNode.SizeBytes;
                    AnalyzerInsights.Add(new StorageInsight
                    {
                        ColorHex = "#8E8E93",
                        DynamicWidth = otherPercentage * totalVisualWidth,
                        TooltipText = $"Other files & folders - {otherSizeBytes.FormatBytes()} ({otherPercentage:P1})",
                        TargetNode = null
                    });
                }
            });
        }
        #endregion

        #region Commands (Cleaner)
        [RelayCommand] private void SelectAll() => SetAllSelected(true);
        [RelayCommand] private void SelectNone() => SetAllSelected(false);
        [RelayCommand] private void SelectDefaults() => SetAllDefaults();
        [RelayCommand] private void ExpandAll() => SetAllExpanded(true);
        [RelayCommand] private void CollapseAll() => SetAllExpanded(false);
        [RelayCommand] private void SortResultsDesc() => SortResultLinesBySize(descending: true);
        [RelayCommand] private void SortResultsAsc() => SortResultLinesBySize(descending: false);
        [RelayCommand] private void ClearDetail() => SelectedResultLine = null;

        [RelayCommand]
        private async Task CleanSelected()
        {
            var entry = Categories.SelectMany(c => c.Entries)
                                  .FirstOrDefault(e => e.Name == SelectedResultLine?.AppName);
            if (entry is not null) await CleanSingleEntryAsync(entry);
        }

        [RelayCommand]
        private async Task ApplyCustomPathAsync()
        {
            var path = CustomPath.Trim();
            if (string.IsNullOrEmpty(path))
            {
                SettingsEngine.CustomWinapp2Path = null;
                IsCustomSource = false;
                StatusText = "";
                await LoadWinapp2Async(new List<string> { Winapp2LocalPath });
                return;
            }
            if (!File.Exists(path)) { StatusText = $"File not found: {path}"; return; }

            SettingsEngine.CustomWinapp2Path = path;
            IsCustomSource = true;
            StatusText = "Custom path saved.";
            await LoadWinapp2Async(new List<string> { path });
        }

        [RelayCommand]
        private async Task RemoveCustomPathAsync()
        {
            CustomPath = "";
            SettingsEngine.CustomWinapp2Path = null;
            IsCustomSource = false;
            StatusText = "";
            await LoadWinapp2Async(new List<string> { Winapp2LocalPath });
        }

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadLatestAsync()
        {
            string targetPath = _lastPaths.FirstOrDefault() ?? Winapp2LocalPath;

            await DownloadFileAsync(Winapp2Url, targetPath, "Winapp2");
            await LoadWinapp2Async(_lastPaths);
        }
        private bool CanDownload() => !IsBusy;
        #endregion

        #region Load (Cleaner)
        [RelayCommand(CanExecute = nameof(CanRefresh))]
        private async Task RefreshAsync() => await LoadWinapp2Async(_lastPaths);
        private bool CanRefresh() => !IsBusy && _lastPaths.Count > 0;

        public async Task LoadWinapp2Async(IList<string> filePaths, CancellationToken token = default)
        {
            IsPostCleanEnabled = SettingsEngine.IsPostCleanEnabled;
            PostCleanCommands = SettingsEngine.PostCleanCommands;
            IsScheduledCleanEnabled = SettingsEngine.IsScheduledCleanEnabled;
            ScheduledCleanDayIndex = SettingsEngine.ScheduledCleanDayIndex;
            ScheduledCleanTime = SettingsEngine.ScheduledCleanTime;

            _lastPaths = new List<string>(filePaths);

            CustomPath = SettingsEngine.CustomWinapp2Path ?? "";
            IsCustomSource = !string.IsNullOrWhiteSpace(SettingsEngine.CustomWinapp2Path);

            var targetPaths = IsCustomSource && File.Exists(CustomPath)
                ? new List<string> { CustomPath }
                : filePaths.Where(File.Exists).ToList();

            IsBusy = true;

            if (token.IsCancellationRequested) return;

            RefreshFileInfo();

            if (targetPaths.Count == 0 || IsWinapp2NotAvailable)
            {
                Categories.Clear();
                RefreshCategoryState();
                IsBusy = false;
                StatusText = ResourceString.GetString("cleanup_status_db_missing") ?? "Database missing. Click the settings icon to download Winapp2.ini.";
                return;
            }

            StatusText = ResourceString.GetString("cleanup_status_parsing") ?? "Parsing database...";

            var allEntries = new List<CleanerEntry>();
            foreach (var path in targetPaths)
            {
                if (token.IsCancellationRequested) return;

                try
                {
                    allEntries.AddRange(await _parser.ParseFileAsync(path));
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            }

            if (token.IsCancellationRequested) return;

            allEntries = allEntries.DistinctBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();

            var installedEntries = await Task.Run(() =>
                allEntries.Where(_detection.IsInstalled).ToList(), token);

            if (token.IsCancellationRequested) return;

            _loadedEntries = installedEntries;
            RebuildVisibleCategories();

            await InitializeHistoryAsync();

            if (token.IsCancellationRequested) return;

            RefreshFileInfo();

            StatusText = string.Format(ResourceString.GetString("cleanup_status_analysis_ready") ?? "Ready. {0} rules applied.", installedEntries.Count, allEntries.Count);
            RefreshCategoryState();
            IsBusy = false;
        }
        #endregion

        #region Analyze & Clean (Cleaner)
        [RelayCommand(CanExecute = nameof(CanAnalyze))]
        private async Task AnalyzeAsync()
        {
            var selected = GetSelectedEntries();
            if (selected.Count == 0) { StatusText = ResourceString.GetString("cleanup_status_nothing_selected") ?? "No items selected."; return; }

            BeginResultsRun(ResourceString.GetString("cleanup_status_scanning") ?? "Scanning selected items...");

            long totalBytes = 0; int totalFiles = 0; int totalReg = 0;
            var progress = new Progress<string>(msg => StatusText = msg);

            foreach (var entry in selected)
            {
                var result = await AnalyzeEntryInternalAsync(entry, progress, keepDetailSelection: false);
                totalBytes += result.TotalBytes;
                totalFiles += result.FilesToDelete.Count;
                totalReg += result.RegistryToDelete.Count;
            }

            TotalSize = totalBytes.FormatBytes();
            StatusText = string.Format(ResourceString.GetString("cleanup_status_scan_complete") ?? "Scan complete: {0} files found.", totalFiles, totalReg, TotalSize);

            UpdateHeatmap(ResultLines);

            IsBusy = false;
        }

        private bool CanAnalyze() => !IsBusy && Categories.Count > 0;

        [RelayCommand(CanExecute = nameof(CanClean))]
        private async Task RunCleanerAsync()
        {
            if (_lastScan.Count == 0)
                await AnalyzeAsync();

            IsBusy = true;
            SelectedResultLine = null;
            StatusText = ResourceString.GetString("cleanup_status_cleaning") ?? "Cleaning...";

            var scannedBytes = _lastScan.Sum(r => r.TotalBytes);
            var (removed, freedBytes) = await CleanResultsAsync(_lastScan.ToList(), new Progress<string>(msg => StatusText = msg));
            var skippedBytes = scannedBytes - freedBytes;

            await RecordCleaningSessionAsync(freedBytes);

            _lastScan.Clear();
            ResultLines.Clear();
            ResultLines.Add(new ScanResultLine(ResourceString.GetString("cleanup_status_done") ?? "Done", removed, 0, "", 0, null));
            ClearAllEntrySizes();

            TotalSize = "";
            StatusText = skippedBytes > 0
                ? string.Format(ResourceString.GetString("cleanup_status_finished_skipped") ?? "Cleaned {0}, Skipped {1}", freedBytes.FormatBytes(), skippedBytes.FormatBytes())
                : string.Format(ResourceString.GetString("cleanup_status_finished_removed") ?? "Removed {0} items ({1})", removed, freedBytes.FormatBytes());

            await RunPostCleanTasksAsync();

            IsBusy = false;
        }
        #endregion

        #region History & Analytics
        private async Task InitializeHistoryAsync()
        {
            var history = await LoadHistoryAsync();
            UpdateAnalyticsDashboard(history);
        }

        private async Task RecordCleaningSessionAsync(long bytesRecovered)
        {
            if (bytesRecovered <= 0) return;

            var history = await LoadHistoryAsync();
            history.Add(new CleaningSession(DateTime.Now, bytesRecovered));

            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            history = history.Where(x => x.Timestamp >= thirtyDaysAgo).ToList();

            await SaveHistoryAsync(history);
            UpdateAnalyticsDashboard(history);
        }

        private void UpdateAnalyticsDashboard(List<CleaningSession> history)
        {
            HistoryChart.Clear();
            if (history.Count == 0) return;

            long totalBytes = history.Sum(x => x.BytesRecovered);
            TotalSpaceSaved = totalBytes.FormatBytes();

            var dailyTotals = history
                .GroupBy(x => x.Timestamp.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.BytesRecovered));

            long maxDailyBytes = dailyTotals.Values.Count > 0 ? dailyTotals.Values.Max() : 1;
            double maxBarHeight = 80.0;

            for (int i = 29; i >= 0; i--)
            {
                var targetDate = DateTime.Now.Date.AddDays(-i);
                long bytesForDay = dailyTotals.TryGetValue(targetDate, out long b) ? b : 0;

                double height = bytesForDay == 0 ? 2 : (bytesForDay / (double)maxDailyBytes) * maxBarHeight;

                HistoryChart.Add(new HistoryBarItem
                {
                    BarHeight = Math.Max(height, 2),
                    DayLabel = targetDate.ToString("dd"),
                    TooltipText = $"{targetDate:MMM dd}: {bytesForDay.FormatBytes()}",
                    Opacity = bytesForDay == 0 ? 0.2 : 1.0
                });
            }
        }

        private async Task<List<CleaningSession>> LoadHistoryAsync()
        {
            if (!File.Exists(_historyFilePath)) return new List<CleaningSession>();
            try
            {
                var json = await File.ReadAllTextAsync(_historyFilePath);

                return JsonSerializer.Deserialize(json, HistoryJsonContext.Default.ListCleaningSession)
                       ?? new List<CleaningSession>();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                return new List<CleaningSession>();
            }
        }

        private async Task SaveHistoryAsync(List<CleaningSession> history)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_historyFilePath)!);

                var json = JsonSerializer.Serialize(history, HistoryJsonContext.Default.ListCleaningSession);
                await File.WriteAllTextAsync(_historyFilePath, json);
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }
        #endregion

        #region Single/Category Cleaning Operations
        private bool CanClean() => !IsBusy && Categories.Count > 0;

        public async Task AnalyzeSingleEntryAsync(DiskCleanupEntryViewModel entryVm)
        {
            if (IsBusy) return;

            IsBusy = true;
            SelectedResultLine = null;

            var result = await AnalyzeEntryInternalAsync(entryVm.Entry,
                new Progress<string>(msg => StatusText = msg), keepDetailSelection: true);

            StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_scanned") ?? "Scanned {0}", entryVm.Name, result.FilesToDelete.Count, result.RegistryToDelete.Count);
            UpdateTotalsFromLastScan();
            IsBusy = false;
        }

        public async Task CleanSingleEntryAsync(DiskCleanupEntryViewModel entryVm)
        {
            if (IsBusy) return;

            IsBusy = true;
            SelectedResultLine = null;

            var result = await EnsureEntryScanAsync(entryVm, new Progress<string>(msg => StatusText = msg));
            if (result.FilesToDelete.Count == 0 && result.RegistryToDelete.Count == 0)
            {
                StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_nothing") ?? "Nothing to clean for {0}", entryVm.Name);
                IsBusy = false;
                return;
            }

            var (removed, freedBytes) = await _cleaner.CleanAsync(result, new Progress<string>(msg => StatusText = msg));
            RemoveScanResult(result);
            entryVm.SizeText = "";

            await RecordCleaningSessionAsync(freedBytes);

            UpdateTotalsFromLastScan();
            StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_cleaned") ?? "Cleaned {0}", entryVm.Name, removed, freedBytes.FormatBytes());
            IsBusy = false;
        }

        public async Task AnalyzeCategoryAsync(DiskCleanupCategoryViewModel categoryVm)
        {
            if (IsBusy) return;

            BeginResultsRun(string.Format(ResourceString.GetString("cleanup_status_cat_scanning") ?? "Scanning {0}...", categoryVm.Name));
            var progress = new Progress<string>(msg => StatusText = msg);

            var selected = categoryVm.Entries.Where(e => e.IsSelected).ToList();
            foreach (var entryVm in selected)
                await AnalyzeEntryInternalAsync(entryVm.Entry, progress, keepDetailSelection: false);

            UpdateTotalsFromLastScan();
            StatusText = string.Format(ResourceString.GetString("cleanup_status_cat_scanned") ?? "Scanned category {0}", categoryVm.Name, selected.Count);
            IsBusy = false;
        }

        public async Task CleanCategoryAsync(DiskCleanupCategoryViewModel categoryVm)
        {
            if (IsBusy) return;

            IsBusy = true;
            SelectedResultLine = null;
            StatusText = string.Format(ResourceString.GetString("cleanup_status_cat_cleaning") ?? "Cleaning {0}...", categoryVm.Name);
            var progress = new Progress<string>(msg => StatusText = msg);

            var selected = categoryVm.Entries.Where(e => e.IsSelected).ToList();
            foreach (var vm in selected.Where(vm => !_lastScan.Any(r => r.Entry == vm.Entry)))
                await AnalyzeEntryInternalAsync(vm.Entry, progress, keepDetailSelection: false);

            var results = _lastScan.Where(r => selected.Any(vm => vm.Entry == r.Entry)).ToList();
            var (removed, freedBytes) = await CleanResultsAsync(results, progress);

            await RecordCleaningSessionAsync(freedBytes);

            UpdateTotalsFromLastScan();
            StatusText = string.Format(ResourceString.GetString("cleanup_status_cat_cleaned") ?? "Cleaned {0}", categoryVm.Name, removed, freedBytes.FormatBytes());
            IsBusy = false;
        }
        #endregion

        #region Warnings
        public IReadOnlyList<string> GetWarningsForSelectedEntries() =>
            BuildWarnings(Categories.SelectMany(c => c.Entries).Where(e => e.IsSelected));

        public IReadOnlyList<string> GetWarningsForEntry(DiskCleanupEntryViewModel entryVm) =>
            BuildWarnings([entryVm]);

        public IReadOnlyList<string> GetWarningsForCategory(DiskCleanupCategoryViewModel categoryVm) =>
            BuildWarnings(categoryVm.Entries);

        private static IReadOnlyList<string> BuildWarnings(IEnumerable<DiskCleanupEntryViewModel> entries) =>
            entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Warning))
                .Select(e => $"{e.Name}{Environment.NewLine}{e.Warning}")
                .Distinct(StringComparer.Ordinal)
                .ToList();
        #endregion

        #region Private Helpers
        private async Task RunPostCleanTasksAsync()
        {
            if (!IsPostCleanEnabled || string.IsNullOrWhiteSpace(PostCleanCommands)) return;

            StatusText = ResourceString.GetString("cleanup_status_post_tasks") ?? "Running post-clean scripts...";

            var commands = PostCleanCommands.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var cmd in commands)
            {
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                await CommandExecutor.InvokeRunCommand(cmd, isPowerShell: false);
            }
        }

        private async Task DownloadFileAsync(string url, string destination, string label)
        {
            IsBusy = true;
            StatusText = $"Downloading {label}…";
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

                http.DefaultRequestHeaders.Add("User-Agent", "EvolveOS_Optimizer");

                var content = await http.GetStringAsync(url);
                await File.WriteAllTextAsync(destination, content);
                StatusText = $"{label} downloaded — {content.Length / 1024} KB";
            }
            catch (Exception ex)
            {
                StatusText = $"Download failed: {ex.Message}";
                ErrorLogging.LogDebug(ex);
            }
            finally { IsBusy = false; }
        }

        private void RefreshFileInfo()
        {
            string targetPath = IsCustomSource && !string.IsNullOrWhiteSpace(CustomPath)
                ? CustomPath
                : (_lastPaths.FirstOrDefault() ?? Winapp2LocalPath);

            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
            {
                IsWinapp2NotAvailable = true;
                Winapp2Info = ResourceString.GetString("cleanup_settings_db_missing") ?? "Not downloaded";
                return;
            }

            IsWinapp2NotAvailable = false;
            Winapp2Info = BuildFileInfo(targetPath);
        }

        private static string BuildFileInfo(string path)
        {
            try
            {
                if (!File.Exists(path)) return "Not downloaded";
                var fi = new FileInfo(path);
                var lines = File.ReadLines(path).Count(l => l.StartsWith('[') && !l.StartsWith("[Winapp2"));
                return $"{lines} entries  •  {fi.Length / 1024} KB  •  {fi.LastWriteTime:yyyy-MM-dd}";
            }
            catch { return ""; }
        }

        private void SortResultLinesBySize(bool descending = true)
        {
            var sorted = descending
                ? ResultLines.OrderByDescending(l => l.Result?.TotalBytes ?? 0).ToList()
                : ResultLines.OrderBy(l => l.Result?.TotalBytes ?? 0).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                int from = ResultLines.IndexOf(sorted[i]);
                if (from != i) ResultLines.Move(from, i);
            }
        }

        private void RebuildVisibleCategories()
        {
            var visible = string.IsNullOrWhiteSpace(SearchText)
                ? _loadedEntries
                : _loadedEntries.Where(e => e.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            RebuildCategories(visible);
        }

        private void RebuildCategories(List<CleanerEntry> entries)
        {
            Categories.Clear();

            var groups = entries
                .Select(e => new { Entry = e, Category = CategoryResolverService.TryMapLangSecRef(e) })
                .GroupBy(x => x.Category)
                .OrderBy(g => g.Key.Order)
                .ThenBy(g => g.Key.Name, StringComparer.OrdinalIgnoreCase);

            var saved = SettingsEngine.SelectedCleanerEntries;

            foreach (var group in groups)
            {
                var catVm = new DiskCleanupCategoryViewModel(group.Key.Name, group.Key.Glyph);
                foreach (var item in group.OrderBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var entryVm = new DiskCleanupEntryViewModel(item.Entry);

                    entryVm.IsSelected = saved.Count > 0
                        ? saved.Contains(item.Entry.Name)
                        : item.Entry.Default;

                    entryVm.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(DiskCleanupEntryViewModel.IsSelected))
                        {
                            SaveSelection(entryVm);
                            catVm.UpdateSelectionState();
                        }
                    };

                    catVm.Entries.Add(entryVm);
                }
                Categories.Add(catVm);
                catVm.UpdateSelectionState();
            }
        }

        private void RefreshCategoryState()
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsNotEmpty));
            OnPropertyChanged(nameof(HasSearchText));
            OnPropertyChanged(nameof(CanRunCleaner));
            AnalyzeCommand.NotifyCanExecuteChanged();
            RunCleanerCommand.NotifyCanExecuteChanged();
            DownloadLatestCommand.NotifyCanExecuteChanged();
        }

        private void BeginResultsRun(string status)
        {
            IsBusy = true;
            SelectedResultLine = null;
            ResultLines.Clear();
            _lastScan.Clear();
            StatusText = status;
        }

        private List<CleanerEntry> GetSelectedEntries() =>
            Categories.SelectMany(c => c.Entries).Where(e => e.IsSelected).Select(e => e.Entry).ToList();

        private int CountVisibleEntries() =>
            Categories.Sum(c => c.Entries.Count);

        private async Task<ScanResult> AnalyzeEntryInternalAsync(CleanerEntry entry, IProgress<string> progress, bool keepDetailSelection)
        {
            RemoveScanResult(entry);

            var result = await _cleaner.AnalyzeAsync(entry, progress);
            _lastScan.Add(result);
            UpdateEntrySize(entry, result.FormattedSize);

            ScanResultLine? line = null;
            if (result.FilesToDelete.Count > 0 || result.RegistryToDelete.Count > 0)
            {
                long rawSize = result.FilesToDelete.Sum(f => new FileInfo(f).Length);

                line = new ScanResultLine(
                    entry.Name,
                    result.FilesToDelete.Count,
                    result.RegistryToDelete.Count,
                    result.FormattedSize,
                    rawSize,
                    result
                );
                ResultLines.Add(line);
            }

            if (keepDetailSelection && line is not null)
                SelectedResultLine = line;

            return result;
        }

        private async Task<ScanResult> EnsureEntryScanAsync(DiskCleanupEntryViewModel entryVm, IProgress<string> progress)
        {
            var existing = _lastScan.FirstOrDefault(r => r.Entry == entryVm.Entry);
            if (existing is not null) return existing;

            StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_scanning") ?? "Scanning {0}", entryVm.Name);
            return await AnalyzeEntryInternalAsync(entryVm.Entry, progress, keepDetailSelection: false);
        }

        private async Task<(int count, long bytes)> CleanResultsAsync(List<ScanResult> results, IProgress<string> progress)
        {
            int count = 0;
            long bytes = 0;
            foreach (var result in results)
            {
                var (c, b) = await _cleaner.CleanAsync(result, progress);
                count += c;
                bytes += b;
                RemoveScanResult(result);
                UpdateEntrySize(result.Entry, "");
            }
            return (count, bytes);
        }

        private void UpdateHeatmap(IEnumerable<ScanResultLine> results)
        {
            CategoryInsights.Clear();

            long totalBytes = results.Sum(r => r.SizeBytes);
            if (totalBytes == 0) return;

            string[] palette = { "#0078D4", "#50E6FF", "#8E44AD", "#27AE60", "#F39C12", "#E74C3C" };
            int colorIndex = 0;

            foreach (var group in results.OrderByDescending(r => r.SizeBytes).Take(6))
            {
                double pct = ((double)group.SizeBytes / totalBytes) * 100;

                if (pct < 1) continue;

                CategoryInsights.Add(new StorageInsight
                {
                    CategoryName = group.AppName,
                    Percentage = pct,
                    ColorHex = palette[colorIndex % palette.Length],
                    DynamicWidth = Math.Max(2, (pct / 100) * 370),
                    TooltipText = $"{group.AppName}: {pct:F1}%"
                });

                colorIndex++;
            }
        }

        private void RemoveScanResult(CleanerEntry entry)
        {
            var existing = _lastScan.FirstOrDefault(r => r.Entry == entry);
            if (existing is not null) RemoveScanResult(existing);
        }

        private void RemoveScanResult(ScanResult result)
        {
            _lastScan.Remove(result);
            var line = ResultLines.FirstOrDefault(l => l.Result == result);
            if (line is not null) ResultLines.Remove(line);
        }

        private void UpdateEntrySize(CleanerEntry entry, string sizeText)
        {
            var vm = Categories.SelectMany(c => c.Entries).FirstOrDefault(e => e.Entry == entry);
            if (vm is not null) vm.SizeText = sizeText;
        }

        private void ClearAllEntrySizes()
        {
            foreach (var entry in Categories.SelectMany(c => c.Entries))
                entry.SizeText = "";
        }

        private void UpdateTotalsFromLastScan()
        {
            TotalSize = _lastScan.Count > 0
                ? _lastScan.Sum(r => r.TotalBytes).FormatBytes()
                : "";
        }

        private void RebuildDetailLines(ScanResultLine? line)
        {
            DetailLines.Clear();
            if (line?.Result is not { } result) return;

            if (result.FilesToDelete.Count > 0)
            {
                string fileHeader = $"{ResourceString.GetString("cleanup_detail_files")} ({result.FilesToDelete.Count})";
                DetailLines.Add(new DetailLine(fileHeader, true));

                foreach (var filePath in result.FilesToDelete)
                {
                    DetailLines.Add(new DetailLine(Path.GetFileName(filePath), false, filePath));
                }
            }

            var regItems = result.RegistryToDelete.Select(r => r.ToString()).ToList();
            if (regItems.Count > 0)
            {
                string regHeader = $"{ResourceString.GetString("cleanup_detail_registry")} ({regItems.Count})";
                DetailLines.Add(new DetailLine(regHeader, true));

                foreach (var regPath in regItems)
                {
                    if (string.IsNullOrEmpty(regPath)) continue;

                    DetailLines.Add(new DetailLine(regPath, false, regPath));
                }
            }
        }

        private void SetAllSelected(bool value)
        {
            _suppressSave = true;
            foreach (var entry in Categories.SelectMany(c => c.Entries))
                entry.IsSelected = value;
            _suppressSave = false;
            SaveSelection();
        }

        private void SetAllDefaults()
        {
            _suppressSave = true;
            foreach (var entry in Categories.SelectMany(c => c.Entries))
                entry.IsSelected = entry.Entry.Default;
            _suppressSave = false;
            SaveSelection();
        }

        private void SetAllExpanded(bool value)
        {
            foreach (var cat in Categories)
                cat.IsExpanded = value;
        }

        private void SaveSelection()
        {
            if (_suppressSave) return;

            SettingsEngine.SelectedCleanerEntries = Categories
                .SelectMany(c => c.Entries)
                .Where(e => e.IsSelected)
                .Select(e => e.Name)
                .ToHashSet();
        }

        private void SaveSelection(DiskCleanupEntryViewModel entry)
        {
            if (_suppressSave) return;

            var selected = SettingsEngine.SelectedCleanerEntries.Count > 0
                ? SettingsEngine.SelectedCleanerEntries
                : _loadedEntries.Where(e => e.Default).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (entry.IsSelected) selected.Add(entry.Name);
            else selected.Remove(entry.Name);

            SettingsEngine.SelectedCleanerEntries = selected;
        }
        #endregion

        #region Cleanup & Memory Management
        public void DisposeCollections()
        {
            _analyzerCts?.Cancel();
            Categories?.Clear();
            ResultLines?.Clear();
            DetailLines?.Clear();
            CategoryInsights?.Clear();
            HistoryChart?.Clear();
            AnalyzedNodes?.Clear();
            _lastScan?.Clear();
            _loadedEntries?.Clear();

            Debug.WriteLine("[DiskCleanupVM] Large collections zeroed for Search & Destroy Purge.");
        }
        #endregion
    }

    #region Supporting Types

    public partial class DiskCleanupCategoryViewModel : ObservableObject
    {
        public string Name { get; }
        public string Glyph { get; }
        public ObservableCollection<DiskCleanupEntryViewModel> Entries { get; } = new();

        [ObservableProperty] public partial bool IsExpanded { get; set; } = false;

        public string SelectionStateLabel
        {
            get => _selectionStateLabel;
            set => SetProperty(ref _selectionStateLabel, value);
        }
        private string _selectionStateLabel = "";

        private Brush? _badgeBackground;
        public Brush? BadgeBackground
        {
            get => _badgeBackground;
            set => SetProperty(ref _badgeBackground, value);
        }

        private Brush? _badgeForeground;
        public Brush? BadgeForeground
        {
            get => _badgeForeground;
            set => SetProperty(ref _badgeForeground, value);
        }

        [RelayCommand] private void SelectAll() { Entries.ToList().ForEach(e => e.IsSelected = true); UpdateSelectionState(); }
        [RelayCommand] private void SelectNone() { Entries.ToList().ForEach(e => e.IsSelected = false); UpdateSelectionState(); }
        [RelayCommand] private void SelectDefaults() { Entries.ToList().ForEach(e => e.IsSelected = e.Entry.Default); UpdateSelectionState(); }

        public DiskCleanupCategoryViewModel(string name, string glyph)
        {
            Name = name;
            Glyph = glyph;
        }

        public void UpdateSelectionState()
        {
            Brush GetBrush(string key) =>
                Application.Current.Resources.TryGetValue(key, out var res) && res is Brush b
                    ? b
                    : new SolidColorBrush(Colors.Transparent);

            if (Entries == null || !Entries.Any())
            {
                SelectionStateLabel = ResourceString.GetString("cleanup_badge_none") ?? "None";
                BadgeBackground = GetBrush("CardBackgroundFillColorSecondaryBrush");
                BadgeForeground = GetBrush("TextFillColorSecondaryBrush");
                return;
            }

            int totalCount = Entries.Count;
            int selectedCount = Entries.Count(e => e.IsSelected);

            if (selectedCount == 0)
            {
                SelectionStateLabel = ResourceString.GetString("cleanup_badge_none") ?? "None";
                BadgeBackground = GetBrush("CardBackgroundFillColorSecondaryBrush");
                BadgeForeground = GetBrush("TextFillColorSecondaryBrush");
            }
            else if (selectedCount == totalCount)
            {
                SelectionStateLabel = ResourceString.GetString("cleanup_badge_all") ?? "All";
                BadgeBackground = GetBrush("SystemFillColorSuccessBackgroundBrush");
                BadgeForeground = GetBrush("SystemFillColorSuccessBrush");
            }
            else
            {
                bool isExactlyDefault = Entries.All(e => e.IsSelected == e.Entry.Default);

                if (isExactlyDefault)
                {
                    SelectionStateLabel = ResourceString.GetString("cleanup_badge_default") ?? "Default";
                    BadgeBackground = GetBrush("AccentFillColorDefaultBrush");
                    BadgeForeground = GetBrush("TextOnAccentFillColorPrimaryBrush");
                }
                else
                {
                    SelectionStateLabel = ResourceString.GetString("cleanup_badge_custom") ?? "Custom";
                    BadgeBackground = GetBrush("SystemFillColorCautionBackgroundBrush");
                    BadgeForeground = GetBrush("SystemFillColorCautionBrush");
                }
            }
        }
    }

    public partial class DiskCleanupEntryViewModel : ObservableObject
    {
        public CleanerEntry Entry { get; }

        [ObservableProperty] public partial bool IsSelected { get; set; }
        [ObservableProperty] public partial string SizeText { get; set; } = "";

        public string Name => Entry.Name;

        public string? Warning
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Entry.Warning))
                    return null;

                if (Entry.Warning.StartsWith("loc:", StringComparison.OrdinalIgnoreCase))
                {
                    string key = Entry.Warning.Substring(4).Trim();
                    return ResourceString.GetString(key);
                }

                return Entry.Warning;
            }
        }

        public bool HasWarning => !string.IsNullOrWhiteSpace(Entry.Warning);

        public DiskCleanupEntryViewModel(CleanerEntry entry)
        {
            Entry = entry;
            IsSelected = entry.Default;
        }
    }

    public record ScanResultLine(string AppName, int FileCount, int RegCount, string Size, long SizeBytes, ScanResult? Result = null)
    {
        public string CountSummary
        {
            get
            {
                var parts = new List<string>();
                if (FileCount > 0) parts.Add(string.Format(ResourceString.GetString("cleanup_summary_files") ?? "{0} files", FileCount));
                if (RegCount > 0) parts.Add(string.Format(ResourceString.GetString("cleanup_summary_registry") ?? "{0} registry keys", RegCount));
                return string.Join(" · ", parts);
            }
        }

        public string Summary => FileCount > 0 || RegCount > 0
            ? string.Format(ResourceString.GetString("cleanup_summary_full") ?? "{0} files, {1} registry keys ({2})", FileCount, RegCount, Size)
            : ResourceString.GetString("cleanup_summary_complete") ?? "Complete";
    }

    public record DetailLine(string Text, bool IsHeader, string Path = "")
    {
        public bool IsNotHeader => !IsHeader;
    }

    [System.Text.Json.Serialization.JsonSerializable(typeof(List<CleaningSession>))]
    internal partial class HistoryJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
    #endregion
}