using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmartHal.Server.Configuration;

namespace SmartHal.Server.Hosting;

/// <summary>
/// Applies <c>SmartHal:ShutdownTimeout</c> to the host, so the host bounds its shutdown with the configured span.
/// </summary>
/// <remarks>
/// An invalid configuration leaves the default timeout in place, so the later options validation
/// can end the start with the dedicated exit code.
/// </remarks>
public sealed class ShutdownTimeoutConfigurator : IConfigureOptions<HostOptions>
{
    private readonly IOptions<SmartHalOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShutdownTimeoutConfigurator"/> class.
    /// </summary>
    /// <param name="options">The options of the <c>SmartHal</c> section.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ShutdownTimeoutConfigurator(IOptions<SmartHalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public void Configure(HostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            options.ShutdownTimeout = _options.Value.ShutdownTimeout;
        }
        // An invalid configuration is reported by step 4 of the start sequence, see the remarks above.
        catch (OptionsValidationException)
        {
            // Ignored on purpose, see above.
        }
    }
}
