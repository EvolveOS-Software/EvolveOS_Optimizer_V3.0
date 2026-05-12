// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ScriptsPage : Page, IPurgeable
    {
        private TimerControlManager? _timer = default;

        private ScriptsViewModel? _viewModel = new();
        public ScriptsViewModel ViewModel => _viewModel ?? new();

        public ScriptsPage()
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

            this.DataContext = ViewModel;

            UIHelper.RegisterPageTransition(RootGrid, this);

            ViewModel.OnScriptsUpdated += UpdateEmptyState;

            this.Unloaded += ScriptsPage_Unloaded;
            this.Loaded += ScriptsPage_Loaded;
        }

        private void ScriptsPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTimer();

            _timer?.Start();

            UpdateEmptyState();
            EmptyStateAnimation.Begin();
        }

        private void ScriptsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }

        private void InitializeTimer()
        {
            if (_timer != null) return;

            _timer = new TimerControlManager(TimeSpan.Zero, TimerControlManager.TimerMode.CountUp, time =>
            {
                if (_viewModel == null || this.DispatcherQueue == null) return;

                if ((int)time.TotalSeconds % 5 == 0)
                {
                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (_viewModel?.RefreshScriptsCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                        {
                            await asyncCmd.ExecuteAsync(null);
                        }
                    });
                }
            });
        }

        private void UpdateEmptyState()
        {
            if (this.DispatcherQueue == null || _viewModel == null) return;

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (_viewModel?.FilteredScripts != null && _viewModel.FilteredScripts.Count > 0)
                    VisualStateManager.GoToState(this, "HasScripts", true);
                else
                    VisualStateManager.GoToState(this, "NoScripts", true);
            });
        }

        private void FileCard_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (ViewModel.IsMultiSelectMode) return;

            if (sender is FrameworkElement element && element.DataContext is Core.Model.ScriptsModel script)
            {
                if (ViewModel.RunSingleScriptCommand.CanExecute(script))
                {
                    ViewModel.RunSingleScriptCommand.Execute(script);
                }
            }
        }

        #region Purge Page
        public void Purge()
        {
            Debug.WriteLine("[ScriptsPage] Caching Purge requested. Halting engines...");

            if (_timer != null)
            {
                _timer.Stop();
                Debug.WriteLine("[ScriptsPage] TimerControlManager paused.");
            }


            Debug.WriteLine("[ScriptsPage] Engines halted. UI and Script collection preserved in RAM.");
        }
        #endregion
    }
}