// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using Microsoft.UI.Xaml.Navigation;

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
                    if (ContentFrame.Content is IPurgeable oldGhostTab)
                    {
                        oldGhostTab.Purge();
                        Debug.WriteLine($"[TweaksPage] Purged ghost tab: {oldGhostTab.GetType().Name}");
                    }

                    ContentFrame.Content = null;

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
            Debug.WriteLine("[TweaksPage] Purge initiated...");

            ContentFrame.Navigated -= ContentFrame_Navigated;
            this.Unloaded -= Page_Unloaded;

            if (ContentFrame?.Content is IPurgeable activeChildPage)
            {
                activeChildPage.Purge();
                Debug.WriteLine($"[TweaksPage] Cascaded Purge to child: {activeChildPage.GetType().Name}");
            }

            if (ContentFrame != null)
            {
                ContentFrame.Content = null;
                ContentFrame.BackStack.Clear();
                ContentFrame.ForwardStack.Clear();
            }

            TweaksNav.SelectedItem = null;
            _previousItem = null;
            this.Content = null;

            Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Debug.WriteLine($"[MemoryGuardian] Aggressive background GC completed for {this.GetType().Name}.");
            });

            Debug.WriteLine("[TweaksPage] Purge Complete.");
        }
        #endregion
    }
}