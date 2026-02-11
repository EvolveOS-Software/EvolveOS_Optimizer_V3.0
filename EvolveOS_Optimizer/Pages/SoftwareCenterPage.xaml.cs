using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SoftwareCenterPage : Page
{
    private readonly PackagesViewModel _sharedViewModel = new PackagesViewModel();

    public SoftwareCenterPage()
    {
        this.InitializeComponent();

        SoftwareNav.SelectedItem = SoftwareNav.MenuItems[0];
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