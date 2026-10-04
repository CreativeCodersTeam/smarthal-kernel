using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SmartHal.Server.Configuration;

namespace SmartHal.Server.Health;

/// <summary>
/// Reports the server as ready only when the <c>SmartHal</c> configuration is bound and valid.
/// </summary>
public sealed class ConfigurationValidatedHealthCheck : IHealthCheck
{
    /// <summary>
    /// The name this check is registered under.
    /// </summary>
    public const string CheckName = "configuration";

    private readonly IOptions<SmartHalOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidatedHealthCheck"/> class.
    /// </summary>
    /// <param name="options">The options of the <c>SmartHal</c> section.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ConfigurationValidatedHealthCheck(IOptions<SmartHalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <inheritdoc/>
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
