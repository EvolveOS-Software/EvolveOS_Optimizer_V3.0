namespace EvolveOS_Optimizer.Pages
{
    public partial class UtilitiesPage : Page
    {
        public UtilitiesPage()
        {
            InitializeComponent();
        }

        private void UtilitiesNav_Loaded(object sender, RoutedEventArgs e)
        {
            if (UtilitiesNav.MenuItems.Count > 0)
            {
                UtilitiesNav.SelectedItem = UtilitiesNav.MenuItems[0];
            }
        }

        private void UtilitiesNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string? tag = selectedItem.Tag?.ToString();
                Type pageType = tag switch
                {
                    "DNSChangerPage" => typeof(DNSChangerPage),
                    "WinBuilderPage" => typeof(WinBuilderPage),
                    "AdvancedUtilsPage" => typeof(AdvancedUtilsPage),
                    _ => typeof(DNSChangerPage)
                };

                ContentFrame.Navigate(pageType);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Content = null;
        }
    }
}