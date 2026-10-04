using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using SmartHal.Server.Configuration;
using SmartHal.Server.Health;
using SmartHal.Server.Hosting;

namespace SmartHal.Server.Composition;

/// <summary>
/// Provides the service registrations of the server.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers <see cref="SmartHalOptions"/>, bound to its section and validated at start.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="configuration">The configuration the section is read from.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IServiceCollection AddSmartHalOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // BindConfiguration takes the same configuration out of the container rather than capturing
        // the instance above, so the binding follows a reload of the sources.
        // PostConfigure runs between binding and validation, so the validator and every consumer see
        // the expanded path.
        services.AddOptions<SmartHalOptions>()
            .BindConfiguration(SmartHalOptions.SectionName)
            .PostConfigure(options => options.DataDirectory = PathExpansion.Expand(options.DataDirectory))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SmartHalOptions>, SmartHalOptionsValidator>();

        return services;
    }

    /// <summary>
    /// Registers the health checks that report the readiness of the server to the log.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// No endpoint is opened; the readiness state is only observable through the log.
    /// </remarks>
    public static IServiceCollection AddSmartHalHealth(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddCheck<ConfigurationValidatedHealthCheck>(
                ConfigurationValidatedHealthCheck.CheckName,
                tags: [HealthTags.Live, HealthTags.Ready]);

        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromMilliseconds(500);
            options.Period = TimeSpan.FromSeconds(30);
            options.Predicate = registration => registration.Tags.Contains(HealthTags.Ready);
        });

        services.AddSingleton<IHealthCheckPublisher, HealthStateMonitor>();

        return services;
    }

    /// <summary>
    /// Registers the hosting services: the orderly shutdown and the <see cref="SubSystemHostedService"/>.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddSmartHalHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IConfigureOptions<HostOptions>, ShutdownTimeoutConfigurator>();

        services.AddSingleton(serviceProvider => new ShutdownSignalHandler(
            serviceProvider.GetRequiredService<IHostApplicationLifetime>(),
            serviceProvider.GetRequiredService<ILogger<ShutdownSignalHandler>>(),
            Environment.Exit));

        services.AddHostedService<SubSystemHostedService>();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IContractValidator"/> unless one is already registered.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddSmartHalValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IContractValidator, ContractValidator>();

        return services;
    }
}
