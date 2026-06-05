// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using CommunityToolkit.WinUI;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SystemAppsPage : Page, IPurgeable
{
    #region Fields & Properties
    public ObservableCollection<SystemAppItem> AppList { get; set; } = new();

    private CancellationTokenSource? cancellationTokenSource;
    private List<SystemAppItem> allApps = new();
    private string? _pendingScrollTarget;

    private bool _isUpdating = false;

    private static readonly Dictionary<string, string[]> ModularAppMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "iCUE", new[] { "Corsair" } },
        { "Armoury", new[] { "ASUS", "ASUSTek" } },
        { "Synapse", new[] { "Razer" } },
        { "G HUB", new[] { "LGHUB" } },
        { "GeForce Experience", new[] { "NVIDIA Corporation" } }
    };

    internal PackagesViewModel? ViewModel { get; private set; }
    #endregion

    #region Initialization & Lifecycle
    public SystemAppsPage()
    {
        InitializeComponent();

        if (SettingsEngine.IsHighPerformanceModeEnabled)
        {
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }
        else
        {
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        RefreshAiStatus();
        LocalMachineSettingsEngine.SettingChanged += OnSettingChanged;

        AiExplainerService.PreWarmConnection();

        Loaded += SystemAppsPage_Loaded;
        Unloaded += SystemAppsPage_Unloaded;

        Loaded += SystemAppsPage_Loaded;
        Unloaded += SystemAppsPage_Unloaded;
    }

    private void OnSettingChanged(object? sender, string settingName)
    {
        if (settingName.Contains("ApiKey") || settingName == "ActiveAiProvider")
        {
            DispatcherQueue.TryEnqueue(() => RefreshAiStatus());
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is PackagesViewModel vm)
        {
            this.ViewModel = vm;
            this.DataContext = vm;
            this.AppList = vm.SystemAppList;

            if (this.AppList == null || this.AppList.Count == 0)
            {
                if (cancellationTokenSource == null)
                    cancellationTokenSource = new CancellationTokenSource();

                LoadInstalledApps(true, false, cancellationTokenSource.Token);
            }
        }
        else if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
        {
            _pendingScrollTarget = optionTag;
        }
    }

    private async void SystemAppsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (cancellationTokenSource == null) cancellationTokenSource = new CancellationTokenSource();

        if (!string.IsNullOrEmpty(_pendingScrollTarget))
        {
            await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
            _pendingScrollTarget = null;
        }
    }

    private void SystemAppsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _ = Purge();
    }
    #endregion

    #region TreeView Interaction
    private void AppTreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        args.Cancel = true;
    }

    private void appTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is SystemAppItem app)
        {
            if (sender.SelectedItems.Contains(app))
            {
                sender.SelectedItems.Remove(app);
            }
            else
            {
                sender.SelectedItems.Add(app);
            }
        }
    }
    #endregion

    #region Data Loading & Background Scanning
    private async void LoadInstalledApps(bool uninstallableOnly = true, bool win32Only = false, CancellationToken cancellationToken = default)
    {
        if (_isUpdating || (cancellationToken != default && cancellationToken.IsCancellationRequested)) return;

        _isUpdating = true;

        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                gettingAppsLoading.Visibility = Visibility.Visible;
                exportButton.Visibility = Visibility.Collapsed;
                appTreeView.Visibility = Visibility.Collapsed;
                uninstallButton.IsEnabled = false;
                uninstallingStatusText.Text = ResourceString.GetString("SystemAppsPage_UninstallTip");
                uninstallingStatusBar.Opacity = 0;
                appsFilter.IsEnabled = false;
                appsSort.IsEnabled = false;
            });

            List<SystemAppItem> installedApps;
            if (win32Only)
            {
                installedApps = await Task.Run(AppManager.GetWin32Apps, cancellationToken);
            }
            else
            {
                installedApps = await Task.Run(() => AppManager.GetInstalledApps(uninstallableOnly), cancellationToken);
            }

            await Task.Run(() => MarkStartupApps(installedApps), cancellationToken);

            DispatcherQueue.TryEnqueue(() =>
            {
                if (cancellationToken.IsCancellationRequested) return;

                AppList.Clear();
                allApps = installedApps.AsParallel().Where(app =>
                !app.DisplayName.Contains("evolveos_optimizer", StringComparison.CurrentCultureIgnoreCase)).ToList();

                foreach (var app in allApps)
                {
                    AppList.Add(app);
                }

                installedAppsCount.Text = string.Format(ResourceString.GetString("SystemAppsPage_TotalApps"), AppList.Count);
                installedAppsCount.Visibility = Visibility.Visible;

                appsFilter.IsEnabled = true;
                appsFilter.Visibility = Visibility.Visible;
                appsFilterText.Visibility = Visibility.Visible;

                appsSort.IsEnabled = true;
                appsSort.Visibility = Visibility.Visible;

                uninstallButton.Visibility = Visibility.Visible;
                exportButton.Visibility = Visibility.Visible;
                appTreeView.Visibility = Visibility.Visible;
                appTreeView.IsEnabled = true;
                uninstallButton.IsEnabled = true;
                uninstallingStatusText.Visibility = Visibility.Visible;
                AppSearchBox.Visibility = Visibility.Visible;
                gettingAppsLoading.Visibility = Visibility.Collapsed;
                TempStackButtonTextBar.Visibility = Visibility.Visible;

                _ = Task.Run(() => UpdateMissingSizesAsync(cancellationToken), cancellationToken);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorLogging.LogWritingFile(ex); }
        finally
        {
            _isUpdating = false;
        }
    }

    private async Task UpdateMissingSizesAsync(CancellationToken token)
    {
        var targets = allApps.Where(a => a.SizeMB <= 0).ToList();

        foreach (var app in targets)
        {
            if (token.IsCancellationRequested) return;

            try
            {
                string discoveryPath = string.Empty;

                if (!string.IsNullOrEmpty(app.InstallLocation) && Directory.Exists(app.InstallLocation))
                {
                    discoveryPath = app.InstallLocation;
                }

                if (string.IsNullOrEmpty(discoveryPath) && !string.IsNullOrEmpty(app.UninstallString))
                {
                    string rawPath = app.UninstallString.Replace("\"", "").Split(" /")[0].Split(" -")[0];
                    if (File.Exists(rawPath)) discoveryPath = Path.GetDirectoryName(rawPath) ?? string.Empty;
                    else if (Directory.Exists(rawPath)) discoveryPath = rawPath;
                }

                List<string> pathsToScan = new();
                if (!string.IsNullOrEmpty(discoveryPath)) pathsToScan.Add(discoveryPath);

                string name = app.DisplayName ?? "";

                var matchedApp = ModularAppMappings.Keys.FirstOrDefault(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

                if (matchedApp != null)
                {
                    foreach (var folder in ModularAppMappings[matchedApp])
                    {
                        pathsToScan.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), folder));
                        pathsToScan.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), folder));
                        pathsToScan.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), folder));
                    }
                }
                else
                {
                    string firstWord = name.Split(' ')[0];

                    if (firstWord.Length > 2 &&
                        !firstWord.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                        !firstWord.Equals("Windows", StringComparison.OrdinalIgnoreCase) &&
                        !firstWord.Equals("Intel", StringComparison.OrdinalIgnoreCase) &&
                        !firstWord.Equals("AMD", StringComparison.OrdinalIgnoreCase))
                    {
                        pathsToScan.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), firstWord));
                        pathsToScan.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), firstWord));
                        pathsToScan.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), firstWord));
                    }
                }

                if (pathsToScan.Count == 0) continue;

                long totalSize = await Task.Run(() =>
                {
                    long currentSize = 0;
                    var options = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };

                    foreach (var path in pathsToScan.Distinct())
                    {
                        if (!Directory.Exists(path)) continue;
                        try
                        {
                            var dirInfo = new DirectoryInfo(path);
                            foreach (var file in dirInfo.EnumerateFiles("*", options))
                            {
                                if (token.IsCancellationRequested) return 0;
                                currentSize += file.Length;
                            }
                        }
                        catch { /* Skip specific folder errors */ }
                    }
                    return currentSize;
                }, token);

                double mbResult = totalSize / 1024.0 / 1024.0;
                Debug.WriteLine($"[SizeScan] Finished {app.DisplayName}: {mbResult:N2} MB");

                if (mbResult > 0)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        app.SizeMB = mbResult;

                        int index = AppList.IndexOf(app);
                        if (index != -1)
                        {
                            AppList[index] = app;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SizeScan] Critical Error for {app.DisplayName}: {ex.Message}");
            }
        }
    }

    private void MarkStartupApps(List<SystemAppItem> apps)
    {
        try
        {
            var startupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keys = new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
            };

            foreach (var path in keys)
            {
                using var keyCurrentUser = Registry.CurrentUser.OpenSubKey(path);
                if (keyCurrentUser != null)
                {
                    foreach (var valName in keyCurrentUser.GetValueNames())
                    {
                        var val = keyCurrentUser.GetValue(valName)?.ToString();
                        if (!string.IsNullOrEmpty(val)) startupNames.Add(valName);
                    }
                }

                using var keyLocalMachine = Registry.LocalMachine.OpenSubKey(path);
                if (keyLocalMachine != null)
                {
                    foreach (var valName in keyLocalMachine.GetValueNames())
                    {
                        var val = keyLocalMachine.GetValue(valName)?.ToString();
                        if (!string.IsNullOrEmpty(val)) startupNames.Add(valName);
                    }
                }
            }

            foreach (var app in apps)
            {
                app.RunsAtStartup = startupNames.Any(s =>
                    app.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    s.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch { }
    }
    #endregion

    #region App Uninstallation & Cleanup
    private async void UninstallSelectedApp_Click(object sender, RoutedEventArgs e)
    {
        if (appTreeView.SelectedItems.Count == 0)
        {
            return;
        }

        var result = await ShowUninstallConfirmationDialog(appTreeView);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        uninstallButton.IsEnabled = false;
        appsFilter.IsEnabled = false;
        appsSort.IsEnabled = false;
        appTreeView.IsEnabled = false;

        var failedUninstalls = new List<string>();
        var successfulUninstalls = new List<string>();

        try
        {
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            var totalApps = appTreeView.SelectedItems.Count;
            var completedApps = 0;

            DispatcherQueue.TryEnqueue(() =>
            {
                uninstallingStatusBar.Value = 0;
                uninstallingStatusBar.Maximum = totalApps;
                uninstallingStatusBar.Opacity = 1;
            });

            foreach (var appInfo in appTreeView.SelectedItems.OfType<SystemAppItem>())
            {
                var selectedAppName = appInfo.DisplayName;
                var isWin32App = appInfo.IsWin32;

                await DispatcherQueue.EnqueueAsync(() =>
                {
                    uninstallingStatusText.Text = ResourceString.GetString("SystemAppsPage_Uninstalling") + " " + selectedAppName;
                });

                try
                {
                    await UninstallApps(selectedAppName, isWin32App);
                    successfulUninstalls.Add(selectedAppName);

                    if (isWin32App)
                    {
                        await HandleLeftoversAsync(selectedAppName);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(new Exception($"Error uninstalling {selectedAppName}: {ex.Message}\nStack Trace: {ex.StackTrace}"));
                    failedUninstalls.Add(selectedAppName);
                }

                completedApps++;
                DispatcherQueue.TryEnqueue(() =>
                {
                    uninstallingStatusBar.Value = completedApps;
                });
            }

            if (successfulUninstalls.Count > 0)
            {
                var successMessage = string.Join("\n", successfulUninstalls);
                App.ShowNotification(
                    ResourceString.GetString("SystemAppsPage_UnInstall"),
                    ResourceString.GetString("SystemAppsPage_UninstallationSuccess") + $":\n{successMessage}",
                    InfoBarSeverity.Success, 5000);
            }

            if (failedUninstalls.Count > 0)
            {
                var errorMessage = string.Join("\n", failedUninstalls);
                App.ShowNotification(
                    ResourceString.GetString("SystemAppsPage_UnInstall"),
                    ResourceString.GetString("SystemAppsPage_UninstallationError") + $":\n{errorMessage}",
                    InfoBarSeverity.Error, 5000);
            }

            appsFilter_SelectionChanged(appsFilter, e);

        }
        catch (Exception ex)
        {
            ErrorLogging.LogWritingFile(new Exception($"Error during uninstallation process: {ex.Message}\nStack Trace: {ex.StackTrace}"));
        }
        finally
        {
            appTreeView.SelectedItems.Clear();

            DispatcherQueue.TryEnqueue(() =>
            {
                uninstallingStatusText.Text = ResourceString.GetString("SystemAppsPage_UninstallTip");
                uninstallingStatusBar.Opacity = 0;
                uninstallButton.IsEnabled = true;
                appsFilter.IsEnabled = true;
                appsSort.IsEnabled = true;
                appTreeView.IsEnabled = true;

                if (MainWindow.Instance?.RootGrid?.DataContext is MainWinViewModel mainVm)
                {
                    mainVm.UpdatePowerState(mainVm.CurrentViewTag);
                }
            });
        }
    }

    private async Task HandleLeftoversAsync(string appName)
    {
        var leftovers = new List<string>();
        var safeAppName = string.Join("_", appName.Split(Path.GetInvalidFileNameChars()));

        var pathsToScan = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };

        foreach (var path in pathsToScan)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var dirs = Directory.GetDirectories(path);
                    foreach (var dir in dirs)
                    {
                        var dirName = Path.GetFileName(dir);
                        if (dirName.Equals(safeAppName, StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals(appName, StringComparison.OrdinalIgnoreCase))
                        {
                            leftovers.Add(dir);
                        }
                    }
                }
            }
            catch { }
        }

        var regKeys = new[] { @"SOFTWARE", @"SOFTWARE\WOW6432Node" };
        var hives = new[] { Registry.CurrentUser, Registry.LocalMachine };
        var foundRegKeys = new List<Tuple<RegistryKey, string>>();

        foreach (var hive in hives)
        {
            foreach (var baseKey in regKeys)
            {
                try
                {
                    using var key = hive.OpenSubKey(baseKey, writable: true);
                    if (key != null)
                    {
                        foreach (var subKey in key.GetSubKeyNames())
                        {
                            if (subKey.Equals(appName, StringComparison.OrdinalIgnoreCase) ||
                                subKey.Equals(safeAppName, StringComparison.OrdinalIgnoreCase))
                            {
                                leftovers.Add($@"Registry: {hive.Name}\{baseKey}\{subKey}");
                                foundRegKeys.Add(new Tuple<RegistryKey, string>(key, subKey));
                            }
                        }
                    }
                }
                catch { }
            }
        }

        if (leftovers.Count > 0)
        {
            string andMoreText = ResourceString.GetString("SystemAppsPage_AndMore") ?? "\n...and more";
            var leftoversList = string.Join("\n", leftovers.Take(8)) + (leftovers.Count > 8 ? andMoreText : "");

            string formatMsg = ResourceString.GetString("SystemAppsPage_DeepCleanMessage") ?? "We found {0} orphaned files/registry keys left behind by {1}. Would you like to permanently delete them to free up space?\n\n{2}";

            var contentTextBlock = new TextBlock
            {
                Text = string.Format(formatMsg, leftovers.Count, appName, leftoversList),
                TextWrapping = TextWrapping.Wrap
            };

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = ResourceString.GetString("SystemAppsPage_DeepCleanTitle") ?? "Deep Cleanup: Leftovers Found",
                Content = contentTextBlock,
                PrimaryButtonText = ResourceString.GetString("SystemAppsPage_CleanLeftoversBtn") ?? "Clean Leftovers",
                CloseButtonText = ResourceString.GetString("SystemAppsPage_SkipBtn") ?? "Skip",
                DefaultButton = ContentDialogButton.Primary
            };

            if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
            {
                dialog.Style = (Style)style;
            }

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                foreach (var item in leftovers.Where(l => !l.StartsWith("Registry:")))
                {
                    try
                    {
                        if (Directory.Exists(item)) Directory.Delete(item, true);
                    }
                    catch { }
                }
                foreach (var rk in foundRegKeys)
                {
                    try
                    {
                        rk.Item1.DeleteSubKeyTree(rk.Item2, false);
                    }
                    catch { }
                }
            }
        }
    }

    private static async Task UninstallApps(string appName, bool isWin32App)
    {
        ErrorLogging.LogDebug(new Exception($"Uninstalling: {appName}"));

        if (!isWin32App)
        {
            if (!appName.Contains("edge.stable", StringComparison.CurrentCultureIgnoreCase))
            {
                var cmdCommandRemoveProvisioned = $"powershell -Command \"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{appName}' }} | ForEach-Object {{ Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName }}\"";
                var cmdCommandRemoveAppxPackage = $"powershell -Command \"Get-AppxPackage -AllUsers | Where-Object {{ $_.Name -eq '{appName}' }} | Remove-AppxPackage\"";

                string windir = Environment.GetEnvironmentVariable("windir")!;

                var processInfoProvisioned = new ProcessStartInfo(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                        ? Path.Combine(windir, @"SysNative\cmd.exe")
                        : Path.Combine(windir, @"System32\cmd.exe"), $"/c {cmdCommandRemoveProvisioned}")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var processProvisioned = new Process { StartInfo = processInfoProvisioned };
                {
                    processProvisioned.Start();
                    var errorProvisioned = await processProvisioned.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(errorProvisioned))
                    {
                        ErrorLogging.LogWritingFile(new Exception(errorProvisioned));
                    }
                }

                var processInfoAppxPackage = new ProcessStartInfo(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                        ? Path.Combine(windir, @"SysNative\cmd.exe")
                        : Path.Combine(windir, @"System32\cmd.exe"), $"/c {cmdCommandRemoveAppxPackage}")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var processAppxPackage = new Process { StartInfo = processInfoAppxPackage };
                {
                    processAppxPackage.Start();
                    var errorAppxPackage = await processAppxPackage.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(errorAppxPackage))
                    {
                        ErrorLogging.LogWritingFile(new Exception(errorAppxPackage));
                        throw new Exception($"Failed to remove Appx package for {appName}: {errorAppxPackage}");
                    }
                }
            }
            else
            {
                var scriptFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "RemoveEdge.ps1");
                var cmdCommand = $"powershell.exe -ExecutionPolicy Bypass -File \"{scriptFilePath}\" -UninstallEdge -RemoveEdgeData -NonInteractive";

                string windir = Environment.GetEnvironmentVariable("windir")!;

                var processInfo = new ProcessStartInfo(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                        ? Path.Combine(windir, @"SysNative\cmd.exe")
                        : Path.Combine(windir, @"System32\cmd.exe"), $"/c {cmdCommand}")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                {
                    process.Start();
                    var error = await process.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(error))
                    {
                        ErrorLogging.LogWritingFile(new Exception(error));
                    }
                }
            }
        }
        else
        {
            try
            {
                var registryKeys = new[]
                {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

                string? uninstallString = null;
                foreach (var registryKey in registryKeys)
                {
                    using (var keyLocalMachine = Registry.LocalMachine.OpenSubKey(registryKey))
                    using (var keyCurrentUser = Registry.CurrentUser.OpenSubKey(registryKey))
                    {
                        if (keyLocalMachine != null || keyCurrentUser != null)
                        {
                            var subKeyNames = keyLocalMachine?.GetSubKeyNames().Concat(keyCurrentUser?.GetSubKeyNames() ?? Enumerable.Empty<string>()) ?? Enumerable.Empty<string>();

                            foreach (var subKeyName in subKeyNames)
                            {
                                using var subKey = keyLocalMachine?.OpenSubKey(subKeyName) ?? keyCurrentUser?.OpenSubKey(subKeyName);
                                var displayName = subKey?.GetValue("DisplayName")?.ToString();

                                if (!string.IsNullOrEmpty(displayName) && displayName.Equals(appName, StringComparison.OrdinalIgnoreCase))
                                {
                                    uninstallString = subKey?.GetValue("QuietUninstallString") as string;
                                    if (string.IsNullOrEmpty(uninstallString))
                                    {
                                        uninstallString = subKey?.GetValue("UninstallString") as string;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(uninstallString)) break;
                }

                if (string.IsNullOrEmpty(uninstallString))
                {
                    ErrorLogging.LogWritingFile(new Exception($"Uninstall string for {appName} not found in registry."));
                }

                if (!string.IsNullOrEmpty(uninstallString))
                {
                    if (!uninstallString.StartsWith("\"") && !uninstallString.EndsWith("\""))
                    {
                        uninstallString = $"\"{uninstallString}\"";
                    }
                }

                var windir = Environment.GetEnvironmentVariable("windir")!;

                var processInfo = new ProcessStartInfo(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                        ? Path.Combine(windir, @"SysNative\cmd.exe")
                        : Path.Combine(windir, @"System32\cmd.exe"),
                                        $"/c {uninstallString}")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var error = await process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    ErrorLogging.LogWritingFile(new Exception(error));
                }

                if (process.ExitCode != 0)
                {
                    ErrorLogging.LogWritingFile(new Exception($"Uninstallation failed with exit code: {process.ExitCode}"));
                }

                ErrorLogging.LogDebug(new Exception($"Successfully uninstalled {appName}"));
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(new Exception($"Error uninstalling {appName}: {ex.Message}"));
            }
        }
    }
    #endregion

    #region UI Control Events & Actions
    private void appsFilter_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        cancellationTokenSource = new CancellationTokenSource();

        switch (appsFilter.SelectedIndex)
        {
            case 0:
                LoadInstalledApps(true, false, cancellationTokenSource.Token);
                break;
            case 1:
                App.ShowNotification(
                    ResourceString.GetString("SystemAppsPage_UnInstall"),
                    ResourceString.GetString("SystemAppsPage_NotificationBody"),
                    InfoBarSeverity.Warning, 5000);

                LoadInstalledApps(false, false, cancellationTokenSource.Token);
                break;
            case 2:
                LoadInstalledApps(false, true, cancellationTokenSource.Token);
                break;
        }
    }

    private void appsSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppSearchBox != null && allApps != null)
        {
            SearchAndSortApps(AppSearchBox.Text.ToLower());
        }
    }

    private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && !string.IsNullOrEmpty(path))
        {
            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", $"\"{path}\"");
            }
            else
            {
                NotificationManager.Show("Location Not Found", "The installation directory does not exist or was moved.").WithSeverity(NotificationManager.NoticeSeverity.Error).WithDuration(3000).Perform();
            }
        }
    }

    private void ForceStopApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string appName && !string.IsNullOrEmpty(appName))
        {
            int killedCount = 0;
            try
            {
                var searchName = appName.Split(' ')[0];
                var processes = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains(searchName, StringComparison.OrdinalIgnoreCase));

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        killedCount++;
                    }
                    catch { }
                }

                if (killedCount > 0)
                {
                    string title = ResourceString.GetString("SystemAppsPage_ForceStopTitle") ?? "Force Stop";
                    string msg = string.Format(ResourceString.GetString("SystemAppsPage_ForceStopMessageSuccess") ?? "Terminated {0} background processes for {1}.", killedCount, appName);
                    NotificationManager.Show(title, msg).WithSeverity(NotificationManager.NoticeSeverity.Success).WithDuration(4000).Perform();
                }
                else
                {
                    string title = ResourceString.GetString("SystemAppsPage_ForceStopTitle") ?? "Force Stop";
                    string msg = string.Format(ResourceString.GetString("SystemAppsPage_ForceStopMessageNone") ?? "No running processes found for {0}.", appName);
                    NotificationManager.Show(title, msg).WithSeverity(NotificationManager.NoticeSeverity.Info).WithDuration(4000).Perform();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex);
            }
        }
    }

    private async void ExportAppsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = $"InstalledApps_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(desktopPath, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("App Name,Version,Install Date,Size (MB),Type,Install Location");

            foreach (var app in allApps.OrderBy(a => a.DisplayName))
            {
                var name = $"\"{app.DisplayName?.Replace("\"", "\"\"")}\"";
                var version = $"\"{app.Version?.Replace("\"", "\"\"")}\"";
                var date = app.InstallDate != DateTime.MinValue ? app.InstallDate.ToShortDateString() : "";
                var size = app.SizeMB > 0 ? app.SizeMB.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "0";
                var type = app.IsWin32 ? "Win32" : "UWP";
                var location = $"\"{app.InstallLocation?.Replace("\"", "\"\"")}\"";

                sb.AppendLine($"{name},{version},{date},{size},{type},{location}");
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

            string title = ResourceString.GetString("SystemAppsPage_ExportTitle") ?? "Export Successful";
            string msg = string.Format(ResourceString.GetString("SystemAppsPage_ExportMessage") ?? "Exported {0} apps to {1}", allApps.Count, fileName);
            NotificationManager.Show(title, msg).WithSeverity(NotificationManager.NoticeSeverity.Success).WithDuration(5000).Perform();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogWritingFile(ex);
            NotificationManager.Show("Export Error", ex.Message).WithSeverity(NotificationManager.NoticeSeverity.Error).WithDuration(5000).Perform();
        }
    }

    private async void TempButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TempStack.Visibility = Visibility.Visible;
            TempProgress.Visibility = Visibility.Visible;
            TempButtonStack.Visibility = Visibility.Collapsed;
            TempStatusText.Text = ResourceString.GetString("SystemAppsPage_DeletingTemp") + "...";

            var result = await AppManager.RemoveTempFiles();

            if (result)
            {
                App.ShowNotification(
                    ResourceString.GetString("SystemAppsPage_UnInstall"),
                    ResourceString.GetString("SystemAppsPage_TempDelSucc"),
                    InfoBarSeverity.Success, 5000);

                if (ViewModel != null) await ViewModel.RefreshAllDataAsync();
            }
            else
            {
                App.ShowNotification(
                    ResourceString.GetString("SystemAppsPage_UnInstall"),
                    ResourceString.GetString("SystemAppsPage_ErrTempDel"),
                    InfoBarSeverity.Error, 5000);
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogWritingFile(ex);
            App.ShowNotification("Error", ex.Message, InfoBarSeverity.Error, 5000);
        }
        finally
        {
            TempStack.Visibility = Visibility.Collapsed;
            TempProgress.Visibility = Visibility.Collapsed;
            TempButtonStack.Visibility = Visibility.Visible;
        }
    }

    private void StartupBadge_Click(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Instance != null)
        {
            MainWindow.Instance.SwitchPage("SystemManager", "StartupManagerPage");
        }
        else
        {
            Debug.WriteLine("❌ MainWindow.Instance is null!");
        }
    }
    #endregion

    #region Searching & Sorting
    private void SearchApps(string query)
    {
        SearchAndSortApps(query);
    }

    private void SearchAndSortApps(string query)
    {
        if (noAppFoundText != null) noAppFoundText.Visibility = Visibility.Collapsed;

        var filteredApps = string.IsNullOrWhiteSpace(query)
            ? allApps
            : allApps.Where(app => app.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        var sortTag = (appsSort?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Name";

        filteredApps = sortTag switch
        {
            "Size" => filteredApps.OrderByDescending(a => a.SizeMB),
            "Date" => filteredApps.OrderByDescending(a => a.InstallDate),
            _ => filteredApps.OrderBy(a => a.DisplayName)
        };

        var finalList = filteredApps.ToList();

        AppList.Clear();
        foreach (var app in finalList)
        {
            AppList.Add(app);
        }

        if (AppList.Count == 0 && noAppFoundText != null)
        {
            noAppFoundText.Visibility = Visibility.Visible;
        }

        if (installedAppsCount != null)
        {
            installedAppsCount.Text = string.Format(ResourceString.GetString("SystemAppsPage_TotalApps"), AppList.Count);
        }
    }

    private void AppSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            SearchApps(sender.Text.ToLower());
        }
    }

    private void AppSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        SearchApps(args.QueryText.ToLower());
    }
    #endregion

    #region Dialogs
    public async Task<ContentDialogResult> ShowUninstallConfirmationDialog(TreeView appTreeView)
    {
        var selectedItemsText = new StringBuilder();

        foreach (var item in appTreeView.SelectedItems.OfType<SystemAppItem>())
        {
            selectedItemsText.AppendLine(item.DisplayName);
        }

        var firstLine = ResourceString.GetString("SystemAppsPage_ConfirmRemoveApps");
        var lastLine = ResourceString.GetString("SystemAppsPage_ConfirmContinue");

        var firstLineTextBlock = new TextBlock
        {
            Text = firstLine,
            Margin = new Thickness(0, 10, 0, 20),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var lastLineTextBlock = new TextBlock
        {
            Text = lastLine,
            Margin = new Thickness(0, 20, 0, 10),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var selectedAppsTextBlock = new TextBlock
        {
            Text = selectedItemsText.ToString(),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
        };

        var scrollViewer = new ScrollViewer
        {
            Content = selectedAppsTextBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 400
        };

        var contentStackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { firstLineTextBlock, scrollViewer, lastLineTextBlock }
        };

        var confirmationDialog = new ContentDialog()
        {
            XamlRoot = this.XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("SystemAppsPage_UnInstall"),
            Content = contentStackPanel,
            CloseButtonText = ResourceString.GetString("SystemAppsPage_Close"),
            PrimaryButtonText = ResourceString.GetString("SystemAppsPage_Continue"),
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
        };

        return await confirmationDialog.ShowAsync();
    }
    #endregion

    #region AI Explainer Integration

    public static readonly DependencyProperty IsAiEnabledProperty =
        DependencyProperty.Register(nameof(IsAiEnabled), typeof(bool), typeof(SystemAppsPage), new PropertyMetadata(false));

    public bool IsAiEnabled
    {
        get => (bool)GetValue(IsAiEnabledProperty);
        set => SetValue(IsAiEnabledProperty, value);
    }

    private void RefreshAiStatus()
    {
        var activeProvider = LocalMachineSettingsEngine.ActiveAiProvider;
        IsAiEnabled = activeProvider switch
        {
            AiProvider.Groq => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GroqApiKey),
            AiProvider.Gemini => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GeminiApiKey),
            AiProvider.OpenRouter => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.OpenRouterApiKey),
            AiProvider.Cohere => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.CohereApiKey),
            AiProvider.Mistral => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.MistralApiKey),
            _ => false
        };
    }

    private async void ExplainApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SystemAppItem app)
        {
            var flyout = button.Flyout as Flyout;
            if (flyout == null) return;

            var stackPanel = flyout.Content as StackPanel;
            var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Tag?.ToString() == "AiExplanationText");

            if (textBlock == null) return;

            textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

            string context = $"App Name: {app.DisplayName}\n" +
                             $"Version: {app.Version}\n" +
                             $"Type: {(app.IsWin32 ? "Win32 Executable" : "UWP/Modern App")}\n" +
                             $"Install Path: {app.InstallLocation}";

            string category = ResourceString.GetString("system_apps_page_category_name") ?? "Installed Application";

            string explanation = await AiExplainerService.ExplainGenericItemAsync(
                itemName: app.DisplayName,
                itemCategory: category,
                contextDetails: context
            );

            textBlock.Text = explanation;
        }
    }
    #endregion

    #region Purge Page
    public async Task Purge()
    {
        Debug.WriteLine("[SystemAppsPage] Caching Purge requested. Pausing page...");

        if (cancellationTokenSource != null)
        {
            try { cancellationTokenSource.Cancel(); cancellationTokenSource.Dispose(); } catch { }
            cancellationTokenSource = null;
        }

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and App Collections...");

            AppList?.Clear();
            allApps?.Clear();

            this.Loaded -= SystemAppsPage_Loaded;
            this.Unloaded -= SystemAppsPage_Unloaded;

            ViewModel = null;
            this.DataContext = null;
            this.Content = null;

            _ = Task.Run(() =>
            {
                DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
            });
        }
        else
        {
            Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
        }
    }
    #endregion
}