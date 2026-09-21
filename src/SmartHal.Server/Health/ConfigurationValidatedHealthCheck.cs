using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SmartHal.Server.Configuration;

namespace SmartHal.Server.Health;

/// <summary>
/// Reports whether the <c>SmartHal</c> section is bound and validated (FR-43).
/// </summary>
/// <remarks>
/// The check asks the options infrastructure for the bound value. That value only exists once
/// binding and every validator have passed, so delivering it without an exception is exactly the
/// statement the check makes. It is the one readiness check slice 0 owns; later slices add their
/// own next to it.
/// </remarks>
public sealed class ConfigurationValidatedHealthCheck : IHealthCheck
{
    /// <summary>
    /// The name this check is registered under.
    /// </summary>
    public const string CheckName = "configuration";

    private readonly IOptions<SmartHalOptions> _options;

    /// <summary>
    /// Initialises a new instance of the <see cref="ConfigurationValidatedHealthCheck"/> class.
    /// </summary>
    /// <param name="options">The bound options of the <c>SmartHal</c> section.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ConfigurationValidatedHealthCheck(IOptions<SmartHalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <summary>
    /// Checks whether the bound configuration can be delivered.
    /// </summary>
    /// <param name="context">The registration this check runs for.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>
    /// A healthy result naming the configured instance, or an unhealthy one carrying the validation
    /// error that keeps the configuration from being delivered.
    /// </returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var options = _options.Value;

            // The instance name is a name the operator chose, never a secret (NFR-4), and it is the
            // one field that tells two running instances apart in the log.
            return Task.FromResult(HealthCheckResult.Healthy(
                $"The configuration of instance '{options.InstanceName}' is bound and valid."));
        }
        catch (OptionsValidationException exception)
        {
            // The message of the exception carries the broken rules, which never contain a configured
            // value (G-9), so it is safe to report here.
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "The configuration is not valid.",
                exception));
        }
    }
}
