// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EvolveOS_Optimizer.Utilities.Extensions;

/// <summary>
/// Acts as the central composition root for the EvolveOS Optimizer application.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separation of Concerns:</b>
/// This class delegates the actual service registrations to specialized extension methods 
/// (<c>AddInfrastructureServices</c>, <c>AddSettingServices</c>, and <c>AddUIServices</c>). 
/// This keeps the main dependency injection container clean, highly modular, and easy to read.
/// </para>
/// </remarks>
public static class CompositionRoot
{
    #region Service Configuration

    /// <summary>
    /// Aggregates all application-specific service registrations into a single call.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The modified service collection for method chaining.</returns>
    public static IServiceCollection ConfigureEvolveServices(this IServiceCollection services)
    {
        services
            .AddInfrastructureServices() // Core systems, DI management, cross-cutting concerns
            .AddSettingServices()        // Domain logic for Customizations and Optimizations
            .AddUIServices();            // WinUI 3 view models and UI-thread bound services

        return services;
    }

    #endregion

    #region Host Builder

    /// <summary>
    /// Creates and configures the default application host builder for the EvolveOS Optimizer.
    /// </summary>
    /// <returns>A fully configured <see cref="IHostBuilder"/> instance ready to be built.</returns>
    public static IHostBuilder CreateEvolveOSHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.ConfigureEvolveServices();
            });
    }

    #endregion
}