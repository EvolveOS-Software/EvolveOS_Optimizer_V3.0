// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Navigation;

namespace EvolveOS_Optimizer.Pages
{
    public partial class TweaksPage : Page
    {
        private NavigationViewItem? _previousItem;
        private bool _isSyncingSelection = false;

        public TweaksPage()
        {
            InitializeComponent();
            ContentFrame.Navigated += ContentFrame_Navigated;
        }

        private void TweaksNav_Loaded(object sender, RoutedEventArgs e)
        {
            if (TweaksNav.MenuItems.Count > 0 && TweaksNav.MenuItems[0] is NavigationViewItem firstItem)
            {
                TweaksNav.SelectedItem = firstItem;
                _previousItem = firstItem;
            }
        }

        private void TweaksNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_isSyncingSelection) return;

            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string? tag = selectedItem.Tag?.ToString();
                Type pageType = tag switch
                {
                    "Confidentiality" => typeof(PrivacyPage),
                    "Interface" => typeof(InterfacePage),
                    "ServiceTweaks" => typeof(ServicesPage),
                    "System" => typeof(SystemPage),
                    _ => typeof(PrivacyPage)
                };

                if (ContentFrame.CurrentSourcePageType != pageType)
                {
                    ContentFrame.Navigate(pageType);

                    if (ContentFrame.CurrentSourcePageType != pageType)
                    {
                        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                        {
                            _isSyncingSelection = true;

                            TweaksNav.SelectedItem = null;
                            TweaksNav.UpdateLayout();
                            TweaksNav.SelectedItem = _previousItem;

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

            foreach (var item in TweaksNav.MenuItems.OfType<NavigationViewItem>())
            {
                string? tag = item.Tag?.ToString();
                Type expectedType = tag switch
                {
                    "Confidentiality" => typeof(PrivacyPage),
                    "Interface" => typeof(InterfacePage),
                    "ServiceTweaks" => typeof(ServicesPage),
                    "System" => typeof(SystemPage),
                    _ => typeof(PrivacyPage)
                };

                if (e.SourcePageType == expectedType)
                {
                    TweaksNav.SelectedItem = item;
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