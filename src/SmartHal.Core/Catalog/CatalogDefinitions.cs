using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the building blocks the definitions of the core catalog are composed of.
/// </summary>
internal static class CatalogDefinitions
{
    /// <summary>The version every type of the first core catalog carries.</summary>
    public static readonly TypeVersion CoreVersion = new TypeVersion(1, 0);

    /// <summary>The timeout of a command with completion mode ack.</summary>
    public static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The timeout of a command with completion mode confirmed.</summary>
    public static readonly TimeSpan ConfirmedTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The timeout of a command with completion mode result.</summary>
    public static readonly TimeSpan ResultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates a read-only map that keeps the order of its entries.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="entries">The entries of the map.</param>
    /// <returns>The read-only map.</returns>
    public static IReadOnlyDictionary<string, T> Map<T>(params (string Key, T Value)[] entries)
    {
        var map = new Dictionary<string, T>(entries.Length, StringComparer.Ordinal);

        foreach (var (key, value) in entries)
        {
            map.Add(key, value);
        }

        return map.AsReadOnly();
    }

    /// <summary>
    /// Creates an empty read-only map.
    /// </summary>
    /// <remarks>
    /// A new map is created on every call rather than handing out a shared empty instance, so no two graphs share a
    /// collection.
    /// </remarks>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <returns>The empty map.</returns>
    public static IReadOnlyDictionary<string, T> None<T>() => new Dictionary<string, T>(StringComparer.Ordinal).AsReadOnly();

    /// <summary>
    /// Creates a percentage from 0 to 100.
    /// </summary>
    /// <param name="step">The granularity of the value; <see langword="null"/> when any value is permitted.</param>
    /// <returns>The data type of the percentage.</returns>
    public static NumberType Percentage(double? step = null) => new NumberType(Units.Percent, 0, 100, step);

    /// <summary>
    /// Creates a capability type of the core catalog in version 1.0.
    /// </summary>
    /// <param name="name">The namespaced name, for example <c>core.onoff</c>.</param>
    /// <param name="properties">The properties.</param>
    /// <param name="commands">The commands; <see langword="null"/> for none.</param>
    /// <param name="events">The events; <see langword="null"/> for none.</param>
    /// <param name="alarms">The alarms; <see langword="null"/> for none.</param>
    /// <param name="features">The feature flags; <see langword="null"/> for none.</param>
    /// <returns>The capability type.</returns>
    public static CapabilityType Capability(
        string name,
        IReadOnlyDictionary<string, PropertyDef> properties,
        IReadOnlyDictionary<string, CommandDef>? commands = null,
        IReadOnlyDictionary<string, EventDef>? events = null,
        IReadOnlyDictionary<string, AlarmDef>? alarms = null,
        IReadOnlyList<string>? features = null) =>
        new CapabilityType(name, CoreVersion, properties, commands ?? None<CommandDef>(), events ?? None<EventDef>(),
            alarms ?? None<AlarmDef>(), features);

    /// <summary>
    /// Creates a state property.
    /// </summary>
    /// <param name="dataType">The data type.</param>
    /// <param name="aggregation">The default aggregation for virtual devices; <see langword="null"/> when none applies.</param>
    /// <param name="history">The default history policy; <see langword="null"/> when the property is not historized.</param>
    /// <param name="feature">The feature flag the property depends on; <see langword="null"/> when it is always present.</param>
    /// <returns>The property definition.</returns>
    public static PropertyDef State(
        DataType dataType,
        Aggregation? aggregation = null,
        HistoryPolicy? history = null,
        string? feature = null) =>
        new PropertyDef(dataType, PropertyCategory.State, Feature: feature, History: history, Aggregation: aggregation);

    /// <summary>
    /// Creates a diagnostic property.
    /// </summary>
    /// <param name="dataType">The data type.</param>
    /// <param name="aggregation">The default aggregation for virtual devices; <see langword="null"/> when none applies.</param>
    /// <param name="history">The default history policy; <see langword="null"/> when the property is not historized.</param>
    /// <returns>The property definition.</returns>
    public static PropertyDef Diagnostic(DataType dataType, Aggregation? aggregation = null, HistoryPolicy? history = null) =>
        new PropertyDef(dataType, PropertyCategory.Diagnostic, History: history, Aggregation: aggregation);

    /// <summary>
    /// Creates a command that completes when the device acknowledges it.
    /// </summary>
    /// <param name="parameters">The parameters; <see langword="null"/> for none.</param>
    /// <param name="required">The required parameters; <see langword="null"/> for none.</param>
    /// <param name="affects">The affected properties; <see langword="null"/> for none.</param>
    /// <returns>The command definition.</returns>
    public static CommandDef Ack(
        IReadOnlyDictionary<string, DataType>? parameters = null,
        IReadOnlyList<string>? required = null,
        IReadOnlyList<string>? affects = null) =>
        new CommandDef(Completion.Ack, AckTimeout, parameters, Affects: affects, RequiredParameters: required);

    /// <summary>
    /// Creates a command that completes when an affected property reaches its target value.
    /// </summary>
    /// <param name="affects">The affected properties.</param>
    /// <param name="parameters">The parameters; <see langword="null"/> for none.</param>
    /// <param name="required">The required parameters; <see langword="null"/> for none.</param>
    /// <param name="feature">The feature flag the command depends on; <see langword="null"/> when it is always present.</param>
    /// <param name="timeout">The timeout; <see langword="null"/> for the default of 30 seconds.</param>
    /// <returns>The command definition.</returns>
    public static CommandDef Confirmed(
        IReadOnlyList<string> affects,
        IReadOnlyDictionary<string, DataType>? parameters = null,
        IReadOnlyList<string>? required = null,
        string? feature = null,
        TimeSpan? timeout = null) =>
        new CommandDef(Completion.Confirmed, timeout ?? ConfirmedTimeout, parameters, Affects: affects, Feature: feature,
            RequiredParameters: required);

    /// <summary>
    /// Creates a command that completes when the device returns a result.
    /// </summary>
    /// <param name="result">The data type of the result.</param>
    /// <param name="parameters">The parameters; <see langword="null"/> for none.</param>
    /// <param name="required">The required parameters; <see langword="null"/> for none.</param>
    /// <param name="timeout">The timeout; <see langword="null"/> for the default of 10 seconds.</param>
    /// <returns>The command definition.</returns>
    public static CommandDef Result(
        DataType result,
        IReadOnlyDictionary<string, DataType>? parameters = null,
        IReadOnlyList<string>? required = null,
        TimeSpan? timeout = null) =>
        new CommandDef(Completion.Result, timeout ?? ResultTimeout, parameters, result, RequiredParameters: required);

    /// <summary>
    /// Creates an alarm the device reports through one of the events of the capability.
    /// </summary>
    /// <param name="severity">The severity of the alarm.</param>
    /// <param name="message">The text shown for the alarm.</param>
    /// <param name="eventName">The name of the event that reports the alarm.</param>
    /// <returns>The alarm definition.</returns>
    public static AlarmDef DeviceAlarm(Severity severity, string message, string eventName) =>
        new AlarmDef(severity, message, new DeviceAlarmSource(eventName));
}
