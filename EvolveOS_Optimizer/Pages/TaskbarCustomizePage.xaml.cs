// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class TaskbarCustomizePage : Page
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
