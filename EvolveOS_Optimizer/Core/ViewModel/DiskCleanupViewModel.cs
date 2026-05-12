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
        #endregion

        #region Observable State
        [ObservableProperty] public partial ObservableCollection<DiskCleanupCategoryViewModel> Categories { get; set; } = [];
        [ObservableProperty] public partial ObservableCollection<ScanResultLine> ResultLines { get; set; } = [];
        [ObservableProperty] public partial ObservableCollection<DetailLine> DetailLines { get; set; } = [];
        [ObservableProperty] public partial ScanResultLine? SelectedResultLine { get; set; }
        [ObservableProperty] public partial string SearchText { get; set; } = "";
        [ObservableProperty] public partial string StatusText { get; set; } = ResourceString.GetString("cleanup_status_loading");
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
                ? string.Format(ResourceString.GetString("cleanup_status_search_found"), CountVisibleEntries())
                : ResourceString.GetString("cleanup_status_search_empty");
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

            if (IsScheduledCleanEnabled)
            {
                // Trigger background service update
            }
        }

        partial void OnScheduledCleanTimeChanged(TimeSpan value)
        {
            SettingsEngine.ScheduledCleanTime = value;

            if (IsScheduledCleanEnabled)
            {
                // Trigger background service update
            }
        }
        #endregion

        #region Commands
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

        #region Load
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

            StatusText = ResourceString.GetString("cleanup_status_parsing");

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

            StatusText = string.Format(ResourceString.GetString("cleanup_status_analysis_ready"), installedEntries.Count, allEntries.Count);
            RefreshCategoryState();
            IsBusy = false;
        }
        #endregion

        #region Analyze & Clean
        [RelayCommand(CanExecute = nameof(CanAnalyze))]
        private async Task AnalyzeAsync()
        {
            var selected = GetSelectedEntries();
            if (selected.Count == 0) { StatusText = ResourceString.GetString("cleanup_status_nothing_selected"); return; }

            BeginResultsRun(ResourceString.GetString("cleanup_status_scanning"));

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
            StatusText = string.Format(ResourceString.GetString("cleanup_status_scan_complete"), totalFiles, totalReg, TotalSize);

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
            StatusText = ResourceString.GetString("cleanup_status_cleaning");

            var scannedBytes = _lastScan.Sum(r => r.TotalBytes);
            var (removed, freedBytes) = await CleanResultsAsync(_lastScan.ToList(), new Progress<string>(msg => StatusText = msg));
            var skippedBytes = scannedBytes - freedBytes;

            await RecordCleaningSessionAsync(freedBytes);

            _lastScan.Clear();
            ResultLines.Clear();
            ResultLines.Add(new ScanResultLine(ResourceString.GetString("cleanup_status_done"), removed, 0, "", 0, null));
            ClearAllEntrySizes();

            TotalSize = "";
            StatusText = skippedBytes > 0
                ? string.Format(ResourceString.GetString("cleanup_status_finished_skipped"), freedBytes.FormatBytes(), skippedBytes.FormatBytes())
                : string.Format(ResourceString.GetString("cleanup_status_finished_removed"), removed, freedBytes.FormatBytes());

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

            StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_scanned"), entryVm.Name, result.FilesToDelete.Count, result.RegistryToDelete.Count);
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
                StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_nothing"), entryVm.Name);
                IsBusy = false;
                return;
            }

            var (removed, freedBytes) = await _cleaner.CleanAsync(result, new Progress<string>(msg => StatusText = msg));
            RemoveScanResult(result);
            entryVm.SizeText = "";

            await RecordCleaningSessionAsync(freedBytes);

            UpdateTotalsFromLastScan();
            StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_cleaned"), entryVm.Name, removed, freedBytes.FormatBytes());
            IsBusy = false;
        }

        public async Task AnalyzeCategoryAsync(DiskCleanupCategoryViewModel categoryVm)
        {
            if (IsBusy) return;

            BeginResultsRun(string.Format(ResourceString.GetString("cleanup_status_cat_scanning"), categoryVm.Name));
            var progress = new Progress<string>(msg => StatusText = msg);

            var selected = categoryVm.Entries.Where(e => e.IsSelected).ToList();
            foreach (var entryVm in selected)
                await AnalyzeEntryInternalAsync(entryVm.Entry, progress, keepDetailSelection: false);

            UpdateTotalsFromLastScan();
            StatusText = string.Format(ResourceString.GetString("cleanup_status_cat_scanned"), categoryVm.Name, selected.Count);
            IsBusy = false;
        }

        public async Task CleanCategoryAsync(DiskCleanupCategoryViewModel categoryVm)
        {
            if (IsBusy) return;

            IsBusy = true;
            SelectedResultLine = null;
            StatusText = string.Format(ResourceString.GetString("cleanup_status_cat_cleaning"), categoryVm.Name);
            var progress = new Progress<string>(msg => StatusText = msg);

            var selected = categoryVm.Entries.Where(e => e.IsSelected).ToList();
            foreach (var vm in selected.Where(vm => !_lastScan.Any(r => r.Entry == vm.Entry)))
                await AnalyzeEntryInternalAsync(vm.Entry, progress, keepDetailSelection: false);

            var results = _lastScan.Where(r => selected.Any(vm => vm.Entry == r.Entry)).ToList();
            var (removed, freedBytes) = await CleanResultsAsync(results, progress);

            await RecordCleaningSessionAsync(freedBytes);

            UpdateTotalsFromLastScan();
            StatusText = string.Format(ResourceString.GetString("cleanup_status_cat_cleaned"), categoryVm.Name, removed, freedBytes.FormatBytes());
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

            StatusText = string.Format(ResourceString.GetString("cleanup_status_entry_scanning"), entryVm.Name);
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
                    ColorHex = palette[colorIndex % palette.Length]
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
            Categories?.Clear();
            ResultLines?.Clear();
            DetailLines?.Clear();
            CategoryInsights?.Clear();
            HistoryChart?.Clear();
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
                SelectionStateLabel = ResourceString.GetString("cleanup_badge_none");
                BadgeBackground = GetBrush("CardBackgroundFillColorSecondaryBrush");
                BadgeForeground = GetBrush("TextFillColorSecondaryBrush");
                return;
            }

            int totalCount = Entries.Count;
            int selectedCount = Entries.Count(e => e.IsSelected);

            if (selectedCount == 0)
            {
                SelectionStateLabel = ResourceString.GetString("cleanup_badge_none");
                BadgeBackground = GetBrush("CardBackgroundFillColorSecondaryBrush");
                BadgeForeground = GetBrush("TextFillColorSecondaryBrush");
            }
            else if (selectedCount == totalCount)
            {
                SelectionStateLabel = ResourceString.GetString("cleanup_badge_all");
                BadgeBackground = GetBrush("SystemFillColorSuccessBackgroundBrush");
                BadgeForeground = GetBrush("SystemFillColorSuccessBrush");
            }
            else
            {
                bool isExactlyDefault = Entries.All(e => e.IsSelected == e.Entry.Default);

                if (isExactlyDefault)
                {
                    SelectionStateLabel = ResourceString.GetString("cleanup_badge_default");
                    BadgeBackground = GetBrush("AccentFillColorDefaultBrush");
                    BadgeForeground = GetBrush("TextOnAccentFillColorPrimaryBrush");
                }
                else
                {
                    SelectionStateLabel = ResourceString.GetString("cleanup_badge_custom");
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
                if (FileCount > 0) parts.Add(string.Format(ResourceString.GetString("cleanup_summary_files"), FileCount));
                if (RegCount > 0) parts.Add(string.Format(ResourceString.GetString("cleanup_summary_registry"), RegCount));
                return string.Join(" · ", parts);
            }
        }

        public string Summary => FileCount > 0 || RegCount > 0
            ? string.Format(ResourceString.GetString("cleanup_summary_full"), FileCount, RegCount, Size)
            : ResourceString.GetString("cleanup_summary_complete");
    }

    public record DetailLine(string Text, bool IsHeader, string Path = "")
    {
        public bool IsNotHeader => !IsHeader;
    }

    [System.Text.Json.Serialization.JsonSerializable(typeof(List<CleaningSession>))]
    internal partial class HistoryJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
    #endregion
}