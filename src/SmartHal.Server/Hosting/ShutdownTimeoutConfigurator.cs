using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmartHal.Server.Configuration;

namespace SmartHal.Server.Hosting;

/// <summary>
/// Hands <c>SmartHal:ShutdownTimeout</c> to the host, so the generic host bounds its own shutdown
/// with the configured span (FR-16).
/// </summary>
/// <remarks>
/// <para>
/// The host cancels the token it stops its hosted services with once
/// <see cref="HostOptions.ShutdownTimeout"/> has passed (FR-14, AC-13). A hosted service that
/// ignores that token is bounded by the second limit of the same span in
/// <see cref="Composition.ServerHost.RunAsync"/>, which the host cannot enforce on its own.
/// </para>
/// <para>
/// The host reads <see cref="HostOptions"/> while it is being built, which is before step 4 of the
/// start sequence validates the <c>SmartHal</c> section. A configuration that breaks a rule has to
/// end the process through that step with exit code 2 (FR-23), so a validation failure here leaves
/// the default timeout in place instead of ending the build with a different error.
/// </para>
/// </remarks>
public sealed class ShutdownTimeoutConfigurator : IConfigureOptions<HostOptions>
{
    private readonly IOptions<SmartHalOptions> _options;

    /// <summary>
    /// Initialises a new instance of the <see cref="ShutdownTimeoutConfigurator"/> class.
    /// </summary>
    /// <param name="options">The bound options of the <c>SmartHal</c> section.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ShutdownTimeoutConfigurator(IOptions<SmartHalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <summary>
    /// Sets <see cref="HostOptions.ShutdownTimeout"/> to the configured span.
    /// </summary>
    /// <param name="options">The host options the value is written to.</param>
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
