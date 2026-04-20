// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SoftwareCenterPage : Page
{
    private PackagesViewModel? _sharedViewModel = new PackagesViewModel();
    private NavigationViewItem? _previousItem;
    private bool _isSyncingSelection = false;

    public SoftwareCenterPage()
    {
        this.InitializeComponent();

        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Disabled;

        var firstItem = SoftwareNav.MenuItems[0] as NavigationViewItem;
        SoftwareNav.SelectedItem = firstItem;
        _previousItem = firstItem;
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is IPurgeable purgeablePage)
        {
            purgeablePage.Purge();
        }

        int originalCacheSize = ContentFrame.CacheSize;
        ContentFrame.CacheSize = 0;

        ContentFrame.Content = null;
        ContentFrame.BackStack.Clear();
        ContentFrame.ForwardStack.Clear();

        ContentFrame.CacheSize = originalCacheSize;

        if (_sharedViewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _sharedViewModel = null;
        this.DataContext = null;

        Debug.WriteLine("[SoftwareCenterPage] Shared ViewModel, Frame, and Child Caches completely PURGED from memory.");
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
                    XamlRoot = this.XamlRoot,
                    // Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
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
        Type pageType = tag switch
        {
            "PackagesPage" => typeof(PackagesPage),
            "SystemAppsPage" => typeof(SystemAppsPage),
            "AppStorePage" => typeof(AppStorePage),
            "StartupManagerPage" => typeof(StartupManagerPage),
            _ => typeof(PackagesPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, _sharedViewModel);
            _previousItem = selectedItem;
        }
    }
}