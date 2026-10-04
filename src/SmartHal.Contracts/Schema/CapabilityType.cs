using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines what a capability means: its properties, commands, events and alarms.
/// </summary>
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
