// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class GamingOptimizePage : Page, IPurgeable
{
    public OptimizeViewModel ViewModel { get; }

    public GamingOptimizePage()
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

        ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string searchText && !string.IsNullOrWhiteSpace(searchText))
        {
            ViewModel.SearchText = searchText;
        }

        _ = ViewModel.GamingViewModel.RefreshSettingStatesAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            _ = Purge();
        }
    }

    #region Purge Page

    public Task Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI...");

            _ = Task.Run(async () =>
            {
                await Task.Delay(350);

                DispatcherQueue?.TryEnqueue(() =>
                {
                    this.Bindings?.StopTracking();
                    this.DataContext = null;
                    this.Content = null;
                });

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