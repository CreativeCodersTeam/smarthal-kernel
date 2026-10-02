using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using static SmartHal.Core.Catalog.CatalogDefinitions;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the actuator capabilities of the core catalog.
/// </summary>
/// <remarks>Every property returns a new, independent instance on each access.</remarks>
public static class ActuatorCapabilities
{
    private static readonly TimeSpan CoverTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets <c>core.onoff@1</c>: switches something on and off.
    /// </summary>
    /// <value>The capability type with <c>on</c> and the commands <c>on()</c>, <c>off()</c> and <c>toggle()</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType OnOff => Capability(
        "core.onoff",
        Map(("on", State(new BooleanType(), Aggregation.Any, HistoryPolicies.State))),
        Map(
            ("on", Confirmed(["on"])),
            ("off", Confirmed(["on"])),
            ("toggle", Confirmed(["on"]))));

    /// <summary>
    /// Gets <c>core.level@1</c>: a level from 0 to 100 percent, for example the brightness of a light.
    /// </summary>
    /// <value>The capability type with <c>level</c> and the command <c>setLevel(level, transition)</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Level => Capability(
        "core.level",
        Map(("level", State(Percentage(step: 1), Aggregation.Avg, HistoryPolicies.Measurement()))),
        Map(("setLevel", Confirmed(
            ["level"],
            Map<DataType>(("level", Percentage(step: 1)), ("transition", new DurationType())),
            ["level"]))));

    /// <summary>
    /// Gets <c>core.color@1</c>: a color as hue, saturation and value, and a color temperature.
    /// </summary>
    /// <value>
    /// The capability type with the features <c>hsv</c> (<c>color</c>, <c>setColor</c>) and <c>ct</c>
    /// (<c>colorTemperature</c> from 1000 to 10000 kelvin, <c>setColorTemperature</c>).
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Color => Capability(
        "core.color",
        Map(
            ("color", State(new RefType(CoreDataTypes.HsvRef), Aggregation.Last, HistoryPolicies.Measurement(), "hsv")),
            ("colorTemperature", State(ColorTemperatureType(), Aggregation.Avg, HistoryPolicies.Measurement(), "ct"))),
        Map(
            ("setColor", Confirmed(
                ["color"],
                Map<DataType>(("color", new RefType(CoreDataTypes.HsvRef)), ("transition", new DurationType())),
                ["color"],
                "hsv")),
            ("setColorTemperature", Confirmed(
                ["colorTemperature"],
                Map<DataType>(("colorTemperature", ColorTemperatureType()), ("transition", new DurationType())),
                ["colorTemperature"],
                "ct"))),
        features: ["hsv", "ct"]);

    /// <summary>
    /// Gets <c>core.cover@1</c>: a blind, shutter or awning.
    /// </summary>
    /// <value>
    /// The capability type with <c>position</c> (100 percent = fully open) and <c>motion</c>, the commands
    /// <c>open()</c>, <c>close()</c>, <c>stop()</c> and <c>setPosition(position)</c>, and the device alarm
    /// <c>blocked</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Cover => Capability(
        "core.cover",
        Map(
            ("position", State(Percentage(), Aggregation.Avg, HistoryPolicies.Measurement())),
            ("motion", State(new EnumType(["opening", "closing", "stopped"]), Aggregation.Last, HistoryPolicies.State))),
        Map(
            ("open", Confirmed(["position"], timeout: CoverTimeout)),
            ("close", Confirmed(["position"], timeout: CoverTimeout)),
            ("stop", Confirmed(["motion"])),
            ("setPosition", Confirmed(["position"], Map<DataType>(("position", Percentage())), ["position"], timeout: CoverTimeout))),
        Map(("blocked", new EventDef(Severity: Severity.Info))),
        Map(("blocked", DeviceAlarm(Severity.Major, "The cover is blocked.", "blocked"))));

    /// <summary>
    /// Gets <c>core.thermostat@1</c>: a heating or cooling controller with one setpoint.
    /// </summary>
    /// <value>
    /// The capability type with <c>mode</c> (<c>off</c>, <c>heat</c>, <c>cool</c>, <c>auto</c>) and
    /// <c>setpoint</c> from 5 to 35 °C, and the commands <c>setMode(mode)</c> and <c>setSetpoint(setpoint)</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Thermostat => Capability(
        "core.thermostat",
        Map(
            ("mode", State(ModeType(), Aggregation.Last, HistoryPolicies.State)),
            ("setpoint", State(SetpointType(), Aggregation.Avg, HistoryPolicies.Measurement()))),
        Map(
            ("setMode", Confirmed(["mode"], Map<DataType>(("mode", ModeType())), ["mode"])),
            ("setSetpoint", Confirmed(["setpoint"], Map<DataType>(("setpoint", SetpointType())), ["setpoint"]))));

    /// <summary>
    /// Gets <c>core.lock@1</c>: a door lock.
    /// </summary>
    /// <value>
    /// The capability type with <c>lockState</c> (<c>locked</c>, <c>unlocked</c>, <c>jammed</c>), the commands
    /// <c>lock()</c> and <c>unlock()</c>, and the device alarm <c>jammed</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Lock => Capability(
        "core.lock",
        Map(("lockState", State(new EnumType(["locked", "unlocked", "jammed"]), Aggregation.Last, HistoryPolicies.State))),
        Map(
            ("lock", Confirmed(["lockState"])),
            ("unlock", Confirmed(["lockState"]))),
        Map(("jammed", new EventDef(Severity: Severity.Info))),
        Map(("jammed", DeviceAlarm(Severity.Major, "The lock is jammed.", "jammed"))));

    /// <summary>
    /// Gets every actuator capability in catalog order.
    /// </summary>
    /// <value>The six actuator capabilities.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static IReadOnlyList<CapabilityType> All => [OnOff, Level, Color, Cover, Thermostat, Lock];

    private static NumberType ColorTemperatureType() => new NumberType(Units.Kelvin, 1000, 10000);

    private static EnumType ModeType() => new EnumType(["off", "heat", "cool", "auto"]);

    private static NumberType SetpointType() => new NumberType(Units.Celsius, 5, 35, 0.5);
}
