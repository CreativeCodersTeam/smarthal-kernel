using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using static SmartHal.Core.Catalog.CatalogDefinitions;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the industrial (IIoT) capabilities of the core catalog.
/// </summary>
/// <remarks>
/// Measured quantities are typed specifically, each with one canonical unit, so that UI, automations and alarm rules
/// know what a value means without further information. Every property returns a new, independent instance on each
/// access.
/// </remarks>
public static class IiotCapabilities
{
    /// <summary>
    /// Gets <c>core.pressure@1</c>: a pressure in bar.
    /// </summary>
    /// <value>The capability type with <c>value</c> and the limit alarms <c>highLimit</c> and <c>lowLimit</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Pressure => Measured("core.pressure", "value", Units.Bar, Aggregation.Avg, 0.01);

    /// <summary>
    /// Gets <c>core.flow@1</c>: a volume flow in cubic metres per hour and the total volume.
    /// </summary>
    /// <value>
    /// The capability type with <c>value</c> and <c>totalVolume</c>, the command <c>resetTotal()</c>, and the limit
    /// alarms on <c>value</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Flow => Capability(
        "core.flow",
        Map(
            ("value", State(new NumberType(Units.CubicMetrePerHour), Aggregation.Sum, HistoryPolicies.Measurement(0.1))),
            ("totalVolume", State(new NumberType(Units.CubicMetre), Aggregation.Sum, HistoryPolicies.Measurement()))),
        Map(("resetTotal", Ack(affects: ["totalVolume"]))),
        alarms: LimitAlarms.For("value", Units.CubicMetrePerHour, Severity.Major));

    /// <summary>
    /// Gets <c>core.rotationalspeed@1</c>: a rotational speed in revolutions per minute.
    /// </summary>
    /// <value>The capability type with <c>value</c> and the limit alarms <c>highLimit</c> and <c>lowLimit</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType RotationalSpeed => Measured("core.rotationalspeed", "value", Units.PerMinute, Aggregation.Avg, 5);

    /// <summary>
    /// Gets <c>core.filllevel@1</c>: a fill level in percent, optionally also as a height.
    /// </summary>
    /// <value>
    /// The capability type with <c>value</c>, the feature <c>height</c> (<c>height</c> in metres), and the limit
    /// alarms on <c>value</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType FillLevel => Capability(
        "core.filllevel",
        Map(
            ("value", State(Percentage(), Aggregation.Avg, HistoryPolicies.Measurement(0.5))),
            ("height", State(new NumberType(Units.Metre), Aggregation.Avg, HistoryPolicies.Measurement(), "height"))),
        alarms: LimitAlarms.For("value", Units.Percent, Severity.Major),
        features: ["height"]);

    /// <summary>
    /// Gets <c>core.vibration@1</c>: a vibration velocity (RMS) in millimetres per second.
    /// </summary>
    /// <value>The capability type with <c>velocity</c> and the limit alarms <c>highLimit</c> and <c>lowLimit</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Vibration => Measured("core.vibration", "velocity", Units.MillimetrePerSecond, Aggregation.Max, 0.1);

    /// <summary>
    /// Gets <c>core.runstate@1</c>: the operating state of a machine.
    /// </summary>
    /// <value>
    /// The capability type with <c>state</c> (<c>stopped</c>, <c>starting</c>, <c>running</c>, <c>stopping</c>,
    /// <c>fault</c>), the diagnostic <c>runtimeHours</c> and <c>startCount</c>, the commands <c>start()</c>,
    /// <c>stop()</c> and <c>resetFault()</c>, and the critical device alarm <c>fault</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType RunState => Capability(
        "core.runstate",
        Map(
            ("state", State(
                new EnumType(["stopped", "starting", "running", "stopping", "fault"]),
                Aggregation.Last,
                HistoryPolicies.State)),
            ("runtimeHours", Diagnostic(new NumberType(Units.Hour), Aggregation.Sum, HistoryPolicies.Diagnostic)),
            ("startCount", Diagnostic(new IntegerType(Minimum: 0), Aggregation.Sum, HistoryPolicies.Diagnostic))),
        Map(
            ("start", Confirmed(["state"], timeout: TimeSpan.FromSeconds(60))),
            ("stop", Confirmed(["state"], timeout: TimeSpan.FromSeconds(60))),
            ("resetFault", Result(new BooleanType()))),
        Map(("fault", new EventDef(Severity: Severity.Info))),
        Map(("fault", DeviceAlarm(Severity.Critical, "The machine reports a fault.", "fault"))));

    /// <summary>
    /// Gets every industrial capability in catalog order.
    /// </summary>
    /// <value>The six industrial capabilities.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static IReadOnlyList<CapabilityType> All => [Pressure, Flow, RotationalSpeed, FillLevel, Vibration, RunState];

    private static CapabilityType Measured(string name, string property, string unit, Aggregation aggregation, double deadband) =>
        Capability(
            name,
            Map((property, State(new NumberType(unit), aggregation, HistoryPolicies.Measurement(deadband)))),
            alarms: LimitAlarms.For(property, unit, Severity.Major));
}
