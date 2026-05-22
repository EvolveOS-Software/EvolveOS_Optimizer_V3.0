// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ServicesPage : Page, IPurgeable
    {
        private ServicesTweaks? _svcTweaks = new ServicesTweaks();

        public ServicesPage()
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

            this.Loaded += ServicesPage_Loaded;
            this.Unloaded += ServicesPage_Unloaded;
        }

        private void ServicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            //DebugAvailableCards();

            var vm = new ServicesViewModel();
            this.DataContext = vm;

            vm.UpdateCounters();
            vm.ApplyRecommendations();
        }

        private void ServicesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }

        private void NativeTgl_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch tgl || !tgl.IsLoaded) return;

            var card = UIHelper.FindParent<Border>(tgl);
            if (card == null) return;

            string key = card.Tag?.ToString() ?? string.Empty;

            if (this.DataContext is ServicesViewModel vm)
            {
                var model = vm[key];
                if (model == null) return;

                if (model.State == tgl.IsOn) return;

                model.State = tgl.IsOn;
                vm.UpdateCounters();
                vm.ApplyRecommendations();

                _svcTweaks?.ApplyTweaks(key, tgl.IsOn);

                if (ExplorerManager.IntfMapping.TryGetValue(key, out bool needRestart) && needRestart)
                {
                    ExplorerManager.Restart();
                }
            }
        }

        private void DebugAvailableCards()
        {
            var allButtonKeys = Enumerable.Range(1, 40).Select(i => $"TglButton{i}").ToList();

            var existingCards = UIHelper.FindVisualChildren<ContentControl>(this)
                                .Where(c => c.Tag?.ToString()?.StartsWith("TglButton") == true)
                                .ToList();

            Debug.WriteLine("--- SERVICES PAGE DIAGNOSTICS ---");

            foreach (var key in allButtonKeys)
            {
                var card = existingCards.FirstOrDefault(c => string.Equals(c.Tag?.ToString(), key, StringComparison.Ordinal));

                if (card == null)
                {
                    Debug.WriteLine($"[MISSING] {key}: Card is not in the XAML at all.");
                }
                else if (card.Visibility == Visibility.Collapsed)
                {
                    Debug.WriteLine($"[HIDDEN] {key}: Card exists but is hidden by Win11/Build logic.");
                }
                else
                {
                    Debug.WriteLine($"[OK] {key}: Visible.");
                }
            }

            int visibleCount = existingCards.Count(c => c.Visibility == Visibility.Visible);
            Debug.WriteLine($"Total Cards Visible: {visibleCount}");
            Debug.WriteLine("----------------------------------");
        }

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and ViewModel...");

                this.Unloaded -= ServicesPage_Unloaded;

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
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
            }

            return Task.CompletedTask;
        }
        #endregion
    }
}