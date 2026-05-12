// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SystemManagerPage : Page
    {
        public static Action<string>? ExternalPaneRequest;
        public static string RequestedPaneOnLoad { get; set; } = "";

        private DateTime _lastNavTime = DateTime.MinValue;

        public SystemManagerPage()
        {
            this.InitializeComponent();
            this.Loaded += SystemManagerPage_Loaded;
            this.Unloaded += SystemManagerPage_Unloaded;
        }

        private void SystemManagerPage_Loaded(object sender, RoutedEventArgs e)
        {
            MainWinViewModel.AppRestored += OnAppRestored;

            ExternalPaneRequest = (requestedPane) =>
            {
                var targetItem = SystemNav.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(item => item.Tag?.ToString() == requestedPane);

                if (targetItem != null)
                {
                    SystemNav.SelectedItem = targetItem;
                }
            };

            if (!string.IsNullOrEmpty(RequestedPaneOnLoad))
            {
                ExternalPaneRequest?.Invoke(RequestedPaneOnLoad);
                RequestedPaneOnLoad = "";
            }
            else if (SystemNav.MenuItems.Count > 0 && SystemNav.SelectedItem == null)
            {
                SystemNav.SelectedItem = SystemNav.MenuItems[0];
            }
        }

        private void SystemManagerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWinViewModel.AppRestored -= OnAppRestored;

            ExternalPaneRequest = null;

            if (ContentFrame.Content is IPurgeable purgeablePage)
            {
                purgeablePage.Purge();
            }

            ContentFrame.Content = null;
        }

        private void OnAppRestored()
        {
            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);
            Debug.WriteLine("[SystemManager] App Restored: High Performance re-asserted by SystemManager host.");
        }

        private void SystemNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if ((DateTime.Now - _lastNavTime).TotalMilliseconds < 300) return;
            _lastNavTime = DateTime.Now;

            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string? selectedTag = selectedItem.Tag?.ToString();
                if (string.IsNullOrEmpty(selectedTag)) return;

                if (ContentFrame.Content?.GetType().Name == selectedTag) return;

                if (MainWindow.Instance?.RootGrid?.DataContext is MainWinViewModel mainVm)
                {
                    EfficiencyModeHelper.IsUIWakeLockActive = true;
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);
                    Debug.WriteLine($"[SystemManager] Tab switched to {selectedTag}. High Performance maintained.");
                }

                if (ContentFrame.Content is IPurgeable oldGhostTab)
                {
                    oldGhostTab.Purge();
                    Debug.WriteLine($"[SystemManager] Purged ghost tab: {oldGhostTab.GetType().Name}");
                }

                ContentFrame.Content = null;

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