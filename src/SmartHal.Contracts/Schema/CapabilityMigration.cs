using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Maps the elements of one major version of a capability type onto the next.
/// </summary>
/// <param name="From">The old type, for example <c>core.level@1</c>.</param>
/// <param name="To">The new type, for example <c>core.level@2</c>.</param>
/// <param name="Properties">The new name of every changed property, <see langword="null"/> as value when it is removed; <see
/// langword="null"/> when no property changes.</param>
/// <param name="Commands">The new name of every changed command, <see langword="null"/> as value when it is removed; <see langword="null"/>
/// when no command changes.</param>
/// <param name="Events">The new name of every changed event, <see langword="null"/> as value when it is removed; <see langword="null"/>
/// when no event changes.</param>
/// <param name="Alarms">The new name of every changed alarm, <see langword="null"/> as value when it is removed; <see langword="null"/>
/// when no alarm changes.</param>
public sealed record CapabilityMigration(
    TypeRef From,
    TypeRef To,
    IReadOnlyDictionary<string, string?>? Properties = null,
    IReadOnlyDictionary<string, string?>? Commands = null,
    IReadOnlyDictionary<string, string?>? Events = null,
    IReadOnlyDictionary<string, string?>? Alarms = null);
