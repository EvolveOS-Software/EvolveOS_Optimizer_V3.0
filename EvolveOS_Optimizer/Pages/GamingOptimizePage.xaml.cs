// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class GamingOptimizePage : Page
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
            Purge();
        }
    }

    #region Purge Page
    private void Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        Bindings.StopTracking();

        this.Content = null;
    }
    #endregion
}
