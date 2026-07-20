// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Pages;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Utilities.Extensions;

/// <summary>
/// Extension methods for registering UI-specific services for the EvolveOS Optimizer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Late Initialization Constraints:</b>
/// <list type="bullet">
/// <item><see cref="IDispatcherService"/> requires late initialization in <c>MainWindow.xaml.cs</c> after the window is created.</item>
/// <item><see cref="IDialogService"/> requires <c>XamlRoot</c> to be explicitly set by <c>MainWindow</c> after the UI content has loaded.</item>
/// </list>
/// </para>
/// <para>
/// <b>ViewModel Dependency Injection:</b>
/// Both Optimize and Customize ViewModels are registered as Singletons to preserve state during inner-page navigation. 
/// Child ViewModels are registered using their specific interfaces (<see cref="IOptimizationFeatureViewModel"/> and <see cref="ICustomizationFeatureViewModel"/>) 
/// so their parent ViewModels (<c>OptimizeViewModel</c> and <c>CustomizeViewModel</c>) can cleanly receive them via <c>IEnumerable&lt;T&gt;</c> injection.
/// </para>
/// </remarks>
public static class UIServicesExtensions
{
    public static IServiceCollection AddUIServices(this IServiceCollection services)
    {
        #region Dispatcher & Dialog Services
        services.AddSingleton<IDispatcherService, DispatcherService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMainWindowProvider, MainWindowProvider>();
        #endregion

        #region Startup Orchestrator
        services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();
        #endregion

        #region Regedit Launcher
        services.AddSingleton<IRegeditLauncher, RegeditLauncher>();
        #endregion

        #region Profile Builder Services
        services.AddTransient<ProfileBuilderViewModel>();
        services.AddTransient<ProfileBuilderPage>();
        #endregion

        #region Setting ViewModels & Infrastructure
        services.AddSingleton<ISettingLocalizationService, SettingLocalizationService>();
        services.AddSingleton<SettingViewModelDependencies>();
        services.AddSingleton<ISettingViewModelEnricher, SettingViewModelEnricher>();
        services.AddSingleton<ISettingViewModelFactory, SettingViewModelFactory>();
        services.AddSingleton<ISettingPreparationPipeline, SettingPreparationPipeline>();
        services.AddSingleton<ISettingsLoadingService, SettingsLoadingService>();
        #endregion

        #region Optimize ViewModels
        services.AddSingleton<OptimizeViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, SoundOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, UpdateOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, NotificationOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, PrivacyOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, PowerOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, GamingOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, AdvancedOptimizationsViewModel>();
        #endregion

        #region Customize ViewModels
        services.AddSingleton<CustomizeViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, ExplorerCustomizationsViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, StartMenuCustomizationsViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, TaskbarCustomizationsViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, WindowsThemeCustomizationsViewModel>();
        #endregion

        return services;
    }
}