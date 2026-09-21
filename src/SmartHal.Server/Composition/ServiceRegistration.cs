using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartHal.Server.Configuration;
using SmartHal.Server.Health;
using SmartHal.Server.Hosting;

namespace SmartHal.Server.Composition;

/// <summary>
/// The service registrations of the composition root; step 3 of the start sequence of section 6.5
/// calls them (FR-22).
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers <see cref="SmartHalOptions"/> so that the section is bound and validated at start.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="configuration">The configuration the section is read from.</param>
    /// <returns><paramref name="services"/>, so further registrations can be chained.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// Validation runs through two validators. The attributes on the options type are checked by
    /// <c>ValidateDataAnnotations</c>, everything they cannot express by
    /// <see cref="SmartHalOptionsValidator"/>; both report into the same result, so a single abort
    /// names every violation (FR-25). <c>ValidateOnStart</c> moves that check in front of the hosted
    /// services instead of leaving it to the first access (FR-22, FR-23).
    /// </remarks>
    public static IServiceCollection AddSmartHalOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // BindConfiguration takes the same configuration out of the container rather than capturing
        // the instance above, so the binding follows a reload of the sources.
        services.AddOptions<SmartHalOptions>()
            .BindConfiguration(SmartHalOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SmartHalOptions>, SmartHalOptionsValidator>();

        return services;
    }

    /// <summary>
    /// Registers the readiness signal: the health checks, their schedule and the publisher that
    /// writes every state change to the log.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <returns><paramref name="services"/>, so further registrations can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Every check carries both tags of <see cref="HealthTags"/> (FR-39); the readiness state of
    /// FR-40 is formed from the <c>ready</c> ones alone, which is what the predicate below selects.
    /// </para>
    /// <para>
    /// The delay and the period are this plan's decision rather than the spec's (G-8): the first
    /// report follows half a second after the host has started, so a start reaches its ready state
    /// without a noticeable wait, and the half minute afterwards keeps a healthy process quiet.
    /// </para>
    /// <para>
    /// The package behind this is the host-neutral one. FR-41 forbids a transport, so nothing here
    /// opens an endpoint, a socket or a file: the state is readable in process through
    /// <c>HealthCheckService</c> and from outside through the log alone.
    /// </para>
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
    /// Registers the services of the orderly shutdown: the handler of the shutdown signals and the
    /// span that bounds the shutdown of the host.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <returns><paramref name="services"/>, so further registrations can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The handler ends the process through <see cref="Environment.Exit(int)"/> when a second
    /// interrupt aborts the shutdown (FR-15, G-7). That call is an argument rather than a line inside
    /// the handler, so a test can drive the same rule in process; the host fixture of
    /// <c>SmartHal.IntegrationTests</c> replaces this registration with one that records the exit code
    /// instead (C-4).
    /// </remarks>
    public static IServiceCollection AddSmartHalHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IConfigureOptions<HostOptions>, ShutdownTimeoutConfigurator>();

        services.AddSingleton(serviceProvider => new ShutdownSignalHandler(
            serviceProvider.GetRequiredService<IHostApplicationLifetime>(),
            serviceProvider.GetRequiredService<ILogger<ShutdownSignalHandler>>(),
            Environment.Exit));

        return services;
    }
}
