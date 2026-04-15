// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Dispatching;
using Microsoft.Management.Deployment;
using System.IO;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class AppStorePage : Page
    {
        #region Data Models & Records
        private sealed record InstalledPackageEntry(string Id, string Name, string Version, string NormalizedId, string NormalizedName);
        private sealed record DiscoveredPackageEntry(string Id, string Name, string Version);
        #endregion

        #region Properties & State Fields
        public ObservableCollection<WingetPackage> PackageList { get; set; } = new();
        public ObservableCollection<WingetPackage> UpdatesList { get; set; } = new();
        public ObservableCollection<WingetPackage> InstalledList { get; set; } = new();

        private List<WingetPackage> _allPackages = new();
        private List<WingetPackage> _updateablePackages = new();
        private readonly List<InstalledPackageEntry> _installedSnapshot = new();

        private CancellationTokenSource _cts = new();
        private PackageManager? _packageManager;
        private PackageCatalog? _wingetCatalog;
        private PackageCatalog? _localCatalog;

        private bool? _isWingetAvailable;
        private bool _isUsingInventoryFallback;
        private bool _isUsingCliDiscoveryFallback;
        private bool _isUpdatesMode;
        private bool _isInstalledMode;
        private bool _isLoading;
        private bool _suppressSearch;
        private int _updateCheckVersion;
        private int _updateCount;
        private int _searchVersion;

        private string _currentSortMode = "Name";

        private string _wingetVersion = string.Empty;
        private bool _hasCheckedWingetUpdate = false;

        private static readonly Dictionary<string, (string[] RelativePaths, string Arguments)> UninstallQuirks = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Mozilla.Firefox", (new[] { @"Mozilla Firefox\uninstall\helper.exe" }, "/S") },
            { "Notepad++.Notepad++", (new[] { @"Notepad++\uninstall.exe" }, "/S") },
            { "VideoLAN.VLC", (new[] { @"VideoLAN\VLC\uninstall.exe" }, "/S") }
        };
        #endregion

        #region Constructor & Lifecycle
        public AppStorePage()
        {
            InitializeComponent();

            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            Loaded += AppStorePage_Loaded;
        }

        private async void AppStorePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            await ErrorLogging.LogInfo("AppStorePage Loaded and starting package load.");

            if (_allPackages.Count == 0)
                await LoadPackagesAsync();
        }
        #endregion

        #region Core Package Management (Load, Install, Update)
        private async Task LoadPackagesAsync()
        {
            _isLoading = true;
            try
            {
                SearchProgressRing.IsActive = true;
                LoadingState.Visibility = Visibility.Visible;
                PackagesGridView.Visibility = Visibility.Collapsed;
                StatusText.Visibility = Visibility.Collapsed;

                if (!await IsWingetAvailableAsync())
                {
                    SearchProgressRing.IsActive = false;
                    LoadingState.Visibility = Visibility.Collapsed;

                    SetErrorState("Winget is not available on this system.");
                    if (!_hasCheckedWingetUpdate)
                    {
                        _hasCheckedWingetUpdate = true;
                        bool installedSuccessfully = await ShowInstallWingetDialogAsync();

                        if (installedSuccessfully)
                        {
                            _isWingetAvailable = null;
                            _wingetCatalog = null;
                            _isLoading = false;
                            _ = LoadPackagesAsync();
                        }
                    }
                    return;
                }

                if (string.IsNullOrEmpty(_wingetVersion))
                {
                    _wingetVersion = await GetWingetVersionAsync();
                    WingetVersionText.Text = string.Format(ResourceString.GetString("winget_version") ?? "WinGet {0}", _wingetVersion);
                }

                if (!_hasCheckedWingetUpdate)
                {
                    _hasCheckedWingetUpdate = true;
                    var (hasUpdate, newVer) = await CheckForWingetUpdateAsync();
                    if (hasUpdate)
                    {
                        SearchProgressRing.IsActive = false;
                        LoadingState.Visibility = Visibility.Collapsed;

                        bool upgraded = await ShowUpgradeWingetDialogAsync(newVer);

                        SearchProgressRing.IsActive = true;
                        LoadingState.Visibility = Visibility.Visible;

                        if (upgraded)
                        {
                            _wingetVersion = await GetWingetVersionAsync();
                            WingetVersionText.Text = string.Format(ResourceString.GetString("winget_version") ?? "WinGet {0}", _wingetVersion);
                        }
                    }
                }

                _allPackages.Clear();
                PackageList.Clear();

                var installedMap = await GetInstalledPackagesMapAsync();
                var catalog = await EnsureWingetCatalogAsync();
                List<DiscoveredPackageEntry> discovered;

                if (catalog is null)
                {
                    ErrorLogging.LogDebug("COM Catalog is null. Forcing CLI Fallback for Package Discovery.");
                    discovered = await DiscoverPackagesFromWingetCliAsync();
                }
                else
                {
                    discovered = await DiscoverPackagesAsync(catalog);
                }

                if (discovered.Count < 200)
                {
                    ErrorLogging.LogDebug("Catalog didn't return enough packages; appending popular-query fallback.");
                    var fallback = await DiscoverPopularPackagesFallbackAsync();
                    var seenIds = new HashSet<string>(discovered.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
                    foreach (var item in fallback)
                    {
                        if (seenIds.Add(item.Id))
                            discovered.Add(item);
                    }

                    if (discovered.Count == 0)
                    {
                        SetErrorState("No packages found. Try Refresh or search by name.");
                        PackagesGridView.Visibility = Visibility.Visible;
                        return;
                    }
                }

                int matched = 0;
                foreach (var d in discovered)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    var pkg = new WingetPackage
                    {
                        Name = d.Name,
                        Id = d.Id,
                        Category = PackageHelper.GetPublisherDisplayName(d.Id),
                        Version = d.Version
                    };

                    (string Name, string Version) inst = default;
                    bool isInst = false;

                    foreach (var key in PackageHelper.GetLookupKeys(pkg.Id, pkg.Name))
                        if (installedMap.TryGetValue(key, out inst)) { isInst = true; break; }

                    if (!isInst) isInst = TryGetInstalledByHeuristic(pkg, out inst);

                    if (isInst)
                    {
                        pkg.IsInstalled = true;
                        matched++;
                        if (!string.IsNullOrWhiteSpace(inst.Version)) pkg.Version = inst.Version;
                        if (!string.IsNullOrWhiteSpace(inst.Name)) pkg.Name = inst.Name;
                    }
                    else if (string.IsNullOrWhiteSpace(pkg.Version))
                    {
                        pkg.Version = "N/A";
                    }

                    _allPackages.Add(pkg);
                }

                var knownIds = new HashSet<string>(_allPackages.Where(p => !string.IsNullOrWhiteSpace(p.Id)).Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
                var knownNames = new HashSet<string>(_allPackages.Where(p => !string.IsNullOrWhiteSpace(p.Name)).Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

                foreach (var inst in _installedSnapshot)
                {
                    if (string.IsNullOrWhiteSpace(inst.Name)) continue;

                    if ((!string.IsNullOrWhiteSpace(inst.Id) && knownIds.Contains(inst.Id)) ||
                        knownNames.Contains(inst.Name))
                    {
                        continue;
                    }

                    var newPkg = new WingetPackage
                    {
                        Name = inst.Name,
                        Id = string.IsNullOrWhiteSpace(inst.Id) ? "Local Package" : inst.Id,
                        Version = string.IsNullOrWhiteSpace(inst.Version) ? "Installed" : inst.Version,
                        Category = PackageHelper.GetPublisherDisplayName(inst.Id ?? string.Empty),
                        IsInstalled = true
                    };

                    _allPackages.Add(newPkg);
                    matched++;

                    if (!string.IsNullOrWhiteSpace(newPkg.Id)) knownIds.Add(newPkg.Id);
                    knownNames.Add(newPkg.Name);
                }

                _allPackages = _allPackages
                    .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                await ErrorLogging.LogInfo($"Loaded {_allPackages.Count} packages, {matched} already installed.");

                int count = 0;
                foreach (var p in _allPackages)
                {
                    PackageList.Add(p);
                    if (++count % 50 == 0) await Task.Delay(1);
                }

                SearchProgressRing.IsActive = false;
                LoadingState.Visibility = Visibility.Collapsed;
                PackagesGridView.Visibility = Visibility.Visible;
                StatusText.Visibility = Visibility.Collapsed;

                UpdateInstalledTabLabel();

                if (_isUsingCliDiscoveryFallback || catalog is null)
                {
                    await ErrorLogging.LogInfo("Notice: WinGet CLI fallback was used for package discovery.");
                }

                if (_isUsingInventoryFallback)
                {
                    await ErrorLogging.LogInfo("Notice: Registry inventory fallback was used for installed app detection.");
                }

                if (PackageList.Count == 0) SetErrorState("No packages found.");

                _ = CheckAndApplyUpdatesAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Error loading packages: {ex.Message}");
                SetErrorState("Failed to load packages.");
            }
            finally
            {
                SearchProgressRing.IsActive = false;
                _isLoading = false;
            }
        }

        private async void InstallSelectedApp_Click(object sender, RoutedEventArgs e)
        {
            var activeView = _isInstalledMode ? InstalledGridView : (_isUpdatesMode ? UpdatesGridView : PackagesGridView);

            var selected = activeView.SelectedItems.Cast<WingetPackage>()
                .Where(p => _isInstalledMode ? p.IsInstalled : (_isUpdatesMode ? p.HasUpdate : !p.IsInstalled)).ToList();

            if (selected.Count == 0)
            {
                string notifyMsg = _isInstalledMode
                    ? ResourceString.GetString("notify_no_packages_uninstall") ?? "No packages selected for uninstallation."
                    : _isUpdatesMode
                        ? ResourceString.GetString("notify_no_packages_update") ?? "No packages selected for update."
                        : ResourceString.GetString("notify_no_packages_install") ?? "No packages selected for installation.";

                NotificationManager.Show("warning", notifyMsg).Perform();
                return;
            }

            InstallButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
            activeView.IsEnabled = false;

            WingetVersionText.Visibility = Visibility.Collapsed;

            installingStatusBar.Opacity = 1;
            installingStatusBar.Maximum = selected.Count;
            installingStatusBar.Value = 0;
            installingStatusBar.IsIndeterminate = true;

            var localCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            int ok = 0, fail = 0;

            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var pkg = selected[i];
                    bool upgrade = pkg.HasUpdate && _isUpdatesMode;
                    bool uninstall = pkg.IsInstalled && _isInstalledMode;

                    string actionWord = uninstall
                        ? ResourceString.GetString("status_uninstalling") ?? "Uninstalling"
                        : upgrade
                            ? ResourceString.GetString("status_updating") ?? "Updating"
                            : ResourceString.GetString("status_installing") ?? "Installing";

                    installingStatusText.Text = $"{actionWord} {pkg.Name} ({i + 1}/{selected.Count})…";

                    try
                    {
                        if (uninstall && UninstallQuirks.TryGetValue(pkg.Id, out var quirk))
                        {
                            string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                            string pf32 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                            string? helperPath = null;

                            foreach (var relPath in quirk.RelativePaths)
                            {
                                string p64 = Path.Combine(pf64, relPath);
                                string p32 = Path.Combine(pf32, relPath);
                                string pLocal = Path.Combine(localAppData, relPath);

                                if (File.Exists(p64)) { helperPath = p64; break; }
                                if (File.Exists(p32)) { helperPath = p32; break; }
                                if (File.Exists(pLocal)) { helperPath = pLocal; break; }
                            }

                            if (helperPath != null)
                            {
                                var customPsi = new ProcessStartInfo
                                {
                                    FileName = helperPath,
                                    Arguments = quirk.Arguments,

                                    UseShellExecute = true,
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    WorkingDirectory = Path.GetDirectoryName(helperPath)
                                };

                                using var customP = Process.Start(customPsi);
                                if (customP != null) await customP.WaitForExitAsync(localCts.Token);

                                ok++;
                                pkg.IsInstalled = false;
                                pkg.HasUpdate = false;
                                pkg.Version = "N/A";
                                InstalledList.Remove(pkg);

                                installingStatusBar.Value = i + 1;
                                continue;
                            }
                        }

                        string cmdArgs = uninstall
                            ? $"uninstall --id \"{pkg.Id}\" --exact --accept-source-agreements --silent --disable-interactivity"
                            : upgrade
                                ? $"upgrade --id \"{pkg.Id}\" --exact --accept-source-agreements --accept-package-agreements --silent --disable-interactivity"
                                : $"install --id \"{pkg.Id}\" --exact --accept-source-agreements --accept-package-agreements --silent --disable-interactivity";

                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c winget {cmdArgs}",
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var p = Process.Start(psi);
                        if (p != null)
                        {
                            await p.WaitForExitAsync(localCts.Token);
                            if (p.ExitCode == 0)
                            {
                                ok++;

                                if (uninstall)
                                {
                                    pkg.IsInstalled = false;
                                    pkg.HasUpdate = false;
                                    pkg.Version = "N/A";
                                    InstalledList.Remove(pkg);
                                }
                                else
                                {
                                    pkg.IsInstalled = true;
                                    if (upgrade)
                                    {
                                        pkg.Version = pkg.LatestVersion;
                                        pkg.HasUpdate = false;
                                        pkg.LatestVersion = string.Empty;
                                        _updateablePackages.Remove(pkg);
                                        _updateCount = Math.Max(0, _updateCount - 1);
                                        UpdatesTabLabel.Text = _updateCount > 0 ? $"{ResourceString.GetString("tab_updates") ?? "Updates"} ({_updateCount})" : (ResourceString.GetString("tab_updates") ?? "Updates");
                                        UpdatesList.Remove(pkg);
                                    }
                                }
                            }
                            else
                            {
                                fail++;
                                ErrorLogging.LogDebug($"Failed to execute '{cmdArgs}' on {pkg.Name}. Exit: {p.ExitCode}");
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        fail++;
                        ErrorLogging.LogDebug($"Exception on {pkg.Name}: {ex.Message}");
                    }

                    installingStatusBar.Value = i + 1;
                }
            }
            finally
            {
                localCts.Dispose();
                InstallButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
                activeView.IsEnabled = true;

                installingStatusText.Text = _isInstalledMode
                    ? ResourceString.GetString("status_select_uninstall") ?? "Select packages to uninstall"
                    : _isUpdatesMode
                        ? ResourceString.GetString("status_select_update") ?? "Select packages to update"
                        : ResourceString.GetString("status_select_pkg") ?? "Select a package to install";

                installingStatusBar.IsIndeterminate = false;
                installingStatusBar.Opacity = 0;
                installingStatusBar.Value = 0;

                WingetVersionText.Visibility = Visibility.Visible;

                activeView.SelectedItems.Clear();

                if (_isUpdatesMode && UpdatesList.Count == 0)
                {
                    StatusText.Text = ResourceString.GetString("status_updates_success") ?? "All updates installed successfully.";
                    StatusText.Visibility = Visibility.Visible;
                }
                else if (_isInstalledMode && InstalledList.Count == 0)
                {
                    StatusText.Text = ResourceString.GetString("status_no_installed_packages") ?? "No installed packages found.";
                    StatusText.Visibility = Visibility.Visible;
                }

                UpdateInstalledTabLabel();

                string notifyFormat = _isInstalledMode
                    ? ResourceString.GetString("notify_uninstall_results") ?? "Uninstallation completed: {0} succeeded, {1} failed."
                    : _isUpdatesMode
                        ? ResourceString.GetString("notify_update_results") ?? "Update completed: {0} succeeded, {1} failed."
                        : ResourceString.GetString("notify_install_results") ?? "Installation completed: {0} succeeded, {1} failed.";

                string finalNotifyMsg = string.Format(notifyFormat, ok, fail);

                NotificationManager.Show(fail == 0 ? "success" : "warning", finalNotifyMsg).Perform();
            }
        }

        private async Task CheckAndApplyUpdatesAsync()
        {
            var myVersion = _updateCheckVersion;
            try
            {
                await ErrorLogging.LogInfo("Starting background update check…");

                var updatables = await GetUpdatablePackagesFromCliAsync();

                if (_updateCheckVersion != myVersion) return;
                if (updatables.Count == 0) { await ErrorLogging.LogInfo("No updates found."); return; }

                var updatableDict = new Dictionary<string, (string Name, string Version)>(StringComparer.OrdinalIgnoreCase);
                foreach (var u in updatables)
                {
                    updatableDict.TryAdd(u.Id, (u.Name, u.AvailableVersion));
                }

                var snapshot = _allPackages.ToList();

                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (_updateCheckVersion != myVersion) return;

                    int count = 0;

                    foreach (var pkg in snapshot)
                    {
                        if (updatableDict.TryGetValue(pkg.Id, out var updateInfo))
                        {
                            pkg.HasUpdate = true;
                            pkg.LatestVersion = updateInfo.Version;
                            pkg.IsInstalled = true;
                            count++;

                            updatableDict.Remove(pkg.Id);
                        }
                    }

                    foreach (var leftover in updatableDict)
                    {
                        var newUpdatePkg = new WingetPackage
                        {
                            Name = leftover.Value.Name,
                            Id = leftover.Key,
                            Version = "Installed",
                            LatestVersion = leftover.Value.Version,
                            HasUpdate = true,
                            IsInstalled = true,
                            Category = PackageHelper.GetPublisherDisplayName(leftover.Key)
                        };

                        _allPackages.Add(newUpdatePkg);
                        snapshot.Add(newUpdatePkg);
                        count++;
                    }

                    _updateCount = count;
                    _updateablePackages = snapshot.Where(p => p.HasUpdate).ToList();
                    UpdatesList.Clear();

                    foreach (var pkg in _updateablePackages) UpdatesList.Add(pkg);

                    UpdatesTabLabel.Text = count > 0 ? $"{ResourceString.GetString("tab_updates") ?? "Updates"} ({count})" : (ResourceString.GetString("tab_updates") ?? "Updates");
                    await ErrorLogging.LogInfo($"Update check done — {count} update(s).");

                    if (_isUpdatesMode) RefreshUpdatesTabList();
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ErrorLogging.LogDebug($"Update check failed: {ex.Message}"); }
        }
        #endregion

        #region UI Event Handlers (Tabs, Buttons, Search, Selection)
        private void TabSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabSegmented.SelectedIndex == 0)
            {
                if (!_isUpdatesMode && !_isInstalledMode) return;
                _isUpdatesMode = false;
                _isInstalledMode = false;
                PackageSearchBox.Visibility = Visibility.Visible;
                InstallButtonText.Text = ResourceString.GetString("btn_install_selected") ?? "Install Selected";
                InstallButtonIcon.Glyph = "\uE896";
                installingStatusText.Text = ResourceString.GetString("status_select_pkg") ?? "Select a package to install";
                UpdatesGridView.Visibility = Visibility.Collapsed;
                InstalledGridView.Visibility = Visibility.Collapsed;
                UpdatesGridView.SelectedItems.Clear();
                InstalledGridView.SelectedItems.Clear();
                StatusText.Visibility = Visibility.Collapsed;
                PackagesGridView.Visibility = Visibility.Visible;

                ApplySearch(PackageSearchBox.Text?.Trim() ?? string.Empty);
            }
            else if (TabSegmented.SelectedIndex == 1)
            {
                if (_isUpdatesMode) return;
                _isUpdatesMode = true;
                _isInstalledMode = false;
                PackageSearchBox.Visibility = Visibility.Collapsed;
                InstallButtonText.Text = ResourceString.GetString("btn_update_selected") ?? "Update Selected";
                InstallButtonIcon.Glyph = "\uE898";
                installingStatusText.Text = ResourceString.GetString("status_select_update") ?? "Select packages to update";
                PackagesGridView.Visibility = Visibility.Collapsed;
                InstalledGridView.Visibility = Visibility.Collapsed;
                PackagesGridView.SelectedItems.Clear();
                InstalledGridView.SelectedItems.Clear();
                RefreshUpdatesTabList();
            }
            else if (TabSegmented.SelectedIndex == 2)
            {
                if (_isInstalledMode) return;
                _isUpdatesMode = false;
                _isInstalledMode = true;
                PackageSearchBox.Visibility = Visibility.Collapsed;
                InstallButtonText.Text = ResourceString.GetString("btn_uninstall_selected") ?? "Uninstall Selected";
                InstallButtonIcon.Glyph = "\uE74D"; // Trash icon
                installingStatusText.Text = ResourceString.GetString("status_select_uninstall") ?? "Select packages to uninstall";
                PackagesGridView.Visibility = Visibility.Collapsed;
                UpdatesGridView.Visibility = Visibility.Collapsed;
                PackagesGridView.SelectedItems.Clear();
                UpdatesGridView.SelectedItems.Clear();
                RefreshInstalledTabList();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            Interlocked.Increment(ref _updateCheckVersion);
            var old = _cts;
            _cts = new CancellationTokenSource();
            try { old.Cancel(); } catch { }
            old.Dispose();

            _wingetCatalog = null;
            _localCatalog = null;
            _isWingetAvailable = null;
            _isUpdatesMode = false;
            _isInstalledMode = false;
            _updateablePackages.Clear();
            _updateCount = 0;
            UpdatesTabLabel.Text = ResourceString.GetString("tab_updates") ?? "Updates";
            InstalledTabLabel.Text = ResourceString.GetString("tab_installed") ?? "Installed";
            TabSegmented.SelectedIndex = 0;
            PackageSearchBox.Visibility = Visibility.Visible;
            InstallButtonText.Text = ResourceString.GetString("btn_install_selected") ?? "Install Selected";
            InstallButtonIcon.Glyph = "\uE896";
            installingStatusText.Text = ResourceString.GetString("status_select_pkg") ?? "Select a package to install";

            _suppressSearch = true;
            try { PackageSearchBox.Text = string.Empty; }
            finally { _suppressSearch = false; }

            PackageList.Clear();
            UpdatesList.Clear();
            InstalledList.Clear();
            PackagesGridView.Visibility = Visibility.Collapsed;
            UpdatesGridView.Visibility = Visibility.Collapsed;
            InstalledGridView.Visibility = Visibility.Collapsed;
            SearchProgressRing.IsActive = true;
            LoadingState.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Collapsed;

            await LoadPackagesAsync();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                _currentSortMode = item.Tag.ToString() ?? "Name";

                if (!_isLoading)
                {
                    ApplySearch(PackageSearchBox.Text?.Trim() ?? string.Empty);
                }
            }
        }

        private void PackageSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_suppressSearch || _isLoading || _isUpdatesMode) return;
            if (args.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen) return;
            ApplySearch(sender.Text?.Trim() ?? string.Empty);
        }

        private void PackageSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (_suppressSearch || _isLoading || _isUpdatesMode) return;
            ApplySearch(args.QueryText?.Trim() ?? string.Empty);
        }

        private async void ApplySearch(string query)
        {
            if (StatusText == null || LoadingState == null || SearchProgressRing == null || PackageList == null)
            {
                return;
            }

            var currentVersion = Interlocked.Increment(ref _searchVersion);

            StatusText.Visibility = Visibility.Collapsed;
            LoadingState.Visibility = Visibility.Collapsed;
            SearchProgressRing.IsActive = false;
            PackageList.Clear();

            IEnumerable<WingetPackage> SortPackages(IEnumerable<WingetPackage> packages)
            {
                return _currentSortMode switch
                {
                    "Id" => packages.OrderBy(p => p.Id ?? string.Empty, StringComparer.CurrentCultureIgnoreCase),
                    "Version" => packages.OrderBy(p => p.Version ?? string.Empty, StringComparer.CurrentCultureIgnoreCase),
                    "Category" => packages.OrderBy(p => p.Category ?? string.Empty, StringComparer.CurrentCultureIgnoreCase),
                    _ => packages.OrderBy(p => p.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                };
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                int count = 0;
                var sortedAll = SortPackages(_allPackages).ToList();

                foreach (var p in sortedAll)
                {
                    if (currentVersion != _searchVersion) return;
                    PackageList.Add(p);
                    if (++count % 50 == 0) await Task.Delay(1);
                }
                return;
            }

            int localCount = 0;
            var startsWithResults = new List<WingetPackage>();
            var containsResults = new List<WingetPackage>();

            foreach (var p in _allPackages)
            {
                if (currentVersion != _searchVersion) return;

                string safeName = p.Name ?? string.Empty;
                string safeId = p.Id ?? string.Empty;

                if (safeName.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    startsWithResults.Add(p);
                }
                else if (safeName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                         safeId.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    containsResults.Add(p);
                }
            }

            var sortedStartsWith = SortPackages(startsWithResults).ToList();
            foreach (var p in sortedStartsWith)
            {
                if (currentVersion != _searchVersion) return;
                PackageList.Add(p);
                if (++localCount % 50 == 0) await Task.Delay(1);
            }

            var sortedContains = SortPackages(containsResults).ToList();
            foreach (var p in sortedContains)
            {
                if (currentVersion != _searchVersion) return;
                PackageList.Add(p);
                if (++localCount % 50 == 0) await Task.Delay(1);
            }

            if (query.Length >= 3)
            {
                await Task.Delay(400);
                if (currentVersion != _searchVersion) return;

                SearchStatusText.Text = ResourceString.GetString("status_searching_online") ?? "Searching online...";
                LoadingState.Visibility = Visibility.Visible;
                SearchProgressRing.IsActive = true;

                try
                {
                    var webResults = await SearchPackagesFromWingetCliAsync(query);

                    if (currentVersion != _searchVersion) return;

                    var newWebPackages = new List<WingetPackage>();

                    foreach (var webItem in webResults)
                    {
                        if (currentVersion != _searchVersion) return;

                        bool alreadyInList = _allPackages.Any(p => (p.Id ?? string.Empty).Equals(webItem.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                        if (!alreadyInList)
                        {
                            var newPkg = new WingetPackage
                            {
                                Name = webItem.Name ?? string.Empty,
                                Id = webItem.Id ?? string.Empty,
                                Version = webItem.Version ?? "N/A",
                                Category = PackageHelper.GetPublisherDisplayName(webItem.Id ?? string.Empty),
                                IsInstalled = false
                            };

                            newWebPackages.Add(newPkg);
                        }
                    }

                    var sortedWebPackages = SortPackages(newWebPackages).ToList();
                    foreach (var p in sortedWebPackages)
                    {
                        if (currentVersion != _searchVersion) return;
                        PackageList.Add(p);
                    }
                }
                catch (Exception ex)
                {
                    await ErrorLogging.LogInfo($"Online search failed: {ex.Message}");
                }
                finally
                {
                    if (currentVersion == _searchVersion)
                    {
                        LoadingState.Visibility = Visibility.Collapsed;
                        SearchProgressRing.IsActive = false;
                        SearchStatusText.Text = ResourceString.GetString("status_loading_pkg") ?? "Loading packages...";
                    }
                }
            }

            if (currentVersion == _searchVersion)
            {
                await ErrorLogging.LogInfo($"ApplySearch Final: '{query}' → {PackageList.Count} total results found.");

                if (PackageList.Count == 0)
                {
                    StatusText.Text = ResourceString.GetString("status_no_results") ?? "No packages found.";
                    StatusText.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void PackagesGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListViewBase view) return;

            try
            {
                var added = e.AddedItems?.Cast<WingetPackage>().ToList();
                if (added != null)
                {
                    foreach (var item in added)
                    {
                        if (!_isInstalledMode && item.IsInstalled && !item.HasUpdate)
                        {
                            view.SelectedItems.Remove(item);
                        }
                    }
                }

                int count = view.SelectedItems.Count;
                UpdateStatusText(count);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Selection failed safely: {ex.Message}");
            }
        }
        #endregion

        #region UI State & Updates Helpers
        private void RefreshUpdatesTabList()
        {
            if (UpdatesList.Count != _updateablePackages.Count)
            {
                UpdatesList.Clear();
                foreach (var pkg in _updateablePackages) UpdatesList.Add(pkg);
            }

            if (UpdatesList.Count == 0)
            {
                StatusText.Text = _allPackages.Count == 0
                    ? ResourceString.GetString("status_loading_wait") ?? "Loading packages, please wait…"
                    : ResourceString.GetString("status_no_updates") ?? "No updates available. All packages are up to date.";
                StatusText.Visibility = Visibility.Visible;
            }
            else
            {
                StatusText.Visibility = Visibility.Collapsed;
            }

            UpdatesGridView.Visibility = Visibility.Visible;
            PackagesGridView.Visibility = Visibility.Collapsed;
            LoadingState.Visibility = Visibility.Collapsed;
        }

        private void RefreshInstalledTabList()
        {
            InstalledList.Clear();
            foreach (var pkg in _allPackages.Where(p => p.IsInstalled)) InstalledList.Add(pkg);

            if (InstalledList.Count == 0)
            {
                StatusText.Text = _allPackages.Count == 0
                    ? ResourceString.GetString("status_loading_wait") ?? "Loading packages, please wait…"
                    : ResourceString.GetString("status_no_installed_packages") ?? "No installed packages found.";
                StatusText.Visibility = Visibility.Visible;
            }
            else
            {
                StatusText.Visibility = Visibility.Collapsed;
            }

            InstalledGridView.Visibility = Visibility.Visible;
            UpdatesGridView.Visibility = Visibility.Collapsed;
            PackagesGridView.Visibility = Visibility.Collapsed;
            LoadingState.Visibility = Visibility.Collapsed;
        }


        private void UpdateStatusText(int count)
        {
            if (count == 0)
            {
                installingStatusText.Text = _isInstalledMode
                    ? ResourceString.GetString("status_select_uninstall") ?? "Select packages to uninstall"
                    : _isUpdatesMode
                        ? ResourceString.GetString("status_select_update") ?? "Select packages to update"
                        : ResourceString.GetString("status_select_pkg") ?? "Select a package to install";
            }
            else
            {
                string packageWord = count == 1
                    ? ResourceString.GetString("status_package") ?? "package"
                    : ResourceString.GetString("status_packages") ?? "packages";

                string actionType = _isInstalledMode
                    ? ResourceString.GetString("status_selected_for_uninstall") ?? "selected for uninstallation"
                    : _isUpdatesMode
                        ? ResourceString.GetString("status_selected_for_update") ?? "selected for update"
                        : ResourceString.GetString("status_selected_for_install") ?? "selected for installation";

                installingStatusText.Text = $"{count} {packageWord} {actionType}";
            }
        }

        private void UpdateInstalledTabLabel()
        {
            int installedCount = _allPackages.Count(p => p.IsInstalled);
            string installedText = ResourceString.GetString("tab_installed") ?? "Installed";

            InstalledTabLabel.Text = installedCount > 0
                ? $"{installedText} ({installedCount})"
                : installedText;
        }

        private void SetErrorState(string message)
        {
            SearchProgressRing.IsActive = false;
            LoadingState.Visibility = Visibility.Collapsed;
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;
            _isLoading = false;
        }
        #endregion

        #region WinGet Versioning & Management
        private async Task<string> GetWingetVersionAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c winget --version",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    var output = await p.StandardOutput.ReadToEndAsync();
                    await p.WaitForExitAsync(_cts.Token);
                    return output.Trim();
                }
            }
            catch { }
            return "Unknown";
        }

        private async Task<(bool HasUpdate, string NewVersion)> CheckForWingetUpdateAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c winget upgrade --id Microsoft.DesktopAppInstaller --exact --accept-source-agreements",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var p = Process.Start(psi);
                if (p == null) return (false, string.Empty);

                var output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync(_cts.Token);

                if (output.Contains("No applicable update found", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, string.Empty);
                }

                var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Microsoft.DesktopAppInstaller", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = Regex.Split(line, @"\s{2,}");
                        if (parts.Length >= 4) return (true, parts[3].Trim());
                    }
                }
            }
            catch (Exception ex) { ErrorLogging.LogDebug($"Winget update check failed: {ex.Message}"); }

            return (false, string.Empty);
        }

        private async Task<bool> ShowInstallWingetDialogAsync()
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = ResourceString.GetString("dialog_install_winget_title") ?? "WinGet Not Found",
                Content = ResourceString.GetString("dialog_install_winget_content") ?? "The Windows Package Manager (WinGet) is missing from your system. Would you like to install it now?",
                PrimaryButtonText = ResourceString.GetString("btn_yes") ?? "Yes",
                CloseButtonText = ResourceString.GetString("btn_no") ?? "No",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string actionWord = ResourceString.GetString("status_installing") ?? "Installing";
                StatusText.Text = $"{actionWord} WinGet...";
                StatusText.Visibility = Visibility.Visible;
                LoadingState.Visibility = Visibility.Visible;

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Invoke-WebRequest -Uri 'https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle' -OutFile \\\"$env:TEMP\\winget.msixbundle\\\"; Add-AppxPackage -Path \\\"$env:TEMP\\winget.msixbundle\\\"\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        await p.WaitForExitAsync(_cts.Token);
                        if (p.ExitCode == 0)
                        {
                            return true;
                        }
                        else
                        {
                            ErrorLogging.LogDebug($"WinGet silent install failed with exit code: {p.ExitCode}");
                        }
                    }
                }
                catch (Exception ex) { ErrorLogging.LogDebug($"WinGet silent install exception: {ex.Message}"); }
            }
            return false;
        }

        private async Task<bool> ShowUpgradeWingetDialogAsync(string newVersion)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = ResourceString.GetString("dialog_upgrade_winget_title") ?? "WinGet Update Available",
                Content = string.Format(ResourceString.GetString("dialog_upgrade_winget_content") ?? "A newer version of WinGet ({0}) is available. Would you like to upgrade it now for better stability?", newVersion),
                PrimaryButtonText = ResourceString.GetString("btn_yes") ?? "Yes",
                CloseButtonText = ResourceString.GetString("btn_no") ?? "No",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                StatusText.Text = "Upgrading WinGet...";
                StatusText.Visibility = Visibility.Visible;

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c winget upgrade --id Microsoft.DesktopAppInstaller --exact --silent --accept-source-agreements --accept-package-agreements --disable-interactivity",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    };
                    using var p = Process.Start(psi);
                    if (p != null) await p.WaitForExitAsync();
                    return true;
                }
                catch (Exception ex) { ErrorLogging.LogDebug($"Winget upgrade failed: {ex.Message}"); }
            }
            return false;
        }
        #endregion

        #region Native Winget COM API
        private async Task<bool> IsWingetAvailableAsync()
        {
            if (_isWingetAvailable.HasValue) return _isWingetAvailable.Value;

            try
            {
                if (await EnsureWingetCatalogAsync() is not null)
                {
                    _isWingetAvailable = true;
                    return true;
                }
            }
            catch { /* COM failed, proceed to CLI check */ }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c winget --version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var p = Process.Start(psi);
                if (p != null)
                {
                    await p.WaitForExitAsync(_cts.Token);
                    if (p.ExitCode == 0)
                    {
                        ErrorLogging.LogDebug("COM API failed, but WinGet CLI is available.");
                        _isWingetAvailable = true;
                        return true;
                    }
                }
            }
            catch { /* CLI check failed */ }

            _isWingetAvailable = false;
            return false;
        }

        private async Task<PackageCatalog?> EnsureWingetCatalogAsync()
        {
            if (_wingetCatalog is not null) return _wingetCatalog;

            try
            {
                var src = await Task.Run(() =>
                {
                    _packageManager ??= new PackageManager();
                    return _packageManager.GetPackageCatalogByName("winget");
                });

                if (src is null)
                {
                    ErrorLogging.LogDebug("Winget COM source catalog returned null.");
                    return null;
                }

                src.AcceptSourceAgreements = true;
                var r = await Task.Run(() => src.ConnectAsync().AsTask());

                if (r.Status == ConnectResultStatus.Ok)
                {
                    _wingetCatalog = r.PackageCatalog;
                    return _wingetCatalog;
                }

                ErrorLogging.LogDebug($"Winget source failed to connect. Status: {r.Status}");
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Winget COM connect error: {ex.Message}");
            }

            return null;
        }

        private async Task<PackageCatalog?> EnsureLocalCatalogAsync()
        {
            if (_localCatalog is not null) return _localCatalog;
            try
            {
                var src = await Task.Run(() =>
                {
                    _packageManager ??= new PackageManager();
                    return _packageManager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);
                });
                var r = await Task.Run(() => src.ConnectAsync().AsTask());
                if (r.Status == ConnectResultStatus.Ok) { _localCatalog = r.PackageCatalog; return _localCatalog; }
                ErrorLogging.LogDebug($"Local catalog failed: {r.Status}");
            }
            catch (Exception ex) { ErrorLogging.LogDebug($"Local catalog connect error: {ex.Message}"); }
            return null;
        }

        private async Task<List<DiscoveredPackageEntry>> DiscoverPackagesAsync(PackageCatalog catalog)
        {
            _isUsingCliDiscoveryFallback = false;
            try
            {
                var packages = new List<DiscoveredPackageEntry>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var options = new FindPackagesOptions { ResultLimit = int.MaxValue };

                var result = await catalog.FindPackagesAsync(options).AsTask();

                foreach (var match in result.Matches)
                {
                    var cp = match.CatalogPackage;
                    var id = cp.Id?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
                    var name = string.IsNullOrWhiteSpace(cp.Name) ? PackageHelper.FormatPackageName(id) : cp.Name;
                    var ver = cp.AvailableVersions.FirstOrDefault()?.Version;
                    packages.Add(new DiscoveredPackageEntry(id, name, string.IsNullOrWhiteSpace(ver) ? "N/A" : ver));
                }

                return packages;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _isUsingCliDiscoveryFallback = true;
                ErrorLogging.LogDebug($"COM catalog unavailable, falling back to CLI. {ex.Message}");
                return await DiscoverPackagesFromWingetCliAsync();
            }
        }

        private async Task<Dictionary<string, (string Name, string Version)>> GetInstalledPackagesMapAsync()
        {
            var result = new Dictionary<string, (string Name, string Version)>(StringComparer.OrdinalIgnoreCase);
            _installedSnapshot.Clear();
            _isUsingInventoryFallback = false;

            try
            {
                var local = await EnsureLocalCatalogAsync();

                if (local is null)
                {
                    ErrorLogging.LogDebug("Local catalog is null. Triggering inventory fallback.");
                    await PopulateInstalledMapFallbackAsync(result);
                    return result;
                }

                var findOperation = await local.FindPackagesAsync(new FindPackagesOptions()).AsTask();

                if (findOperation.Matches == null || findOperation.Matches.Count < 15)
                {
                    ErrorLogging.LogDebug("Native catalog returned suspiciously few apps. Forcing fallback.");
                    await PopulateInstalledMapFallbackAsync(result);
                }
                else
                {
                    foreach (var match in findOperation.Matches)
                    {
                        var ip = match.CatalogPackage;
                        var ver = ip.InstalledVersion?.Version ?? ip.AvailableVersions.FirstOrDefault()?.Version ?? string.Empty;
                        var iid = ip.Id ?? string.Empty;
                        var iname = ip.Name ?? string.Empty;

                        _installedSnapshot.Add(new InstalledPackageEntry(
                            iid, iname, ver, PackageHelper.NormalizeLookupKey(iid), PackageHelper.NormalizeLookupKey(iname)));

                        if (!string.IsNullOrWhiteSpace(iid))
                        {
                            result[iid] = (iname, ver);
                            foreach (var key in PackageHelper.GetLookupKeys(iid, iname))
                                result.TryAdd(key, (iname, ver));
                        }
                        if (!string.IsNullOrWhiteSpace(iname))
                            result.TryAdd(PackageHelper.NormalizeLookupKey(iname), (iname, ver));
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Local catalog query failed: {ex.Message}");
                await PopulateInstalledMapFallbackAsync(result);
            }

            return result;
        }
        #endregion

        #region CLI Fallbacks & Parsing
        private async Task<List<(string Name, string Id, string AvailableVersion)>> GetUpdatablePackagesFromCliAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c winget upgrade --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process is null) return [];

            var stdOut = process.StandardOutput.ReadToEndAsync();
            var stdErr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(_cts.Token); }
            catch (OperationCanceledException) { TryTerminateProcess(process); throw; }

            var output = await stdOut;
            _ = await stdErr;

            var results = new List<(string, string, string)>();
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool headerPassed = false, sepPassed = false;

            foreach (var line in lines)
            {
                if (!headerPassed)
                {
                    if (line.Contains("Available", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains("Version", StringComparison.OrdinalIgnoreCase))
                        headerPassed = true;
                    continue;
                }
                if (!sepPassed) { if (line.All(c => c == '-' || c == ' ')) { sepPassed = true; continue; } }

                if (line.EndsWith("available.", StringComparison.OrdinalIgnoreCase)) continue;

                var parts = Regex.Split(line, @"\s{2,}");
                if (parts.Length < 4) continue;

                var name = parts[0].Trim();
                var id = parts[1].Trim();
                var available = parts[3].Trim();

                // Commented out to ensure to get all updates. Uncomment for only traditional WinGet packages.
                // if (!PackageHelper.IsLikelyWingetPackageId(id)) continue;

                if (string.IsNullOrWhiteSpace(available)) continue;

                results.Add((name, id, available));
            }

            return results;
        }

        private async Task<List<DiscoveredPackageEntry>> DiscoverPackagesFromWingetCliAsync()
        {
            _isUsingCliDiscoveryFallback = true;
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c winget search --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process is null) { ErrorLogging.LogDebug("Failed to start winget CLI."); return []; }

            var stdOut = process.StandardOutput.ReadToEndAsync();
            var stdErr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(_cts.Token); }
            catch (OperationCanceledException) { TryTerminateProcess(process); throw; }

            var output = await stdOut;
            _ = await stdErr;
            return process.ExitCode != 0 ? [] : ParseWingetSearchOutput(output);
        }

        private async Task<List<DiscoveredPackageEntry>> DiscoverPopularPackagesFallbackAsync()
        {
            var results = new List<DiscoveredPackageEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var q in new[] { "browser", "media", "code", "chat", "game", "archive", "social", "utility", "system" })
                foreach (var item in await SearchPackagesFromWingetCliAsync(q))
                    if (seen.Add(item.Id)) results.Add(item);

            return results;
        }

        private async Task<List<DiscoveredPackageEntry>> SearchPackagesFromWingetCliAsync(
            string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return [];
            var token = cancellationToken.CanBeCanceled ? cancellationToken : _cts.Token;

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c winget search --query \"{query.Replace("\"", "\"\"")}\" --source winget --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process is null) return [];

            var stdOut = process.StandardOutput.ReadToEndAsync();
            var stdErr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(token); }
            catch (OperationCanceledException) { TryTerminateProcess(process); throw; }

            var output = await stdOut;
            _ = await stdErr;
            return process.ExitCode != 0 ? [] : ParseWingetSearchOutput(output);
        }

        private static List<DiscoveredPackageEntry> ParseWingetSearchOutput(string output)
        {
            var packages = new List<DiscoveredPackageEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith("The `msstore`", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Do you agree", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) && line.Contains("Id")) continue;
                if (line.All(c => c == '-' || c == ' ')) continue;

                var parts = Regex.Split(line, @"\s{2,}");
                if (parts.Length < 2) continue;

                var id = parts[1].Trim();
                if (!PackageHelper.IsLikelyWingetPackageId(id) || !seen.Add(id)) continue;

                var name = string.IsNullOrWhiteSpace(parts[0]) ? PackageHelper.FormatPackageName(id) : parts[0].Trim();
                var ver = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : "N/A";
                if (ver.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) ver = "N/A";

                packages.Add(new DiscoveredPackageEntry(id, name, ver));
            }

            return packages;
        }
        #endregion

        #region Registry Fallback & Heuristics
        private async Task PopulateInstalledMapFallbackAsync(Dictionary<string, (string Name, string Version)> result)
        {
            try
            {
                _isUsingInventoryFallback = true;

                List<Tuple<string, string, bool>> apps = await AppManager.GetInstalledApps(true);

                foreach (var app in apps)
                {
                    if (string.IsNullOrWhiteSpace(app.Item1)) continue;

                    var name = app.Item1.Trim();
                    var key = PackageHelper.NormalizeLookupKey(name);

                    if (string.IsNullOrWhiteSpace(key)) continue;

                    result.TryAdd(key, (name, "Installed"));

                    _installedSnapshot.Add(new InstalledPackageEntry(
                        string.Empty,
                        name,
                        "Installed",
                        string.Empty,
                        key));
                }

                await ErrorLogging.LogInfo($"Inventory fallback: {_installedSnapshot.Count} installed apps.");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Inventory fallback failed: {ex.Message}");
            }
        }

        private bool TryGetInstalledByHeuristic(WingetPackage pkg, out (string Name, string Version) installed)
        {
            installed = default;
            if (_installedSnapshot.Count == 0) return false;

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in PackageHelper.GetLookupKeys(pkg.Id, pkg.Name))
            {
                var n = PackageHelper.NormalizeLookupKey(key);
                if (!string.IsNullOrWhiteSpace(n) && n.Length > 3) keys.Add(n);
            }

            if (keys.Count == 0) return false;

            foreach (var c in _installedSnapshot)
            {
                if (!string.IsNullOrWhiteSpace(c.NormalizedName) && keys.Contains(c.NormalizedName)) { installed = (c.Name, c.Version); return true; }
                if (!string.IsNullOrWhiteSpace(c.NormalizedId) && keys.Contains(c.NormalizedId)) { installed = (c.Name, c.Version); return true; }

                foreach (var key in keys)
                {
                    if (key.Length < 5 || string.IsNullOrWhiteSpace(c.NormalizedName)) continue;

                    if (c.NormalizedName.StartsWith(key, StringComparison.OrdinalIgnoreCase) ||
                        c.NormalizedName.Contains(key, StringComparison.OrdinalIgnoreCase))
                    {
                        installed = (c.Name, c.Version);
                        return true;
                    }
                }
            }

            return false;
        }
        #endregion

        #region Process Utilities
        private static void TryTerminateProcess(Process p)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
        }
        #endregion
    }
}