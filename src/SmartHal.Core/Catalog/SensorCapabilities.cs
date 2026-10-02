using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using static SmartHal.Core.Catalog.CatalogDefinitions;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the sensor capabilities of the core catalog.
/// </summary>
/// <remarks>Every property returns a new, independent instance on each access.</remarks>
public static class SensorCapabilities
{
    /// <summary>
    /// Gets <c>core.temperature@1</c>: a temperature in degrees Celsius.
    /// </summary>
    /// <value>The capability type with <c>value</c> and the limit alarms <c>highLimit</c> and <c>lowLimit</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Temperature => Capability(
        "core.temperature",
        Map(("value", State(new NumberType(Units.Celsius), Aggregation.Avg, HistoryPolicies.Measurement(0.1)))),
        alarms: LimitAlarms.For("value", Units.Celsius, Severity.Warning));

    /// <summary>
    /// Gets <c>core.humidity@1</c>: a relative humidity in percent.
    /// </summary>
    /// <value>The capability type with <c>value</c> and the limit alarms <c>highLimit</c> and <c>lowLimit</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Humidity => Capability(
        "core.humidity",
        Map(("value", State(Percentage(), Aggregation.Avg, HistoryPolicies.Measurement(1)))),
        alarms: LimitAlarms.For("value", Units.Percent, Severity.Warning));

    /// <summary>
    /// Gets <c>core.contact@1</c>: an open/closed contact, for example on a window.
    /// </summary>
    /// <value>The capability type with <c>open</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Contact => Capability(
        "core.contact",
        Map(("open", State(new BooleanType(), Aggregation.Any, HistoryPolicies.State))));

    /// <summary>
    /// Gets <c>core.motion@1</c>: a motion or occupancy sensor.
    /// </summary>
    /// <value>The capability type with <c>occupied</c> and <c>lastMotion</c> and the event <c>motionDetected</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Motion => Capability(
        "core.motion",
        Map(
            ("occupied", State(new BooleanType(), Aggregation.Any, HistoryPolicies.State)),
            ("lastMotion", State(new TimestampType(), Aggregation.Max))),
        events: Map(("motionDetected", new EventDef(Severity: Severity.Info))));

    /// <summary>
    /// Gets <c>core.button@1</c>: a push button.
    /// </summary>
    /// <value>The capability type with the event <c>pressed</c>, whose payload names the press type.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Button => Capability(
        "core.button",
        None<PropertyDef>(),
        events: Map((
            "pressed",
            new EventDef(
                new ObjectType(Map<DataType>(("type", new EnumType(["single", "double", "long"]))), ["type"]),
                Severity.Info))));

    /// <summary>
    /// Gets every sensor capability in catalog order.
    /// </summary>
    /// <value>The five sensor capabilities.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static IReadOnlyList<CapabilityType> All => [Temperature, Humidity, Contact, Motion, Button];
}
