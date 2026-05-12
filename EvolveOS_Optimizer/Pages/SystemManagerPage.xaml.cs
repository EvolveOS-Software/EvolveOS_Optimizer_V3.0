// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SystemManagerPage : Page, IPurgeable
    {
        public static Action<string>? ExternalPaneRequest;
        public static string RequestedPaneOnLoad { get; set; } = "";

        private DateTime _lastNavTime = DateTime.MinValue;
        private NavigationViewItem? _previousItem;

        public SystemManagerPage()
        {
            this.InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

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
                _previousItem = SystemNav.MenuItems[0] as NavigationViewItem;
            }
        }

        private void SystemManagerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWinViewModel.AppRestored -= OnAppRestored;
            Purge();
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

                _previousItem = selectedItem;
            }
        }

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine("[SystemManagerPage] Caching Purge requested. Broadcasting sleep signal...");

            ExternalPaneRequest = null;

            if (ContentFrame.Content is IPurgeable purgeablePage)
            {
                purgeablePage.Purge();
            }

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking Host Frame...");

                _previousItem = null;

                MainWinViewModel.AppRestored -= OnAppRestored;
                this.Loaded -= SystemManagerPage_Loaded;
                this.Unloaded -= SystemManagerPage_Unloaded;

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