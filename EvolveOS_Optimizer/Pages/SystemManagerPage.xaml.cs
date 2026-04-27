namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SystemManagerPage : Page
    {
        public SystemManagerPage()
        {
            this.InitializeComponent();
            this.Loaded += SystemManagerPage_Loaded;
        }

        private void SystemManagerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (SystemNav.MenuItems.Count > 0)
            {
                SystemNav.SelectedItem = SystemNav.MenuItems[0];
            }
        }

        private void SystemNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string? selectedTag = selectedItem.Tag?.ToString();

                switch (selectedTag)
                {
                    case "ProcessManagerPage":
                        ContentFrame.Navigate(typeof(ProcessManagerPage));
                        break;

                    case "ServiceManagerPage":
                        ContentFrame.Navigate(typeof(ServiceManagerPage));
                        break;

                    case "StartupManagerPage":
                        ContentFrame.Navigate(typeof(StartupManagerPage));
                        break;
                }
            }
        }
    }
}