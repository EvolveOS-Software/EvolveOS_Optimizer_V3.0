// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SoftwareCenterPage : Page
{
    #region Static Members (For Tray/External Navigation)
    public static Action<string>? ExternalPaneRequest;
    public static string RequestedPaneOnLoad { get; set; } = "";
    #endregion

    private PackagesViewModel? _sharedViewModel = new PackagesViewModel();
    private NavigationViewItem? _previousItem;
    private bool _isSyncingSelection = false;

    public SoftwareCenterPage()
    {
        this.InitializeComponent();

        if (SettingsEngine.IsHighPerformanceModeEnabled)
        {
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }
        else
        {
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        this.Loaded += SoftwareCenterPage_Loaded;
        this.Unloaded += Page_Unloaded;
    }

    private void SoftwareCenterPage_Loaded(object sender, RoutedEventArgs e)
    {
        ExternalPaneRequest = (requestedPane) =>
        {
            var targetItem = SoftwareNav.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == requestedPane);

            if (targetItem != null)
            {
                SoftwareNav.SelectedItem = targetItem;
            }
        };

        if (!string.IsNullOrEmpty(RequestedPaneOnLoad))
        {
            ExternalPaneRequest?.Invoke(RequestedPaneOnLoad);
            RequestedPaneOnLoad = "";
        }
        else if (SoftwareNav.MenuItems.Count > 0 && SoftwareNav.SelectedItem == null)
        {
            var firstItem = SoftwareNav.MenuItems[0] as NavigationViewItem;
            SoftwareNav.SelectedItem = firstItem;
            _previousItem = firstItem;
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        Purge();
    }

    private async void SoftwareNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isSyncingSelection) return;

        if (args.SelectedItemContainer is NavigationViewItem selectedItem)
        {
            if (ContentFrame.Content is IBusyPage busyPage && busyPage.IsBusy)
            {
                _isSyncingSelection = true;
                sender.SelectedItem = _previousItem;
                _isSyncingSelection = false;

                ContentDialog dialog = new ContentDialog
                {
                    Title = busyPage.BusyTitle,
                    Content = busyPage.BusyMessage,
                    PrimaryButtonText = ResourceString.GetString("btn_proceed") ?? "Proceed",
                    CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await busyPage.CancelWorkAsync();
                    _isSyncingSelection = true;
                    sender.SelectedItem = selectedItem;
                    _isSyncingSelection = false;
                    PerformNavigation(selectedItem);
                }
                return;
            }

            PerformNavigation(selectedItem);
        }
    }

    private void PerformNavigation(NavigationViewItem selectedItem)
    {
        string? tag = selectedItem.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        if (MainWindow.Instance?.RootGrid?.DataContext is MainWinViewModel mainVm)
        {
            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);
        }

        Type pageType = tag switch
        {
            "SystemAppsPage" => typeof(SystemAppsPage),
            "AppStorePage" => typeof(AppStorePage),
            "PackagesPage" => typeof(PackagesPage),
            _ => typeof(SystemAppsPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, _sharedViewModel);
            _previousItem = selectedItem;
        }
    }

    #region Purge Page
    public void Purge()
    {
        Debug.WriteLine("[SoftwareCenterPage] Caching Purge requested. Broadcasting sleep signal...");
        ExternalPaneRequest = null;

        if (ContentFrame.Content is IPurgeable purgeablePage)
        {
            purgeablePage.Purge();
        }

        Debug.WriteLine("[SoftwareCenterPage] Sleep signal sent. Host frame and Shared VM preserved in cache.");
    }
    #endregion
}