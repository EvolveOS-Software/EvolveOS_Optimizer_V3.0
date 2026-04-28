// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.Management.Deployment;
using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class AppStorePage : Page, IBusyPage
    {
        #region Data Models & Records
        private sealed record InstalledPackageEntry(string Id, string Name, string Version, string NormalizedId, string NormalizedName, string Source);
        private sealed record DiscoveredPackageEntry(string Id, string Name, string Version, string Source);
        private sealed record UpdatablePackageEntry(string Id, string Name, string Version, string AvailableVersion, string Source);
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

        #region Navigation Guard
        public bool IsBusy { get; private set; }
        public string BusyTitle => ResourceString.GetString("dialog_install_in_progress_title") ?? "Installation in Progress";
        public string BusyMessage => ResourceString.GetString("dialog_install_in_progress_content") ?? "Leaving this tab will cancel the current installation. Proceed?";
        #endregion

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

            Unloaded += AppStorePage_Unloaded;
        }

        private async void AppStorePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }

            if (_isLoading) return;

            await ErrorLogging.LogInfo("AppStorePage Loaded and starting package load.");

            if (_allPackages.Count == 0)
                await LoadPackagesAsync();
        }

        private void AppStorePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _cts.Cancel();

                _wingetCatalog = null;
                _localCatalog = null;
                _packageManager = null;
            }
            catch { }
        }
        #endregion

        #region Core Package Management (Load, Install, Update)
        private async Task LoadPackagesAsync()
        {
            _isLoading = true;

            if (!await NetworkHelper.IsConnectedAsync())
            {
                SetErrorState(ResourceString.GetString("status_offline") ?? "Internet connection required to load the App Store.");
                _isLoading = false;
                return;
            }

            UpdatesLoadingRing.Visibility = Visibility.Visible;
            UpdatesLoadingRing.IsActive = true;
            InstalledLoadingRing.Visibility = Visibility.Visible;
            InstalledLoadingRing.IsActive = true;

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

                    InstalledLoadingRing.IsActive = false;
                    InstalledLoadingRing.Visibility = Visibility.Collapsed;

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

                        InstalledLoadingRing.IsActive = false;
                        InstalledLoadingRing.Visibility = Visibility.Collapsed;

                        bool upgraded = await ShowUpgradeWingetDialogAsync(newVer);

                        SearchProgressRing.IsActive = true;
                        LoadingState.Visibility = Visibility.Visible;

                        InstalledLoadingRing.Visibility = Visibility.Visible;
                        InstalledLoadingRing.IsActive = true;

                        if (upgraded)
                        {
                            _wingetVersion = await GetWingetVersionAsync();
                            WingetVersionText.Text = string.Format(ResourceString.GetString("winget_version") ?? "WinGet {0}", _wingetVersion);
                        }
                    }
                }

                _allPackages.Clear();
                PackageList.Clear();

                var installedMap = await GetInstalledPackagesMapAsync(_cts.Token);
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

                    string safeDId = d.Id ?? string.Empty;
                    string finalSource = d.Source ?? string.Empty;

                    if (safeDId.Contains("PowerShell", StringComparison.OrdinalIgnoreCase))
                        finalSource = "PowerShell";
                    else if (safeDId.StartsWith("Microsoft.DotNet", StringComparison.OrdinalIgnoreCase))
                        finalSource = "DotNet";

                    var pkg = new WingetPackage
                    {
                        Name = d.Name ?? string.Empty,
                        Id = safeDId,
                        Category = PackageHelper.GetPublisherDisplayName(safeDId),
                        Version = d.Version ?? "N/A",
                        Source = finalSource
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
                    string safeInstName = inst.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(safeInstName)) continue;

                    string safeInstId = inst.Id ?? string.Empty;

                    if ((!string.IsNullOrWhiteSpace(safeInstId) && knownIds.Contains(safeInstId)) ||
                        knownNames.Contains(safeInstName))
                    {
                        continue;
                    }

                    string localSource = inst.Source ?? string.Empty;
                    if (safeInstId.Contains("PowerShell", StringComparison.OrdinalIgnoreCase))
                        localSource = "PowerShell";
                    else if (safeInstId.StartsWith("Microsoft.DotNet", StringComparison.OrdinalIgnoreCase))
                        localSource = "DotNet";

                    var newPkg = new WingetPackage
                    {
                        Name = safeInstName,
                        Id = string.IsNullOrWhiteSpace(safeInstId) ? "Local Package" : safeInstId,
                        Version = string.IsNullOrWhiteSpace(inst.Version) ? "Installed" : inst.Version,
                        Category = PackageHelper.GetPublisherDisplayName(safeInstId),
                        IsInstalled = true,
                        Source = localSource
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
                InstalledLoadingRing.IsActive = false;
                InstalledLoadingRing.Visibility = Visibility.Collapsed;
                _isLoading = false;
            }
        }

        private void InstallSelectedApp_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            ExecuteInstallAsync(installAsAdmin: false, skipIntegrityCheck: false, installMachineWide: false);
        }

        private void MenuInstallAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            ExecuteInstallAsync(installAsAdmin: true, skipIntegrityCheck: false, installMachineWide: false);
        }

        private void MenuSkipIntegrity_Click(object sender, RoutedEventArgs e)
        {
            ExecuteInstallAsync(installAsAdmin: false, skipIntegrityCheck: true, installMachineWide: false);
        }

        private void MenuMachineScope_Click(object sender, RoutedEventArgs e)
        {
            ExecuteInstallAsync(installAsAdmin: false, skipIntegrityCheck: false, installMachineWide: true);
        }

        private void MenuInteractive_Click(object sender, RoutedEventArgs e)
        {
            ExecuteInstallAsync(installAsAdmin: false, skipIntegrityCheck: false, installMachineWide: false, interactive: true);
        }

        private async void ExecuteInstallAsync(bool installAsAdmin, bool skipIntegrityCheck, bool installMachineWide, bool interactive = false)
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

            if (!await NetworkHelper.IsConnectedAsync())
            {
                return;
            }

            IsBusy = true;

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

                        string baseArgs = $"--id \"{pkg.Id}\" --exact --accept-source-agreements --accept-package-agreements";

                        if (!interactive)
                        {
                            baseArgs += " --silent --disable-interactivity";
                        }

                        if (skipIntegrityCheck)
                        {
                            baseArgs += " --ignore-security-hash --force";
                        }

                        if (installMachineWide && !uninstall)
                        {
                            baseArgs += " --scope machine";
                        }

                        string cmdArgs = uninstall
                            ? $"uninstall {baseArgs}"
                            : upgrade
                                ? $"upgrade {baseArgs}"
                                : $"install {baseArgs}";

                        var psi = new ProcessStartInfo
                        {
                            FileName = "winget.exe",
                            Arguments = cmdArgs,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                            UseShellExecute = installAsAdmin || interactive,
                            Verb = installAsAdmin ? "runas" : string.Empty,
                            CreateNoWindow = !interactive,
                            WindowStyle = interactive ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
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

                if (!_cts.Token.IsCancellationRequested)
                {
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
                }

                UpdateInstalledTabLabel();

                if (!_cts.Token.IsCancellationRequested)
                {
                    string notifyFormat = _isInstalledMode
                        ? ResourceString.GetString("notify_uninstall_results") ?? "Uninstallation completed: {0} succeeded, {1} failed."
                        : _isUpdatesMode
                            ? ResourceString.GetString("notify_update_results") ?? "Update completed: {0} succeeded, {1} failed."
                            : ResourceString.GetString("notify_install_results") ?? "Installation completed: {0} succeeded, {1} failed.";

                    string finalNotifyMsg = string.Format(notifyFormat, ok, fail);

                    NotificationManager.Show(fail == 0 ? "success" : "warning", finalNotifyMsg).Perform();
                }

                IsBusy = false;
            }
        }

        private async Task CheckAndApplyUpdatesAsync()
        {
            var myVersion = _updateCheckVersion;

            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatesLoadingRing.Visibility = Visibility.Visible;
                UpdatesLoadingRing.IsActive = true;
            });

            try
            {
                await ErrorLogging.LogInfo("Starting background update check…");

                var updatables = await GetUpdatablePackagesFromCliAsync(_cts.Token);

                if (_updateCheckVersion != myVersion)
                {
                    StopUpdatesSpinner();
                    return;
                }

                if (updatables.Count == 0)
                {
                    await ErrorLogging.LogInfo("No updates found.");
                    StopUpdatesSpinner();
                    return;
                }

                var updatableDict = new Dictionary<string, (string Name, string CurrentVer, string AvailableVer, string Source)>(StringComparer.OrdinalIgnoreCase);
                foreach (var u in updatables)
                {
                    updatableDict.TryAdd(u.Id, (u.Name, u.CurrentVersion, u.AvailableVersion, u.Source));
                }

                var snapshot = _allPackages.ToList();

                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (_updateCheckVersion != myVersion)
                    {
                        StopUpdatesSpinner();
                        return;
                    }

                    int count = 0;

                    foreach (var pkg in snapshot)
                    {
                        var match = updatableDict.FirstOrDefault(u =>
                            string.Equals(u.Key, pkg.Id, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(u.Value.Name, pkg.Name, StringComparison.OrdinalIgnoreCase)
                        );

                        if (match.Key != null)
                        {
                            var updateInfo = match.Value;

                            pkg.HasUpdate = true;
                            pkg.LatestVersion = updateInfo.AvailableVer;
                            pkg.IsInstalled = true;
                            pkg.Source = updateInfo.Source;

                            if (!string.IsNullOrWhiteSpace(match.Key) && match.Key.Contains('.'))
                            {
                                pkg.Id = match.Key;
                            }

                            if (string.IsNullOrWhiteSpace(pkg.Version) || pkg.Version == "N/A" || pkg.Version == "Installed")
                            {
                                pkg.Version = updateInfo.CurrentVer;
                            }

                            count++;
                            updatableDict.Remove(match.Key);
                        }
                    }

                    foreach (var leftover in updatableDict)
                    {
                        if (_allPackages.Any(p =>
                            string.Equals(p.Id, leftover.Key, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Name, leftover.Value.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        var newUpdatePkg = new WingetPackage
                        {
                            Name = leftover.Value.Name,
                            Id = leftover.Key,
                            Version = leftover.Value.CurrentVer,
                            LatestVersion = leftover.Value.AvailableVer,
                            HasUpdate = true,
                            IsInstalled = true,
                            Category = PackageHelper.GetPublisherDisplayName(leftover.Key),
                            Source = leftover.Value.Source
                        };

                        _allPackages.Add(newUpdatePkg);
                        snapshot.Add(newUpdatePkg);
                        count++;
                    }

                    _updateCount = count;
                    _updateablePackages = snapshot.Where(p => p.HasUpdate).ToList();
                    UpdatesList.Clear();

                    foreach (var pkg in _updateablePackages) UpdatesList.Add(pkg);

                    UpdatesTabLabel.Text = count > 0
                        ? $"{ResourceString.GetString("tab_updates") ?? "Updates"} ({count})"
                        : (ResourceString.GetString("tab_updates") ?? "Updates");

                    StopUpdatesSpinner();

                    await ErrorLogging.LogInfo($"Update check done — {count} update(s).");

                    UpdateInstalledTabLabel();

                    if (_isInstalledMode)
                    {
                        RefreshInstalledTabList();
                    }

                    if (_isUpdatesMode) RefreshUpdatesTabList();
                });
            }
            catch (OperationCanceledException)
            {
                StopUpdatesSpinner();
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Update check failed: {ex.Message}");
                StopUpdatesSpinner();
            }
        }

        public async Task CancelWorkAsync()
        {
            try
            {
                await ErrorLogging.LogInfo("Cancellation requested via navigation guard.");
                _cts.Cancel();

                IsBusy = false;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Error during navigation cancellation: {ex.Message}");
            }

            await Task.CompletedTask;
        }
        #endregion

        #region UI Event Handlers (Tabs, Buttons, Search, Selection)
        private void TabSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PackageSearchBox == null || InstallButtonText == null || PackagesGridView == null) return;

            PackageSearchBox.Visibility = Visibility.Visible;

            if (TabSegmented.SelectedIndex == 0)
            {
                if (!_isUpdatesMode && !_isInstalledMode) return;
                _isUpdatesMode = false;
                _isInstalledMode = false;

                InstallButtonText.Text = ResourceString.GetString("btn_install_selected") ?? "Install Selected";
                InstallButtonIcon.Glyph = "\uE896";
                installingStatusText.Text = ResourceString.GetString("status_select_pkg") ?? "Select a package to install";

                UpdatesGridView.Visibility = Visibility.Collapsed;
                InstalledGridView.Visibility = Visibility.Collapsed;
                UpdatesGridView.SelectedItems.Clear();
                InstalledGridView.SelectedItems.Clear();
                StatusText.Visibility = Visibility.Collapsed;
                PackagesGridView.Visibility = Visibility.Visible;

                if (MenuInstallAsAdmin != null) MenuInstallAsAdmin.Text = ResourceString.GetString("menu_install_as_admin") ?? "Install as Administrator";
                if (MenuSkipIntegrity != null) MenuSkipIntegrity.Visibility = Visibility.Visible;
                if (MenuMachineScope != null)
                {
                    MenuMachineScope.Visibility = Visibility.Visible;
                    MenuMachineScope.Text = ResourceString.GetString("menu_install_machine") ?? "Install for all users";
                }
                if (MenuInteractive != null)
                {
                    MenuInteractive.Visibility = Visibility.Visible;
                    MenuInteractive.Text = ResourceString.GetString("menu_interactive_install") ?? "Interactive installation";
                }

                ApplySearch(PackageSearchBox.Text?.Trim() ?? string.Empty);
            }
            else if (TabSegmented.SelectedIndex == 1)
            {
                if (_isUpdatesMode) return;
                _isUpdatesMode = true;
                _isInstalledMode = false;

                InstallButtonText.Text = ResourceString.GetString("btn_update_selected") ?? "Update Selected";
                InstallButtonIcon.Glyph = "\uE898";
                installingStatusText.Text = ResourceString.GetString("status_select_update") ?? "Select packages to update";

                PackagesGridView.Visibility = Visibility.Collapsed;
                InstalledGridView.Visibility = Visibility.Collapsed;
                PackagesGridView.SelectedItems.Clear();
                InstalledGridView.SelectedItems.Clear();

                if (MenuInstallAsAdmin != null) MenuInstallAsAdmin.Text = ResourceString.GetString("menu_update_as_admin") ?? "Update as Administrator";
                if (MenuSkipIntegrity != null) MenuSkipIntegrity.Visibility = Visibility.Visible;
                if (MenuMachineScope != null)
                {
                    MenuMachineScope.Visibility = Visibility.Visible;
                    MenuMachineScope.Text = ResourceString.GetString("menu_update_machine") ?? "Update for all users";
                }
                if (MenuInteractive != null)
                {
                    MenuInteractive.Visibility = Visibility.Visible;
                    MenuInteractive.Text = ResourceString.GetString("menu_interactive_update") ?? "Interactive update";
                }

                RefreshUpdatesTabList();
                FilterLocalPackages(PackageSearchBox.Text?.Trim() ?? string.Empty);
            }
            else if (TabSegmented.SelectedIndex == 2)
            {
                if (_isInstalledMode) return;
                _isUpdatesMode = false;
                _isInstalledMode = true;

                InstallButtonText.Text = ResourceString.GetString("btn_uninstall_selected") ?? "Uninstall Selected";
                InstallButtonIcon.Glyph = "\uE74D";
                installingStatusText.Text = ResourceString.GetString("status_select_uninstall") ?? "Select packages to uninstall";

                PackagesGridView.Visibility = Visibility.Collapsed;
                UpdatesGridView.Visibility = Visibility.Collapsed;
                PackagesGridView.SelectedItems.Clear();
                UpdatesGridView.SelectedItems.Clear();

                if (MenuInstallAsAdmin != null) MenuInstallAsAdmin.Text = ResourceString.GetString("menu_uninstall_as_admin") ?? "Uninstall as Administrator";
                if (MenuSkipIntegrity != null) MenuSkipIntegrity.Visibility = Visibility.Collapsed;
                if (MenuMachineScope != null) MenuMachineScope.Visibility = Visibility.Collapsed;
                if (MenuInteractive != null)
                {
                    MenuInteractive.Visibility = Visibility.Visible;
                    MenuInteractive.Text = ResourceString.GetString("menu_interactive_uninstall") ?? "Interactive uninstall";
                }

                RefreshInstalledTabList();
                FilterLocalPackages(PackageSearchBox.Text?.Trim() ?? string.Empty);
            }

            if (SearchModeSimilar != null)
            {
                if (_isInstalledMode || _isUpdatesMode)
                {
                    SearchModeSimilar.IsEnabled = false;
                    if (InstantSearchCheckBox != null) InstantSearchCheckBox.Visibility = Visibility.Visible;

                    if (SearchModeSimilar.IsChecked == true)
                    {
                        if (SearchModeBoth != null) SearchModeBoth.IsChecked = true;
                    }
                }
                else
                {
                    SearchModeSimilar.IsEnabled = true;
                    if (InstantSearchCheckBox != null) InstantSearchCheckBox.Visibility = Visibility.Collapsed;
                }
            }

            if (SourceWingetCheckBox != null)
            {
                if (!_isInstalledMode && !_isUpdatesMode)
                {
                    SourceWingetCheckBox.Visibility = Visibility.Visible;
                    if (SourceMsStoreCheckBox != null) SourceMsStoreCheckBox.Visibility = Visibility.Visible;
                    if (SourceDotNetCheckBox != null) SourceDotNetCheckBox.Visibility = Visibility.Visible;
                    if (SourcePowerShellCheckBox != null) SourcePowerShellCheckBox.Visibility = Visibility.Visible;
                    if (SourceLocalCheckBox != null) SourceLocalCheckBox.Visibility = Visibility.Collapsed;
                }
                else if (_isUpdatesMode)
                {
                    SourceWingetCheckBox.Visibility = Visibility.Visible;
                    if (SourceMsStoreCheckBox != null) SourceMsStoreCheckBox.Visibility = Visibility.Visible;
                    if (SourceDotNetCheckBox != null) SourceDotNetCheckBox.Visibility = Visibility.Collapsed;
                    if (SourcePowerShellCheckBox != null) SourcePowerShellCheckBox.Visibility = Visibility.Collapsed;
                    if (SourceLocalCheckBox != null) SourceLocalCheckBox.Visibility = Visibility.Collapsed;
                }
                else if (_isInstalledMode)
                {
                    SourceWingetCheckBox.Visibility = Visibility.Visible;
                    if (SourceMsStoreCheckBox != null) SourceMsStoreCheckBox.Visibility = Visibility.Visible;
                    if (SourceLocalCheckBox != null) SourceLocalCheckBox.Visibility = Visibility.Visible;
                    if (SourceDotNetCheckBox != null) SourceDotNetCheckBox.Visibility = Visibility.Collapsed;
                    if (SourcePowerShellCheckBox != null) SourcePowerShellCheckBox.Visibility = Visibility.Collapsed;
                }
            }

            if (StandardHeader != null && UpdatesHeader != null)
            {
                if (_isUpdatesMode)
                {
                    StandardHeader.Visibility = Visibility.Collapsed;
                    UpdatesHeader.Visibility = Visibility.Visible;
                }
                else
                {
                    StandardHeader.Visibility = Visibility.Visible;
                    UpdatesHeader.Visibility = Visibility.Collapsed;
                }
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

            if (MenuInstallAsAdmin != null) MenuInstallAsAdmin.Text = ResourceString.GetString("menu_install_as_admin") ?? "Install as Administrator";
            if (MenuSkipIntegrity != null) MenuSkipIntegrity.Visibility = Visibility.Visible;
            if (MenuMachineScope != null)
            {
                MenuMachineScope.Visibility = Visibility.Visible;
                MenuMachineScope.Text = ResourceString.GetString("menu_install_machine") ?? "Install for all users";
            }
            if (MenuInteractive != null)
            {
                MenuInteractive.Visibility = Visibility.Visible;
                MenuInteractive.Text = ResourceString.GetString("menu_interactive_install") ?? "Interactive installation";
            }

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

        private void ToggleFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            FiltersSplitView.IsPaneOpen = !FiltersSplitView.IsPaneOpen;
        }

        private void FilterOptions_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading || PackageSearchBox == null) return;

            string query = PackageSearchBox.Text?.Trim() ?? string.Empty;

            if (_isInstalledMode || _isUpdatesMode)
            {
                FilterLocalPackages(query);
            }
            else
            {
                ApplySearch(query);
            }
        }

        private void PackageSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_suppressSearch || _isLoading) return;

            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            string query = sender.Text?.Trim() ?? string.Empty;

            if (_isInstalledMode || _isUpdatesMode)
            {
                FilterLocalPackages(query);
            }
            else
            {
                ApplySearch(query);
            }
        }

        private void PackageSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (_suppressSearch || _isLoading) return;

            string query = args.QueryText?.Trim() ?? string.Empty;

            if (_isInstalledMode || _isUpdatesMode)
            {
                FilterLocalPackages(query);
            }
            else
            {
                ApplySearch(query);
            }
        }

        private void FilterLocalPackages(string query)
        {
            bool isQueryActive = !string.IsNullOrWhiteSpace(query);

            if (_isInstalledMode)
            {
                InstalledList.Clear();
                var masterInstalled = _allPackages.Where(p => p.IsInstalled).ToList();
                var filtered = masterInstalled.Where(p => DoesPackageMatchFilter(p, query)).ToList();

                foreach (var pkg in filtered) InstalledList.Add(pkg);

                if (InstalledList.Count == 0)
                {
                    if (isQueryActive)
                    {
                        StatusText.Text = ResourceString.GetString("status_no_results") ?? "No packages found matching your search.";
                    }
                    else
                    {
                        StatusText.Text = ResourceString.GetString("status_no_installed_packages") ?? "No installed packages found.";
                    }

                    StatusText.Visibility = Visibility.Visible;
                    InstalledGridView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    StatusText.Visibility = Visibility.Collapsed;
                    InstalledGridView.Visibility = Visibility.Visible;
                }
            }
            else if (_isUpdatesMode)
            {
                UpdatesList.Clear();
                var masterUpdates = _allPackages.Where(p => p.HasUpdate).ToList();
                var filtered = masterUpdates.Where(p => DoesPackageMatchFilter(p, query)).ToList();

                foreach (var pkg in filtered) UpdatesList.Add(pkg);

                if (UpdatesList.Count == 0)
                {
                    if (isQueryActive)
                    {
                        StatusText.Text = ResourceString.GetString("status_no_results") ?? "No packages found matching your search.";
                    }
                    else
                    {
                        StatusText.Text = ResourceString.GetString("status_no_updates") ?? "No updates available. All packages are up to date.";
                    }

                    StatusText.Visibility = Visibility.Visible;
                    UpdatesGridView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    StatusText.Visibility = Visibility.Collapsed;
                    UpdatesGridView.Visibility = Visibility.Visible;
                }
            }
        }

        private async void ApplySearch(string query)
        {
            if (StatusText == null || PackageList == null || SearchTopProgressBar == null)
            {
                return;
            }

            var currentVersion = Interlocked.Increment(ref _searchVersion);

            StatusText.Visibility = Visibility.Collapsed;
            SearchTopProgressBar.Visibility = Visibility.Collapsed;
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
            var filteredLocal = _allPackages.Where(p => DoesPackageMatchFilter(p, query)).ToList();
            var sortedLocal = SortPackages(filteredLocal).ToList();

            foreach (var p in sortedLocal)
            {
                if (currentVersion != _searchVersion) return;
                PackageList.Add(p);
                if (++localCount % 50 == 0) await Task.Delay(1);
            }

            if (query.Length >= 3)
            {
                await Task.Delay(400);
                if (currentVersion != _searchVersion) return;

                if (!await NetworkHelper.IsConnectedAsync())
                {
                    SearchTopProgressBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SearchTopProgressBar.Visibility = Visibility.Visible;

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

                                if (DoesPackageMatchFilter(newPkg, query))
                                {
                                    newWebPackages.Add(newPkg);
                                }
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
                            SearchTopProgressBar.Visibility = Visibility.Collapsed;
                        }
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

        private bool DoesPackageMatchFilter(WingetPackage pkg, string query)
        {
            string safeName = pkg.Name ?? string.Empty;
            string safeId = pkg.Id ?? string.Empty;
            string safeSource = pkg.Source ?? string.Empty;

            bool isDotNet = safeSource.Equals("DotNet", StringComparison.OrdinalIgnoreCase) ||
                            safeId.StartsWith("Microsoft.DotNet", StringComparison.OrdinalIgnoreCase) ||
                            safeName.Contains(".NET", StringComparison.OrdinalIgnoreCase);

            bool isPowerShell = safeSource.Equals("PowerShell", StringComparison.OrdinalIgnoreCase) ||
                                safeId.Contains("PowerShell", StringComparison.OrdinalIgnoreCase) ||
                                safeName.Contains("PowerShell", StringComparison.OrdinalIgnoreCase);

            bool isMsStore = safeSource.Equals("msstore", StringComparison.OrdinalIgnoreCase) ||
                             (string.IsNullOrWhiteSpace(safeSource) && safeId.Length == 12 && Regex.IsMatch(safeId, "^[a-zA-Z0-9]+$"));

            bool isWinget = (safeSource.Equals("winget", StringComparison.OrdinalIgnoreCase) ||
                            (string.IsNullOrWhiteSpace(safeSource) && safeId.Contains(".")))
                            && !isDotNet && !isPowerShell && !isMsStore;

            bool isLocal = string.IsNullOrWhiteSpace(safeSource) && !isMsStore && !isDotNet && !isPowerShell && !isWinget;

            bool isLocalList = _isInstalledMode || _isUpdatesMode;

            if (SourceWingetCheckBox?.IsChecked == false && isWinget) return false;
            if (SourceMsStoreCheckBox?.IsChecked == false && isMsStore) return false;
            if (SourceDotNetCheckBox?.IsChecked == false && isDotNet) return false;
            if (SourcePowerShellCheckBox?.IsChecked == false && isPowerShell) return false;

            if (isLocalList)
            {
                if (SourceLocalCheckBox?.IsChecked == false && isLocal) return false;
            }

            if (string.IsNullOrWhiteSpace(query)) return true;

            string activeQuery = query;
            StringComparison compMode = MatchCaseCheckBox?.IsChecked == true
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;

            if (IgnoreSpecialCharsCheckBox?.IsChecked == true)
            {
                var regex = new Regex("[^a-zA-Z0-9]");
                safeName = regex.Replace(safeName, "");
                safeId = regex.Replace(safeId, "");
                activeQuery = regex.Replace(activeQuery, "");
            }

            if (string.IsNullOrWhiteSpace(activeQuery)) return true;

            if (SearchModeExact?.IsChecked == true)
            {
                return safeName.Equals(activeQuery, compMode) || safeId.Equals(activeQuery, compMode);
            }

            if (SearchModeSimilar?.IsChecked == true)
            {
                return safeName.StartsWith(activeQuery, compMode) || safeId.Contains(activeQuery, compMode);
            }

            if (SearchModeName?.IsChecked == true)
            {
                return safeName.Contains(activeQuery, compMode);
            }

            if (SearchModeId?.IsChecked == true)
            {
                return safeId.Contains(activeQuery, compMode);
            }

            return safeName.Contains(activeQuery, compMode) || safeId.Contains(activeQuery, compMode);
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

        private void StopUpdatesSpinner()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatesLoadingRing.IsActive = false;
                UpdatesLoadingRing.Visibility = Visibility.Collapsed;
            });
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

                string fallbackSource = catalog.Info?.Name ?? string.Empty;

                foreach (var match in result.Matches)
                {
                    var cp = match.CatalogPackage;
                    var id = cp.Id?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;

                    var name = string.IsNullOrWhiteSpace(cp.Name) ? PackageHelper.FormatPackageName(id) : cp.Name;
                    var ver = cp.AvailableVersions.FirstOrDefault()?.Version;

                    string pkgSource = cp.DefaultInstallVersion?.PackageCatalog?.Info?.Name ?? fallbackSource;

                    packages.Add(new DiscoveredPackageEntry(
                        id,
                        name,
                        string.IsNullOrWhiteSpace(ver) ? "N/A" : ver,
                        pkgSource
                    ));
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

        private async Task<Dictionary<string, (string Name, string Version)>> GetInstalledPackagesMapAsync(CancellationToken ct = default)
        {
            var result = new Dictionary<string, (string Name, string Version)>(StringComparer.OrdinalIgnoreCase);
            _installedSnapshot.Clear();
            _isUsingInventoryFallback = false;

            try
            {
                var cliPackages = await GetInstalledPackagesFromCliAsync(ct);

                foreach (var pkg in cliPackages)
                {
                    _installedSnapshot.Add(pkg);

                    if (!string.IsNullOrWhiteSpace(pkg.Id))
                    {
                        result[pkg.Id] = (pkg.Name, pkg.Version);
                        foreach (var key in PackageHelper.GetLookupKeys(pkg.Id, pkg.Name))
                        {
                            result.TryAdd(key, (pkg.Name, pkg.Version));
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(pkg.Name))
                    {
                        result.TryAdd(PackageHelper.NormalizeLookupKey(pkg.Name), (pkg.Name, pkg.Version));
                    }
                }

                await PopulateInstalledMapFallbackAsync(result, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"CLI installed list query failed: {ex.Message}");
                await PopulateInstalledMapFallbackAsync(result, ct);
            }

            return result;
        }

        private async Task<List<InstalledPackageEntry>> GetInstalledPackagesFromCliAsync(CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget.exe",
                Arguments = "list --accept-source-agreements",
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

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                TryTerminateProcess(process);

                _ = stdOut.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                _ = stdErr.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);

                return [];
            }

            var output = await stdOut;
            _ = await stdErr;

            var results = new List<InstalledPackageEntry>();
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool sepPassed = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) && line.Contains("Id")) continue;

                if (line.All(c => c == '-' || c == ' '))
                {
                    sepPassed = true;
                    continue;
                }

                if (!sepPassed) continue;

                var parts = Regex.Split(line, @"\s{2,}");
                if (parts.Length < 2) continue;

                string name = parts[0].Trim();
                string id = parts.Length > 2 ? parts[1].Trim() : name;
                string version = parts.Length > 2 ? parts[2].Trim() : parts[1].Trim();

                if (name.Equals("Name", StringComparison.OrdinalIgnoreCase) || id.Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string source = string.Empty;
                if (parts.Length >= 4)
                {
                    string lastPart = parts.Last().Trim();
                    if (lastPart.Equals("winget", StringComparison.OrdinalIgnoreCase) ||
                        lastPart.Equals("msstore", StringComparison.OrdinalIgnoreCase))
                    {
                        source = lastPart;
                    }
                }

                results.Add(new InstalledPackageEntry(
                    id,
                    name,
                    version,
                    PackageHelper.NormalizeLookupKey(id),
                    PackageHelper.NormalizeLookupKey(name),
                    source
                ));
            }

            return results;
        }
        #endregion

        #region CLI Fallbacks & Parsing
        private async Task<List<(string Name, string Id, string CurrentVersion, string AvailableVersion, string Source)>> GetUpdatablePackagesFromCliAsync(CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget.exe",
                Arguments = "upgrade --accept-source-agreements",
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

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                TryTerminateProcess(process);

                _ = stdOut.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                _ = stdErr.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);

                return [];
            }

            var output = await stdOut;
            _ = await stdErr;

            var results = new List<(string, string, string, string, string)>();
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool headerPassed = false, sepPassed = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) && line.Contains("Id"))
                {
                    headerPassed = true;
                    continue;
                }

                if (line.All(c => c == '-' || c == ' '))
                {
                    if (headerPassed) sepPassed = true;
                    continue;
                }

                if (!headerPassed || !sepPassed) continue;

                if (line.EndsWith("available.", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("applicable update found", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = Regex.Split(line, @"\s{2,}");
                if (parts.Length < 4) continue;

                var name = parts[0].Trim();
                var id = parts[1].Trim();
                var currentVer = parts[2].Trim();
                var available = parts[3].Trim();

                if (name.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                    id.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Commented out to ensure to get all updates. Uncomment for only traditional WinGet packages.
                // if (!PackageHelper.IsLikelyWingetPackageId(id)) continue;

                if (string.IsNullOrWhiteSpace(available)) continue;

                string source = string.Empty;
                if (parts.Length >= 5)
                {
                    string lastPart = parts.Last().Trim();
                    if (lastPart.Equals("winget", StringComparison.OrdinalIgnoreCase) ||
                        lastPart.Equals("msstore", StringComparison.OrdinalIgnoreCase))
                    {
                        source = lastPart;
                    }
                }

                results.Add((name, id, currentVer, available, source));
            }

            return results;
        }

        private async Task<List<DiscoveredPackageEntry>> DiscoverPackagesFromWingetCliAsync()
        {
            _isUsingCliDiscoveryFallback = true;

            var psi = new ProcessStartInfo
            {
                FileName = "winget.exe",
                Arguments = "search --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                ErrorLogging.LogDebug("Failed to start winget CLI.");
                return [];
            }

            var stdOut = process.StandardOutput.ReadToEndAsync();
            var stdErr = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                TryTerminateProcess(process);

                _ = stdOut.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                _ = stdErr.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);

                throw;
            }

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
                FileName = "winget.exe",
                Arguments = $"search --query \"{query.Replace("\"", "\"\"")}\" --source winget --accept-source-agreements",
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

            try
            {
                await process.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                TryTerminateProcess(process);

                _ = stdOut.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                _ = stdErr.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);

                throw;
            }

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

                string source = string.Empty;
                if (parts.Length >= 4)
                {
                    string lastPart = parts.Last().Trim();
                    if (lastPart.Equals("winget", StringComparison.OrdinalIgnoreCase) ||
                        lastPart.Equals("msstore", StringComparison.OrdinalIgnoreCase))
                    {
                        source = lastPart;
                    }
                }

                packages.Add(new DiscoveredPackageEntry(id, name, ver, source));
            }

            return packages;
        }
        #endregion

        #region Registry Fallback & Heuristics
        private async Task PopulateInstalledMapFallbackAsync(Dictionary<string, (string Name, string Version)> result, CancellationToken ct = default)
        {
            try
            {
                _isUsingInventoryFallback = true;

                List<SystemAppItem> apps = await AppManager.GetInstalledApps(true);

                foreach (var app in apps)
                {
                    if (string.IsNullOrWhiteSpace(app.DisplayName)) continue;

                    var name = app.DisplayName.Trim();
                    var key = PackageHelper.NormalizeLookupKey(name);

                    if (string.IsNullOrWhiteSpace(key)) continue;

                    result.TryAdd(key, (name, "Installed"));

                    _installedSnapshot.Add(new InstalledPackageEntry(
                        string.Empty,
                        name,
                        "Installed",
                        string.Empty,
                        key,
                        string.Empty
                    ));
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