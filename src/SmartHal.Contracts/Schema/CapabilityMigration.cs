using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Maps the elements of one major version of a capability type onto the next.
/// </summary>
/// <remarks>
/// <para>
/// On a major version change the capability instance keeps its id. Automations that use removed elements are
/// marked and the user is warned.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
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
