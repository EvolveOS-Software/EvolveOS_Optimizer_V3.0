// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.EventHandlers;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EvolveOS_Optimizer.Utilities.Extensions;

/// <summary>
/// Configures and registers core infrastructure services for the EvolveOS Optimizer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Architectural Note on Dependency Injection Overrides:</b>
/// This method uses <c>TryAddSingleton</c> to provide safe, empty fallback implementations for the dispatcher registries:
/// <see cref="ISpecialDiscoveryRegistry"/>, <see cref="ISpecialSettingHandlerRegistry"/>, and <see cref="IActionCommandRegistry"/>.
/// </para>
/// <para>
/// <b>Testing Scenario:</b> By registering these empty defaults, the infrastructure container remains 
/// completely decoupled and self-contained, allowing isolated integration or smoke tests to run without crashing.
/// </para>
/// <para>
/// <b>Runtime Scenario:</b> During normal application startup, the UI composition root (<c>AddSettingServices</c>) 
/// re-registers these exact registries with actual functional handlers (like <c>PowerService</c> or <c>UpdateService</c>). 
/// Because the UI root runs later, its specialized registrations cleanly override these infrastructure defaults.
/// </para>
/// </remarks>
public static class InfrastructureServicesExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        #region Core Infrastructure Services & Dependency Manager
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<ILogService, EvolveOS_Optimizer.Utilities.Services.LogService>();
        services.AddSingleton<IInteractiveUserService, InteractiveUserService>();
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();
        services.AddSingleton<IDependencyManager, DependencyManager>();
        #endregion

        #region Windows Services
        services.AddSingleton<IWindowsVersionService, WindowsVersionService>();
        services.AddSingleton<IWindowsUIManagementService, WindowsUIManagementService>();
        #endregion

        #region User Preferences Service
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
        #endregion

        #region New Badge Service
        services.AddSingleton<INewBadgeService, NewBadgeService>();
        #endregion

        #region Localization Service
        services.AddSingleton<ILocalizationService, LocalizationService>();
        #endregion

        #region Event Bus
        services.AddSingleton<IEventBus, EventBus>();
        #endregion

        #region Initialization Service
        services.AddSingleton<IInitializationService, InitializationService>();
        #endregion

        #region Settings Registry
        services.AddSingleton<IGlobalSettingsRegistry, GlobalSettingsRegistry>();
        #endregion

        #region Global Settings Preloader
        services.AddSingleton<IGlobalSettingsPreloader, GlobalSettingsPreloader>();
        #endregion

        #region File System Service
        services.AddSingleton<IFileSystemService, FileSystemService>();
        #endregion

        #region Power Scheme Operations
        services.AddSingleton<IPowerSchemeOperations, PowerSchemeOperations>();
        #endregion

        #region System Parameters
        services.AddSingleton<ISystemParametersService, SystemParametersService>();
        #endregion

        #region PowerShell Runner
        services.AddSingleton<IPowerShellRunner, PowerShellRunner>();
        #endregion

        #region Settings Discovery and Application
        services.TryAddSingleton<ISpecialDiscoveryRegistry>(_ =>
            new SpecialDiscoveryRegistry([]));
        services.TryAddSingleton<ISpecialSettingHandlerRegistry>(_ =>
            new SpecialSettingHandlerRegistry(new Dictionary<string, ISpecialSettingHandler>()));
        services.TryAddSingleton<IActionCommandRegistry>(_ =>
            new ActionCommandRegistry(new Dictionary<string, IActionCommandProvider>()));
        services.AddSingleton<ISystemSettingsDiscoveryService, SystemSettingsDiscoveryService>();
        services.AddSingleton<IProcessRestartManager, ProcessRestartManager>();
        services.AddSingleton<IPowerCfgApplier, PowerCfgApplier>();
        services.AddSingleton<ISettingDependencyResolver, SettingDependencyResolver>();
        services.AddSingleton<IRecommendedSettingsApplier, RecommendedSettingsApplier>();
        services.AddSingleton<IBulkSettingsActionService, BulkSettingsActionService>();
        services.AddSingleton<ISettingOperationExecutor, SettingOperationExecutor>();
        services.AddSingleton<ISettingApplicationService, SettingApplicationService>();
        #endregion

        #region ComboBox Services
        services.AddSingleton<IComboBoxSetupService, ComboBoxSetupService>();
        services.AddSingleton<IComboBoxResolver, ComboBoxResolver>();
        services.AddSingleton<IPowerPlanComboBoxService, PowerPlanComboBoxService>();
        #endregion

        #region Settings Compatibility
        services.AddSingleton<ICompatibleSettingsRegistry, CompatibleSettingsRegistry>();
        services.AddSingleton<IWindowsCompatibilityFilter, WindowsCompatibilityFilter>();
        services.AddSingleton<IHardwareCompatibilityFilter, HardwareCompatibilityFilter>();
        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();
        #endregion

        #region Script Services
        services.AddSingleton<IPowerSettingsQueryService, PowerSettingsQueryService>();
        services.AddSingleton<IPowerSettingsValidationService, PowerSettingsValidationService>();
        #endregion

        #region System Services
        services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
        services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        #endregion

        #region Task Progress Service
        services.AddSingleton<TaskProgressService>();
        services.AddSingleton<ITaskProgressService>(sp => sp.GetRequiredService<TaskProgressService>());
        services.AddSingleton<IMultiScriptProgressService>(sp => sp.GetRequiredService<TaskProgressService>());
        #endregion

        #region Tooltip Services
        services.AddSingleton<ITooltipDataService, TooltipDataService>();
        services.AddSingleton<TooltipRefreshEventHandler>();
        #endregion

        #region Recommended Settings Service
        services.AddSingleton<IRecommendedSettingsService>(provider =>
            new RecommendedSettingsService(
                provider.GetRequiredService<ICompatibleSettingsRegistry>(),
                provider.GetRequiredService<IWindowsVersionService>(),
                provider.GetRequiredService<ILogService>()));
        #endregion

        #region Http Client
        services.TryAddSingleton<System.Net.Http.HttpClient>();
        #endregion

        return services;
    }
}