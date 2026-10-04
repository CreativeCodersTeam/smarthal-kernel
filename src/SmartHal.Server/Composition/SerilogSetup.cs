using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace SmartHal.Server.Composition;

/// <summary>
/// Sets up Serilog as the logging provider of the server from the <c>Serilog</c> configuration section.
/// </summary>
/// <remarks>
/// This is the only type that may reference Serilog; everything else logs through <c>ILogger&lt;T&gt;</c>.
/// </remarks>
public static class SerilogSetup
{
    /// <summary>
    /// Registers Serilog as the logging provider of the host.
    /// </summary>
    /// <param name="builder">The host builder the logger is attached to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
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
