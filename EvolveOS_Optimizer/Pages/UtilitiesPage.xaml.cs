using Microsoft.UI.Xaml.Navigation;

namespace EvolveOS_Optimizer.Pages
{
    public partial class UtilitiesPage : Page
    {
        private NavigationViewItem? _previousItem;
        private bool _isSyncingSelection = false;

        public UtilitiesPage()
        {
            InitializeComponent();
            ContentFrame.Navigated += ContentFrame_Navigated;
        }

        private void UtilitiesNav_Loaded(object sender, RoutedEventArgs e)
        {
            if (UtilitiesNav.MenuItems.Count > 0 && UtilitiesNav.MenuItems[0] is NavigationViewItem firstItem)
            {
                UtilitiesNav.SelectedItem = firstItem;
                _previousItem = firstItem;
            }
        }

        private void UtilitiesNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_isSyncingSelection) return;

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

                if (ContentFrame.CurrentSourcePageType != pageType)
                {
                    ContentFrame.Navigate(pageType);

                    if (ContentFrame.CurrentSourcePageType != pageType)
                    {
                        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                        {
                            _isSyncingSelection = true;

                            UtilitiesNav.SelectedItem = null;
                            UtilitiesNav.UpdateLayout();
                            UtilitiesNav.SelectedItem = _previousItem;

                            _isSyncingSelection = false;
                        });
                    }
                    else
                    {
                        _previousItem = selectedItem;
                    }
                }
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            _isSyncingSelection = true;

            foreach (var item in UtilitiesNav.MenuItems.OfType<NavigationViewItem>())
            {
                string? tag = item.Tag?.ToString();
                Type expectedType = tag switch
                {
                    "DNSChangerPage" => typeof(DNSChangerPage),
                    "WinBuilderPage" => typeof(WinBuilderPage),
                    "AdvancedUtilsPage" => typeof(AdvancedUtilsPage),
                    _ => typeof(DNSChangerPage)
                };

                if (e.SourcePageType == expectedType)
                {
                    UtilitiesNav.SelectedItem = item;
                    _previousItem = item;
                    break;
                }
            }

            _isSyncingSelection = false;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Content = null;
            ContentFrame.Navigated -= ContentFrame_Navigated;
        }
    }
}