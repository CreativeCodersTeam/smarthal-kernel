using System.Text.Json.Nodes;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using static SmartHal.Core.Catalog.CatalogDefinitions;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the system capabilities of the core catalog, which live on the root channel <c>0</c> of a device.
/// </summary>
/// <remarks>Every property returns a new, independent instance on each access.</remarks>
public static class SystemCapabilities
{
    private static readonly TimeSpan AlarmCommandTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets <c>core.deviceinfo@1</c>: the serial number and the hardware version of the device.
    /// </summary>
    /// <value>
    /// The capability type. Manufacturer and model come from the device type; the firmware version lives in
    /// <see cref="Firmware"/> only.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType DeviceInfo => Capability(
        "core.deviceinfo",
        Map(
            ("serialNumber", Diagnostic(new StringType())),
            ("hardwareVersion", Diagnostic(new StringType()))));

    /// <summary>
    /// Gets <c>core.connectivity@1</c>: whether the device is reachable, derived from the status of its bindings.
    /// </summary>
    /// <value>
    /// The capability type with <c>status</c> (<c>online</c>, <c>degraded</c>, <c>offline</c>), <c>lastSeen</c> and
    /// the diagnostic <c>linkQuality</c>, and the rule alarm <c>offline</c> when the status stays offline longer
    /// than a delay of 5 minutes by default.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Connectivity => Capability(
        "core.connectivity",
        Map(
            ("status", State(new EnumType(["online", "degraded", "offline"]), Aggregation.Last, HistoryPolicies.State)),
            ("lastSeen", State(new TimestampType(), Aggregation.Max)),
            ("linkQuality", Diagnostic(Percentage(), Aggregation.Min, HistoryPolicies.Diagnostic))),
        alarms: Map(
            ("offline", new AlarmDef(
                Severity.Major,
                "The device has been offline for longer than the delay.",
                new RuleAlarmSource("status", AlarmCondition.Equals),
                Map(
                    ("value", new AlarmParameter(new EnumType(["online", "degraded", "offline"]), JsonValue.Create("offline"))),
                    ("delay", new AlarmParameter(new DurationType(), JsonValue.Create("PT5M"))))))));

    /// <summary>
    /// Gets <c>core.identify@1</c>: makes the device identify itself, for example by blinking.
    /// </summary>
    /// <value>The capability type with the command <c>identify(duration)</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Identify => Capability(
        "core.identify",
        None<PropertyDef>(),
        Map(("identify", Ack(Map<DataType>(("duration", new DurationType())), ["duration"]))));

    /// <summary>
    /// Gets <c>core.firmware@1</c>: the firmware version of the device and its update.
    /// </summary>
    /// <value>
    /// The capability type with <c>currentVersion</c>, <c>availableVersion</c> and <c>updateState</c>, the command
    /// <c>startUpdate()</c> that returns the installed version, and the events <c>updateFinished</c> and
    /// <c>updateFailed</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Firmware => Capability(
        "core.firmware",
        Map(
            ("currentVersion", State(new StringType())),
            ("availableVersion", State(new StringType())),
            ("updateState", State(
                new EnumType(["idle", "available", "downloading", "installing", "failed"]),
                history: HistoryPolicies.State))),
        Map(("startUpdate", Result(new StringType(), timeout: TimeSpan.FromHours(1)))),
        Map(
            ("updateFinished", new EventDef(Severity: Severity.Info)),
            ("updateFailed", new EventDef(Severity: Severity.Warning))));

    /// <summary>
    /// Gets <c>core.battery@1</c>: the charge level of the battery.
    /// </summary>
    /// <value>
    /// The capability type with <c>level</c> and <c>charging</c>, the event <c>lowBattery</c>, the rule alarm
    /// <c>batteryLow</c> (level below 20 percent by default) and the device alarm <c>batteryLowReported</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Battery => Capability(
        "core.battery",
        Map(
            ("level", State(Percentage(), Aggregation.Min, HistoryPolicies.Measurement())),
            ("charging", State(new BooleanType(), Aggregation.Any, HistoryPolicies.State))),
        events: Map(("lowBattery", new EventDef(Severity: Severity.Info))),
        alarms: Map(
            ("batteryLow", new AlarmDef(
                Severity.Warning,
                "The battery level is below its limit.",
                new RuleAlarmSource("level", AlarmCondition.Below),
                LimitAlarms.Parameters(Units.Percent, limit: 20, TimeSpan.Zero, hysteresis: 5))),
            ("batteryLowReported", DeviceAlarm(Severity.Warning, "The device reports a low battery.", "lowBattery"))));

    /// <summary>
    /// Gets <c>core.bridge@1</c>: the gateway function of a device that connects sub-devices.
    /// </summary>
    /// <value>
    /// The capability type with <c>childCount</c> and <c>joinMode</c>, the command <c>permitJoin(duration)</c> and
    /// the events <c>deviceJoined</c> and <c>deviceLeft</c>, which carry the key of the sub-device.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Bridge => Capability(
        "core.bridge",
        Map(
            ("childCount", State(new IntegerType(Minimum: 0), history: HistoryPolicies.Measurement())),
            ("joinMode", State(new BooleanType(), history: HistoryPolicies.State))),
        Map(("permitJoin", Ack(Map<DataType>(("duration", new DurationType())), ["duration"], ["joinMode"]))),
        Map(
            ("deviceJoined", new EventDef(DeviceKeyPayload(), Severity.Info)),
            ("deviceLeft", new EventDef(DeviceKeyPayload(), Severity.Info))));

    /// <summary>
    /// Gets <c>core.alarms@1</c>: acknowledging and shelving every alarm of the device.
    /// </summary>
    /// <value>
    /// The capability type with <c>activeCount</c> and <c>unackedCount</c> and the commands
    /// <c>acknowledge(alarmId)</c>, which returns the new alarm state, and <c>shelve(alarmId, duration)</c>, which
    /// returns the time the alarm is shelved until. The alarm definitions themselves stay in their capabilities.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Alarms => Capability(
        "core.alarms",
        Map(
            ("activeCount", State(new IntegerType(Minimum: 0), Aggregation.Sum, HistoryPolicies.Measurement())),
            ("unackedCount", State(new IntegerType(Minimum: 0), Aggregation.Sum, HistoryPolicies.Measurement()))),
        Map(
            ("acknowledge", Result(
                new EnumType(["active_unacked", "active_acked", "cleared_unacked", "cleared"]),
                Map<DataType>(("alarmId", new StringType())),
                ["alarmId"],
                AlarmCommandTimeout)),
            ("shelve", Result(
                new TimestampType(),
                Map<DataType>(("alarmId", new StringType()), ("duration", new DurationType())),
                ["alarmId", "duration"],
                AlarmCommandTimeout))));

    /// <summary>
    /// Gets every system capability in catalog order.
    /// </summary>
    /// <value>The seven system capabilities.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static IReadOnlyList<CapabilityType> All => [DeviceInfo, Connectivity, Identify, Firmware, Battery, Bridge, Alarms];

    private static ObjectType DeviceKeyPayload() => new ObjectType(Map<DataType>(("deviceKey", new StringType())), ["deviceKey"]);
}
