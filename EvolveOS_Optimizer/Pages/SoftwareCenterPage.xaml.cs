using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SoftwareCenterPage : Page
{
    private PackagesViewModel? _sharedViewModel = new PackagesViewModel();

    public SoftwareCenterPage()
    {
        this.InitializeComponent();

        SoftwareNav.SelectedItem = SoftwareNav.MenuItems[0];
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_sharedViewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }

        ContentFrame.Content = null;

        _sharedViewModel = null;

        this.DataContext = null;

        Debug.WriteLine("[SoftwareCenterPage] Shared ViewModel and Frame cleared.");
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
                _ => typeof(PackagesPage)
            };

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType, _sharedViewModel);
            }
        }
    }
}