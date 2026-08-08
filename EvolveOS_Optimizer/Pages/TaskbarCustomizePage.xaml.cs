// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class TaskbarCustomizePage : Page, IPurgeable
{
    public CustomizeViewModel ViewModel { get; }

    public TaskbarCustomizePage()
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

        ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string searchText && !string.IsNullOrWhiteSpace(searchText))
        {
            ViewModel.SearchText = searchText;
        }

        _ = ViewModel.TaskbarViewModel.RefreshSettingStatesAsync();
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

        return Task.CompletedTask;
    }
    #endregion
}
