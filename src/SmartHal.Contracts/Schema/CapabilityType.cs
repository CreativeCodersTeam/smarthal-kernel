using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines what a capability means: its properties, commands, events and alarms.
/// </summary>
/// <remarks>
/// <para>
/// Capability types are versioned and live in namespaces, for example <c>core.onoff@1</c> or
/// <c>vendor.acme.filter@1</c>. They are extended by composition only: a new capability is added, a core capability
/// is never changed.
/// </para>
/// <para>
/// A minor version is purely additive; a major version is incompatible and comes with a
/// <see cref="CapabilityMigration"/>.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Name">The namespaced name of the type, for example <c>core.pressure</c> or <c>vendor.acme.filter</c>.</param>
/// <param name="Version">The version of the type.</param>
/// <param name="Properties">The properties, keyed by name.</param>
/// <param name="Commands">The commands, keyed by name.</param>
/// <param name="Events">The events, keyed by name.</param>
/// <param name="Alarms">The alarms, keyed by name.</param>
/// <param name="Features">The optional feature flags an instance may activate, for example <c>hsv</c> and <c>ct</c>; <see langword="null"/>
/// when there are none.</param>
public sealed record CapabilityType(
    string Name,
    TypeVersion Version,
    IReadOnlyDictionary<string, PropertyDef> Properties,
    IReadOnlyDictionary<string, CommandDef> Commands,
    IReadOnlyDictionary<string, EventDef> Events,
    IReadOnlyDictionary<string, AlarmDef> Alarms,
    IReadOnlyList<string>? Features = null);
