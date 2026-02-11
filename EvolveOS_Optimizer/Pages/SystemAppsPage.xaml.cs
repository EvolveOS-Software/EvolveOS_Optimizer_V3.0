using CommunityToolkit.WinUI;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SystemAppsPage : Page
{
    public ObservableCollection<Tuple<string, string, bool>> AppList { get; set; } = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private List<Tuple<string, string, bool>> allApps = new();
    private string? _pendingScrollTarget;

    internal PackagesViewModel? ViewModel { get; private set; }

    public SystemAppsPage()
    {
        InitializeComponent();

        ErrorLogging.LogDebug(new Exception("Initializing SystemAppsPage"));
        this.NavigationCacheMode = NavigationCacheMode.Required;
        Loaded += SystemAppsPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is PackagesViewModel vm)
        {
            this.ViewModel = vm;
            this.DataContext = vm;

            this.AppList = vm.SystemAppList;
        }
        else if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
        {
            _pendingScrollTarget = optionTag;
        }

        base.OnNavigatedTo(e);
    }

    private async void SystemAppsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_pendingScrollTarget))
        {
            await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
            _pendingScrollTarget = null;
        }
    }

    private void AppTreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        args.Cancel = true;
    }

    private void appTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is Tuple<string, string, bool> app)
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

    private async void LoadInstalledApps(bool uninstallableOnly = true, bool win32Only = false, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            DispatcherQueue.TryEnqueue(() =>
            {
                gettingAppsLoading.Visibility = Visibility.Visible;
                appTreeView.Visibility = Visibility.Collapsed;
                uninstallButton.IsEnabled = false;
                uninstallingStatusText.Text = ResourceString.GetString("SystemAppsPage_UninstallTip");
                uninstallingStatusBar.Opacity = 0;
                appsFilter.IsEnabled = false;
            });

            ErrorLogging.LogDebug(new Exception("Loading InstalledApps"));

            List<Tuple<string, string, bool>> installedApps;
            if (win32Only)
            {
                installedApps = await Task.Run(AppManager.GetWin32Apps);
            }
            else
            {
                installedApps = await Task.Run(() => AppManager.GetInstalledApps(uninstallableOnly));
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                AppList.Clear();

                allApps = installedApps.AsParallel().Where(app =>
                !app.Item1.Contains("rytunex", StringComparison.CurrentCultureIgnoreCase)).ToList();

                foreach (var app in allApps)
                {
                    AppList.Add(app);
                }

                installedAppsCount.Text = string.Format(ResourceString.GetString("SystemAppsPage_TotalApps"), AppList.Count);
                installedAppsCount.Visibility = Visibility.Visible;
                appsFilter.IsEnabled = true;
                appsFilter.Visibility = Visibility.Visible;
                appsFilterText.Visibility = Visibility.Visible;
                uninstallButton.Visibility = Visibility.Visible;
                appTreeView.Visibility = Visibility.Visible;
                appTreeView.IsEnabled = true;
                uninstallButton.IsEnabled = true;
                uninstallingStatusText.Visibility = Visibility.Visible;
                AppSearchBox.Visibility = Visibility.Visible;
                gettingAppsLoading.Visibility = Visibility.Collapsed;
                TempStackButtonTextBar.Visibility = Visibility.Visible;
            });
        }
        catch (OperationCanceledException ex)
        {
            ErrorLogging.LogDebug(ex);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogWritingFile(ex);
        }
    }

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
        appTreeView.IsEnabled = false;

        var failedUninstalls = new List<string>();
        var successfulUninstalls = new List<string>();

        try
        {
            var totalApps = appTreeView.SelectedItems.Count;
            var completedApps = 0;

            DispatcherQueue.TryEnqueue(() =>
            {
                uninstallingStatusBar.Value = 0;
                uninstallingStatusBar.Maximum = totalApps;
                uninstallingStatusBar.Opacity = 1;
            });

            foreach (var appInfo in appTreeView.SelectedItems.OfType<Tuple<string, string, bool>>())
            {
                var selectedAppName = appInfo.Item1;
                var isWin32App = appInfo.Item3;

                await DispatcherQueue.EnqueueAsync(() =>
                {
                    uninstallingStatusText.Text = ResourceString.GetString("SystemAppsPage_Uninstalling") + " " + selectedAppName;
                });

                try
                {
                    await UninstallApps(selectedAppName, isWin32App);
                    successfulUninstalls.Add(selectedAppName);
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(ex);
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

                if (ViewModel != null)
                {
                    await ViewModel.RefreshAllDataAsync();
                }
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
            ErrorLogging.LogWritingFile(ex);
        }
        finally
        {
            appTreeView.SelectedItems.Clear();

            DispatcherQueue.TryEnqueue(() =>
            {
                uninstallingStatusText.Text = ResourceString.GetString("SystemAppsPage_UninstallTip");
                uninstallButton.IsEnabled = true;
                appsFilter.IsEnabled = true;
                appTreeView.IsEnabled = true;
            });
        }
    }

    private static async Task UninstallApps(string appName, bool isWin32App)
    {
        ErrorLogging.LogDebug(new Exception($"Uninstalling: {appName}"));

        if (!isWin32App)
        {
            if (!appName.Contains("edge.stable", StringComparison.CurrentCultureIgnoreCase))
            {
                var cmdCommandRemoveProvisioned = $"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{appName}' }} | ForEach-Object {{ Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName }}";
                var cmdCommandRemoveAppxPackage = $"Get-AppxPackage -AllUsers | Where-Object {{ $_.Name -eq '{appName}' }} | Remove-AppxPackage";

                try
                {
                    await CommandExecutor.RunCommand("/c powershell -Command \"" + cmdCommandRemoveProvisioned + "\"");
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(ex);
                }

                try
                {
                    await CommandExecutor.RunCommand("/c powershell -Command \"" + cmdCommandRemoveAppxPackage + "\"");
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogWritingFile(ex);
                    throw;
                }
            }
            else
            {
                var scriptFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "RemoveEdge.ps1");
                var cmdCommand = $"powershell.exe -ExecutionPolicy Bypass -File \"{scriptFilePath}\" -UninstallEdge -RemoveEdgeData -NonInteractive";

                await CommandExecutor.RunCommand("/c " + cmdCommand);
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
                            var subKeyNames = (keyLocalMachine?.GetSubKeyNames() ?? Enumerable.Empty<string>())
                                              .Concat(keyCurrentUser?.GetSubKeyNames() ?? Enumerable.Empty<string>());

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
                    return;
                }

                if (!uninstallString.StartsWith("\"") && !uninstallString.EndsWith("\""))
                {
                    uninstallString = $"\"{uninstallString}\"";
                }

                await CommandExecutor.RunCommand("/c " + uninstallString);

                ErrorLogging.LogDebug(new Exception($"Successfully uninstalled {appName}"));
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex);
            }
        }
    }

    private void appsFilter_SelectionChanged(object sender, RoutedEventArgs e)
    {
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

    private async void TempButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TempStack.Visibility = Visibility.Visible;
            TempProgress.Visibility = Visibility.Visible;
            TempButtonStack.Visibility = Visibility.Collapsed;
            TempStatusText.Text = ResourceString.GetString("SystemAppsPage_DeletingTemp") + "...";

            var result = await AppManager.RemoveTempFiles();

            TempStack.Visibility = Visibility.Collapsed;
            TempProgress.Visibility = Visibility.Collapsed;
            TempButtonStack.Visibility = Visibility.Visible;

            if (result)
            {
                App.ShowNotification(
                    ResourceString.GetString("SystemAppsPage_UnInstall"),
                    ResourceString.GetString("SystemAppsPage_TempDelSucc"),
                    InfoBarSeverity.Success, 5000);

                if (ViewModel != null)
                {
                    await ViewModel.RefreshAllDataAsync();
                }
            }
            else
            {
                App.ShowNotification(
                    ResourceString.GetString("SystemAppsPage_UnInstall"),
                    ResourceString.GetString("SystemAppsPage_ErrTempDel"),
                    InfoBarSeverity.Error, 5000);
            }
        }
        catch (Exception)
        {
            TempStack.Visibility = Visibility.Collapsed;
            TempProgress.Visibility = Visibility.Collapsed;
            TempButtonStack.Visibility = Visibility.Visible;

            App.ShowNotification(
                ResourceString.GetString("SystemAppsPage_UnInstall"),
                ResourceString.GetString("SystemAppsPage_ErrTempDel"),
                InfoBarSeverity.Error, 5000);
        }
    }

    private void SearchApps(string query)
    {
        noAppFoundText.Visibility = Visibility.Collapsed;

        var filteredApps = allApps.AsParallel()
                                  .Where(app => app.Item1.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                                  .OrderBy(app => app.Item1)
                                  .ToList();

        AppList.Clear();
        foreach (var app in filteredApps)
        {
            AppList.Add(app);
        }

        if (AppList.Count == 0)
        {
            noAppFoundText.Visibility = Visibility.Visible;
        }

        installedAppsCount.Text = string.Format(ResourceString.GetString("SystemAppsPage_TotalApps"), AppList.Count);
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

    public async Task<ContentDialogResult> ShowUninstallConfirmationDialog(TreeView appTreeView)
    {
        var selectedItemsText = new StringBuilder();

        foreach (var item in appTreeView.SelectedItems.OfType<Tuple<string, string, bool>>())
        {
            selectedItemsText.AppendLine(item.Item1);
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
            XamlRoot = XamlRoot,
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
}