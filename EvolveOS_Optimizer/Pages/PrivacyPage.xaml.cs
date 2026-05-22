// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class PrivacyPage : Page, IPurgeable
    {
        private PrivacyTweaks? _confTweaks = new PrivacyTweaks();

        public PrivacyPage()
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

            if (!WindowsLicense.IsWindowsActivated)
            {
                NotificationManager.Show("info", "warn_activate_noty").Perform();
            }

            this.Loaded += PrivacyPage_loaded;
            this.Unloaded += PrivacyPage_Unloaded;
        }

        private void PrivacyPage_loaded(object sender, RoutedEventArgs e)
        {
            //DebugAvailableCards();

            var vm = new PrivacyViewModel();
            this.DataContext = vm;

            vm.UpdateCounters();
            vm.ApplyRecommendations();
        }

        private void PrivacyPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }

        private void NativeTgl_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch tgl)
            {
                if (!tgl.IsLoaded || tgl.FocusState == FocusState.Unfocused) return;

                var card = UIHelper.FindParent<Border>(tgl);
                if (card != null)
                {
                    string key = card.Tag?.ToString() ?? string.Empty;
                    bool isOn = tgl.IsOn;

                    if (this.DataContext is PrivacyViewModel vm)
                    {
                        var model = vm[key];
                        if (model != null)
                        {
                            model.State = isOn;
                            vm.UpdateCounters();
                            vm.ApplyRecommendations();
                        }
                    }

                    _confTweaks?.ApplyTweaks(key, isOn);
                }
            }
        }

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and ViewModel...");

                this.Unloaded -= PrivacyPage_Unloaded;

                this.DataContext = null;
                this.Content = null;
                //this.Bindings?.StopTracking();

                _ = Task.Run(() =>
                {
                    DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup();
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