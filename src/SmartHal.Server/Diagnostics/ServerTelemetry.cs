using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SmartHal.Server.Diagnostics;

/// <summary>
/// Provides the shared tracing and metrics sources of the server.
/// </summary>
public static class ServerTelemetry
{
    /// <summary>
    /// The name both primitives carry, taken from the assembly this type lives in.
    /// </summary>
    private static readonly string TelemetryName =
        typeof(ServerTelemetry).Assembly.GetName().Name ?? nameof(SmartHal);

    /// <summary>
    /// Gets the activity source all traces of the server start from.
    /// </summary>
    /// <value>A source named after the assembly.</value>
    public static ActivitySource ActivitySource { get; } = new ActivitySource(TelemetryName);

    /// <summary>
    /// Gets the meter all instruments of the server are created on.
    /// </summary>
    /// <value>A meter named after the assembly.</value>
    public static Meter Meter { get; } = new Meter(TelemetryName);
}
