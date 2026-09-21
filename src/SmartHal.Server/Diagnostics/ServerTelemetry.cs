using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SmartHal.Server.Diagnostics;

/// <summary>
/// The telemetry primitives of <c>SmartHal.Server</c>: the <see cref="ActivitySource"/> every trace
/// of this assembly starts from and the <see cref="Meter"/> every instrument is created on (FR-37,
/// FR-38).
/// </summary>
/// <remarks>
/// Both carry the assembly name, so a listener can subscribe to the assembly that produced the
/// telemetry. Nothing here wires an OpenTelemetry SDK or an exporter; slice 0 provides the built-in
/// primitives and nothing more (FR-38).
/// </remarks>
public static class ServerTelemetry
{
    /// <summary>
    /// The name both primitives carry, taken from the assembly this type lives in (FR-37).
    /// </summary>
    private static readonly string TelemetryName =
        typeof(ServerTelemetry).Assembly.GetName().Name ?? nameof(SmartHal);

    /// <summary>
    /// Gets the activity source of this assembly.
    /// </summary>
    /// <value>
    /// A source named after the assembly, for example <c>SmartHal.Server</c>. Correlation across
    /// component boundaries runs through the <see cref="Activity"/> it starts, using W3C
    /// TraceContext; there is no separate correlation identifier (FR-36).
    /// </value>
    public static ActivitySource ActivitySource { get; } = new(TelemetryName);

    /// <summary>
    /// Gets the meter of this assembly.
    /// </summary>
    /// <value>A meter named after the assembly, for example <c>SmartHal.Server</c>.</value>
    public static Meter Meter { get; } = new(TelemetryName);
}
