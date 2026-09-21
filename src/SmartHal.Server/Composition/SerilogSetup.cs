using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace SmartHal.Server.Composition;

/// <summary>
/// Puts Serilog behind <c>ILogger&lt;T&gt;</c> and builds it entirely from the <c>Serilog</c> section
/// of the configuration (FR-28, FR-29, FR-30, FR-31, FR-32).
/// </summary>
/// <remarks>
/// <para>
/// This is the only type of the solution that names a Serilog type; every other type - production
/// code and tests alike - logs through <c>ILogger&lt;T&gt;</c> (FR-27, FR-60, C-6). The architecture
/// test <c>SerilogOnlyInCompositionRoot</c> enforces that boundary.
/// </para>
/// <para>
/// The sinks, their formatters, the minimum level and every per-component override come from the
/// configuration, never from code. Which formatter the console uses is therefore a matter of
/// <c>appsettings.Development.json</c> - readable text there, compact JSON everywhere else (G-18,
/// NFR-6).
/// </para>
/// </remarks>
public static class SerilogSetup
{
    /// <summary>
    /// Registers Serilog as the logging provider of <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The host builder the logger is attached to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <c>AddSerilog</c> is the counterpart of <c>UseSerilog</c> for
    /// <see cref="HostApplicationBuilder"/>: the latter extends <c>IHostBuilder</c>, which this
    /// process does not use (FR-28).
    /// </para>
    /// <para>
    /// The logger is built lazily, when the host resolves its logger factory, so every configuration
    /// source - including those a test appends after this call - is already in place.
    /// </para>
    /// </remarks>
    public static void Configure(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The generic host installs a console, a debug and an event source provider by default. They
        // would write a second, unstructured copy of every event next to Serilog's own console sink
        // and break the promise that every console line outside Development is JSON (NFR-6).
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog(
            (services, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services),
            writeToProviders: true);
    }
}
