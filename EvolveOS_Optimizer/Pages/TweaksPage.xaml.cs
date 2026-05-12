// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Pages
{
    public partial class TweaksPage : Page, IPurgeable
    {
        private DateTime _lastNavTime = DateTime.MinValue;

        private NavigationViewItem? _previousItem;
        private bool _isSyncingSelection = false;

        public TweaksPage()
        {
            InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            ContentFrame.Navigated += ContentFrame_Navigated;
            this.Unloaded += Page_Unloaded;
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

            if ((DateTime.Now - _lastNavTime).TotalMilliseconds < 300) return;
            _lastNavTime = DateTime.Now;

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
            Purge();
        }

        #region Purge Page
        public void Purge()
        {
            Debug.WriteLine("[TweaksPage] Caching Purge requested. Broadcasting sleep signal...");

            if (ContentFrame?.Content is IPurgeable activeChildPage)
            {
                activeChildPage.Purge();
                Debug.WriteLine($"[TweaksPage] Sleep signal sent to child: {activeChildPage.GetType().Name}");
            }

            Debug.WriteLine("[TweaksPage] Host frame preserved in cache.");
        }
        #endregion
    }
}