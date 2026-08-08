// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class NotificationOptimizePage : Page
{
    public OptimizeViewModel ViewModel { get; }

    public NotificationOptimizePage()
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

        _ = ViewModel.NotificationViewModel.RefreshSettingStatesAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            Purge();
        }
    }

    #region Purge Page
    private void Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        _ = Task.Run(async () =>
        {
            await Task.Delay(350);

            DispatcherQueue?.TryEnqueue(() =>
            {
                this.Bindings?.StopTracking();
                this.Content = null;
            });

            DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
        });
    }
    #endregion
}
