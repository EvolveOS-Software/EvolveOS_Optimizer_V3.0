// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Events;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Utilities.Extensions;

/// <summary>
/// Extension methods for registering setting services and dispatcher registries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dispatcher Registries:</b>
/// <see cref="ISpecialSettingHandlerRegistry"/> and <see cref="IActionCommandRegistry"/> act as 
/// ID-keyed dispatcher registries, directly mapping a <c>SettingId</c> to its specific execution handler.
/// </para>
/// <para>
/// <b>Discovery Registry Rules:</b>
/// Only handlers that explicitly override <c>DiscoverSpecialSettingsAsync</c> (i.e., those that self-filter 
/// and return raw system values) belong in the <see cref="ISpecialDiscoveryRegistry"/>.
/// </para>
/// <para>
/// <b>Declarative Application:</b>
/// Handlers like <see cref="ThemeWallpaperApplier"/> no longer manually manage shell restarts. Actions like 
/// restarting Explorer are now handled declaratively via <c>SettingDefinition.RestartProcess</c>.
/// </para>
/// </remarks>
public static class SettingServicesExtensions
{
    #region Main Registration
    public static IServiceCollection AddSettingServices(this IServiceCollection services)
    {
        services
            .AddCustomizationServices()
            .AddOptimizationServices();

        #region Dispatcher Registries
        services.AddSingleton<ISpecialSettingHandlerRegistry>(sp =>
            new SpecialSettingHandlerRegistry(new Dictionary<string, ISpecialSettingHandler>
            {
                [SettingIds.PowerPlanSelection] = sp.GetRequiredService<PowerService>(),
                [SettingIds.UpdatesPolicyMode] = sp.GetRequiredService<UpdateService>(),
                [SettingIds.ThemeModeWindows] = sp.GetRequiredService<ThemeWallpaperApplier>(),

                ["gaming-performance-mouse-sensitivity"] = sp.GetRequiredService<InputDeviceService>(),
                ["gaming-performance-keyboard-delay"] = sp.GetRequiredService<InputDeviceService>(),
                ["gaming-performance-keyboard-speed"] = sp.GetRequiredService<InputDeviceService>(),
            }));

        services.AddSingleton<IActionCommandRegistry>(sp =>
            new ActionCommandRegistry(new Dictionary<string, IActionCommandProvider>
            {
                [SettingIds.TaskbarClean] = sp.GetRequiredService<TaskbarService>(),
                [SettingIds.StartMenuCleanWin10] = sp.GetRequiredService<StartMenuService>(),
                [SettingIds.StartMenuCleanWin11] = sp.GetRequiredService<StartMenuService>(),

                ["gaming-performance-os-compression"] = sp.GetRequiredService<OSCompressionService>(),
            }));
        #endregion

        #region Discovery Registries
        services.AddSingleton<ISpecialDiscoveryRegistry>(sp =>
            new SpecialDiscoveryRegistry(new List<ISpecialSettingHandler>
            {
                sp.GetRequiredService<PowerService>(),
                sp.GetRequiredService<UpdateService>(),
            }));
        #endregion

        return services;
    }
    #endregion

    #region Customization Services
    public static IServiceCollection AddCustomizationServices(this IServiceCollection services)
    {
        #region Wallpaper & Theme Services
        services.AddSingleton<IWallpaperService, WallpaperService>();
        services.AddSingleton<ThemeWallpaperApplier>();
        #endregion

        #region Shell Customization Services
        services.AddSingleton<StartMenuService>();
        services.AddSingleton<TaskbarService>();
        #endregion

        return services;
    }
    #endregion

    #region Optimization Services
    public static IServiceCollection AddOptimizationServices(this IServiceCollection services)
    {
        #region Input Device Services
        services.AddSingleton<InputDeviceService>();
        #endregion

        #region Storage Services
        services.AddSingleton<OSCompressionService>();
        #endregion

        #region Power Management Services
        services.AddSingleton<PowerService>(sp => new PowerService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<IPowerSettingsQueryService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>(),
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<IPowerPlanComboBoxService>(),
            sp.GetRequiredService<IProcessExecutor>(),
            sp.GetRequiredService<IFileSystemService>(),
            sp.GetRequiredService<IPowerSchemeOperations>()
        ));

        services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());
        #endregion

        #region Windows Update Services
        services.AddSingleton<UpdateService>();
        #endregion

        return services;
    }
    #endregion
}