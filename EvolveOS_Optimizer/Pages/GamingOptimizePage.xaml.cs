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
    private int _searchRevision = 0;

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

        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        if (e.Parameter is string searchText && !string.IsNullOrWhiteSpace(searchText))
        {
            ViewModel.SearchText = searchText;
            ForceScrollToSearchedItem(searchText);
        }
        else if (!string.IsNullOrWhiteSpace(ViewModel.SearchText))
        {
            ForceScrollToSearchedItem(ViewModel.SearchText);
        }

        _ = ViewModel.GamingViewModel.RefreshSettingStatesAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SearchText))
        {
            string text = ViewModel.SearchText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                ForceScrollToSearchedItem(text);
            }
        }
    }

    private async void ForceScrollToSearchedItem(string searchText)
    {
        int currentRevision = ++_searchRevision;

        await Task.Delay(250);
        if (currentRevision != _searchRevision) return;

        var targetElement = FindElementBySettingName(this.Content, searchText);
        if (targetElement != null)
        {
            targetElement.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true,
                VerticalAlignmentRatio = 0.0f,
                VerticalOffset = -40
            });
        }
    }

    private FrameworkElement? FindElementBySettingName(DependencyObject parent, string searchText)
    {
        if (parent == null) return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element)
            {
                if (element.DataContext != null)
                {
                    var nameProp = element.DataContext.GetType().GetProperty("Name");
                    if (nameProp != null)
                    {
                        var nameValue = nameProp.GetValue(element.DataContext) as string;
                        if (!string.IsNullOrEmpty(nameValue) && nameValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            return element;
                        }
                    }
                }

                var found = FindElementBySettingName(element, searchText);
                if (found != null) return found;
            }
        }
        return null;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

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