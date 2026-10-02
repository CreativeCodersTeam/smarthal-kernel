using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Describes the internal logic of a virtual device: which member capabilities it aggregates and how it fans
/// commands out to them.
/// </summary>
/// <remarks>
/// <para>
/// <c>aggregate</c> computes a value from the member properties; the quality is aggregated as well, so a
/// <c>bad</c> member makes the result <c>uncertain</c>. <c>fanout</c> sends a command to every member.
/// </para>
/// <para>
/// A member that cannot execute a fanned-out command directly is served through a <see cref="Substitution"/>;
/// without a matching one it is skipped and its child invocation ends as <c>skipped</c>.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Members">The capabilities of the member devices.</param>
/// <param name="Aggregations">The aggregation per property that overrides the default of the property definition; <see langword="null"/>
/// when every default applies.</param>
/// <param name="Substitutions">The substitute calls for members without a matching command; <see langword="null"/> when there are
/// none.</param>
public sealed record InternalBinding(
    IReadOnlyList<CapabilityAddress> Members,
    IReadOnlyDictionary<string, Aggregation>? Aggregations = null,
    IReadOnlyList<Substitution>? Substitutions = null);
