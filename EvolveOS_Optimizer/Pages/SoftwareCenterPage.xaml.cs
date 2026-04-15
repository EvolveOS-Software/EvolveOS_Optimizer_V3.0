using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SoftwareCenterPage : Page
{
    private PackagesViewModel? _sharedViewModel = new PackagesViewModel();

    public SoftwareCenterPage()
    {
        this.InitializeComponent();

        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Disabled;

        SoftwareNav.SelectedItem = SoftwareNav.MenuItems[0];
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

    private void SoftwareNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem selectedItem)
        {
            string? tag = selectedItem.Tag?.ToString();
            Type pageType = tag switch
            {
                "PackagesPage" => typeof(PackagesPage),
                "SystemAppsPage" => typeof(SystemAppsPage),
                "AppStorePage" => typeof(AppStorePage),
                _ => typeof(PackagesPage)
            };

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType, _sharedViewModel);
            }
        }
    }
}