// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Pages
{
    public partial class UtilitiesPage : Page, IPurgeable
    {
        private NavigationViewItem? _previousItem;
        private bool _isSyncingSelection = false;

        public UtilitiesPage()
        {
            InitializeComponent();

            // BLUEPRINT: Adjustable Cache Mode
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

        private void UtilitiesNav_Loaded(object sender, RoutedEventArgs e)
        {
            if (UtilitiesNav.MenuItems.Count > 0 && UtilitiesNav.SelectedItem == null)
            {
                var firstItem = UtilitiesNav.MenuItems[0] as NavigationViewItem;
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
                    "AdvancedUtilsPage" => typeof(AdvancedUtilsPage),
                    //"WinBuilderPage" => typeof(WinBuilderPage),
                    _ => typeof(AdvancedUtilsPage)
                };

                if (ContentFrame.CurrentSourcePageType != pageType)
                {
                    ContentFrame.Navigate(pageType);

                    if (ContentFrame.CurrentSourcePageType != pageType)
                    {
                        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                        {
                            _isSyncingSelection = true;
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
                    "AdvancedUtilsPage" => typeof(AdvancedUtilsPage),
                    //"WinBuilderPage" => typeof(WinBuilderPage),
                    _ => typeof(AdvancedUtilsPage)
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
            Purge();
        }

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine("[UtilitiesPage] Caching Purge requested. Broadcasting sleep signal...");

            if (ContentFrame.Content is IPurgeable purgeablePage)
            {
                purgeablePage.Purge();
            }

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking Host Frame...");

                _previousItem = null;

                if (ContentFrame != null) ContentFrame.Navigated -= ContentFrame_Navigated;
                this.Unloaded -= Page_Unloaded;

                if (ContentFrame != null) ContentFrame.Content = null;

                this.DataContext = null;
                this.Content = null;
                //this.Bindings?.StopTracking();

                _ = Task.Run(() =>
                {
                    DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
                });
            }
            else
            {
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: Host frame preserved in RAM cache.");
            }

            return Task.CompletedTask;
        }
        #endregion
    }
}